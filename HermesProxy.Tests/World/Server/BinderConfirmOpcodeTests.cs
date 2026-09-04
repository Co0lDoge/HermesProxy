using HermesProxy.World.Enums;
using Xunit;
using V343 = HermesProxy.World.Enums.V3_4_3_54261;

namespace HermesProxy.Tests.World.Server;

public class BinderConfirmOpcodeTests
{
    [Fact]
    public void V343_BinderConfirm_AliasesNpcInteractionOpenResult()
    {
        // WPP V3_4_3_51666 has no SMSG_BINDER_CONFIRM. Innkeeper confirm
        // is SMSG_NPC_INTERACTION_OPEN_RESULT (0x288A / 10378) with
        // InteractionType=Binder, same wire as bank and spirit healer.
        Assert.Equal(10378u, (uint)V343.Opcode.SMSG_BINDER_CONFIRM);
        Assert.Equal((uint)V343.Opcode.SMSG_SHOW_BANK, (uint)V343.Opcode.SMSG_BINDER_CONFIRM);
        Assert.Equal((uint)V343.Opcode.SMSG_SPIRIT_HEALER_CONFIRM, (uint)V343.Opcode.SMSG_BINDER_CONFIRM);
        Assert.Equal((uint)V343.Opcode.SMSG_PLAYER_TABARD_VENDOR_ACTIVATE, (uint)V343.Opcode.SMSG_BINDER_CONFIRM);
        Assert.Equal(20, (int)PlayerInteractionType.Binder);
        Assert.Equal(14, (int)PlayerInteractionType.GuildTabardVendor);
    }
}
