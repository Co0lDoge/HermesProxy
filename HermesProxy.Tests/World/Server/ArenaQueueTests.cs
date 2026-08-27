using System;
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

    // TrinityCore packs CMSG_BATTLEFIELD_PORT as a single uint64 queue id and matches the
    // whole struct: BattlemasterListId = (p >> 16) & 0xFFFF, BracketId = (p >> 8) & 0x7F,
    // TeamSize = p & 0x7F, plus a 0x1F90 marker in the top bits. The legacy field order
    // the proxy writes lines up with that packing byte for byte, so a hardcoded bracket
    // silently addresses a queue the player is not in.
    [Theory]
    [InlineData(6u, (byte)14, (byte)3)]   // 3v3 skirmish, level 80 bracket
    [InlineData(6u, (byte)0, (byte)2)]    // 2v2, first bracket
    [InlineData(2u, (byte)9, (byte)0)]    // Warsong Gulch, non-arena
    public void LegacyPortFieldsPackIntoTrinityQueueId(uint bgTypeId, byte bracketId, byte teamSize)
    {
        // Same byte order as HandleBattlefieldPort writes for V2_0_1+.
        var bytes = new byte[8];
        bytes[0] = teamSize;
        bytes[1] = bracketId;
        BitConverter.GetBytes(bgTypeId).CopyTo(bytes, 2);
        BitConverter.GetBytes((ushort)0x1F90).CopyTo(bytes, 6);
        ulong packed = BitConverter.ToUInt64(bytes, 0);

        Assert.Equal(bgTypeId, (uint)((packed >> 16) & 0xFFFF));
        Assert.Equal(bracketId, (byte)((packed >> 8) & 0x7F));
        Assert.Equal(teamSize, (byte)(packed & 0x7F));
        Assert.Equal(0x1F90000000000000UL, packed & 0xFFFF000000000000UL);
    }
}
