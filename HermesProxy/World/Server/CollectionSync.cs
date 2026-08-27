using HermesProxy;
using HermesProxy.World;
using HermesProxy.World.Enums;
using Classic = HermesProxy.World.Enums.Classic;
using Vanilla = HermesProxy.World.Enums.Vanilla;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;
using System.Linq;

namespace HermesProxy.World.Server;

public static class CollectionSync
{
    public static void StampSummonedBattlePet(ObjectUpdate update, GameSessionData state)
    {
        if (!state.SummonedBattlePetGuid.IsEmpty())
        {
            update.ActivePlayerData.SummonedBattlePetGUID = state.SummonedBattlePetGuid;
            if (!state.SummonedCompanionCreatureGuid.IsEmpty())
                update.UnitData.Critter = state.SummonedCompanionCreatureGuid;
        }
    }

    public static void StampCompanionCreature(ObjectUpdate update, GlobalSessionData session, WowGuid64 legacyGuid)
    {
        var state = session.GameState;
        if (update.UnitData == null)
            return;
        uint entry = (uint)(update.ObjectData?.EntryID ?? 0);
        if (entry == 0 || !GameData.TryGetSpeciesByCreatureId(entry, out var species))
            return;

        var player = state.CurrentPlayerGuid;
        bool summonedByUs = update.UnitData.SummonedBy == player || update.UnitData.CreatedBy == player;
        bool ownerMissing = (update.UnitData.SummonedBy == null || update.UnitData.SummonedBy.Value.IsEmpty())
            && (update.UnitData.CreatedBy == null || update.UnitData.CreatedBy.Value.IsEmpty());
        bool ours = !player.IsEmpty()
            && (summonedByUs || ownerMissing || !state.SummonedBattlePetGuid.IsEmpty());
        if (!ours)
            return;

        var journalGuid = WowGuid128.Create(HighGuidType703.BattlePet, species.SpeciesId);
        update.UnitData.BattlePetCompanionGUID = journalGuid;
        update.UnitData.BattlePetDBID = species.SpeciesId;
        update.UnitData.WildBattlePetLevel = 1;

        state.SummonedBattlePetGuid = journalGuid;
        state.SummonedCompanionCreatureGuid = update.Guid;
        state.SummonedCompanionLegacyGuid = legacyGuid;
        if (GameData.TryGetSummonSpellForSpecies(species.SpeciesId, out uint spellId))
            state.BattlePetGuidToSummonSpell[journalGuid] = spellId;
        state.CurrentPlayerStorage?.Settings?.SetLastSummonedPetSpecies(species.SpeciesId);

        SendSummonedBattlePet(session);
    }

    public static void SendSummonedBattlePet(GlobalSessionData session)
    {
        if (session.GameState.CurrentPlayerGuid.IsEmpty())
            return;

        var state = session.GameState;
        var updateData = new ObjectUpdate(state.CurrentPlayerGuid, UpdateTypeModern.Values, session);
        updateData.ActivePlayerData.SummonedBattlePetGUID = state.SummonedBattlePetGuid;
        updateData.UnitData.Critter = state.SummonedCompanionCreatureGuid;
        var updatePacket = new UpdateObject(state);
        updatePacket.ObjectUpdates.Add(updateData);
        session.WorldClient?.SendPacketToClient(updatePacket);
    }

    // WotLK "Learning" (55884) — same visual mount/companion items use when consumed
    public const uint LearningSpellId = 55884;
    public const uint LearningSpellXSpellVisualId = 346509;

    public static void PlayToyLearnVisual(GlobalSessionData session)
    {
        var state = session.GameState;
        var player = state.CurrentPlayerGuid;
        if (player.IsEmpty() || session.WorldClient == null)
            return;

        uint mapId = (uint)(state.CurrentMapId ?? 0);
        var castId = WowGuid128.Create(HighGuidType703.Cast, SpellCastSource.Normal, mapId, LearningSpellId, LearningSpellId + player.GetCounter());

        var cast = new SpellCastData
        {
            CasterGUID = player,
            CasterUnit = player,
            CastID = castId,
            SpellID = (int)LearningSpellId,
            SpellXSpellVisualID = LearningSpellXSpellVisualId,
            CastTime = 0,
        };
        cast.Target.Flags = SpellCastTargetFlags.Unit;
        cast.Target.Unit = player;
        cast.HitTargets.Add(player);

        session.WorldClient.SendPacketToClient(new SpellStart { Cast = cast });
        session.WorldClient.SendPacketToClient(new SpellGo { Cast = cast });
    }

    public static void SendToys(GlobalSessionData session)
    {
        var state = session.GameState;
        if (state.CurrentPlayerGuid.IsEmpty() || session.WorldClient == null)
            return;

        var learned = state.CollectionFavorites?.LearnedToys;
        var updateData = new ObjectUpdate(state.CurrentPlayerGuid, UpdateTypeModern.Values, session);
        updateData.ActivePlayerData.Toys = learned == null
            ? []
            : learned.OrderBy(id => id).Select(id => (int)id).ToList();
        var updatePacket = new UpdateObject(state);
        updatePacket.ObjectUpdates.Add(updateData);
        session.WorldClient.SendPacketToClient(updatePacket);
    }

    public static void HideLearnedToysStillInBags(GlobalSessionData session)
    {
        var state = session.GameState;
        var learned = state.CollectionFavorites?.LearnedToys;
        if (learned == null || session.WorldClient == null)
            return;

        foreach (uint itemId in learned)
        {
            if (state.HiddenToyByItemId.ContainsKey(itemId))
                continue;
            var found = state.FindItemInInventoryById(itemId);
            if (found == null)
                continue;
            HideToyFromClient(session, found.Value.guid, found.Value.containerSlot, found.Value.slot);
        }
    }

    public static void HideToyFromClient(GlobalSessionData session, WowGuid128 itemGuid, byte containerSlot, byte slot)
    {
        var state = session.GameState;
        if (itemGuid.IsEmpty() || session.WorldClient == null)
            return;

        uint itemId = state.GetItemId(itemGuid);
        if (itemId != 0)
            state.HiddenToyByItemId[itemId] = itemGuid;

        var destroy = new UpdateObject(state);
        destroy.DestroyedGuids.Add(itemGuid);
        session.WorldClient.SendPacketToClient(destroy);

        if (state.CurrentPlayerGuid.IsEmpty())
            return;

        if (containerSlot == ItemConst.NullSlot
            || containerSlot == Classic.InventorySlots.Bag0)
        {
            int packIndex = slot - Vanilla.InventorySlots.ItemStart;
            if (packIndex < 0 || packIndex >= 24)
                return;
            var updateData = new ObjectUpdate(state.CurrentPlayerGuid, UpdateTypeModern.Values, session);
            updateData.ActivePlayerData.PackSlots[packIndex] = WowGuid128.Empty;
            var updatePacket = new UpdateObject(state);
            updatePacket.ObjectUpdates.Add(updateData);
            session.WorldClient.SendPacketToClient(updatePacket);
            return;
        }

        var bagGuid64 = state.GetInventorySlotItem(containerSlot);
        if (bagGuid64 == WowGuid64.Empty)
            return;
        var bagGuid = bagGuid64.To128(state);
        var bagUpdate = new ObjectUpdate(bagGuid, UpdateTypeModern.Values, session);
        if (slot < bagUpdate.ContainerData.Slots.Length)
            bagUpdate.ContainerData.Slots[slot] = WowGuid128.Empty;
        var bagPacket = new UpdateObject(state);
        bagPacket.ObjectUpdates.Add(bagUpdate);
        session.WorldClient.SendPacketToClient(bagPacket);
    }
}
