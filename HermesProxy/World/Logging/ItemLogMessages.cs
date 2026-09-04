using Microsoft.Extensions.Logging;

namespace HermesProxy.World.Logging;

/// <summary>
/// Source-generated logging for the inventory and vendor translation path — the client's
/// equip / swap / store requests on their way to the legacy server, and the refusal that
/// comes back.
///
/// These replaced interpolated <c>Log.Print(LogType.Trace, $"…")</c> calls. That form builds
/// its string before the level is checked, so every one of them cost a full format on every
/// inventory action even in a production run with Trace disabled.
///
/// Guids are logged as their two raw halves rather than as a WowGuid128: the record struct's
/// generated ToString allocates, and these fire per item action. Grep a single item's history
/// with the Low value.
///
/// <c>SourceFile</c> is an intentional overflow property (SYSLIB1015 suppressed) so the Serilog
/// template keeps rendering the emitting file in its own column rather than collapsing every
/// line to the bare "Server" category.
///
/// EventId 400-409 is reserved for this file (100-199 WorldSocket dispatch, 200-299 WorldClient
/// dispatch, 300-399 spell, 410-419 character enum, 900-909 object lifecycle).
/// </summary>
#pragma warning disable SYSLIB1015
internal static partial class ItemLogMessages
{
    [LoggerMessage(
        EventId = 400,
        Level = LogLevel.Trace,
        Message = "[VendorTrace] CMSG_BUY_ITEM forward: vendorLow={VendorLow} vendorHigh={VendorHigh} itemID={ItemId} quantity={Quantity} (rawQty={RawQuantity}) MuID={MuId} Slot={Slot} BagSlot={BagSlot} ItemType={ItemType}")]
    public static partial void VendorBuyItemForward(
        ILogger logger, string SourceFile, ulong vendorLow, ulong vendorHigh, uint itemId, uint quantity,
        uint rawQuantity, uint muId, uint slot, uint bagSlot, uint itemType);

    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Trace,
        Message = "[InventoryTrace] CMSG_SWAP_INV_ITEM forward (V3_4_3): raw(Slot2={RawSlot2},Slot1={RawSlot1}) -> legacy src={LegacySrc} dst={LegacyDst}")]
    public static partial void SwapInvItemForwardV343(
        ILogger logger, string SourceFile, byte rawSlot2, byte rawSlot1, byte legacySrc, byte legacyDst);

    [LoggerMessage(
        EventId = 402,
        Level = LogLevel.Trace,
        Message = "[InventoryTrace] CMSG_AUTO_STORE_BAG_ITEM forward: raw(A={RawContainerA},{RawSlotA} B={RawContainerB}) -> legacy bag={LegacyBag} slot={LegacySlot} dst={LegacyDst}")]
    public static partial void AutoStoreBagItemForward(
        ILogger logger, string SourceFile, byte rawContainerA, byte rawSlotA, byte rawContainerB,
        byte legacyBag, byte legacySlot, byte legacyDst);

    [LoggerMessage(
        EventId = 403,
        Level = LogLevel.Trace,
        Message = "[InventoryTrace] {Opcode} forward: raw(PackSlot={RawPackSlot},Slot={RawSlot}) -> legacy bag={LegacyBag} slot={LegacySlot}")]
    public static partial void AutoEquipForward(
        ILogger logger, string SourceFile, Enums.Opcode opcode, byte rawPackSlot, byte rawSlot,
        byte legacyBag, byte legacySlot);

    [LoggerMessage(
        EventId = 404,
        Level = LogLevel.Trace,
        Message = "[InventoryTrace] SMSG_INVENTORY_CHANGE_FAILURE {Path}: BagResult={BagResult} ({BagResultValue}) item0Low={Item0Low} item1Low={Item1Low} ContainerBSlot={ContainerBSlot} Level={Level} LimitCategory={LimitCategory}")]
    public static partial void InventoryChangeFailureWrite(
        ILogger logger, string SourceFile, string path, Enums.InventoryResult bagResult, int bagResultValue,
        ulong item0Low, ulong item1Low, byte containerBSlot, int level, int limitCategory);
}
