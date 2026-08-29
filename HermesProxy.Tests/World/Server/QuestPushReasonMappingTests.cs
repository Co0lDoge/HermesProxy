using System;
using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class QuestPushReasonMappingTests
{
    [Fact]
    public void WotlkLogFull_IsDead_IfPassedThrough()
    {
        Assert.Equal(5, (int)QuestPushReasonWotLK.LogFull);
        Assert.Equal(5, (int)QuestPushReason.Dead);
        Assert.Equal(6, (int)QuestPushReason.LogFull);
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
