using Microsoft.Extensions.Logging;

namespace HermesProxy.World.Logging;

internal static partial class BattleGroundLogMessages
{
    // EventId 1000-1009 reserved for battleground / battlefield-mgr translation.

    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug,
        Message = "[WG] {Opcode} battle={BattleId} zone={ZoneId} expire={ExpireUnix} -> ticket={TicketId} list={ListId} timeoutMs={TimeoutMs}")]
    public static partial void EntryInvite(ILogger logger, string Opcode, uint BattleId, uint ZoneId, uint ExpireUnix, uint TicketId, uint ListId, uint TimeoutMs);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "[WG] {Opcode} battle={BattleId} warmup={Warmup} -> ticket={TicketId}")]
    public static partial void QueueInvite(ILogger logger, string Opcode, uint BattleId, byte Warmup, uint TicketId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug,
        Message = "[WG] {Opcode} battle={BattleId} canQueue={CanQueue} loggingIn={LoggingIn} warmup={Warmup}")]
    public static partial void QueueResponse(ILogger logger, string Opcode, uint BattleId, byte CanQueue, byte LoggingIn, byte Warmup);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug,
        Message = "[WG] {Opcode} battle={BattleId} -> STATUS_ACTIVE map={MapId}")]
    public static partial void Entered(ILogger logger, string Opcode, uint BattleId, uint MapId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug,
        Message = "[WG] {Opcode} battle={BattleId} reason={Reason} status={Status} relocated={Relocated}")]
    public static partial void Ejected(ILogger logger, string Opcode, uint BattleId, byte Reason, byte Status, byte Relocated);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug,
        Message = "[WG] PORT ticket={TicketId} accepted={Accepted} -> {Opcode} battle={BattleId}")]
    public static partial void PortAsMgr(ILogger logger, uint TicketId, bool Accepted, string Opcode, uint BattleId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Debug,
        Message = "[WG] LEAVE -> EXIT_REQUEST battle={BattleId}")]
    public static partial void LeaveAsMgr(ILogger logger, uint BattleId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug,
        Message = "[Phase] SMSG_SET_PHASE_SHIFT mask={Mask} -> flags={Flags} phases={PhaseCount}")]
    public static partial void PhaseShift(ILogger logger, uint Mask, uint Flags, int PhaseCount);

}
