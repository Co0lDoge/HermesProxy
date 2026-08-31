using System.Collections.Frozen;
using System.Collections.Generic;

namespace HermesProxy.World;

/// <summary>
/// DisplayID -> DestructibleModelData.db2 id, for GAMEOBJECT_TYPE_DESTRUCTIBLE_BUILDING.
///
/// A V3_4_3 client resolves a destructible building's model through DestructibleModelData
/// rather than through the object's DisplayID, and it takes the record id from the raw bit
/// pattern of GameObjectData.ParentRotation's first component. Without a resolvable id it
/// creates the object but draws no geometry — issue #184. See
/// <c>UpdateHandler.BuildDestructibleParentRotation</c>.
///
/// The authoritative id is gameobject_template.data[18], which arrives in
/// SMSG_QUERY_GAME_OBJECT_RESPONSE. That response comes back *after* the create, so the first
/// create of an entry would otherwise ship an unresolvable id and the building would stay
/// invisible for the rest of that battleground round. This table closes that window: every
/// DestructibleModelData record names its intact model in State0WMO, which is exactly the
/// DisplayID the create already carries, so the id can be recovered from the create alone.
///
/// Generated from DestructibleModelData.db2 for build 3.4.3.54261 (41 records, ids 25-67),
/// keyed by State0WMO.
///
/// Several records share an intact model, and picking between them matters: 7906 is State0WMO
/// for records 41, 42 and 43, but **record 41 has State2WMO = 0** — no destroyed model at all.
/// A gate resolved to 41 renders and takes damage normally, then refuses to change on
/// destruction: the client has nothing to swap to, so the intact geometry and its collision
/// stay put while the server and the world map consider the gate destroyed. Observed live in
/// Strand of the Ancients — the one gate that had come from the server template (43) collapsed
/// correctly while three on the 41 fallback stayed solid and impassable.
///
/// So where several records share a State0WMO, prefer the one that actually defines the
/// damaged and destroyed states, breaking ties on the lowest id. Entries below that still list
/// no destroyed model are the only record for that display — that is the client's data, not a
/// choice made here.
/// </summary>
internal static class DestructibleModelLookup
{
    private static readonly FrozenDictionary<int, int> DisplayIdToModelId =
        new Dictionary<int, int>
        {
            { 628, 26 },     // state1 0     state2 0
            { 7541, 27 },    // state1 0     state2 0
            { 7552, 28 },    // state1 0     state2 0
            { 7595, 58 },    // state1 7540  state2 7541
            { 7877, 31 },    // state1 7897  state2 7874
            { 7878, 55 },    // state1 8173  state2 7875
            { 7900, 39 },    // state1 8169  state2 7898
            { 7906, 42 },    // state1 8198  state2 7855   SotA gates — 41 has no destroyed model
            { 7909, 40 },    // state1 8186  state2 7908
            { 7910, 33 },    // state1 7908  state2 0
            { 7914, 36 },    // state1 7913  state2 0
            { 7915, 34 },    // state1 7912  state2 0
            { 8165, 37 },    // state1 8166  state2 8167   WG keep doors
            { 8208, 44 },    // state1 8209  state2 8210   WG vehicle workshops
            { 8250, 46 },    // state1 8246  state2 0
            { 8251, 48 },    // state1 8249  state2 0
            { 8335, 29 },    // state1 0     state2 0
            { 8387, 50 },    // state1 8387  state2 8386
            { 8459, 25 },    // state1 0     state2 0
            { 8523, 30 },    // state1 0     state2 0
            { 8590, 56 },    // state1 8584  state2 8585
            { 8593, 51 },    // state1 8593  state2 8591
            { 8996, 59 },    // state1 9003  state2 9003
            { 8997, 60 },    // state1 8997  state2 9000
            { 9048, 61 },    // state1 8996  state2 9003
            { 9059, 63 },    // state1 9060  state2 0
            { 9085, 62 },    // state1 8997  state2 9000
            { 9256, 65 },    // state1 9257  state2 9258
            { 9276, 67 },    // state1 9257  state2 9258
        }.ToFrozenDictionary();

    /// <summary>
    /// Returns the DestructibleModelData id whose intact model is <paramref name="displayId"/>,
    /// or 0 when the display is not a known destructible model.
    /// </summary>
    public static int GetModelIdForDisplay(int displayId) =>
        DisplayIdToModelId.TryGetValue(displayId, out int modelId) ? modelId : 0;
}
