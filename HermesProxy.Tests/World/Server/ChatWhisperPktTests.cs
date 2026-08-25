using HermesProxy.World;
using Xunit;

namespace HermesProxy.Tests.World.Server;

/// <summary>
/// The test assembly is pinned to 1.14, so ChatMessageWhisper.Read cannot take the
/// V3_4_3 branch here. These cases lock the captured 3.4.3 bit widths (9 + 11)
/// that the production reader must use.
/// </summary>
public class ChatWhisperPktTests
{
    private static (string Target, string Text) ReadV343Body(byte[] body)
    {
        var framed = new byte[body.Length + 2];
        body.CopyTo(framed, 2);
        using var packet = new WorldPacket(framed);
        _ = packet.ReadUInt32();
        uint targetLen = packet.ReadBits<uint>(9);
        uint textLen = packet.ReadBits<uint>(11);
        return (packet.ReadString(targetLen), packet.ReadString(textLen));
    }

    [Fact]
    public void V343Wire_KeepsQuestionMarkOnCoQuery()
    {
        byte[] body =
        {
            0x07, 0x00, 0x00, 0x00, 0x04, 0x00, 0x40,
            0x41, 0x6e, 0x61, 0x6c, 0x79, 0x6e, 0x6e, 0x61,
            0x63, 0x6f, 0x20, 0x3f
        };

        var (target, text) = ReadV343Body(body);
        Assert.Equal("Analynna", target);
        Assert.Equal("co ?", text);
    }

    [Fact]
    public void V343Wire_KeepsFullStrategyCommand()
    {
        byte[] body =
        {
            0x07, 0x00, 0x00, 0x00, 0x04, 0x81, 0x30,
            0x46, 0x65, 0x6c, 0x6c, 0x6f, 0x6e, 0x69, 0x61, 0x6e,
            0x63, 0x6f, 0x20, 0x2b, 0x74, 0x61, 0x6e, 0x6b, 0x2c,
            0x2d, 0x68, 0x65, 0x61, 0x6c, 0x2c, 0x2d, 0x64, 0x70, 0x73
        };

        var (target, text) = ReadV343Body(body);
        Assert.Equal("Fellonian", target);
        Assert.Equal("co +tank,-heal,-dps", text);
    }
}
