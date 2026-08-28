using Microsoft.Extensions.Logging;

namespace HermesProxy.World.Logging;

/// <summary>
/// Source-generated logging for the spell / cast translation path.
///
/// EventId 300-399 is reserved for this file (100-199 WorldSocket dispatch, 200-299
/// WorldClient dispatch, 900-909 object lifecycle).
///
/// All Trace level, so they cost nothing unless Log.Server.MinimumLevel=Verbose.
/// </summary>
internal static partial class SpellLogMessages
{
    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Trace,
        Message = "[SpellCooldown] synthesized from legacy item template itemId={ItemId} spellId={SpellId} cooldownMs={CooldownMs}")]
    public static partial void ItemCooldownSynthesized(
        ILogger logger, uint itemId, uint spellId, int cooldownMs);
}
