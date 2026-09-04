using HermesProxy.World;
using HermesProxy.World.Objects;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class BattlefieldMgrTranslationTests
{
    [Fact]
    public void TicketFor_EntryAndQueue_DoNotCollideWithBgSlots()
    {
        uint entry = BattlefieldMgrTranslation.TicketFor(1, BattlefieldMgrTicketKind.Entry);
        uint queue = BattlefieldMgrTranslation.TicketFor(1, BattlefieldMgrTicketKind.Queue);

        Assert.Equal(101u, entry);
        Assert.Equal(201u, queue);
        Assert.NotEqual(entry, queue);
        Assert.True(entry > 3);
    }

    [Fact]
    public void TryDecodeTicket_RoundTripsEntryAndQueue()
    {
        Assert.True(BattlefieldMgrTranslation.TryDecodeTicket(101u, out uint entryBattle, out var entryKind));
        Assert.Equal(1u, entryBattle);
        Assert.Equal(BattlefieldMgrTicketKind.Entry, entryKind);

        Assert.True(BattlefieldMgrTranslation.TryDecodeTicket(201u, out uint queueBattle, out var queueKind));
        Assert.Equal(1u, queueBattle);
        Assert.Equal(BattlefieldMgrTicketKind.Queue, queueKind);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(100u)]
    [InlineData(200u)]
    public void TryDecodeTicket_RejectsBgAndBaseSlots(uint ticket)
    {
        Assert.False(BattlefieldMgrTranslation.TryDecodeTicket(ticket, out _, out _));
    }

    [Fact]
    public void ListIdForBattle_WintergraspIsClassicBattlemasterList()
    {
        Assert.Equal(1089u, BattlefieldMgrTranslation.ListIdForBattle(1));
        Assert.Equal(2u, BattlefieldMgrTranslation.ListIdForBattle(2));
    }

    [Fact]
    public void TimeoutMs_UsesRemainingSeconds()
    {
        Assert.Equal(20_000u, BattlefieldMgrTranslation.TimeoutMs(1_000, 980));
        Assert.Equal(BattlefieldMgrTranslation.DefaultInviteTimeoutMs,
            BattlefieldMgrTranslation.TimeoutMs(100, 200));
    }

    [Theory]
    [InlineData(4197u, true, true)]
    [InlineData(4197u, false, false)]
    [InlineData(1519u, true, false)]
    [InlineData(0u, true, false)]
    public void ShouldRouteLeaveToMgr_OnlyWhileInWintergraspWithTicket(
        uint zoneId, bool hasTicket, bool expected)
    {
        Assert.Equal(expected, BattlefieldMgrTranslation.ShouldRouteLeaveToMgr(zoneId, hasTicket));
    }

    [Fact]
    public void PhaseShift_DefaultMaskIsUnphased()
    {
        var msg = PhaseShiftTranslation.ToModern(1, WowGuid128.Empty);
        Assert.Equal(8u, msg.PhaseShiftFlags);
        Assert.Empty(msg.Phases);
    }

    [Fact]
    public void PhaseShift_FactoryBitsMapToClassicPhaseIds()
    {
        var horde = PhaseShiftTranslation.ToModern(1u | 16u, WowGuid128.Empty);
        Assert.Equal(0u, horde.PhaseShiftFlags);
        Assert.Single(horde.Phases);
        Assert.Equal((ushort)1, horde.Phases[0].Flags);
        Assert.Equal((ushort)173, horde.Phases[0].Id);

        var alliance = PhaseShiftTranslation.ToModern(1u | 32u, WowGuid128.Empty);
        Assert.Single(alliance.Phases);
        Assert.Equal((ushort)174, alliance.Phases[0].Id);
    }
}
