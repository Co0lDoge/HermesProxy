using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class GuildRosterMemberDataTests
{
    [Fact]
    public void Write_EmitsMemberName()
    {
        var member = new GuildRosterMemberData
        {
            Guid = WowGuid128.Create(HighGuidType703.Player, 1),
            Name = "Brahand",
            RankID = 0,
            AreaID = 1519,
            Level = 80,
            ClassID = Class.Warrior,
            SexID = Gender.Male,
            RaceID = Race.Human,
            Status = 1,
            Authenticated = true
        };

        var packet = new GuildRoster();
        packet.MemberData.Add(member);
        packet.WelcomeText = "hello";
        packet.CreateDate = 1700000000;
        packet.WritePacketData();

        byte[]? bytes = packet.GetData();
        Assert.NotNull(bytes);
        string ascii = System.Text.Encoding.ASCII.GetString(bytes);
        Assert.Contains("Brahand", ascii);
        Assert.Contains("hello", ascii);
    }
}
