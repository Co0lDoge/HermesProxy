using HermesProxy.World;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class AutoStoreBagItemTests
{
    [Fact]
    public void Read_IsInvThenSourceBagDestBagSourceSlot()
    {
        var payload = new WorldPacket(1u);
        payload.WriteBits(0u, 2);
        payload.FlushBits();
        payload.WriteUInt8(255);
        payload.WriteUInt8(255);
        payload.WriteUInt8(4);

        using var packet = new AutoStoreBagItem(new WorldPacket(Frame(payload.GetData())));
        packet.Read();

        Assert.Equal(255, packet.ContainerSlotA);
        Assert.Equal(255, packet.ContainerSlotB);
        Assert.Equal(4, packet.SlotA);
        Assert.Empty(packet.Inv.Items);
    }

    static byte[] Frame(byte[] body)
    {
        var framed = new byte[body.Length + 2];
        body.CopyTo(framed, 2);
        return framed;
    }
}
