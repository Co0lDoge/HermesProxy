# Version-Dependent Packet Shape

A design memo, not a description of current behaviour. It records why the codebase is full of
exact-build comparisons, what breaks when a new modern client arrives, and how that connects to
the discriminated-union work on `perf/union-object-update`.

## The problem

There are **192** exact-equality build comparisons in `HermesProxy/`:

```csharp
ModernVersion.Build == ClientVersionBuild.V3_4_3_54261
```

Distribution:

| location | count | kind |
|---|---:|---|
| `World/Server/Packets` | 87 | wire structure |
| `World/Client/PacketHandlers` | 54 | translation behaviour |
| `World/Server/PacketHandlers` | 22 | translation behaviour |
| `World`, `World/Objects`, misc | 29 | mixed |

Only the first group is about *shape* — how bytes are laid out. The rest decide *what* to send,
which is a separate concern and out of scope for this memo.

## The failure mode

Exact equality means a future modern build — Cataclysm Classic, or even a V3_4_4 patch — falls
into every `else` branch, which is the **V1_14 / V2_5 layout**. That is not graceful
degradation. Cata Classic's wire format is far closer to WotLK Classic than to TBC Classic, so
the fallback is wrong for essentially every packet with a version branch, and it fails silently:
no exception, no log, just misread fields.

Worth stating plainly, because the whole file reads like paranoia otherwise: nothing is broken
today. This is a cliff we walk off the day a V4_x build is added to
`VersionChecker.IsSupportedModernVersion`.

## Why it looks like this

`LegacyVersion` has ordering helpers:

```csharp
public static bool AddedInVersion(ClientVersionBuild build)   => Build >= build;
public static bool RemovedInVersion(ClientVersionBuild build) => Build <  build;
public static bool InVersion(ClientVersionBuild a, ClientVersionBuild b);
```

`ModernVersion` has none — only `Build`, `ExpansionVersion`, `MajorVersion`, `MinorVersion`.
When `==` is the only tool available, you get 192 of them. The idiom followed the API.

### The trap in the obvious fix

Copying `AddedInVersion` verbatim to `ModernVersion` is **not** safe. `ClientVersionBuild` is
keyed by build number, and build numbers do not order by expansion across the original/Classic
split:

| build | value |
|---|---:|
| `V4_3_4_15595` — original Cataclysm, 2012 | 15595 |
| `V3_4_3_54261` — WotLK Classic, 2023 | 54261 |

So `Build >= V3_4_3_54261` is *false* for original Cataclysm and *true* for Cata Classic. It
happens to work within the Classic re-release line, where builds increase over time, but the
predicate is not expressing what the caller means. The honest ordering key is
`(ExpansionVersion, MajorVersion, MinorVersion)`, parsed from the enum name and already cached.

## What actually varies

Only **11 of 648** packet classes contain a version branch:

| side | classes |
|---|---|
| `ClientPacket` (Read) | `ChatMessage`, `ChatMessageWhisper`, `ChatMessageChannel`, `AuctionListOwnerItems`, `LootRoll`, `PartyInviteResponse`, `PartyUninvite`, `SetAssistantLeader`, `SetEveryoneIsAssistant` |
| `ServerPacket` (Write) | `SpellGo`, `AuctionHelloResponse`, `GossipPOI`, `StartLootRoll` |

637 classes never vary. Any solution that duplicates all of them to isolate 11 is a bad trade —
see `World/Objects/Version/V1_14_0_40237/` vs `V1_14_1_40688/`, where two ~1720-line
`ObjectUpdateBuilder.cs` copies differ by 38 lines and must be kept in sync by hand.

## What is actually objectionable

Not the branch's condition — its placement. From `ChatPackets.cs` after the #177 fix:

```csharp
public override void Read()
{
    Language = _worldPacket.ReadUInt32();
    ChannelGUID = _worldPacket.ReadPackedGuid128();

    if (ModernVersion.Build == ClientVersionBuild.V3_4_3_54261)
    {
        uint targetLen343 = _worldPacket.ReadBits<uint>(9);
        uint textLen343 = _worldPacket.ReadBits<uint>(11);
        if (_worldPacket.HasBit())
            IsSecure = _worldPacket.HasBit();
        Target = _worldPacket.ReadString(targetLen343);
        Text = _worldPacket.ReadString(textLen343);
        return;
    }

    uint targetLen = _worldPacket.ReadBits<uint>(9);
    uint textLen = _worldPacket.ReadBits<uint>(9);
    Target = _worldPacket.ReadString(targetLen);
    Text = _worldPacket.ReadString(textLen);
}
```

One method holds two layouts, so neither reads as a coherent packet, and the shape is
re-decided on every read. Swapping `==` for a range predicate fixes the forward-compatibility
cliff but leaves this shape untouched. **The requirement is that the shape is chosen once, not
per packet.**

## Prior art: WowPacketParser

WPP parses roughly twenty client versions across Retail, Classic, TBC, WotLK and Cata. It has
already solved both halves of this, and its solutions are worth copying rather than reinventing.

### ClientBranch

WPP does not compare raw build numbers across unrelated clients. It carries a branch alongside
the build:

```csharp
public enum ClientBranch { Retail = 0, Classic = 1, TBC = 2, WotLK = 3, Cata = 4, MoP = 5 }
```

and scopes the comparison helpers to it:

```csharp
public static bool AddedInVersion(ClientBranch branch, ClientVersionBuild build)
    => _branch == branch && AddedInVersion(build);

public static bool RemovedInVersion(ClientBranch branch, ClientVersionBuild build)
    => _branch == branch && RemovedInVersion(build);
```

This dissolves the build-number trap above outright: original Cataclysm and WotLK Classic are
different branches, so their build numbers are never compared. There is also an expansion-level
form, `AddedInVersion(ClientType expansion)`, comparing `_expansion` rather than the build.

`ModernVersion` has no equivalent of any of this. Adding a branch is the single highest-value
piece to borrow, and it is independent of everything else in this memo.

### Version-ranged registration, resolved once

`ParserAttribute` is applied to handler methods with an optional branch and version range:

```csharp
public ParserAttribute(Opcode opcode, ClientBranch branch, ClientVersionBuild addedInVersion)
{
    if (ClientVersion.AddedInVersion(branch, addedInVersion))
        Opcode = opcode;
}
```

The predicate runs in the attribute *constructor*. If the version does not match, `Opcode` is
left unset and the registration loop skips that method. The attribute is `AllowMultiple`, so
several methods can claim the same opcode over different ranges, and exactly the applicable one
lands in the dispatch table — which is built once by reflection at startup.

That is precisely the property we want: the shape is chosen once, at table-build time, and never
re-evaluated per packet. HermesProxy already builds `_clientPacketTable` by the same reflection
scan over `[PacketHandler]` methods, so the mechanism transplants with little ceremony — the
attribute gains optional branch/range parameters, and packet classes or handler methods declare
the versions they serve.

### Borrow the concepts, not the mechanism

WPP is an offline sniff parser. It is free to allocate, and it does. This proxy is not — see
`docs/perf.md` and the zero-allocation rules in `CLAUDE.md`. Three specific reasons not to copy
its dispatch verbatim:

- **The predicate runs in an attribute constructor.** That only works because WPP builds its
  attributes reflectively at runtime, after `ClientVersion` is set. It makes initialisation
  order load-bearing — a hazard this codebase already has, since `ModernVersion.RequireBuild()`
  throws when accessed before `VersionBootstrap` assigns, and the test suite has to set it from
  a module initializer for exactly that reason.
- **It is invisible to source generators.** A generator cannot evaluate
  `ClientVersion.AddedInVersion(...)` at compile time. Since the object-update path is already
  generated, any version range a generator must see has to be expressible as *literal attribute
  arguments* — data, not a predicate. The descriptor attributes in
  `World/Objects/Version/Attributes/` are written that way and are the better model.
- **Reflective dispatch allocates per packet.** WPP's does; so does ours, today.

So: take `ClientBranch` unreservedly — it is a data concept, costs nothing, and fixes the
ordering trap. Take the *idea* of version-ranged registration resolved once. Do not take the
runtime-reflection machinery.

## What "better" looks like

Our own dispatch has the same allocation problem we would be criticising:

```csharp
using var clientPacket = (ClientPacket)Activator.CreateInstance(packetType, packet)!;
clientPacket.Read();
methodCaller(session, clientPacket);
```

That is a reflective allocation for **every inbound packet**, plus a delegate hop through
`CreateDelegate<P1>`'s closure, on a class that exists only to be read once and disposed. Any
redesign that fixes version dispatch should fix this at the same time — they are the same code
path, and doing them separately means touching it twice.

The pieces for something better already exist in the repo:

- `SpanPacketReader` / `SpanPacketWriter` — `ref struct` readers over `Span<byte>`, benchmarked
  in `docs/perf.md` at 0.08 ns per `ReadInt64` against 157.98 ns for `ByteBuffer`, zero
  allocation.
- The direct-indexed opcode tables in `LegacyVersion` / `ModernVersion` — arrays indexed by
  opcode, built once, no dictionary hashing.
- `ObjectUpdateBuilderGenerator` — the proof that per-version wire layout can be declared as
  attributes and emitted as straight-line code with no runtime branch.

Sketch of the target, to be argued with rather than accepted:

- Version ranges are **literal attribute data** on the packet or layout declaration, so both the
  generator and any runtime table can read them.
- The generator emits one `Read` per applicable version, plus a dispatch table per version.
- Startup selects the table once — a direct-indexed array, same scheme as the opcode tables.
- Per packet: index the array, call a static method against a `SpanPacketReader`. No
  `Activator`, no reflection, no per-packet class instance, no version check.

The open question is how far to push this. Turning packet classes into `ref struct` readers is a
much larger change than fixing version dispatch, and the two are separable — the version work
can land on the existing class-based packets first and the allocation work second. But the
generated-dispatch design should not *preclude* the second step, which arguing it through now is
meant to ensure.

## Direction: discriminated unions

This is the same argument as `perf/union-object-update` (`7ecd0822`), which replaces
`ObjectUpdate`'s eight nullable data fields with an `ObjectSpecificData` union so that invalid
combinations are unrepresentable at the type level.

Version variants are the same shape of problem:

```
ChatMessageChannel
├─ V1_14  { Language, ChannelGUID, Target, Text }
├─ V2_5   { Language, ChannelGUID, Target, Text }
└─ V3_4_3 { Language, ChannelGUID, Target, Text, IsSecure }
```

`IsSecure` is the tell. It was added to the shared class by the #177 fix and is meaningless for
two of the three versions that use it — representable, invalid, silently ignored. Under a union
it does not exist outside the variant that has it, and the variant is selected once at
construction.

The read path already supports this. `WorldSocket._clientPacketTable` is built **once at
startup** by reflection; `PacketHandler` stores `packetType` and does
`Activator.CreateInstance(packetType, packet)` per packet, with `CreateDelegate<P1>` casting
`(P1)p`. A version-specific type resolved at table-build time satisfies that cast, so handler
signatures do not change. The write path has no equivalent central table — server packets are
constructed at call sites — so it needs a factory or a startup-resolved delegate.

## Dependency that changed since April

`perf/union-object-update` was last touched **2026-04-17**, when `ObjectUpdateBuilder` was
entirely hand-written. It no longer is. The ActivePlayer Create path is now source-generated,
and `ObjectUpdateBuilderGenerator` *emits* field accesses directly:

| symbol | references on `feature/wotlk-classic-v3.4.3` |
|---|---:|
| `UnitData` | 154 |
| `ActivePlayerData` | 106 |
| `CorpseData` | 23 |
| `DynamicObjectData` | 11 |
| `_updateData.` in generated builder | 27 |

Converting `ObjectUpdate` to a union is therefore no longer a source-edit alone — the generator
has to emit union-aware access or the generated file will not compile. That cuts both ways: it
is a new dependency for the union branch, but one emit-template change covers all 27 generated
sites rather than hand-editing each.

## Suggested sequencing

1. **Add `ClientBranch` to `ModernVersion`**, with branch-scoped `AddedInVersion` /
   `RemovedInVersion` / `InVersion`. Independent of everything else here, small, and it is what
   makes any range predicate correct rather than accidentally correct. Nothing has to adopt it
   immediately.
2. **Refresh `perf/union-object-update`** onto the current branch and make
   `ObjectUpdateBuilderGenerator` union-aware. This is the enabling step for the union direction
   and where the real work is.
3. **Apply the variant pattern to the 11 divergent packet classes**, with the version range as
   literal attribute data and the concrete shape resolved once at startup.
4. **Leave the 105 handler-level checks alone.** They are behaviour, not shape, and need their
   own analysis.

Two things deliberately *not* recommended:

- **Mechanically rewriting the 192 comparisons to range predicates.** Unverifiable while no V4
  client exists to test against, some sites may be genuinely build-specific, and a blind sweep
  across that many wire-format branches is exactly the change that breaks things invisibly.
- **Copying WPP's dispatch.** Take `ClientBranch` and the ranged-registration idea; leave the
  runtime reflection and the attribute-constructor predicate.

`ChatMessageChannel` is the natural first case for step 3 — small, and PR #178 verified both
layouts against a live client.

## Open questions

- Does the packet redesign stop at version dispatch, or continue into `ref struct` readers and
  the removal of per-packet `Activator.CreateInstance`? They are separable; the first should not
  preclude the second.
- Do version variants become union cases, or per-version types selected at table-build time?
  Unions make invalid field combinations unrepresentable, which is the stronger property, but
  they depend on the C# 15 / .NET 11 move that `perf/union-object-update` already assumes.
- Is `ClientBranch` alone sufficient for the handler-level checks too, or do those want a
  capability-style predicate ("does this build have X") rather than a version comparison?
