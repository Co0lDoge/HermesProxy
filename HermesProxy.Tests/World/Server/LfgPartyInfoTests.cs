using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class LfgPartyInfoTests
{
    private static readonly WowGuid128 Player = WowGuid128.Create(HighGuidType703.Player, 42);

    [Fact]
    public void Write_EmitsSoftLockInsteadOfZero()
    {
        var info = new LFGPartyInfo();
        info.Players.Add(new LFGBlackListEntry
        {
            PlayerGuid = Player,
            Locks =
            {
                new LFGLockInfoData
                {
                    Slot = LfgSlots.PackSlot(LfgSlots.LfgTypeRandom, LfgSlots.TitanRuneGammaHeaderId),
                    LockStatus = (uint)LFGLockStatus.NotInSeason,
                    SoftLock = (uint)LFGSoftLock.Unk2,
                }
            }
        });

        info.WritePacketData();
        byte[] body = info.GetData()!;

        var framed = new byte[body.Length + 2];
        body.CopyTo(framed, 2);
        using var packet = new WorldPacket(framed);

        Assert.Equal(1u, packet.ReadUInt32());
        Assert.True(packet.HasBit());
        Assert.Equal(1u, packet.ReadUInt32());
        Assert.Equal(Player, packet.ReadPackedGuid128());
        Assert.Equal(LfgSlots.PackSlot(LfgSlots.LfgTypeRandom, LfgSlots.TitanRuneGammaHeaderId), packet.ReadUInt32());
        Assert.Equal((uint)LFGLockStatus.NotInSeason, packet.ReadUInt32());
        Assert.Equal(0, packet.ReadInt32());
        Assert.Equal(0, packet.ReadInt32());
        Assert.Equal((uint)LFGSoftLock.Unk2, packet.ReadUInt32());
    }
}
