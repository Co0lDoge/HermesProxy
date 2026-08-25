using System;
using System.Buffers.Binary;
using HermesProxy.World.Server.Packets;
using Xunit;
using ByteBuffer = global::Framework.IO.ByteBuffer;

namespace HermesProxy.Tests.World.Server;

/// <summary>
/// Native ChangeRealmTicketResponse writes Ticket only when Allow is set.
/// Writing it on deny, plus an auth reconnect that AC rejects, is WOW51900300.
/// </summary>
public class ChangeRealmTicketResponseTests
{
    private const uint Token = 0x11223344;

    [Fact]
    public void Write_WhenDenied_OmitsTicket()
    {
        var packet = new ChangeRealmTicketResponse
        {
            Token = Token,
            Allow = false,
            Ticket = new ByteBuffer(new byte[] { 0xAB }),
        };

        packet.WritePacketData();
        byte[] data = packet.GetData()!;

        Assert.Equal(5, data.Length);
        Assert.Equal(0x44, data[0]);
        Assert.Equal(0x33, data[1]);
        Assert.Equal(0x22, data[2]);
        Assert.Equal(0x11, data[3]);
        Assert.Equal(0x00, data[4]);
    }

    [Fact]
    public void Write_WhenAllowed_WritesTicketAfterFlushedAllowBit()
    {
        var packet = new ChangeRealmTicketResponse
        {
            Token = Token,
            Allow = true,
            Ticket = new ByteBuffer(new byte[] { 0xAB }),
        };

        packet.WritePacketData();
        byte[] data = packet.GetData()!;

        Assert.Equal(10, data.Length);
        Assert.Equal(0x80, data[4]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(5, 4)));
        Assert.Equal(0xAB, data[9]);
    }
}
