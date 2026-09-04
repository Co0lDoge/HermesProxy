# Baseline 2026-09 (pre-refactor): raw data

Companion to the dated summary in [perf.md](perf.md). This file keeps the full tables so a
later comparison has the whole distribution, not a hand-picked subset. Everything here was
captured on the `feature/wotlk-classic-v3.4.3` branch at 6ada57bf plus the Phase 0
measurement commit, before any of the packet-struct / session-loop work.

## Why these numbers exist

Every inbound modern packet today goes `SocketBuffer.Resize` (fresh `byte[]`) →
`new WorldPacket(byte[])` → `Activator.CreateInstance(packetType, packet)` → `ClientPacket`
instance → `Read()` → closure delegate → handler. Every outbound `ServerPacket` rents a
256-byte `ByteBuffer` in its constructor whether or not it is `ISpanWritable`, copies the
serialised bytes into a fresh `byte[]`, then `SendPacket` copies again for opcode framing and
again for the header. `ByteBuffer` carries a finalizer, so every `WorldPacket` that is not
disposed is promoted and finalised. The benchmarks below put a number on each of those steps
so the deferred redesign can be judged against them.

## Micro-benchmarks

### Host A: Windows dev box

```
BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

Command:

```
dotnet run --project HermesProxy.Benchmarks -c Release -- --filter "*PacketDispatch*" "*SendPipeline*" "*PackedGuid*" --exporters json
```

#### PacketDispatchBenchmarks (inbound, framed bytes → populated packet)

`*_Activator` is the production path (`WorldSocket.PacketHandler.Invoke`). `*_Direct` drops
the reflection. `*_Span` is the floor for a struct + `SpanPacketReader` design.

| Method                    | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| BuyBackItem_Activator     | 280.3504 ns | 187.7859 ns | 10.2932 ns | 1.001 |    0.04 | 0.0877 |     368 B |        1.00 |
| BuyBackItem_Direct        |  89.6094 ns |  14.1781 ns |  0.7771 ns | 0.320 |    0.01 | 0.0267 |     112 B |        0.30 |
| BuyBackItem_Span          |   5.9634 ns |   0.9159 ns |  0.0502 ns | 0.021 |    0.00 |      - |         - |        0.00 |
| SetActionButton_Activator | 260.6797 ns |  17.3339 ns |  0.9501 ns | 0.931 |    0.03 | 0.0839 |     352 B |        0.96 |
| SetActionButton_Direct    |  77.6962 ns |  11.1076 ns |  0.6088 ns | 0.277 |    0.01 | 0.0229 |      96 B |        0.26 |
| SetActionButton_Span      |   0.5492 ns |   1.0473 ns |  0.0574 ns | 0.002 |    0.00 |      - |         - |        0.00 |
| AttackSwing_Activator     | 270.8967 ns |  20.3100 ns |  1.1133 ns | 0.967 |    0.03 | 0.0858 |     360 B |        0.98 |
| AttackSwing_Direct        |  87.7382 ns |  21.7137 ns |  1.1902 ns | 0.313 |    0.01 | 0.0248 |     104 B |        0.28 |
| AttackSwing_Span          |   5.1427 ns |   0.3955 ns |  0.0217 ns | 0.018 |    0.00 |      - |         - |        0.00 |
| Whisper_Activator         | 348.0998 ns |  38.8482 ns |  2.1294 ns | 1.243 |    0.04 | 0.1316 |     552 B |        1.50 |
| Whisper_Direct            | 173.1497 ns | 105.2703 ns |  5.7702 ns | 0.618 |    0.03 | 0.0706 |     296 B |        0.80 |
| Whisper_Span              |  55.7181 ns |   4.2433 ns |  0.2326 ns | 0.199 |    0.01 | 0.0249 |     104 B |        0.28 |

Reading: reflection alone (`Activator` minus `Direct`) is ~190 ns and ~256 B per packet,
constant across packet shapes. The remaining `Direct` cost (~80 ns, ~100 B) is the
`ClientPacket` + `WorldPacket` objects. Value-only packets reach zero allocation on the span
path; the whisper's 104 B is the two `string`s, which any design must pay unless handlers
consume UTF-8 spans.

#### SendPipelineBenchmarks (outbound, `new ServerPacket()` → encrypted wire frame)

`*_Construct` is the constructor alone. `*_WritePacketData` adds serialisation.
`*_Wire` adds opcode framing, AES-GCM, and the 16-byte header, as `WorldSocket.SendPacket`
does today. `*_SpanOnly` is `WriteToSpan` into a caller-owned buffer.

| Method                          | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| PowerUpdate_Construct           | 457.32 ns | 194.22 ns | 10.646 ns |  1.00 |    0.03 | 0.0405 | 0.0257 | 0.0019 |     165 B |        1.00 |
| PowerUpdate_WritePacketData     | 156.40 ns |  21.73 ns |  1.191 ns |  0.34 |    0.01 | 0.0629 |      - |      - |     264 B |        1.60 |
| PowerUpdate_Wire                | 617.82 ns | 103.05 ns |  5.648 ns |  1.35 |    0.03 | 0.1297 |      - |      - |     544 B |        3.30 |
| PowerUpdate_SpanOnly            | 467.25 ns | 147.71 ns |  8.096 ns |  1.02 |    0.03 | 0.0381 | 0.0324 | 0.0010 |     158 B |        0.96 |
| MonsterMove_Construct           | 427.66 ns |  71.28 ns |  3.907 ns |  0.94 |    0.02 | 0.0510 | 0.0353 | 0.0014 |     210 B |        1.27 |
| MonsterMove_WritePacketData     | 223.63 ns | 188.27 ns | 10.320 ns |  0.49 |    0.02 | 0.0918 |      - |      - |     384 B |        2.33 |
| MonsterMove_Wire                | 710.50 ns | 468.85 ns | 25.699 ns |  1.55 |    0.06 | 0.1774 |      - |      - |     744 B |        4.51 |
| MonsterMove_SpanOnly            | 505.48 ns | 390.46 ns | 21.402 ns |  1.11 |    0.05 | 0.0792 | 0.0286 | 0.0067 |     311 B |        1.88 |
| CriteriaDeleted_Construct       | 359.41 ns | 101.26 ns |  5.551 ns |  0.79 |    0.02 | 0.0210 | 0.0143 | 0.0024 |      81 B |        0.49 |
| CriteriaDeleted_WritePacketData |  95.36 ns |  46.60 ns |  2.554 ns |  0.21 |    0.01 | 0.0324 |      - |      - |     136 B |        0.82 |
| CriteriaDeleted_Wire            | 509.87 ns |  20.18 ns |  1.106 ns |  1.12 |    0.02 | 0.0916 |      - |      - |     384 B |        2.33 |

Reading: the constructor-only rows are *slower* than constructor + serialise and are the
only rows with Gen1/Gen2 activity. `WritePacketData` disposes the `WorldPacket`; the
constructor-only and `SpanOnly` rows do not, so those measure the `ByteBuffer` finalizer
being queued and run (~300 ns and a promotion per undisposed packet). Any `ServerPacket`
constructed and dropped without being sent pays this in production. Wire bytes for a
~30-byte `PowerUpdate` are 544 B: three `byte[]` copies plus header and buffer objects.

#### PackedGuidBenchmarks

| Method                         | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------- |-----------:|-----------:|-----------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| WorldPacket_WritePackedGuid128 |  37.498 ns |  21.361 ns |  1.1708 ns |  1.00 |    0.04 | 0.0153 |      - |      - |      64 B |        1.00 |
| SpanWriter_WritePackedGuid128  |  17.969 ns |   1.541 ns |  0.0845 ns |  0.48 |    0.01 |      - |      - |      - |         - |        0.00 |
| WorldPacket_ReadPackedGuid128  | 187.225 ns | 264.596 ns | 14.5034 ns |  5.00 |    0.36 | 0.0153 | 0.0134 | 0.0017 |      64 B |        1.00 |
| SpanReader_ReadPackedGuid128   |   6.510 ns |   1.250 ns |  0.0685 ns |  0.17 |    0.01 |      - |      - |      - |         - |        0.00 |

Reading: `WorldPacket.PackUInt64` allocates `new byte[8]` per GUID half (64 B per GUID).
The read row includes the read-mode `WorldPacket` object because that is how every inbound
GUID is read today; its Gen1/Gen2 columns are the finalizer again.

## Live runs

Topology: proxy on the Windows dev box (i7-6700K), V3_4_3_54261 client on the same box,
AzerothCore + mod-playerbots (~500 bots active) in a colima VM on a Mac mini M4 at
192.168.88.44:3725, so the legacy socket crosses a wired LAN hop. Launched with
`test-loop2.ps1 -AcoreBots -EnterWorld -Metrics -QuietLogs -NoBuild`: Release build,
Information log levels, sniff capture on (`PacketsLog: true` is the production default).

Environment caveats found on the way, both fixed before the numbers below were taken:

- The Mac mini idle-sleeps after one minute (`pmset -g`: `sleep 1`, `powernap 1`), which
  freezes the VM and every AC process in it while the host still answers ping and ssh. Two
  early runs died with `SocketException (10060)` on the legacy socket 60-90 s after login.
  Fixed for the session with `caffeinate -i -s -w <limactl pid>`; the durable fix is
  `sudo pmset -c sleep 0 powernap 0`.
- Debug packet logging inflates per-packet allocation roughly 3-4× (a 813 B
  `CMSG_TIME_SYNC_RESPONSE` shows as ~2.8 KB), which is why `-QuietLogs` exists.

### Run 3: login → enter world → idle in Orgrimmar, 6 minutes (`hermes-20260902_143705.log`)

GC line per 60 s window:

| Uptime | C→S pkts (rate) | S→C pkts (rate) | gen0 / gen1 / gen2 | allocated | heap |
|---|---|---|---|---|---|
| 01:02 | 53 (0.9/s) | 443 (7.1/s) | +22 / +11 / +5 | 172.2 MB (2.77 MB/s) | 115.5 MB |
| 02:02 | 60 (0.1/s) | 650 (3.4/s) | 0 / 0 / 0 | 2.0 MB (0.03 MB/s) | 115.5 MB |
| 03:02 | 66 (0.1/s) | 816 (2.8/s) | 0 / 0 / 0 | 1.5 MB (0.02 MB/s) | 115.5 MB |
| 04:02 | 73 (0.1/s) | 1030 (3.6/s) | +1 / +1 / 0 | 2.1 MB (0.03 MB/s) | 116.0 MB |
| 05:02 | 79 (0.1/s) | 1200 (2.8/s) | 0 / 0 / 0 | 1.5 MB (0.02 MB/s) | 116.0 MB |
| 06:02 | 85 (0.1/s) | 1384 (3.1/s) | +1 / +1 / 0 | 1.6 MB (0.03 MB/s) | 116.0 MB |

The first window is startup (GameData + hotfix load) plus the login burst; steady idle
in-world is 1.5-2 MB/min with zero or one gen0 per minute.

Cumulative per-opcode allocation after 6 minutes, server → client (58.7 MB total):

| Opcode | Packets | Avg B | P99 B | Max B | Total KB | Share |
|---|---:|---:|---:|---:|---:|---:|
| SMSG_ITEM_QUERY_SINGLE_RESPONSE | 36 | 1,246,240 | 3,747,400 | 3,748,512 | 43,813.1 | 72.9% |
| SMSG_COMPRESSED_UPDATE_OBJECT | 92 | 117,866 | 1,889,889 | 3,350,352 | 10,589.5 | 17.6% |
| SMSG_ALL_ACHIEVEMENT_DATA | 1 | 1,364,032 | 1,364,032 | 1,364,032 | 1,332.1 | 2.2% |
| SMSG_UPDATE_OBJECT | 98 | 13,430 | 28,502 | 32,064 | 1,285.3 | 2.1% |
| SMSG_ON_MONSTER_MOVE | 531 | 1,563 | 2,166 | 7,648 | 810.6 | 1.3% |
| SMSG_AURA_UPDATE_ALL | 88 | 4,635 | 13,952 | 21,288 | 398.4 | 0.7% |
| SMSG_LOGIN_VERIFY_WORLD | 1 | 382,808 | 382,808 | 382,808 | 373.8 | 0.6% |
| SMSG_AURA_UPDATE | 105 | 3,141 | 4,220 | 4,248 | 322.1 | 0.5% |
| SMSG_SET_PROFICIENCY | 12 | 24,063 | 253,195 | 284,440 | 282.0 | 0.5% |
| SMSG_DESTROY_OBJECT | 98 | 1,564 | 2,013 | 2,696 | 149.7 | 0.2% |
| SMSG_SPELL_GO | 47 | 3,097 | 3,384 | 3,480 | 142.2 | 0.2% |
| SMSG_SPELL_START | 41 | 2,680 | 2,680 | 2,680 | 107.3 | 0.2% |

Client → server (2.87 MB total): `CMSG_PLAYER_LOGIN` 1 × 2,541,080 B (84.5%),
`CMSG_DB_QUERY_BULK` 3 × 67 KB, `CMSG_MOVE_HEARTBEAT` first packet 124 KB, then everything
else at 0.8-5 KB per packet (`CMSG_TIME_SYNC_RESPONSE` 36 × 813 B is the steady-state floor
for a trivial round trip through dispatch + translate + send).

Leads, in order of size:

1. **`SMSG_ITEM_QUERY_SINGLE_RESPONSE` at 1.25 MB per packet** (p99 3.7 MB) is 73% of all
   server-to-client allocation and is not a logging artefact. Every item the client sees for
   the first time pays it. First thing to profile with an allocation trace.
2. **`SMSG_COMPRESSED_UPDATE_OBJECT` at 118 KB average, 3.3 MB max**: inflate + parse +
   `ObjectUpdateBuilder` per create batch. Also the slowest steady handler (p99 17 ms, max
   130 ms in the BG run).
3. **`CMSG_PLAYER_LOGIN` 2.5 MB once** and `SMSG_ALL_ACHIEVEMENT_DATA` 1.36 MB once: login
   burst, one-off, lower priority.
4. **A recurring ~284 KB allocation** lands on whichever small packet is in flight
   (`SMSG_SET_PROFICIENCY` max 284,440 here; `SMSG_POWER_UPDATE` max 285,360 and
   `SMSG_FORCE_RUN_SPEED_CHANGE` max 283,312 in the BG run). Not the sniff writer (64 KB
   buffer). Smells like an `ArrayPool<byte>.Shared` 256 KB bucket miss. Open question;
   needs an allocation-tick trace to name the type.
5. `SMSG_ON_MONSTER_MOVE` 1.5 KB per packet × the highest steady volume: the benchmark's
   744 B wire cost plus the legacy parse. The zero-copy send path halves it.

### Run 5: Arathi Basin with bots (`hermes-20260902_144854.log`)

Player queued and played an Arathi Basin against/with playerbots. `dotnet-counters` attached
for 180 s starting 14:50:24, during the first minutes of the match
(`artifacts/profile/20260902_125022/counters.csv`, gitignored):

| Counter | Value over 177 s |
|---|---|
| `dotnet.gc.heap.total_allocated` rate | avg 0.98 MB/s, min 24 KB/s, max 6.1 MB/s |
| `dotnet.gc.collections` | gen0 32, gen1 10, gen2 0 |
| `dotnet.gc.pause.time` | 33 ms total (0.02%) |
| `dotnet.monitor.lock_contentions` | 4 total |
| `dotnet.process.cpu.time` | user 2.52 s + system 0.64 s = 1.8% of one core |
| `dotnet.gc.last_collection.heap.size` | gen2 ~72 MB, LOH 21.7 MB, gen1 0.1-2.9 MB |
| `dotnet.gc.last_collection.memory.committed_size` | 135-137 MB |
| `dotnet.process.memory.working_set` | 218-234 MB |
| `dotnet.thread_pool.work_item.count` | avg 47/s, max 146/s |
| `dotnet.exceptions` | `UnmappedOpcodeException` ~0.1/s (unmapped `SMSG_MOVE_SPLINE_SET_WALK_BACK_SPEED`; exception per packet on the hot path) |
| System.Net.Sockets bytes | 726 KB received, 1.27 MB sent |

The proxy is nowhere near CPU- or GC-bound at this load; the cost that matters is
allocation volume (steady ~1 MB/s, ~12 gen0/min) and the per-packet latency tail on the
big update batches.

Once the match was fully populated the 60 s summaries reached 620 packets/s server → client
(50,482 packets in one window), 1.5 MB/s allocated, 23 gen0 per minute, heap flat at 92 MB.

#### CPU profile during the match

`dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x4c14fccbd:5` for 90 s
starting 14:53:51, the 500-620 packets/s stretch
(`artifacts/profile/20260902_125351/trace.nettrace`, gitignored). A first trace taken while
`dotnet-counters` was attached is unusable: the EventCounter machinery
(`CounterGroup.OnTimer`, `PollingCounter.WritePayload`, `MetricsEventSource`) plus its string
formatting took 50% of samples. Never run the two together.

`dotnet-trace report topN --inclusive false`, clean trace:

| Rank | Frame | Inclusive | Exclusive |
|---:|---|---:|---:|
| 1 | `.ctor()` reached via `ObjectUpdate..ctor(WowGuid128, UpdateTypeModern, GlobalSessionData)` (42.3% inclusive) | 38.9% | 34.1% |
| 2 | `Enum.GetEnumInfo.InitializeEnumInfo` | 17.2% | 17.2% |
| 3 | Serilog `DisposingAggregateSink.Emit` | 21.8% | 7.7% |
| 4 | `String.Ctor(ReadOnlySpan<char>)` | 4.9% | 4.9% |
| 6 | `StringBuilder.AppendWithExpansion` | 4.4% | 4.4% |
| 7 | `LegacyVersion.GetUpdateField<T>` | 3.2% | 3.1% |
| 9 | `WorldClient.ReceiveLoop.MoveNext` | 72.4% | 2.9% |
| 10 | `WorldClient.ReadValuesUpdateBlock` | 2.8% | 2.3% |
| 13 | `WorldClient.SendPacket` | 1.4% | 1.4% |
| 14 | `ByteBuffer.GetData` | 1.3% | 1.3% |
| 15 | `DefaultBinder.BindToMethod` (reflection, `Activator.CreateInstance` per inbound packet) | 1.3% | 1.3% |
| 16 | `WorldPacket.PackUInt64` | 1.1% | 1.1% |

Inclusive: `WorldClient.HandlePacket` 66.5%, `HandleUpdateObject` 59.7%,
`HandleCompressedUpdateObject` 59.6%, `Enum.ToString` 14.4%, Serilog
`ThemedDisplayValueFormatter.FormatLiteralValue` 14.1%.

Two findings that were not visible from the allocation tables:

1. **`ObjectUpdate`'s constructor is a third of all CPU.** `UpdatePackets.cs:47-85` allocates
   the full field-data objects up front by object type: Player → `UnitData` + `PlayerData` +
   `ActivePlayerData`. `ActivePlayerData` (`World/Objects/ActivePlayerData.cs`) carries 33
   array initialisers: seven `ushort?[256]` skill arrays (~7 KB), `ulong?[875] QuestCompleted`
   (~14 KB), `ulong?[240] ExploredZones` (~4 KB), six `WowGuid128?[]` inventory slot arrays,
   `int?[32] CombatRatings`, and so on, roughly 30 KB zeroed per instance. Only the local
   player ever has ActivePlayer fields, yet every bot's every Values update pays for it. In a
   bot battleground almost every object is a Player. Lazily constructing `ActivePlayerData`
   (and the array-heavy parts of `UnitData`) only when a field is actually set is the highest
   value single change found today.
2. **Logging is a fifth of CPU at Information level.** Per-packet Warn messages
   (`MonsterMove exceeded MaxSize`, `No V3_4_3_54261 opcode mapping ... packet dropped`,
   `No handler for opcode`) go through interpolated `Log.Print` → Serilog → console theme,
   and each formats enum values (`Enum.ToString` → `GetEnumInfo`). Together with the
   `UnmappedOpcodeException` thrown per unmapped legacy packet, this is the production
   default cost of unmapped/oversized packets. The CLAUDE.md `[LoggerMessage]` rule exists for
   exactly this; these call sites predate it.

Reflection dispatch (`DefaultBinder.BindToMethod`) is visible but small at 1.3%: the inbound
client packet rate (5-7/s) is two orders of magnitude below the legacy inbound rate, so the
Activator cost matters for allocation per packet, not for CPU.

#### Cumulative per-opcode tables at 15 minutes (login + queue + one full Arathi Basin)

Windows during the match: 390-620 packets/s server → client, 1.5-2.3 MB/s allocated,
22-35 gen0 and 2-4 gen1 per minute, one gen2 in 15 minutes, heap flat at 92 MB.

Server → client, top by allocated bytes (1,009.41 MB total, 336,659 packets):

| Opcode | Packets | Avg B | P99 B | Max B | Total KB | Share |
|---|---:|---:|---:|---:|---:|---:|
| SMSG_COMPRESSED_UPDATE_OBJECT | 10,266 | 94,352 | 342,360 | 3,318,888 | 820,059.1 | 79.3% |
| SMSG_AURA_UPDATE | 16,060 | 3,130 | 4,208 | 37,168 | 49,948.0 | 4.8% |
| SMSG_ITEM_QUERY_SINGLE_RESPONSE | 36 | 1,241,796 | 3,747,090 | 3,748,512 | 43,656.9 | 4.2% |
| SMSG_ON_MONSTER_MOVE | 14,576 | 1,475 | 4,619 | 9,848 | 22,130.9 | 2.1% |
| SMSG_SPELL_GO | 5,795 | 3,166 | 3,600 | 20,792 | 17,940.1 | 1.7% |
| SMSG_UPDATE_OBJECT | 1,217 | 11,001 | 23,345 | 41,968 | 13,307.2 | 1.3% |
| SMSG_PARTY_MEMBER_PARTIAL_STATE | 254,885 | 64 | 840 | 4,312 | 10,729.9 | 1.0% |
| SMSG_POWER_UPDATE | 6,792 | 1,356 | 2,376 | 285,360 | 9,881.5 | 1.0% |
| SMSG_SPELL_START | 3,083 | 2,777 | 3,704 | 3,752 | 8,302.1 | 0.8% |
| SMSG_AURA_UPDATE_ALL | 570 | 11,358 | 35,869 | 41,632 | 6,322.4 | 0.6% |
| SMSG_DESTROY_OBJECT | 3,590 | 1,572 | 2,040 | 2,064 | 5,499.9 | 0.5% |
| SMSG_FORCE_RUN_SPEED_CHANGE | 74 | 54,231 | 283,312 | 283,312 | 3,919.1 | 0.4% |
| SMSG_ATTACKER_STATE_UPDATE | 1,253 | 2,109 | 2,392 | 2,600 | 2,587.2 | 0.3% |
| SMSG_ALL_ACHIEVEMENT_DATA | 2 | 1,318,988 | 1,363,131 | 1,364,032 | 2,576.1 | 0.2% |
| SMSG_SPELL_PERIODIC_AURA_LOG | 2,884 | 856 | 856 | 904 | 2,410.1 | 0.2% |
| SMSG_SPELL_NON_MELEE_DAMAGE_LOG | 2,037 | 726 | 984 | 1,176 | 1,445.9 | 0.1% |
| MSG_MOVE_HEARTBEAT | 1,005 | 1,251 | 1,464 | 1,656 | 1,228.8 | 0.1% |
| SMSG_STAND_STATE_UPDATE | 2,672 | 409 | 616 | 1,368 | 1,081.4 | 0.1% |
| SMSG_MOVE_SPLINE_SET_WALK_BACK_SPEED (unmapped, dropped) | 37 | 28,599 | 313,888 | 313,888 | 1,033.4 | 0.1% |
| SMSG_CRITERIA_UPDATE | 1,369 | 656 | 656 | 656 | 877.0 | 0.1% |

Server → client latency: nothing above 1 ms at p50 except the login-time packets;
`SMSG_COMPRESSED_UPDATE_OBJECT` p99 17.6 ms / max 129.7 ms (from the earlier snapshot),
`SMSG_GROUP_LIST` p99 3.7 ms, `SMSG_CHAT` p99 2.4 ms.

Client → server, top by allocated bytes (92.55 MB total, 5,052 packets):

| Opcode | Packets | Avg B | P99 B | Max B | Total KB | Share |
|---|---:|---:|---:|---:|---:|---:|
| CMSG_MOVE_SET_FACING_HEARTBEAT | 1,397 | 27,863 | 394,136 | 394,304 | 34,774.6 | 36.7% |
| CMSG_MOVE_HEARTBEAT | 291 | 65,993 | 394,120 | 394,120 | 18,753.8 | 19.8% |
| CMSG_MOVE_STOP_STRAFE | 305 | 18,158 | 394,120 | 394,120 | 5,408.4 | 5.7% |
| CMSG_MOVE_SET_PITCH | 156 | 32,293 | 394,120 | 394,120 | 4,919.6 | 5.2% |
| CMSG_MOVE_SET_FACING | 161 | 28,921 | 394,120 | 394,120 | 4,547.2 | 4.8% |
| CMSG_MOVE_START_STRAFE_LEFT | 154 | 27,260 | 394,136 | 394,256 | 4,099.7 | 4.3% |
| CMSG_MOVE_START_STRAFE_RIGHT | 153 | 27,065 | 394,136 | 394,200 | 4,043.8 | 4.3% |
| CMSG_MOVE_START_FORWARD | 102 | 37,598 | 394,191 | 394,408 | 3,745.1 | 4.0% |
| CMSG_PLAYER_LOGIN | 1 | 2,425,104 | 2,425,104 | 2,425,104 | 2,368.3 | 2.5% |
| CMSG_MOVE_FALL_LAND | 79 | 24,763 | 394,120 | 394,120 | 1,910.4 | 2.0% |
| CMSG_MOVE_STOP | 161 | 11,863 | 393,336 | 394,104 | 1,865.2 | 2.0% |

Client → server latency: movement p50 0.07 ms, p99 1.8-2.1 ms, max 3.9 ms.

Observations specific to this run:

- **`SMSG_COMPRESSED_UPDATE_OBJECT` is 79% of all allocation under load**, 94 KB per packet
  on average, and it is the same code path the CPU trace put at 60% of samples. The
  `ActivePlayerData`-per-Player construction explains both numbers at once.
- **Every client movement packet costs 12-66 KB on average with a hard 394 KB spike** at
  the p99. The spike is the same constant on every movement opcode, and on the legacy thread
  the constant is ~284 KB (`SMSG_POWER_UPDATE`, `SMSG_FORCE_RUN_SPEED_CHANGE`,
  `SMSG_SET_PROFICIENCY`). Two threads, two constants, attributed to whichever handler is
  running: something periodic and large is being allocated per thread. The sniff writer's
  buffer is 64 KB, so it is not that. Resolve with an in-process `EventListener` on
  `GCAllocationTick` (type name + size) or a PerfView allocation trace before touching the
  movement handlers; the 394 KB is 94% of the movement bytes.
- **Dropping an unmapped legacy packet costs ~28 KB and an exception.**
  `SMSG_MOVE_SPLINE_SET_WALK_BACK_SPEED` is unmapped on V3_4_3 and each of its 37 arrivals
  threw `UnmappedOpcodeException`, was caught, and was logged at Warn with enum formatting.
- `SMSG_PARTY_MEMBER_PARTIAL_STATE`: 254,885 packets at 64 B average. The 2026-08-25
  throttle work holds; the un-throttled path is no longer visible in the profile.
- No `OBJECT_UPDATE_FAILED`, no reason-7 disconnect, no legacy socket error during the match
  (see the corpse-Values note under Run 4 for the disconnect that ended the previous attempt).

### Host B: Mac mini M4 (quiet box, only the AzerothCore playerbots stack running)

```
BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.2 (25F84) [Darwin 25.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

Same commit, same command. SDK installed user-locally at `~/.dotnet` (no admin); repo at
a checkout on the Mac. Use this host for timing comparisons: the error bars are
2-5× tighter than on the Windows dev box. Allocation columns match Host A byte for byte except
where noted.

#### PacketDispatchBenchmarks

| Method                    | Mean        | Error      | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |------------:|-----------:|----------:|------:|-------:|----------:|------------:|
| BuyBackItem_Activator     | 122.0946 ns | 17.4862 ns | 0.9585 ns | 1.000 | 0.0439 |     368 B |        1.00 |
| BuyBackItem_Direct        |  36.7783 ns |  6.7828 ns | 0.3718 ns | 0.301 | 0.0134 |     112 B |        0.30 |
| BuyBackItem_Span          |   1.9733 ns |  3.7322 ns | 0.2046 ns | 0.016 |      - |         - |        0.00 |
| SetActionButton_Activator | 137.5370 ns | 10.9138 ns | 0.5982 ns | 1.127 | 0.0420 |     352 B |        0.96 |
| SetActionButton_Direct    |  40.3626 ns |  8.0563 ns | 0.4416 ns | 0.331 | 0.0114 |      96 B |        0.26 |
| SetActionButton_Span      |   0.0000 ns |  0.0000 ns | 0.0000 ns | 0.000 |      - |         - |        0.00 |
| AttackSwing_Activator     | 120.4108 ns |  4.9743 ns | 0.2727 ns | 0.986 | 0.0429 |     360 B |        0.98 |
| AttackSwing_Direct        |  37.2090 ns | 12.9299 ns | 0.7087 ns | 0.305 | 0.0124 |     104 B |        0.28 |
| AttackSwing_Span          |   1.8202 ns |  0.9499 ns | 0.0521 ns | 0.015 |      - |         - |        0.00 |
| Whisper_Activator         | 160.7774 ns | 19.0077 ns | 1.0419 ns | 1.317 | 0.0658 |     552 B |        1.50 |
| Whisper_Direct            |  73.6598 ns |  3.7003 ns | 0.2028 ns | 0.603 | 0.0353 |     296 B |        0.80 |
| Whisper_Span              |  28.9254 ns |  0.0490 ns | 0.0027 ns | 0.237 | 0.0124 |     104 B |        0.28 |

#### SendPipelineBenchmarks

| Method                          | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| PowerUpdate_Construct           | 268.53 ns |  69.03 ns |  3.784 ns |  1.00 |    0.02 | 0.0229 | 0.0086 | 0.0014 |     183 B |        1.00 |
| PowerUpdate_WritePacketData     |  73.30 ns |  97.82 ns |  5.362 ns |  0.27 |    0.02 | 0.0315 |      - |      - |     264 B |        1.44 |
| PowerUpdate_Wire                | 766.04 ns | 471.67 ns | 25.854 ns |  2.85 |    0.09 | 0.2451 | 0.0010 |      - |    2056 B |       11.23 |
| PowerUpdate_SpanOnly            | 289.86 ns |  76.35 ns |  4.185 ns |  1.08 |    0.02 | 0.0196 | 0.0091 | 0.0005 |     163 B |        0.89 |
| MonsterMove_Construct           | 346.46 ns |  15.30 ns |  0.838 ns |  1.29 |    0.02 | 0.0262 | 0.0110 | 0.0010 |     214 B |        1.17 |
| MonsterMove_WritePacketData     |  88.22 ns |  10.02 ns |  0.549 ns |  0.33 |    0.00 | 0.0459 |      - |      - |     384 B |        2.10 |
| MonsterMove_Wire                | 941.93 ns | 115.56 ns |  6.334 ns |  3.51 |    0.05 | 0.2689 | 0.0010 |      - |    2256 B |       12.33 |
| MonsterMove_SpanOnly            | 265.61 ns |  49.54 ns |  2.715 ns |  0.99 |    0.01 | 0.0362 | 0.0148 | 0.0014 |     293 B |        1.60 |
| CriteriaDeleted_Construct       | 253.90 ns |  40.78 ns |  2.235 ns |  0.95 |    0.01 | 0.0145 | 0.0076 | 0.0010 |     115 B |        0.63 |
| CriteriaDeleted_WritePacketData |  36.64 ns |  20.60 ns |  1.129 ns |  0.14 |    0.00 | 0.0162 |      - |      - |     136 B |        0.74 |
| CriteriaDeleted_Wire            | 584.68 ns | 126.16 ns |  6.915 ns |  2.18 |    0.03 | 0.2260 | 0.0010 |      - |    1896 B |       10.36 |

Reading: the `*_Wire` rows allocate ~1.5 KB more than on Windows. `WorldCrypt` uses the
BouncyCastle GCM fallback on macOS (the platform `AesGcm` rejects the 12-byte tag), and that
path allocates per call. Relevant to anyone hosting the proxy on a Mac; irrelevant to the
Windows/Linux figures.

#### PackedGuidBenchmarks

| Method                         | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------- |-----------:|-----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| WorldPacket_WritePackedGuid128 |  18.635 ns |  0.3317 ns | 0.0182 ns |  1.00 |    0.00 | 0.0076 |      - |      64 B |        1.00 |
| SpanWriter_WritePackedGuid128  |   7.724 ns |  0.0172 ns | 0.0009 ns |  0.41 |    0.00 |      - |      - |         - |        0.00 |
| WorldPacket_ReadPackedGuid128  | 108.312 ns | 13.9900 ns | 0.7668 ns |  5.81 |    0.04 | 0.0076 | 0.0038 |      64 B |        1.00 |
| SpanReader_ReadPackedGuid128   |   1.988 ns |  0.1114 ns | 0.0061 ns |  0.11 |    0.00 |      - |      - |         - |        0.00 |
