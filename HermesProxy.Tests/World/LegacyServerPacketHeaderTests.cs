using System;
using HermesProxy.World;
using HermesProxy.World.Client;
using Xunit;

namespace HermesProxy.Tests.World;

/// <summary>
/// Regression cover for issue #200: WotLK cores widen the server header from 4 bytes to 5 once a
/// packet passes 0x7FFF, and HermesProxy used to read a fixed 4. The stray byte both mis-framed the
/// packet and stalled the RC4 receive keystream, so every packet after the first large one decoded
/// as garbage and the server dropped the connection. A level-80 character's
/// <c>SMSG_ALL_ACHIEVEMENT_DATA</c> (~60 KB) reaches that size on any WotLK backend.
/// </summary>
public class LegacyServerPacketHeaderTests
{
    /// <summary>
    /// Builds a header the way AzerothCore/TrinityCore 3.3.5a <c>ServerPktHeader</c> does.
    /// <paramref name="size"/> counts the payload plus the 2-byte opcode.
    /// </summary>
    private static byte[] BuildServerHeader(uint size, ushort opcode)
    {
        var bytes = new byte[size > 0x7FFF ? 5 : 4];
        int i = 0;

        if (size > 0x7FFF)
            bytes[i++] = (byte)(0x80 | ((size >> 16) & 0xFF));

        bytes[i++] = (byte)((size >> 8) & 0xFF);
        bytes[i++] = (byte)(size & 0xFF);
        bytes[i++] = (byte)(opcode & 0xFF);
        bytes[i] = (byte)((opcode >> 8) & 0xFF);

        return bytes;
    }

    [Theory]
    [InlineData(4u, (ushort)0x0236)]        // tiny packet
    [InlineData(0x7FFFu, (ushort)0x047D)]   // exactly at the boundary — still the narrow header
    public void Read_NarrowHeader_RoundTrips(uint size, ushort opcode)
    {
        var wire = BuildServerHeader(size, opcode);
        Assert.Equal(LegacyServerPacketHeader.StructSize, wire.Length);
        Assert.False(LegacyServerPacketHeader.IsLargePacket(wire[0]));

        var header = new LegacyServerPacketHeader();
        header.Read(wire, large: false);

        Assert.Equal(size, header.Size);
        Assert.Equal(opcode, header.Opcode);
    }

    [Theory]
    [InlineData(0x8000u, (ushort)0x047D)]   // first size that widens the header
    [InlineData(59942u, (ushort)0x047D)]    // the live SMSG_ALL_ACHIEVEMENT_DATA from issue #200
    [InlineData(0x7FFFFFu, (ushort)0x0001)] // widest size the 3-byte field can express
    public void Read_WideHeader_RoundTrips(uint size, ushort opcode)
    {
        var wire = BuildServerHeader(size, opcode);
        Assert.Equal(LegacyServerPacketHeader.LargeStructSize, wire.Length);
        Assert.True(LegacyServerPacketHeader.IsLargePacket(wire[0]));

        var header = new LegacyServerPacketHeader();
        header.Read(wire, large: true);

        Assert.Equal(size, header.Size);
        Assert.Equal(opcode, header.Opcode);
    }

    /// <summary>
    /// The opcode moves one byte along in the wide form. Reading a wide header with the narrow
    /// layout is exactly the bug: the size collapses to 0x80EA and the opcode becomes 0x7D26
    /// (32038) — the value that showed up in both the reporter's log and the local repro.
    /// </summary>
    [Fact]
    public void Read_WideHeaderWithNarrowLayout_ProducesTheIssue200Garbage()
    {
        var wire = BuildServerHeader(59942, 0x047D);

        var header = new LegacyServerPacketHeader();
        header.Read(wire, large: false);

        Assert.NotEqual(59942u, header.Size);
        Assert.Equal(32038, header.Opcode);
    }

    /// <summary>
    /// The receive keystream has to advance over all five header bytes. <c>Decrypt</c> is pinned to
    /// the narrow width and ignores anything shorter, so the trailing byte needs
    /// <c>DecryptLargeHeaderByte</c> — without it the byte stays ciphertext and, worse, the stream
    /// stays one byte behind for the rest of the session.
    /// </summary>
    [Fact]
    public void DecryptLargeHeaderByte_KeepsTheKeystreamAlignedWithASingleFiveBytePass()
    {
        ReadOnlySpan<byte> sessionKey = "0123456789ABCDEF0123456789ABCDEF0123456789"u8;

        // Reference: one crypt consuming the 5 header bytes as a single run, which is what the
        // server did when it encrypted them.
        var reference = new WotlkWorldCrypt();
        reference.Initialize(sessionKey);
        var wholeRun = new byte[8];
        reference.Decrypt(wholeRun.AsSpan(0, 4));
        reference.DecryptLargeHeaderByte(wholeRun.AsSpan(4, 1));

        // Split the same 5 bytes the way the receive loop does, then keep going into the next
        // header to prove the stream did not fall behind.
        var split = new WotlkWorldCrypt();
        split.Initialize(sessionKey);
        var stream = new byte[8];
        split.Decrypt(stream.AsSpan(0, 4));
        split.DecryptLargeHeaderByte(stream.AsSpan(4, 1));

        Assert.Equal(wholeRun[..5], stream[..5]);

        // The 5th byte must actually have been transformed; a no-op would leave it at zero.
        Assert.NotEqual(0, stream[4]);

        // And the following header must decrypt identically on both, i.e. both consumed exactly 5.
        reference.Decrypt(wholeRun.AsSpan(5, 3));
        split.Decrypt(stream.AsSpan(5, 3));
        Assert.Equal(wholeRun, stream);
    }

    /// <summary>Vanilla and TBC never see the wide header, so their crypts keep the no-op default.</summary>
    [Fact]
    public void DecryptLargeHeaderByte_IsANoOpForPreWotlkCrypts()
    {
        ReadOnlySpan<byte> sessionKey = "0123456789ABCDEF0123456789ABCDEF0123456789"u8;

        LegacyWorldCrypt vanilla = new VanillaWorldCrypt();
        vanilla.Initialize(sessionKey);
        var untouched = new byte[1];
        vanilla.DecryptLargeHeaderByte(untouched);

        Assert.Equal(0, untouched[0]);
    }
}
