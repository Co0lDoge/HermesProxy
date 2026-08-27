using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Framework.Util;
using HermesProxy;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Objects.Version.V3_4_3_54261;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World;

public class GlyphSlotUpdateOrderTests
{
    [Fact]
    public void WriteUpdateGlyphsGroup_InterleavesSlotThenGlyphPerIndex()
    {
        var session = (GameSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GameSessionData));
        typeof(GameSessionData).GetField(nameof(GameSessionData.OriginalObjectTypes))!
            .SetValue(session, new Dictionary<WowGuid128, ObjectType>());
        typeof(GameSessionData).GetField(nameof(GameSessionData.ActiveGlyphSlotIds))!
            .SetValue(session, new uint[] { 21, 22, 23, 24, 25, 26 });
        typeof(GameSessionData).GetField(nameof(GameSessionData.ActiveGlyphs))!
            .SetValue(session, new ushort[] { 493, 487, 485, 0, 0, 0 });

        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        typeof(GameSessionData).GetField(nameof(GameSessionData.CurrentPlayerGuid))!
            .SetValue(session, guid);

        var global = (GlobalSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GlobalSessionData));
        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, global);
        update.ActivePlayerData ??= new ActivePlayerData();
        var builder = new ObjectUpdateBuilder(update, session);

        uint[] blocksBuf = new uint[48];
        var blocks = new StackBitMask(blocksBuf);
        for (int i = 0; i < PlayerConst.MaxGlyphSlots; i++)
        {
            blocks.SetBit(1513 + i);
            blocks.SetBit(1519 + i);
        }

        var packet = new WorldPacket();
        builder.WriteUpdateActivePlayerGlyphsGroup(packet, ref blocks, update.ActivePlayerData);

        using var reader = new WorldPacket(0, packet.GetData()!);
        ushort[] glyphs = [493, 487, 485, 0, 0, 0];
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(21u + (uint)i, reader.ReadUInt32());
            Assert.Equal(glyphs[i], reader.ReadUInt32());
        }
        Assert.False(reader.CanRead());
    }
}
