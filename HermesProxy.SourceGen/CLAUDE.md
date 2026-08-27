# HermesProxy.SourceGen

Roslyn source generators. They emit code that lands directly on the wire, so a mistake here
does not throw — it produces a subtly malformed packet that the client silently drops or
mis-renders. Treat every change as a wire-format change.

## The three generators

| Generator | Emits | Driven by |
|---|---|---|
| `ObjectUpdateBuilderGenerator` | `WriteCreate{Section}Data` / `WriteUpdate{Section}Data` on the per-version `ObjectUpdateBuilder` | `[DescriptorSection]` enums in `World/Enums/<build>/*Field.cs` |
| `OpcodeTableGenerator` | Opcode lookup tables | per-version `Opcode.cs` enums |
| `UpdateFieldTableGenerator` | Legacy update-field tables | per-version update-field enums |

Generated output lands in `HermesProxy/obj/Generated/HermesProxy.SourceGen/...`. Read it when
debugging — it is the actual code that runs, and diffing it is usually faster than reasoning
about the attributes.

## The descriptor tree (V3_4_3)

WotLK Classic replaced the legacy DWORD-indexed update-field system with retail's
descriptor/change-set model. There is no `UpdateFieldsArray` to port. Instead each section's
`*Field.cs` enum **is** the wire definition:

**Declaration order is wire order.** The generator emits Create writes in the order members
appear. Migrating a hand-written block is therefore a strict top-down walk — no bit arithmetic.

Attribute vocabulary (`World/Objects/Version/Attributes/DescriptorAttributes.cs`):

- `[DescriptorSection]` — marks the enum, sets `MaskMode` / `BlockMaskShape`
- `[DescriptorCreateField]` — a Create write from a source property. Supports `ArrayCount`,
  `ArrayMode` (`Grouped` / `PerElement`), `DefaultExpression`, `DefaultExpressionByIndex`,
  `OwnerOnly`, `Cast`, `CustomWriter`
- `[DescriptorUpdateField]` — the Update-path equivalent, keyed by changesMask bit
- `[DescriptorCreatePlaceholder]` — a constant write (natural zero, or an explicit literal like
  `"1f"`). With `CustomWriter` set it becomes a **positioned hook**: the generator emits
  `{CustomWriter}(data, src);` at that point in the sequence
- `[DescriptorCreateBitsPlaceholder]` — `WriteBits(value, n)` (+ optional `FlushBits`)
- `[DescriptorMaskMutator]`, `[DescriptorMaskPreamble]`, `[DescriptorUpdateBitsPreamble]`,
  `[DescriptorUpdatePostFlush]`, `[DescriptorCustomField]` — Update-path shaping

Members with no descriptor attribute are skipped, so an enum can carry non-wire entries.

### When to reach for a CustomWriter

The declarative form cannot express:

- **computed values** — e.g. `GetModernInvSlot` fans legacy slot arrays into the modern flat layout
- **session fallbacks** — `?? _gameState.SummonedBattlePetGuid` is not a literal default
- **interleaved arrays** — N parallel arrays woven through one index loop. `ArrayCount` writes
  one array's elements consecutively, which is a different wire shape

Interleaving is common enough to have a house pattern: position a `DescriptorCreatePlaceholder`
with `CustomWriter` at the right point and let the callback weave. See `UnitField`'s
`UNIT_STATS_INTERLEAVED_CUSTOM` / `UNIT_POWER_INTERLEAVED_CUSTOM`, and ActivePlayer's four
`WriteCreateActivePlayer*Interleaved` writers.

## Safety net — use it, do not skip it

Two independent layers, and they catch different things:

1. **Byte-equivalence tests** (`HermesProxy.Tests/SourceGen/*SectionEquivalenceTests.cs`) —
   a frozen copy of the hand-port is kept in the test project as an oracle, and
   `Assert.Equal(expected.GetData(), actual.GetData())` proves the generated writer produces
   identical bytes across a scenario matrix. This is what makes migration safe.
2. **Verify snapshots** (`ObjectUpdateBuilderGeneratorTests.*.verified.txt`) — pin the generated
   *source*. A deliberate generator change will fail these; inspect the `.received.txt` diff,
   confirm it contains only what you intended, then accept it. Snapshots are UTF-8 **with BOM,
   CRLF** — preserve that when accepting by hand or the whole file shows as changed.

A green equivalence test plus a snapshot diff limited to your intended lines is strong evidence.
Neither alone is. And neither proves the *oracle* is right — for anything user-visible, finish
with a play-test.

## In-progress: ActivePlayer Create migration

`WriteCreateActivePlayerRest` is the shrinking remainder of what used to be one ~293-line
`WriteCreateActivePlayerAll` mega-writer, declared as a single placeholder. It is being migrated
top-down into declarative members. The census of the original block:

- 84 simple field writes, 42 literal placeholders, 6 simple array loops
- 4 zero-fill loops, 4 interleaved loops, 6 bit-writes, **0 conditionals**

`ActivePlayerSectionEquivalenceTests.WriteCreateActivePlayerData_HandPort` is the frozen
pre-migration oracle. **Do not "fix" that copy** — its value is that it does not change while
the real writer is decomposed.

Worth finishing: those 42 literal zeros include several marked `live property exists, TODO`,
and a hand-written zero in this block is what caused the action-bar-reset bug (`8087167e`)
while the generated Update path was correct.

## Not yet wired

- `Corpse` and `DynamicObject` have `*Field.cs` enums but no `[DescriptorSection]`. Create is
  hand-written; **Update does not exist** — the Values dispatch has no branch for them
- All 12 `*DynamicField` enums are unwired; dynamic fields are handled via custom callbacks
- `AreaTrigger` / `Conversation` / `SceneObject` — enums exist, nothing wired, likely dead for
  a WotLK backend
