using HermesProxy.World;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class ArenaQueueTests
{
    [Theory]
    [InlineData(false, false, (byte)0, (byte)2)]
    [InlineData(false, true, (byte)3, (byte)2)]
    [InlineData(true, false, (byte)3, (byte)0)]
    [InlineData(true, true, (byte)2, (byte)2)]
    [InlineData(true, true, (byte)3, (byte)3)]
    [InlineData(true, true, (byte)5, (byte)5)]
    [InlineData(true, true, (byte)0, (byte)2)]
    public void ForLegacyPort_UsesQueuedArenaTypeOnV343Arenas(bool isV343, bool isArena, byte queued, byte expected)
    {
        Assert.Equal(expected, BattlefieldQueueArenaType.ForLegacyPort(isV343, isArena, queued));
    }

    [Theory]
    [InlineData(-3, 3)]
    [InlineData(-12, 12)]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    public void ToModernJoinError_NegatesAcCodes(int ac, int modern)
    {
        Assert.Equal(modern, BattlefieldQueueArenaType.ToModernJoinError(ac));
    }

    [Fact]
    public void JoinErrorText_PartySizeAndGroupFailed()
    {
        Assert.Equal("Incorrect party size for this arena.", BattlefieldQueueArenaType.JoinErrorText(-3, null));
        Assert.Equal("Join as a group failed. Every grouped player must be on the same arena team.", BattlefieldQueueArenaType.JoinErrorText(-12, null));
        Assert.Equal("Mildeena was unavailable to join the queue.", BattlefieldQueueArenaType.JoinErrorText(-11, "Mildeena"));
        Assert.Null(BattlefieldQueueArenaType.JoinErrorText(6, null));
    }
}
