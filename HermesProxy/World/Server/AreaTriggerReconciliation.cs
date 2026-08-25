using System.Collections.Frozen;
using System.Linq;

namespace HermesProxy.World.Server;

// V3_4_3 client / 3.3.5a server AreaTrigger ID drift reconciliation.
//
// Cataclysm renumbered most static AreaTrigger.dbc IDs and dropped many of the
// pre-BC walk-through triggers. The 2.5.3/3.4.3 Classic clients ship the
// post-Cataclysm DB2, while 3.3.5a server emulators (TC, cMaNGOS, …) keep the
// original WotLK-era IDs in `areatrigger_teleport`. Two failure modes show up:
//
//   1. Modern client fires CMSG_AREA_TRIGGER with a renumbered id (e.g. 4356)
//      that the legacy server doesn't know about. → remap to the legacy id.
//   2. Modern client has no static entry on this map at all (e.g. the Blasted
//      Lands Dark Portal vanished from AreaTrigger.db2 entirely). → synthesize
//      CMSG_AREA_TRIGGER from movement position.
internal static class AreaTriggerReconciliation
{
    internal readonly record struct Entry(
        uint LegacyId,
        uint? ModernId,
        uint MapId,
        Vector3 Center,
        float Radius);

    private static readonly Entry[] All =
    [
        // Dark Portal — Blasted Lands → Outland. No entry in 2.5.3/3.4.3
        // AreaTrigger.db2 (verified empirically). Center placed at the portal
        // frame's footprint (published 3.3.5a coords ~(-11898,-3210,-22) plus
        // empirical observation of the player walking the platform at Z ~ -16).
        // Generous 30-unit radius — covers walking through from either approach.
        new(LegacyId: 4354, ModernId: null, MapId: 0,
            Center: new Vector3(-11900f, -3210f, -16f),
            Radius: 30f),

        // Dark Portal — Outland → Blasted Lands. TC 3.3.5a's
        // areatrigger_teleport row 4352 ("Dark Portal - E. Kingdoms Target")
        // is the correct legacy ID — *not* 4524, which is what public sniff
        // data and the modern DB2 use. The 2.5.3/3.4.3 DB2 has trigger 4356
        // at (-248, 1042, 54) R=50 but its AreaTriggerActionSet 26557 is a
        // dangling reference — the V3_4_3 client never fires it anyway.
        // ModernId=4356 kept as defensive remap; the real path is proximity
        // synth on the south face of the Outland portal frame. Tight radius
        // so the BL→Outland spawn at (-248, 922.9, 84) sits OUTSIDE the
        // sphere — otherwise we'd auto-re-teleport on arrival.
        new(LegacyId: 4352, ModernId: 4356, MapId: 530,
            Center: new Vector3(-248f, 905f, 84f),
            Radius: 10f),

        // Warsong Gulch flag rooms. 3646 (Silverwing Hold) and 3647 (Warsong Lumber
        // Mill) do not exist in the 3.4.3 client's AreaTrigger.db2 at all — verified
        // against wago.tools build 3.4.3.54261, which has zero rows for either id.
        // They were replaced by box-shaped triggers 4628 / 4629 sitting on the same
        // two flag rooms (map 489, 4628 at 1470,1475,373.7 and 4629 at 973,1445,367).
        //
        // 3.3.5a cores gate flag capture on the old ids — BattlegroundWS::HandleAreaTrigger
        // switches on 3646/3647 and explicitly no-ops 4628/4629 — so without this remap the
        // client fires a trigger the server discards and returning the flag never scores.
        //
        // The remap alone is not enough. The client fires CMSG_AREA_TRIGGER once, on
        // entry into its box, while the server re-validates against the *legacy* volume
        // (WorldSession::HandleAreaTriggerOpcode -> Player::IsInAreaTriggerRadius). The
        // modern boxes sit ~60-70 units from the legacy triggers, which sit on the flags
        // themselves, so the single client-fired event is frequently rejected as "too
        // far" and nothing retries — observed as having to stand on the flag for ~30s
        // until some stray step re-crossed the box boundary.
        //
        // So these also get proximity synthesis, centred on the flag spawns themselves
        // (AzerothCore BattlegroundWS.cpp:445-446, matching TrinityCore) with a sphere
        // wide enough to cover the flag room. While inside, the legacy id is re-fired
        // every LegacyAreaTriggerResendIntervalMs; the server's volume + flag-state
        // checks decide when it actually scores, so repeats are harmless.
        //
        // Map 489 = Warsong Gulch.

        // Silverwing Hold — the Alliance flag room, at the south end of the map.
        // An Alliance player carrying the Horde flag scores here, provided the
        // Alliance flag is on its base.
        new(LegacyId: 3646, ModernId: 4628, MapId: 489,
            Center: new Vector3(1540.423f, 1481.325f, 351.828f), // BG_WS_OBJECT_A_FLAG
            Radius: 25f),

        // Warsong Lumber Mill — the Horde flag room, at the north end of the map.
        // A Horde player carrying the Alliance flag scores here, provided the
        // Horde flag is on its base.
        new(LegacyId: 3647, ModernId: 4629, MapId: 489,
            Center: new Vector3(916.023f, 1434.405f, 345.413f), // BG_WS_OBJECT_H_FLAG
            Radius: 25f),
    ];

    internal static readonly FrozenDictionary<uint, uint> ModernToLegacy =
        All.Where(e => e.ModernId is not null)
           .ToFrozenDictionary(e => e.ModernId!.Value, e => e.LegacyId);

    // All entries with a positive radius get proximity synthesis, regardless
    // of whether they also have a modern remap id. Multiple sends of the same
    // legacy id are idempotent server-side (volume check + teleport).
    internal static readonly FrozenDictionary<uint, Entry[]> ProximityByMap =
        All.Where(e => e.Radius > 0f)
           .GroupBy(e => e.MapId)
           .ToFrozenDictionary(g => g.Key, g => g.ToArray());
}
