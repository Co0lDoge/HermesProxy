using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;
using Microsoft.Extensions.Logging;

namespace HermesProxy.World.Logging;

/// <summary>
/// Source-generated logging for the character-list envelope and the per-character body inside
/// it, plus the action-bar packet that follows a login.
///
/// These replaced interpolated <c>Log.Print(LogType.Trace, $"…")</c> calls that ran on every
/// character enumeration regardless of level. The hex-dump variants are worse than the usual
/// case: their arguments came from <c>GetData()</c>, which allocates a copy of the entire
/// packet, so a ten-character account paid roughly twenty whole-buffer copies per request even
/// with Trace disabled. Callers now build those strings inside an explicit
/// <c>IsEnabled(LogLevel.Trace)</c> block — the attribute alone would not have helped, since it
/// suppresses the format but not the evaluation of the arguments handed to it.
///
/// Race, Class, Gender and CharacterFlags stay as their enum types rather than being widened to
/// a byte, so the output still reads <c>race=Draenei class=Hunter</c> the way the interpolated
/// form did. <c>SourceFile</c> is an intentional overflow property (SYSLIB1015 suppressed) so
/// the Serilog template keeps rendering the emitting file in its own column instead of
/// collapsing every line to the bare "Server" category.
///
/// Type names come from <c>nameof</c> so a rename cannot leave the log lines pointing at a
/// class that no longer exists. <c>nameof</c> is a constant expression, so it still satisfies
/// the attribute's compile-time-constant requirement.
///
/// EventId 410-429 is reserved for this file (400-409 item and inventory, 300-399 spell,
/// 200-299 WorldClient dispatch, 100-199 WorldSocket dispatch).
/// </summary>
#pragma warning disable SYSLIB1015
internal static partial class CharacterEnumLogMessages
{
    [LoggerMessage(
        EventId = 410,
        Level = LogLevel.Trace,
        Message = nameof(EnumCharactersResult) + ".Write: ENTER expansion={Expansion} chars={CharacterCount}")]
    public static partial void EnumEnter(
        ILogger logger, string SourceFile, byte expansion, int characterCount);

    [LoggerMessage(
        EventId = 411,
        Level = LogLevel.Trace,
        Message = nameof(EnumCharactersResult) + ".Write: branch=V3_4_3 (WPP layout, 7 bits + 5 UInt32s)")]
    public static partial void EnumBranchV343(ILogger logger, string SourceFile);

    [LoggerMessage(
        EventId = 412,
        Level = LogLevel.Trace,
        Message = nameof(EnumCharactersResult) + ".Write: EXIT total={TotalBytes}b (V3_4_3 path)")]
    public static partial void EnumExitV343(ILogger logger, string SourceFile, int totalBytes);

    [LoggerMessage(
        EventId = 413,
        Level = LogLevel.Trace,
        Message = nameof(EnumCharactersResult) + ".Write: branch=Legacy (V1_14/V2_5 layout)")]
    public static partial void EnumBranchLegacy(ILogger logger, string SourceFile);

    [LoggerMessage(
        EventId = 414,
        Level = LogLevel.Trace,
        Message = "[CharEnumEnv] charsCount={CharacterCount} maxLevel={MaxLevel} raceCount={RaceCount} envBytes={EnvelopeBytes} envFirst40={EnvelopeFirst40}")]
    public static partial void EnvelopeSummary(
        ILogger logger, string SourceFile, int characterCount, int maxLevel, int raceCount,
        int envelopeBytes, string envelopeFirst40);

    [LoggerMessage(
        EventId = 415,
        Level = LogLevel.Trace,
        Message = "[CharEnumEnv] customizations[0]={Customizations}")]
    public static partial void EnvelopeCustomizations(
        ILogger logger, string SourceFile, string customizations);

    [LoggerMessage(
        EventId = 416,
        Level = LogLevel.Trace,
        Message = "[CharInfo] name={Name} race={Race} class={Class} level={Level} visItems={VisualItems} bytes={Bytes}")]
    public static partial void CharInfoSummary(
        ILogger logger, string SourceFile, string name, Race race, Class @class, byte level,
        int visualItems, int bytes);

    [LoggerMessage(
        EventId = 417,
        Level = LogLevel.Trace,
        Message = "[CharInfo] first40={First40}")]
    public static partial void CharInfoFirst40(ILogger logger, string SourceFile, string first40);

    [LoggerMessage(
        EventId = 418,
        Level = LogLevel.Trace,
        Message = "[CharInfo] last30={Last30}")]
    public static partial void CharInfoLast30(ILogger logger, string SourceFile, string last30);

    [LoggerMessage(
        EventId = 419,
        Level = LogLevel.Trace,
        Message = nameof(EnumCharactersResult.CharacterInfo) + ".Write_V3_4_3: ENTER name='{Name}' guidLow={GuidLow} guidHigh={GuidHigh} race={Race} class={Class} sex={Sex} flags={Flags} flags2=0x{Flags2:X8} flags3=0x{Flags3:X8} flags4=0x{Flags4:X8}")]
    public static partial void CharInfoWriteV343Enter(
        ILogger logger, string SourceFile, string name, ulong guidLow, ulong guidHigh,
        Race race, Class @class, Gender sex, CharacterFlags flags,
        uint flags2, uint flags3, uint flags4);

    [LoggerMessage(
        EventId = 420,
        Level = LogLevel.Trace,
        Message = "[ActionButtonsTrace] " + nameof(UpdateActionButtons) + " write: legacyCount={LegacyCount} nonZeroSlots={NonZeroSlots} paddedTo={PaddedTo} Reason={Reason} sample:{Sample}")]
    public static partial void ActionButtonsWrite(
        ILogger logger, string SourceFile, int legacyCount, int nonZeroSlots, int paddedTo,
        byte reason, string sample);
}
