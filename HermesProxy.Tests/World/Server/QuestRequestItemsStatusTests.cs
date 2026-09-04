using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class QuestRequestItemsStatusTests
{
    [Theory]
    [InlineData(0x00u, false, QuestGiverRequestItems.StatusIncomplete)]
    [InlineData(0x03u, false, QuestGiverRequestItems.StatusIncomplete)]
    [InlineData(0x00u, true, QuestGiverRequestItems.StatusIncomplete)]
    [InlineData(0x03u, true, QuestGiverRequestItems.StatusComplete)]
    [InlineData(0x01u, true, QuestGiverRequestItems.StatusComplete)]
    public void StatusForClient_UsesAcFlagsAndBagCount(uint acFlags, bool itemsMet, uint expected)
    {
        Assert.Equal(expected, QuestGiverRequestItems.StatusForClient(acFlags, itemsMet));
    }

    [Fact]
    public void GossipCompleteIcon_DoesNotEnableContinue_WithoutItems()
    {
        // Multi-quest givers list unfinished item turn-ins as icon 4.
        // AC still writes 0x00 when the bags are empty.
        Assert.Equal(
            QuestGiverRequestItems.StatusIncomplete,
            QuestGiverRequestItems.StatusForClient(0x00, requiredItemsMet: false));
    }
}
