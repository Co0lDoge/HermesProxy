using HermesProxy;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;

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
}
