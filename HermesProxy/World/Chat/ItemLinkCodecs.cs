using System;
using System.Text;

namespace HermesProxy.World.Chat;

// Item-hyperlink field translation between modern and legacy itemstring formats.
//
// The modern client emits an itemstring the legacy server's chat validator does not
// accept. TrinityCore's `LinkTags::item::StoreTo` requires exactly nine numeric tokens,
// forbids trailing fields, and requires the gem4 slot to be zero; the modern client sends
// seventeen tokens, most of them empty. A message carrying such a link is discarded
// silently — `ValidateHyperlinksAndMaybeKick` returns false and `HandleMessagechatOpcode`
// returns without broadcasting. See issue 139.
//
// mangos-family servers only parse per-tag fields at ChatStrictLinkChecking.Severity 3,
// so they accept the modern string as-is at typical settings. Translation is therefore
// not required for them, but emitting the native legacy format is still correct: it is
// exactly what those servers generate themselves.
//
// Rather than N*M direct conversions, every format parses into `ItemLinkFields` and is
// emitted from it. Adding an era is one codec, not a new conversion pair.

/// <summary>
/// Version-neutral item-link payload. Field semantics follow the *legacy* convention,
/// because it is the stricter of the two: <see cref="RandomPropertyId"/> is signed, with
/// negative values indexing ItemRandomSuffix.dbc and positive values ItemRandomProperties.dbc.
/// </summary>
public readonly record struct ItemLinkFields(
    int ItemId,
    int EnchantId,
    int Gem1,
    int Gem2,
    int Gem3,
    int Gem4,
    int RandomPropertyId,
    int RandomSuffixSeed,
    int LinkLevel);

/// <summary>
/// Parses and formats the portion of an item hyperlink between <c>|Hitem:</c> and <c>|h</c>.
/// </summary>
public interface IItemLinkCodec
{
    /// <summary>Parses a link body. Returns false when the body does not match this format.</summary>
    bool TryParse(ReadOnlySpan<char> body, out ItemLinkFields fields);

    /// <summary>Appends a link body in this format. Does not write the surrounding tags.</summary>
    void Format(in ItemLinkFields fields, StringBuilder builder);
}

/// <summary>
/// Modern itemstring, as sent by 1.14 / 2.5.x / 3.4.3 clients:
/// <c>itemID:enchant:gem1:gem2:gem3:gem4:suffix:unique:linkLevel:spec:upgrade:context:numBonusIDs[:...]</c>
/// <para>
/// Only the first eight indices are read. The trailing groups (specialization, upgrade,
/// item context, bonus and modifier lists) vary in count between builds and have no legacy
/// representation, so one codec covers every modern build we support.
/// </para>
/// </summary>
public sealed class ModernItemLinkCodec : IItemLinkCodec
{
    public static readonly ModernItemLinkCodec Instance = new();

    // Indices after itemID.
    private const int IdxEnchant = 0;
    private const int IdxGem1 = 1;
    private const int IdxGem2 = 2;
    private const int IdxGem3 = 3;
    private const int IdxGem4 = 4;
    private const int IdxSuffix = 5;
    private const int IdxLinkLevel = 7;

    public bool TryParse(ReadOnlySpan<char> body, out ItemLinkFields fields)
    {
        fields = default;

        var tokens = new ItemLinkTokenizer(body);
        if (!tokens.TryNext(out int itemId) || itemId <= 0)
            return false;

        Span<int> values = stackalloc int[IdxLinkLevel + 1];
        for (int i = 0; i < values.Length; i++)
        {
            // Modern links leave unused fields empty rather than zero, and short links are
            // legal — a missing trailing field is simply absent, not malformed.
            if (!tokens.TryNext(out int value))
                value = 0;
            values[i] = value;
        }

        fields = new ItemLinkFields(
            ItemId: itemId,
            EnchantId: values[IdxEnchant],
            Gem1: values[IdxGem1],
            Gem2: values[IdxGem2],
            Gem3: values[IdxGem3],
            Gem4: values[IdxGem4],
            RandomPropertyId: values[IdxSuffix],
            // ITEM_FIELD_PROPERTY_SEED has no modern equivalent. Suffix stat values are
            // derived from it server-side, so a rewritten link renders the suffix name but
            // not its rolled magnitude.
            RandomSuffixSeed: 0,
            LinkLevel: values[IdxLinkLevel]);
        return true;
    }

    public void Format(in ItemLinkFields fields, StringBuilder builder)
    {
        // Emitted with the trailing spec/upgrade/context group zeroed rather than empty.
        // The modern client parses positionally and accepts both, and explicit zeros keep
        // the output unambiguous when it appears in a log.
        builder.Append(fields.ItemId).Append(':')
               .Append(fields.EnchantId).Append(':')
               .Append(fields.Gem1).Append(':')
               .Append(fields.Gem2).Append(':')
               .Append(fields.Gem3).Append(':')
               .Append(fields.Gem4).Append(':')
               .Append(fields.RandomPropertyId).Append(':')
               .Append(0).Append(':')
               .Append(fields.LinkLevel)
               .Append(":0:0:0");
    }

    // The random-property value is carried through unchanged in both directions.
    //
    // It is tempting to assume the modern client stores the id positive and that legacy
    // needs it negative, and to negate on the way out — the downstream jimsproxy fix does
    // exactly that. It is wrong: the modern client already uses the same signed convention
    // as legacy, where negative means ItemRandomSuffix.dbc and positive means
    // ItemRandomProperties.dbc.
    //
    // Verified against TrinityCore, which rejects a mismatched sign outright:
    //
    //     if (randomPropertyId < 0) { if (!val.Item->RandomSuffix) return false; ... }
    //     else if (randomPropertyId > 0) { if (!val.Item->RandomProperty) return false; ... }
    //
    // Bloodstrike Dagger (15247) has RandomProperty=5324 and RandomSuffix=0, so it can only
    // appear with a positive id. A 3.4.3 client linking "Bloodstrike Dagger of Healing" sent
    // +2042; negating it produced `Hitem:15247:0:0:0:0:0:-2042:0:80`, which the server logged
    // as an invalid link and discarded. A 2.5.3 client was separately captured emitting a
    // negative value directly. Both are the client signing the field correctly on its own.
}

/// <summary>
/// WotLK 3.3.5a itemstring:
/// <c>itemID:enchant:gem1:gem2:gem3:gem4:randomProperty:randomSuffixSeed:renderLevel</c>
/// <para>
/// Verified against TrinityCore <c>Chat/HyperlinkTags.cpp</c> <c>LinkTags::item::StoreTo</c>,
/// which consumes exactly these nine tokens and then requires <c>IsEmpty()</c> and a zero
/// gem4 slot. CMaNGOS agrees — <c>mangos-wotlk/src/game/Chat/Chat.cpp</c> carries the example
/// <c>|Hitem:812:0:0:0:0:0:0:0:70|h[Glowing Brightwood Staff]|h|r</c>. Its neighbouring prose
/// comment lists one field too many and is off by one; the example is the one to trust.
/// </para>
/// </summary>
public sealed class WotLkItemLinkCodec : IItemLinkCodec
{
    public static readonly WotLkItemLinkCodec Instance = new();

    private const int TokenCount = 9;

    public bool TryParse(ReadOnlySpan<char> body, out ItemLinkFields fields)
    {
        fields = default;

        var tokens = new ItemLinkTokenizer(body);
        Span<int> values = stackalloc int[TokenCount];
        for (int i = 0; i < TokenCount; i++)
        {
            if (!tokens.TryNext(out values[i]))
                return false;
        }

        // Trailing tokens mean this is not a legacy link — most likely a modern one that
        // happens to start with nine parsable fields.
        if (!tokens.IsExhausted)
            return false;

        if (values[0] <= 0)
            return false;

        fields = new ItemLinkFields(
            ItemId: values[0],
            EnchantId: values[1],
            Gem1: values[2],
            Gem2: values[3],
            Gem3: values[4],
            Gem4: values[5],
            RandomPropertyId: values[6],
            RandomSuffixSeed: values[7],
            LinkLevel: values[8]);
        return true;
    }

    public void Format(in ItemLinkFields fields, StringBuilder builder)
    {
        builder.Append(fields.ItemId).Append(':')
               .Append(fields.EnchantId).Append(':')
               .Append(fields.Gem1).Append(':')
               .Append(fields.Gem2).Append(':')
               .Append(fields.Gem3).Append(':')
               // The parser rejects a non-zero gem4 outright (`!dummy`). 3.3.5a items have
               // no fourth socket, so discarding a modern gem4 loses nothing real.
               .Append(0).Append(':')
               .Append(fields.RandomPropertyId).Append(':')
               .Append(fields.RandomSuffixSeed).Append(':')
               .Append(fields.LinkLevel);
    }
}

/// <summary>
/// Splits a colon-separated link body without allocating. An empty token reads as zero,
/// which is what the modern format's omitted fields mean.
/// </summary>
internal ref struct ItemLinkTokenizer
{
    private ReadOnlySpan<char> _remaining;
    private bool _done;

    public ItemLinkTokenizer(ReadOnlySpan<char> body)
    {
        _remaining = body;
        _done = false;
    }

    /// <summary>True once every token has been consumed.</summary>
    public readonly bool IsExhausted => _done;

    public bool TryNext(out int value)
    {
        value = 0;
        if (_done)
            return false;

        int separator = _remaining.IndexOf(':');
        ReadOnlySpan<char> token;
        if (separator < 0)
        {
            token = _remaining;
            _done = true;
        }
        else
        {
            token = _remaining[..separator];
            _remaining = _remaining[(separator + 1)..];
        }

        if (token.IsEmpty)
            return true;

        return int.TryParse(token, out value);
    }
}
