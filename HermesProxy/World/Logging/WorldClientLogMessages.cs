using HermesProxy.World.Enums;
using Microsoft.Extensions.Logging;

namespace HermesProxy.World.Logging;

/// <summary>
/// Source-generated logging methods for <see cref="Client.WorldClient"/> hot paths.
/// </summary>
#pragma warning disable SYSLIB1015
internal static partial class WorldClientLogMessages
{
    // EventId 200-299 range is reserved for WorldClient packet dispatch.

    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Debug,
        Message = "Received opcode {Opcode} ({OpcodeId}).")]
    public static partial void PacketReceived(
        ILogger logger,
        string SourceFile,
        string NetDir,
        Opcode Opcode,
        uint OpcodeId);

    /// <summary>
    /// Verbose-level variant of <see cref="PacketReceived"/> for noisy opcodes
    /// (movement spam etc.). Same payload, different level — gated by
    /// <c>Log.Server.MinimumLevel=Verbose</c>. See <see cref="NoisyOpcodes"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 208,
        Level = LogLevel.Trace,
        Message = "Received opcode {Opcode} ({OpcodeId}).")]
    public static partial void PacketReceivedNoisy(
        ILogger logger,
        string SourceFile,
        string NetDir,
        Opcode Opcode,
        uint OpcodeId);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Debug,
        Message = "Sending opcode {Opcode} ({OpcodeId}) with size {Size}.")]
    public static partial void PacketSent(
        ILogger logger,
        string SourceFile,
        string NetDir,
        Opcode Opcode,
        uint OpcodeId,
        ushort Size);

    /// <summary>Verbose variant of <see cref="PacketSent"/> for noisy opcodes.</summary>
    [LoggerMessage(
        EventId = 209,
        Level = LogLevel.Trace,
        Message = "Sending opcode {Opcode} ({OpcodeId}) with size {Size}.")]
    public static partial void PacketSentNoisy(
        ILogger logger,
        string SourceFile,
        string NetDir,
        Opcode Opcode,
        uint OpcodeId,
        ushort Size);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "No handler for opcode {Opcode} ({OpcodeId}) (Got unknown packet from WorldServer)")]
    public static partial void NoHandlerForOpcode(
        ILogger logger,
        string SourceFile,
        string NetDir,
        Opcode Opcode,
        uint OpcodeId);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Error,
        Message = "Packet Read Error: {Message}")]
    public static partial void PacketReadError(
        ILogger logger,
        System.Exception ex,
        string SourceFile,
        string NetDir,
        string Message);

    [LoggerMessage(EventId = 204, Level = LogLevel.Information, Message = "Connecting to world server...")]
    public static partial void ConnectingToWorldServer(
        ILogger logger, string SourceFile, string NetDir);

    [LoggerMessage(
        EventId = 205,
        Level = LogLevel.Information,
        Message = "World Server address {Address}:{Port} resolved as {ResolvedAddress}:{Port}")]
    public static partial void WorldServerResolved(
        ILogger logger,
        string SourceFile,
        string NetDir,
        string Address,
        int Port,
        string ResolvedAddress);

    [LoggerMessage(EventId = 206, Level = LogLevel.Information, Message = "Connection established!")]
    public static partial void ConnectionEstablished(
        ILogger logger, string SourceFile, string NetDir);

    [LoggerMessage(EventId = 207, Level = LogLevel.Information, Message = "Authentication succeeded!")]
    public static partial void AuthenticationSucceeded(
        ILogger logger, string SourceFile, string NetDir);

    [LoggerMessage(
        EventId = 210,
        Level = LogLevel.Debug,
        Message = "Guild bank query results tab={Tab} tabs={TabCount} items={ItemCount} fullUpdate={FullUpdate} money={Money}")]
    public static partial void GuildBankQueryResults(
        ILogger logger,
        string SourceFile,
        string NetDir,
        int Tab,
        int TabCount,
        int ItemCount,
        bool FullUpdate,
        ulong Money);

    [LoggerMessage(
        EventId = 211,
        Level = LogLevel.Debug,
        Message = "Inspect talents unspent={Unspent} specs={Specs} active={Active} talents={Talents}")]
    public static partial void InspectTalents(
        ILogger logger,
        string SourceFile,
        string NetDir,
        uint Unspent,
        byte Specs,
        byte Active,
        int Talents);

    [LoggerMessage(
        EventId = 212,
        Level = LogLevel.Debug,
        Message = "Dropped arena petition list count={Count} firstEntry={FirstEntry}")]
    public static partial void ArenaPetitionDropped(
        ILogger logger,
        string SourceFile,
        string NetDir,
        int Count,
        uint FirstEntry);

    [LoggerMessage(
        EventId = 213,
        Level = LogLevel.Debug,
        Message = "RequestItems quest={QuestId} ac=0x{AcFlags:X} status=0x{Status:X} itemsMet={ItemsMet} collect={Collect} auto={Auto}")]
    public static partial void RequestItems(
        ILogger logger,
        string SourceFile,
        string NetDir,
        uint QuestId,
        uint AcFlags,
        uint Status,
        bool ItemsMet,
        int Collect,
        bool Auto);
}
