namespace HermesProxy.World.Enums;

public enum Currency : uint
{
    ArenaPoints = 1900,
    HonorPoints = 1901,
}

/// <summary>
/// One row of <c>CSV/CurrencyTypes{expansion}.csv</c>: a currency the modern client displays,
/// paired with the legacy item that stands in for it and the cap the client should show.
///
/// Legacy carries these as items in the dedicated currency-token slots and has no currency packet
/// at all, while V3_4_3 expects currency records - <c>.additem 47241</c> on a native server hands
/// over an Emblem of Triumph the client cannot display, whereas <c>.modify currency 301</c>
/// renders it in the currency tab. Native bridges the two through <c>g_ItemToCurrencyStore</c>
/// (SharedDefines.h), reached from <c>Player::ModifyCurrencyFromItemId</c>.
///
/// The item ids originate in the 3.3.5a client's own <c>CurrencyTypes.dbc</c>, whose
/// <c>ItemID</c> column 3.4.3's DB2 no longer carries, so the pairing cannot be recovered at
/// runtime and has to ship as data. <c>MaxQuantity</c> comes from the 3.4.3 DB2 (wago build
/// 3.4.3.54261) - 75000 for honor, 10000 for arena, 100 for the older battleground marks.
/// </summary>
public readonly record struct CurrencyTypeRecord(uint CurrencyId, uint ItemId, uint MaxQuantity);
