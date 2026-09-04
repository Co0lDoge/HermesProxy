using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HermesProxy;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using Xunit;

namespace HermesProxy.Tests.World.Server;

/// <summary>
/// QuestLogProgress is one flat short[] sliced per log slot. A wrong stride would let one
/// quest's counters land on another quest's row, which the client renders as the neighbour
/// silently resetting to 0.
/// </summary>
public class QuestLogProgressCacheTests
{
    static GameSessionData CreateState()
    {
        var state = (GameSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GameSessionData));
        // GetUninitializedObject skips field initializers, so seed the backing array.
        typeof(GameSessionData)
            .GetField(nameof(GameSessionData.QuestLogProgress), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(state, new short[QuestConst.MaxQuestLogSize * QuestConst.MaxQuestCounts]);
        return state;
    }

    static QuestLog LogWith(params ReadOnlySpan<(int Index, int Count)> counters)
    {
        var log = new QuestLog();
        foreach (var (index, count) in counters)
            log.ObjectiveProgress[index] = (short)count;
        return log;
    }

    [Fact]
    public void RestoreQuestLogProgress_ReadsBackOnlyItsOwnSlot()
    {
        var state = CreateState();
        state.RememberQuestLogProgress(0, LogWith((0, 3)));
        state.RememberQuestLogProgress(1, LogWith((0, 7), (1, 1)));
        state.RememberQuestLogProgress(QuestConst.MaxQuestLogSize - 1, LogWith((0, 5)));

        var slot0 = new QuestLog();
        var slot1 = new QuestLog();
        var last = new QuestLog();
        state.RestoreQuestLogProgress(0, slot0);
        state.RestoreQuestLogProgress(1, slot1);
        state.RestoreQuestLogProgress(QuestConst.MaxQuestLogSize - 1, last);

        Assert.Equal((short)3, slot0.ObjectiveProgress[0]);
        Assert.Equal((short)0, slot0.ObjectiveProgress[1]);
        Assert.Equal((short)7, slot1.ObjectiveProgress[0]);
        Assert.Equal((short)1, slot1.ObjectiveProgress[1]);
        Assert.Equal((short)5, last.ObjectiveProgress[0]);
    }

    [Fact]
    public void RestoreQuestLogProgress_DoesNotOverwriteValuesPresentInTheUpdate()
    {
        var state = CreateState();
        state.RememberQuestLogProgress(2, LogWith((0, 7), (1, 1)));

        // Inbound update carried a fresh count for objective 0 but nothing for objective 1.
        var inbound = LogWith((0, 9));
        state.RestoreQuestLogProgress(2, inbound);

        Assert.Equal((short)9, inbound.ObjectiveProgress[0]);
        Assert.Equal((short)1, inbound.ObjectiveProgress[1]);
    }

    [Fact]
    public void ClearQuestLogProgress_LeavesNeighbouringSlotsIntact()
    {
        var state = CreateState();
        state.RememberQuestLogProgress(0, LogWith((0, 3)));
        state.RememberQuestLogProgress(1, LogWith((0, 7)));
        state.RememberQuestLogProgress(2, LogWith((0, 5)));

        state.ClearQuestLogProgress(1);

        var slot0 = new QuestLog();
        var slot1 = new QuestLog();
        var slot2 = new QuestLog();
        state.RestoreQuestLogProgress(0, slot0);
        state.RestoreQuestLogProgress(1, slot1);
        state.RestoreQuestLogProgress(2, slot2);

        Assert.Equal((short)3, slot0.ObjectiveProgress[0]);
        Assert.Equal((short)0, slot1.ObjectiveProgress[0]);
        Assert.Equal((short)5, slot2.ObjectiveProgress[0]);
    }

    [Fact]
    public void RememberQuestLogProgress_WritesEveryCounterOfTheLastSlotInsideItsOwnRow()
    {
        var state = CreateState();
        int lastSlot = QuestConst.MaxQuestLogSize - 1;
        var full = new QuestLog();
        for (int i = 0; i < QuestConst.MaxQuestCounts; i++)
            full.ObjectiveProgress[i] = (short)(i + 1);

        state.RememberQuestLogProgress(lastSlot, full);

        var readBack = new QuestLog();
        state.RestoreQuestLogProgress(lastSlot, readBack);
        for (int i = 0; i < QuestConst.MaxQuestCounts; i++)
            Assert.Equal((short)(i + 1), readBack.ObjectiveProgress[i]);

        // The row before it must not have been touched by the write.
        var previous = new QuestLog();
        state.RestoreQuestLogProgress(lastSlot - 1, previous);
        for (int i = 0; i < QuestConst.MaxQuestCounts; i++)
            Assert.Equal((short)0, previous.ObjectiveProgress[i]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(QuestConst.MaxQuestLogSize)]
    [InlineData(int.MaxValue)]
    public void OutOfRangeSlots_AreIgnoredRatherThanThrowing(int slot)
    {
        var state = CreateState();
        var log = LogWith((0, 4));

        state.RememberQuestLogProgress(slot, log);
        state.ClearQuestLogProgress(slot);
        state.RestoreQuestLogProgress(slot, log);

        Assert.Equal((short)4, log.ObjectiveProgress[0]);
    }
}
