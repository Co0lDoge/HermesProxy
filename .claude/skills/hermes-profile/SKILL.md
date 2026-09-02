---
name: hermes-profile
description: Attach .NET diagnostics (dotnet-trace / counters / gcdump / dump) or run BenchmarkDotNet against a live HermesProxy process — for CPU and memory profiling
user-invocable: true
---

# HermesProxy Profile

Attach Microsoft's `dotnet-trace` / `dotnet-counters` / `dotnet-gcdump` / `dotnet-dump` to a running HermesProxy process to find CPU hot paths, allocation rates, GC pressure, and memory leaks. Plus a thin wrapper around the existing `HermesProxy.Benchmarks` project for micro-benchmarks of specific functions.

Use **release builds** for sampling — `Debug` skews hot-path rankings (inlining off, optimizations off). Pair this skill with `/hermes-run release` (and optionally `--metrics` for the proxy's own per-opcode latency report).

## Subcommands

```
/hermes-profile install                        # one-time: install the four CLI tools
/hermes-profile cpu [seconds=30] [--detailed]  # CPU sampling → trace.nettrace + speedscope JSON
/hermes-profile counters [seconds=60]          # Live runtime counters → counters.csv
/hermes-profile heap                           # Heap snapshot → trace.gcdump
/hermes-profile dump                           # Process dump → trace.dmp (for hangs/deadlocks)
/hermes-profile bench <filter>                 # Run a BenchmarkDotNet filter (e.g. *OpcodeLookup*)
/hermes-profile pid                            # Just print the detected HermesProxy PID
```

### Dispatch (script per subcommand)

| Subcommand | Script |
|---|---|
| `install` | `pwsh .claude\skills\hermes-profile\scripts\install.ps1` |
| `cpu [seconds] [--detailed]` | `pwsh .claude\skills\hermes-profile\scripts\cpu.ps1 [-Seconds <int>] [-Detailed] [-TargetPid <int>]` |
| `counters [seconds]` | `pwsh .claude\skills\hermes-profile\scripts\counters.ps1 [-Seconds <int>] [-TargetPid <int>] [-Counters <list>]` |
| `heap` | `pwsh .claude\skills\hermes-profile\scripts\heap.ps1 [-TargetPid <int>]` |
| `dump` | `pwsh .claude\skills\hermes-profile\scripts\dump.ps1 [-TargetPid <int>] [-Type Heap\|Full\|Mini\|Triage]` |
| `bench <filter>` | `pwsh .claude\skills\hermes-profile\scripts\bench.ps1 -Filter <pattern>` |
| `pid` | `pwsh .claude\skills\hermes-profile\scripts\pid.ps1` |

PID-detection lives in `scripts\_common.ps1` (dot-sourced; not user-callable). All scripts resolve the repo root via `git rev-parse --show-toplevel`.

Output goes to `artifacts/profile/<UTC-timestamp>/` (already in `.gitignore`).
BenchmarkDotNet results go to `BenchmarkDotNet.Artifacts/results/` (also gitignored).

## Setup

```
/hermes-profile install
```

Idempotent: checks `dotnet tool list -g` first, only installs missing tools. Tools land in `~/.dotnet/tools` — make sure that's on `PATH`.

## Typical workflow

1. Terminal A: `/hermes-run release --metrics`
2. Wait for `Listening for modern client connections on port 1119` and connect with the WoW client.
3. Terminal B: `/hermes-profile cpu 60` → drop into a steady gameplay state for 60s.
4. Open `artifacts/profile/<ts>/trace.speedscope.json` at <https://www.speedscope.app> (drag-drop, all client-side) or in PerfView. Hot frames appear at the top.
5. If allocations look high in counters.csv → `/hermes-profile heap` to see what's retained.
6. If you find a specific suspect → `/hermes-profile bench "*Foo*"` to micro-benchmark it.

## How PID detection works

Every subcommand that needs a process ID runs `dotnet-trace ps`, filters lines whose command line contains `HermesProxy` (excluding the dotnet-trace process itself), and:
- 0 matches → error: "no HermesProxy process found, start it with `/hermes-run` first"
- 1 match → uses it
- N matches → error: "multiple HermesProxy processes found", lists them, asks for explicit `-Pid`

Override with `-Pid <int>` on any subcommand.

## Subcommand details

### `cpu [seconds=30] [--detailed]`
Calls `dotnet-trace collect --profile cpu-sampling -p <pid> --duration 00:00:<sec>` then `dotnet-trace convert --format speedscope`.

`--detailed` swaps `--profile cpu-sampling` for the full provider mask `Microsoft-Windows-DotNETRuntime:0x4c14fccbd:5` (GC + JIT + contention + threadpool + exceptions + async). Higher overhead, deeper detail. Use it when sampling alone doesn't explain a regression.

Output: `artifacts/profile/<ts>/trace.nettrace` + `trace.speedscope.json`.

### `counters [seconds=60]`
Calls `dotnet-counters collect -p <pid> --counters System.Runtime,System.Net.Sockets --refresh-interval 1 --format csv -o counters.csv` for `<seconds>` seconds.

Useful columns to watch: `cpu-usage`, `gc-heap-size`, `alloc-rate`, `gen-2-gc-count`, `threadpool-thread-count`, `monitor-lock-contention-count`, `exception-count`.

Output: `artifacts/profile/<ts>/counters.csv`.

### `heap`
Calls `dotnet-gcdump collect -p <pid>`, then `dotnet-gcdump report` for a text top-types summary.

Output: `artifacts/profile/<ts>/trace.gcdump` + `trace.gcdump.txt`.

For interactive analysis, open `.gcdump` in Visual Studio or PerfView.

### `dump`
Calls `dotnet-dump collect -p <pid>` for a full process dump. Use when the proxy hangs or you suspect a deadlock — sampling can't see a thread that isn't running.

Output: `artifacts/profile/<ts>/trace.dmp`.

To analyse: `dotnet-dump analyze artifacts\profile\<ts>\trace.dmp`, then commands like `clrstack -all`, `dumpheap -stat`, `gcroot <addr>`, `parallelstacks`.

### `bench <filter>`
Wraps `dotnet run --project HermesProxy.Benchmarks -c Release -- --filter "<filter>" --exporters json`. The filter syntax is BenchmarkDotNet's (e.g. `*OpcodeLookup*`, `Namespace.Class.Method`).

Output: `BenchmarkDotNet.Artifacts/results/*.json` (also `.md` and `.html`).

The `HermesProxy.Benchmarks` project already covers `ByteBuffer*`, `SpanPacket*`, `BnetPacketParser*`, `OpcodeLookup*`, `UpdateField*`, `Logging*`, `MonsterMove*` — list with `bench --list flat`.

To benchmark a new function (e.g. `GetFirstFreeId`), add a `[Benchmark]`-annotated class to `HermesProxy.Benchmarks/` first; this skill won't write benchmark code for you.

### `pid`
Prints the detected HermesProxy PID and exits. Useful for piping or sanity checks.

## Pitfalls

- **Tools not on PATH.** After `install`, `~/.dotnet/tools` must be on `$env:PATH`. PowerShell sessions opened before installation don't see new tools — reopen the shell or `$env:PATH += ';' + "$HOME\.dotnet\tools"`.
- **Profile a Release build.** Profiling Debug gives you a flamegraph dominated by stack-frame setup, not real hot code.
- **Sampling captures elapsed wall time, not work.** A function that spends 90% of its time blocked on `socket.Receive` will dominate the flamegraph; that's not a bug, that's idle. Sort by `Self time` and discard frames that are obviously I/O wait.
- **`GetFirstFreeId` is unlikely to appear.** It runs once per DBC/hotfix entry during initialization, not on the per-packet path. If it does appear, it's because a hotfix replay is firing during the capture window — extend the capture to cover steady-state gameplay instead.
- **`--metrics` and `dotnet-trace` measure different things.** `--metrics` (in HermesProxy itself) measures handler latency and managed bytes allocated per opcode, plus GC counts. `dotnet-trace` measures CPU time per stack frame. They're complementary, not redundant.
- **`dotnet-dump` files are big.** A live HermesProxy dump is typically 200–500 MB. Don't accidentally commit one.

## See also

- `/hermes-run release [--metrics]` — what to launch first.
- `/hermes-logs` — for the proxy's own log output (errors, opcodes, session summary).
