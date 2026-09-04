namespace HermesProxy.World;

internal enum BattlefieldMgrTicketKind : byte
{
    Entry = 0,
    Queue = 1,
}

internal static class BattlefieldMgrTranslation
{
    public const uint WintergraspBattleId = 1;
    public const uint WintergraspZoneId = 4197;
    public const uint ModernWintergraspListId = 1089;
    public const uint LegacyWintergraspMapId = 571;
    public const uint DefaultInviteTimeoutMs = 20_000;
    public const byte MinLevel = 71;
    public const byte MaxLevel = 80;

    public const uint EntryTicketBase = 100;
    public const uint QueueTicketBase = 200;

    public static uint TicketFor(uint battleId, BattlefieldMgrTicketKind kind) =>
        (kind == BattlefieldMgrTicketKind.Queue ? QueueTicketBase : EntryTicketBase) + battleId;

    public static bool TryDecodeTicket(uint ticketId, out uint battleId, out BattlefieldMgrTicketKind kind)
    {
        if (ticketId > QueueTicketBase)
        {
            battleId = ticketId - QueueTicketBase;
            kind = BattlefieldMgrTicketKind.Queue;
            return battleId != 0;
        }

        if (ticketId > EntryTicketBase && ticketId < QueueTicketBase)
        {
            battleId = ticketId - EntryTicketBase;
            kind = BattlefieldMgrTicketKind.Entry;
            return battleId != 0;
        }

        battleId = 0;
        kind = BattlefieldMgrTicketKind.Entry;
        return false;
    }

    public static uint ListIdForBattle(uint battleId) =>
        battleId == WintergraspBattleId ? ModernWintergraspListId : battleId;

    public static bool ShouldRouteLeaveToMgr(uint currentZoneId, bool hasTicket) =>
        hasTicket && currentZoneId == WintergraspZoneId;

    public static uint TimeoutMs(uint expireUnix, long nowUnix)
    {
        if (expireUnix <= (uint)nowUnix)
            return DefaultInviteTimeoutMs;
        ulong ms = (ulong)(expireUnix - (uint)nowUnix) * 1000UL;
        return ms > uint.MaxValue ? uint.MaxValue : (uint)ms;
    }
}
