using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HermesProxy;
using HermesProxy.Enums;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Objects.Version.V3_4_3_54261;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World;

public class KnownTitlesTests
{
    [Fact]
    public void FoldKnownTitles_Empty_ReturnsZero()
    {
        var dest = new ulong[6];
        Assert.Equal(0, ObjectUpdateBuilder.FoldKnownTitles(new uint?[12], dest));
        Assert.Equal(0, ObjectUpdateBuilder.FoldKnownTitles(null!, dest));
    }

    [Fact]
    public void FoldKnownTitles_WotlkSixWords_FoldsIntoSixUInt64()
    {
        // Brahand on AC: 6 uint32 knownTitles words (TITLES + TITLES1 + TITLES2).
        var src = new uint?[12];
        src[0] = 3758129150u;
        src[1] = 4294966511u;
        src[2] = 4294868991u;
        src[3] = 4294967039u;
        src[4] = 32767u;
        src[5] = 0u;

        var dest = new ulong[6];
        Assert.Equal(6, ObjectUpdateBuilder.FoldKnownTitles(src, dest));
        Assert.Equal(3758129150uL | ((ulong)4294966511u << 32), dest[0]);
        Assert.Equal(4294868991uL | ((ulong)4294967039u << 32), dest[1]);
        Assert.Equal(32767uL, dest[2]);
        Assert.Equal(0uL, dest[3]);
        Assert.Equal(0uL, dest[4]);
        Assert.Equal(0uL, dest[5]);
    }

    [Fact]
    public void WriteCreateActivePlayerAll_WithTitles_AppendsSixUInt64Payload()
    {
        int empty = CreateLength(titles: null);
        int withTitles = CreateLength(titles: new uint?[]
        {
            3758129150u, 4294966511u, 4294868991u, 4294967039u, 32767u, 0u,
            null, null, null, null, null, null
        });

        Assert.Equal(empty + 6 * sizeof(ulong), withTitles);
    }

    [Fact]
    public void WriteCreateActivePlayerAll_WithoutTitles_KeepsZeroSize()
    {
        int empty = CreateLength(titles: null);
        int explicitEmpty = CreateLength(titles: new uint?[12]);
        Assert.Equal(empty, explicitEmpty);
    }

    private static int CreateLength(uint?[]? titles)
    {
        if (VersionBootstrap.LegacyBuild == ClientVersionBuild.Zero)
            VersionBootstrap.LegacyBuild = ClientVersionBuild.V3_3_5a_12340;

        var session = (GameSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GameSessionData));
        typeof(GameSessionData).GetField(nameof(GameSessionData.OriginalObjectTypes))!
            .SetValue(session, new Dictionary<WowGuid128, ObjectType>());
        typeof(GameSessionData).GetField(nameof(GameSessionData.ActiveGlyphSlotIds))!
            .SetValue(session, new uint[] { 21, 22, 23, 24, 25, 26 });
        typeof(GameSessionData).GetField(nameof(GameSessionData.ActiveGlyphs))!
            .SetValue(session, new ushort[6]);

        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        typeof(GameSessionData).GetField(nameof(GameSessionData.CurrentPlayerGuid))!
            .SetValue(session, guid);

        var global = (GlobalSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GlobalSessionData));
        var update = new ObjectUpdate(guid, UpdateTypeModern.CreateObject1, global);
        update.ActivePlayerData ??= new ActivePlayerData();
        if (titles != null)
        {
            for (int i = 0; i < titles.Length; i++)
                update.ActivePlayerData.KnownTitles[i] = titles[i];
        }

        var packet = new WorldPacket();
        new ObjectUpdateBuilder(update, session).WriteCreateActivePlayerData(packet);
        return packet.GetData()!.Length;
    }
}
