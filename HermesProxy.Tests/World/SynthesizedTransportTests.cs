using HermesProxy.World.Objects;
using Xunit;

namespace HermesProxy.Tests.World;

public class SynthesizedTransportTests
{
    private static SynthesizedTransport WithDeadline(uint deadline) =>
        new(60133, default, 0f, deadline, 25);

    [Fact]
    public void IsSailing_NoDeadlineScheduled_False()
    {
        var transport = new SynthesizedTransport(60133, default, 0f);

        Assert.False(transport.IsSailing(now: 1000));
    }

    [Fact]
    public void IsSailing_DeadlineAhead_True()
    {
        Assert.True(WithDeadline(154524).IsSailing(now: 94391));
    }

    [Fact]
    public void IsSailing_DeadlineReached_False()
    {
        Assert.False(WithDeadline(154524).IsSailing(now: 154524));
        Assert.False(WithDeadline(154524).IsSailing(now: 154525));
    }

    [Fact]
    public void IsSailing_DeadlineAheadAcrossClockWrap_True()
    {
        // The proxy clock is ms since start and wraps after 49 days. A deadline stamped
        // just past the wrap must still read as ahead of a clock just before it.
        Assert.True(WithDeadline(100).IsSailing(now: uint.MaxValue - 50));
    }

    [Fact]
    public void IsSailing_DeadlineJustPassedAcrossClockWrap_False()
    {
        // ...and the mirror image must not read as 49 days ahead.
        Assert.False(WithDeadline(uint.MaxValue - 50).IsSailing(now: 100));
    }
}
