using Xunit;
using V343 = HermesProxy.World.Enums.V3_4_3_54261;

namespace HermesProxy.Tests.World.Server;

public class LootRollOpcodeTests
{
    /// <summary>
    /// SMSG_LOOT_START_ROLL sat at the 0u placeholder, so the packet that opens the
    /// Need/Greed/Pass dialog was discarded at send time and group loot silently
    /// resolved around the player. The rest of the family was mapped, which is why the
    /// rolls still scrolled past in chat and a winner was announced.
    ///
    /// It is named SMSG_START_LOOT_ROLL in the 3.4.3 sources, and the gap sits *below*
    /// the mapped block (0x261F is SMSG_MASTER_LOOT_CANDIDATE_LIST, not this), which is
    /// how it was missed. Pinning the neighbours documents that.
    /// </summary>
    [Fact]
    public void V343_LootRollOpcodes_MatchLineagedr()
    {
        Assert.Equal(9757u, (uint)V343.Opcode.SMSG_LOOT_START_ROLL);        // 0x261D
        Assert.Equal(9758u, (uint)V343.Opcode.SMSG_LOOT_ROLL);              // 0x261E
        Assert.Equal(9760u, (uint)V343.Opcode.SMSG_LOOT_ROLLS_COMPLETE);    // 0x2620
        Assert.Equal(9761u, (uint)V343.Opcode.SMSG_LOOT_ALL_PASSED);        // 0x2621
        Assert.Equal(9762u, (uint)V343.Opcode.SMSG_LOOT_ROLL_WON);          // 0x2622
        Assert.Equal(12820u, (uint)V343.Opcode.CMSG_LOOT_ROLL);
    }

    /// <summary>
    /// A zero here means "no wire value for this build", and ServerPacket throws
    /// UnmappedOpcodeException rather than sending — the exact failure this fixes.
    /// </summary>
    [Fact]
    public void V343_LootStartRoll_IsNotAPlaceholder()
    {
        Assert.NotEqual(0u, (uint)V343.Opcode.SMSG_LOOT_START_ROLL);
    }
}
