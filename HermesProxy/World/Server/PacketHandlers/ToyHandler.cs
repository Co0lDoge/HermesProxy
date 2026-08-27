using HermesProxy.Enums;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.World.Server;

public partial class WorldSocket
{
    [PacketHandler(Opcode.CMSG_ADD_TOY)]
    void HandleAddToy(AddToy add)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        uint itemId = GetSession().GameState.GetItemId(add.Guid);
        if (itemId == 0)
            return;

        var session = GetSession();
        var favorites = EnsureCollectionFavorites();
        bool firstLearn = favorites.LearnedToys.Add(itemId);
        if (firstLearn)
            session.AccountMetaDataMgr.SaveCollectionFavorites(favorites);

        if (firstLearn)
            CollectionSync.PlayToyLearnVisual(session);

        CollectionSync.SendToys(session);
        SendPacket(AccountToyUpdate.FromSession(session.GameState));

        if (firstLearn || !session.GameState.HiddenToyByItemId.ContainsKey(itemId))
        {
            var found = session.GameState.FindItemInInventory(add.Guid.To64());
            if (found != null)
                CollectionSync.HideToyFromClient(session, add.Guid, found.Value.containerSlot, found.Value.slot);
        }
    }

    [PacketHandler(Opcode.CMSG_USE_TOY)]
    void HandleUseToy(UseToy use)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        uint itemId = use.ItemId;
        if (itemId == 0)
            return;

        var favorites = EnsureCollectionFavorites();
        if (!favorites.LearnedToys.Contains(itemId))
            return;

        var found = GetSession().GameState.FindItemInInventoryById(itemId);
        if (found == null)
            return;

        UseInventoryItem(found.Value.guid, found.Value.containerSlot, found.Value.slot, use.Cast);
    }

    [PacketHandler(Opcode.CMSG_COLLECTION_ITEM_SET_FAVORITE)]
    void HandleCollectionItemSetFavorite(CollectionItemSetFavorite setFavorite)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;
        if (setFavorite.Type != ItemCollectionType.Toy || setFavorite.ID == 0)
            return;

        var favorites = EnsureCollectionFavorites();
        if (setFavorite.IsFavorite)
            favorites.FavoriteToys.Add(setFavorite.ID);
        else
            favorites.FavoriteToys.Remove(setFavorite.ID);
        GetSession().AccountMetaDataMgr.SaveCollectionFavorites(favorites);
        SendPacket(AccountToyUpdate.FromSession(GetSession().GameState));
    }

    [PacketHandler(Opcode.CMSG_TOY_CLEAR_FANFARE)]
    void HandleToyClearFanfare(ToyClearFanfare _)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;
    }
}
