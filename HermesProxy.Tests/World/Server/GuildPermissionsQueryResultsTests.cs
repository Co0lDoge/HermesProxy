using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class GuildPermissionsQueryResultsTests
{
    [Fact]
    public void Write_EmitsRankFlagsAndSixTabs()
    {
        var packet = new GuildPermissionsQueryResults
        {
            RankID = 0,
            Flags = -1,
            WithdrawGoldLimit = -1,
            NumTabs = 1
        };
        for (int i = 0; i < 6; i++)
            packet.Tab.Add(new GuildRankTabPermissions { Flags = 0xFF, WithdrawItemLimit = -1 });

        packet.WritePacketData();
        byte[]? bytes = packet.GetData();
        Assert.NotNull(bytes);
        // uint32 RankID + int32 Flags + int32 gold + int32 NumTabs + uint32 tabCount
        // + 6 * (int32 flags + int32 limit) = 20 + 48 = 68
        Assert.Equal(68, bytes.Length);
        Assert.Equal(0u, System.BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(unchecked((uint)(-1)), System.BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(6u, System.BitConverter.ToUInt32(bytes, 16));
    }
}
