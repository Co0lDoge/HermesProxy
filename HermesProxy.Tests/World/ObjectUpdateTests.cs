using System.Runtime.CompilerServices;
using HermesProxy.Enums;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;
using Xunit;

namespace HermesProxy.Tests.World;

public class ObjectUpdateConstructorTests
{
    private static GlobalSessionData CreateGlobalSession()
    {
        return (GlobalSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GlobalSessionData));
    }

    [Fact]
    public void NeedsWmoMapObjectFlag_Type15MoTransport_TrueForAnyDisplay()
    {
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.MOTransport, 8409));
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.MOTransport, 455));
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.MOTransport, null));
    }

    [Fact]
    public void NeedsWmoMapObjectFlag_Type11_SplitsOnDisplay()
    {
        GameData.LoadWmoGameObjectDisplays();
        Assert.NotEmpty(GameData.WmoGameObjectDisplays);

        // Strand of the Ancients / Isle of Conquest gunships -- WMO displays.
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, 8409));
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, 8410));
        Assert.True(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, 8587));

        // Undercity elevator / Deeprun tram car -- M2 doodads, must stay unflagged.
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, 455));
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, 3831));
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag((sbyte)GameObjectTypeModern.Transport, null));
    }

    [Fact]
    public void NeedsWmoMapObjectFlag_DoorOrGeneric_False()
    {
        GameData.LoadWmoGameObjectDisplays();

        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag(0, 8409));
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag(5, 8409));
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag(33, 8409));
        Assert.False(ObjectUpdate.NeedsWmoMapObjectFlag(null, 8409));
    }

    [Fact]
    public void Constructor_ItemGuid_InitializesItemAndContainerData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Item, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.ItemData);
        Assert.NotNull(update.ContainerData);
        Assert.NotNull(update.ObjectData);
    }

    [Fact]
    public void Constructor_CreatureGuid_InitializesUnitData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Creature, 0, 1234, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.UnitData);
    }

    [Fact]
    public void Constructor_PlayerGuid_InitializesUnitAndPlayerAndActivePlayerData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.UnitData);
        Assert.NotNull(update.PlayerData);
        Assert.NotNull(update.ActivePlayerData);
    }

    [Fact]
    public void Constructor_GameObjectGuid_InitializesGameObjectData()
    {
        var guid = WowGuid128.Create(HighGuidType703.GameObject, 0, 5678, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.GameObjectData);
    }

    [Fact]
    public void Constructor_DynamicObjectGuid_InitializesDynamicObjectData()
    {
        var guid = WowGuid128.Create(HighGuidType703.DynamicObject, 0, 100, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.DynamicObjectData);
    }

    [Fact]
    public void Constructor_CorpseGuid_InitializesCorpseData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Corpse, 0, 200, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.CorpseData);
    }

    [Fact]
    public void Constructor_CreateObject1_InitializesCreateData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Creature, 0, 1234, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.CreateObject1, session);

        Assert.NotNull(update.CreateData);
    }

    [Fact]
    public void Constructor_CreateObject2_InitializesCreateData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.CreateObject2, session);

        Assert.NotNull(update.CreateData);
    }

    [Fact]
    public void Constructor_ValuesType_DoesNotInitializeCreateData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Creature, 0, 1234, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.Null(update.CreateData);
    }
}

public class ObjectUpdateFieldExclusivityTests
{
    private static GlobalSessionData CreateGlobalSession()
    {
        return (GlobalSessionData)RuntimeHelpers.GetUninitializedObject(typeof(GlobalSessionData));
    }

    [Fact]
    public void Constructor_ItemGuid_UnitDataIsNull()
    {
        var guid = WowGuid128.Create(HighGuidType703.Item, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.Null(update.UnitData);
        Assert.Null(update.PlayerData);
        Assert.Null(update.ActivePlayerData);
        Assert.Null(update.GameObjectData);
        Assert.Null(update.DynamicObjectData);
        Assert.Null(update.CorpseData);
    }

    [Fact]
    public void Constructor_UnitGuid_ItemDataIsNull()
    {
        var guid = WowGuid128.Create(HighGuidType703.Creature, 0, 1234, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.Null(update.ItemData);
        Assert.Null(update.ContainerData);
        Assert.Null(update.PlayerData);
        Assert.Null(update.ActivePlayerData);
        Assert.Null(update.GameObjectData);
        Assert.Null(update.DynamicObjectData);
        Assert.Null(update.CorpseData);
    }

    [Fact]
    public void Constructor_PlayerGuid_ItemAndGameObjectDataAreNull()
    {
        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.Null(update.ItemData);
        Assert.Null(update.ContainerData);
        Assert.Null(update.GameObjectData);
        Assert.Null(update.DynamicObjectData);
        Assert.Null(update.CorpseData);
    }

    [Fact]
    public void Constructor_GameObjectGuid_UnitAndItemDataAreNull()
    {
        var guid = WowGuid128.Create(HighGuidType703.GameObject, 0, 5678, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.Null(update.ItemData);
        Assert.Null(update.ContainerData);
        Assert.Null(update.UnitData);
        Assert.Null(update.PlayerData);
        Assert.Null(update.ActivePlayerData);
        Assert.Null(update.DynamicObjectData);
        Assert.Null(update.CorpseData);
    }

    [Fact]
    public void Constructor_AlwaysInitializesObjectData()
    {
        var guid = WowGuid128.Create(HighGuidType703.Player, 1);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.Values, session);

        Assert.NotNull(update.ObjectData);
    }

    [Fact]
    public void Constructor_StoresGuidAndType()
    {
        var guid = WowGuid128.Create(HighGuidType703.Player, 42);
        var session = CreateGlobalSession();

        var update = new ObjectUpdate(guid, UpdateTypeModern.CreateObject1, session);

        Assert.Equal(guid, update.Guid);
        Assert.Equal(UpdateTypeModern.CreateObject1, update.Type);
        Assert.Same(session, update.GlobalSession);
    }
}
