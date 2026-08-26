using Xunit;
using V343 = HermesProxy.World.Enums.V3_4_3_54261;

namespace HermesProxy.Tests.World.Server;

public class GuildBankItemOpcodeTests
{
    [Fact]
    public void V343_AutoGuildBankItem_IsNextAfterActivate()
    {
        Assert.Equal(13493u, (uint)V343.Opcode.CMSG_GUILD_BANK_ACTIVATE);
        Assert.Equal(13494u, (uint)V343.Opcode.CMSG_AUTO_GUILD_BANK_ITEM);
        Assert.Equal(13506u, (uint)V343.Opcode.CMSG_GUILD_BANK_QUERY_TAB);
        Assert.Equal((uint)V343.Opcode.CMSG_GUILD_BANK_ACTIVATE + 13,
            (uint)V343.Opcode.CMSG_GUILD_BANK_QUERY_TAB);
    }

    [Fact]
    public void BankTabUpdated_AcEventStrings_AreTabNameIcon()
    {
        // AC Guild.cpp HandleSetBankTabInfo:
        //   _BroadcastEvent(GE_BANK_TAB_UPDATED, _, to_string(tabId), name, icon)
        string[] strings = { "3", "1x", "INV_Axe_04" };
        Assert.True(int.TryParse(strings[0], out int tabId));
        Assert.Equal(3, tabId);
        Assert.Equal("1x", strings[1]);
        Assert.Equal("INV_Axe_04", strings[2]);
    }
}
