using System;
using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class QuestPushReasonMappingTests
{
    [Fact]
    public void WotlkLogFull_IsBusy_IfPassedThrough()
    {
        // Verified against a live V3_4_3.54261 client: 4 renders "is too far away to
        // receive your quest", 5 "is busy", 6 "is dead". So a passed-through AC
        // LOG_FULL = 5 shows "is busy", and the client's LogFull is 7.
        Assert.Equal(5, (int)QuestPushReasonWotLK.LogFull);
        Assert.Equal(5, (int)QuestPushReason.Busy);
        Assert.Equal(7, (int)QuestPushReason.LogFull);
    }

    [Fact]
    public void ModernEnum_MatchesClientOrdinals()
    {
        Assert.Equal(4, (int)QuestPushReason.TooFar);
        Assert.Equal(5, (int)QuestPushReason.Busy);
        Assert.Equal(6, (int)QuestPushReason.Dead);
        Assert.Equal(7, (int)QuestPushReason.LogFull);
    }

    [Theory]
    [InlineData(QuestPushReasonWotLK.LogFull, QuestPushReason.LogFull)]
    [InlineData(QuestPushReasonWotLK.OnQuest, QuestPushReason.OnQuest)]
    [InlineData(QuestPushReasonWotLK.AlreadyDone, QuestPushReason.AlreadyDone)]
    [InlineData(QuestPushReasonWotLK.Busy, QuestPushReason.Busy)]
    [InlineData(QuestPushReasonWotLK.NotInParty, QuestPushReason.NotInParty)]
    public void WotlkCastEnum_MapsByName(QuestPushReasonWotLK ac, QuestPushReason expected)
    {
        Assert.Equal(expected, ac.CastEnum<QuestPushReason>());
    }
}
