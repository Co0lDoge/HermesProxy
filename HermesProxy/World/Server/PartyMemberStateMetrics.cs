using System;
using System.Threading;
using Framework.Logging;

namespace HermesProxy.World.Server;

/// <summary>
/// Instrumentation for the SMSG_PARTY_MEMBER_PARTIAL_STATE hot path, which a bot-filled
/// battleground drives at over 1,500 packets per second. Counts what arrives, what survives
/// the throttle, and how many managed bytes the parse plus send allocates, then reports a
/// windowed summary so runs can be compared with the throttle on and off.
///
/// Counters are process-wide and lock-free; the report is emitted from whichever thread
/// crosses the window boundary.
/// </summary>
public static class PartyMemberStateMetrics
{
    private const long ReportIntervalMs = 10_000;

    private static long _parsed;
    private static long _forwarded;
    private static long _throttled;
    private static long _withAuras;
    private static long _withPosition;
    private static long _allocBytesForwarded;
    private static long _allocBytesThrottled;
    private static long _highFreqOnly;
    private static long _fullMask;
    private static long _withStatusBit;
    private static long _windowStartMs = Environment.TickCount64;

    // Fields a raid frame cannot infer between updates; anything outside this set means the
    // update is safe to rate limit.
    private const uint HighFrequencyMask =
        0x00000002 | 0x00000004 |            // CurrentHealth, MaxHealth
        0x00000008 | 0x00000010 | 0x00000020 | // PowerType, CurrentPower, MaxPower
        0x00000100;                          // Position

    public static void RecordFlags(uint flags)
    {
        if ((flags & ~HighFrequencyMask) == 0)
            Interlocked.Increment(ref _highFreqOnly);
        if (flags == 0x000FFFFF)
            Interlocked.Increment(ref _fullMask);
        if ((flags & 0x00000001) != 0)
            Interlocked.Increment(ref _withStatusBit);
    }

    public static void RecordThrottled() => Interlocked.Increment(ref _throttled);

    public static void RecordForwarded(long allocatedBytes, bool hasAuras, bool hasPosition)
    {
        Interlocked.Increment(ref _parsed);
        Interlocked.Increment(ref _forwarded);
        Interlocked.Add(ref _allocBytesForwarded, allocatedBytes);
        if (hasAuras)
            Interlocked.Increment(ref _withAuras);
        if (hasPosition)
            Interlocked.Increment(ref _withPosition);
    }

    public static void RecordParsedOnly(long allocatedBytes)
    {
        Interlocked.Increment(ref _parsed);
        Interlocked.Add(ref _allocBytesThrottled, allocatedBytes);
    }

    /// <summary>
    /// Emits a summary when the reporting window has elapsed, then resets the counters.
    /// Cheap enough to call on every packet: one TickCount64 read in the common case.
    /// </summary>
    public static void MaybeReport()
    {
        long nowMs = Environment.TickCount64;
        long startMs = Interlocked.Read(ref _windowStartMs);
        long elapsedMs = nowMs - startMs;
        if (elapsedMs < ReportIntervalMs)
            return;

        if (Interlocked.CompareExchange(ref _windowStartMs, nowMs, startMs) != startMs)
            return; // another thread is reporting this window

        long parsed = Interlocked.Exchange(ref _parsed, 0);
        long forwarded = Interlocked.Exchange(ref _forwarded, 0);
        long throttled = Interlocked.Exchange(ref _throttled, 0);
        long withAuras = Interlocked.Exchange(ref _withAuras, 0);
        long withPosition = Interlocked.Exchange(ref _withPosition, 0);
        long highFreqOnly = Interlocked.Exchange(ref _highFreqOnly, 0);
        long fullMask = Interlocked.Exchange(ref _fullMask, 0);
        long withStatusBit = Interlocked.Exchange(ref _withStatusBit, 0);
        long allocFwd = Interlocked.Exchange(ref _allocBytesForwarded, 0);
        long allocThr = Interlocked.Exchange(ref _allocBytesThrottled, 0);
        long allocBytes = allocFwd + allocThr;

        if (parsed == 0 && throttled == 0)
            return;

        double seconds = elapsedMs / 1000.0;
        double inRate = parsed / seconds;   // parsed covers forwarded + throttled
        double perForwarded = forwarded > 0 ? (double)allocFwd / forwarded : 0;
        double perThrottled = throttled > 0 ? (double)allocThr / throttled : 0;

        Log.Print(LogType.Server,
            $"[PartyStateMetrics] window={seconds:F1}s in={parsed} ({inRate:F0}/s) " +
            $"forwarded={forwarded} throttled={throttled} " +
            $"alloc={allocBytes / 1024.0 / 1024.0:F2}MB ({allocBytes / 1024.0 / seconds:F0}KB/s) " +
            $"fwd={allocFwd / 1024.0 / 1024.0:F2}MB ({perForwarded:F0}B/pkt) " +
            $"wasted={allocThr / 1024.0 / 1024.0:F2}MB ({perThrottled:F0}B/pkt) " +
            $"withAuras={withAuras} withPosition={withPosition} " +
            $"highFreqOnly={highFreqOnly} fullMask={fullMask} statusBit={withStatusBit} " +
            $"gcTotal={GC.GetTotalAllocatedBytes(false) / 1024.0 / 1024.0:F0}MB " +
            $"gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)}");
    }
}
