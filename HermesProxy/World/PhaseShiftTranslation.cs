using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.World;

internal static class PhaseShiftTranslation
{
    // 3.3.5 SMSG_SET_PHASE_SHIFT is one uint32 mask. 3.4.3 Phase.db2 has no IDs
    // 16/32/64/128. The factory auras AC applies name the modern phase in
    // SpellEffect.MiscValue_1 (build 54261): 56618 Horde factory -> 173, 56617
    // Alliance factory -> 174. Keep-control 55773/55774 point at 414/375, which
    // are not Phase.db2 rows, so they are left unmapped.
    public static PhaseShiftChange ToModern(uint mask, WowGuid128 player)
    {
        var msg = new PhaseShiftChange { Client = player };

        if ((mask & 16u) != 0)
            msg.Phases.Add((1, 173));
        if ((mask & 32u) != 0)
            msg.Phases.Add((1, 174));

        msg.PhaseShiftFlags = msg.Phases.Count == 0 ? 8u : 0u;
        return msg;
    }
}
