using System.Text;
using HermesProxy.World.Chat;
using Xunit;

namespace HermesProxy.Tests.World.Chat;

// Codec-level tests. These exercise the parse/format pair directly rather than going
// through ItemLinkTranslator, because the translator picks its legacy codec from
// LegacyVersion.ExpansionVersion, which is process-wide static state established at
// startup and not settable per test.
//
// Every modern sample here is a real capture taken from a play session against a live
// backend, not a hand-written string — the empty-field pattern and the trailing group are
// easy to get subtly wrong by hand.
public class ItemLinkTranslatorTests
{
    private static string Format(IItemLinkCodec codec, in ItemLinkFields fields)
    {
        var builder = new StringBuilder();
        codec.Format(fields, builder);
        return builder.ToString();
    }

    [Theory]
    // Captured from a 3.4.3.54261 client. Seven empty fields, player level at index 7,
    // then nine more empty fields.
    [InlineData("33447::::::::80:::::::::", 33447, 80)]
    [InlineData("2592::::::::80:::::::::", 2592, 80)]
    [InlineData("6948::::::::80:::::::::", 6948, 80)]
    [InlineData("49778::::::::80:::::::::", 49778, 80)]
    public void ModernCodec_ParsesRealCaptures(string body, int expectedItemId, int expectedLevel)
    {
        Assert.True(ModernItemLinkCodec.Instance.TryParse(body, out var fields));
        Assert.Equal(expectedItemId, fields.ItemId);
        Assert.Equal(expectedLevel, fields.LinkLevel);
        Assert.Equal(0, fields.EnchantId);
        Assert.Equal(0, fields.RandomPropertyId);
    }

    [Fact]
    public void ModernToWotLk_PlainItem_ProducesNineNumericTokens()
    {
        // The exact shape TrinityCore's LinkTags::item::StoreTo requires: nine tokens,
        // gem4 zero, nothing trailing.
        Assert.True(ModernItemLinkCodec.Instance.TryParse("6948::::::::80:::::::::", out var fields));

        string legacy = Format(WotLkItemLinkCodec.Instance, fields);

        Assert.Equal("6948:0:0:0:0:0:0:0:80", legacy);
        Assert.Equal(9, legacy.Split(':').Length);
    }

    [Fact]
    public void WotLkCodec_RoundTripsItsOwnOutput()
    {
        Assert.True(ModernItemLinkCodec.Instance.TryParse("33447::::::::80:::::::::", out var fromModern));
        string legacy = Format(WotLkItemLinkCodec.Instance, fromModern);

        Assert.True(WotLkItemLinkCodec.Instance.TryParse(legacy, out var reparsed));
        Assert.Equal(fromModern, reparsed);
    }

    [Fact]
    public void WotLkCodec_RejectsModernLink()
    {
        // A modern link starts with nine parsable-looking fields, so the trailing-token
        // check is what actually distinguishes the two formats. Without it, inbound
        // translation would happily "parse" outbound strings.
        Assert.False(WotLkItemLinkCodec.Instance.TryParse("33447::::::::80:::::::::", out _));
    }

    [Fact]
    public void WotLkCodec_ParsesCMaNGOSDocumentedExample()
    {
        // |cffa335ee|Hitem:812:0:0:0:0:0:0:0:70|h[Glowing Brightwood Staff]|h|r
        // from mangos-wotlk/src/game/Chat/Chat.cpp — nine tokens, level 70.
        Assert.True(WotLkItemLinkCodec.Instance.TryParse("812:0:0:0:0:0:0:0:70", out var fields));
        Assert.Equal(812, fields.ItemId);
        Assert.Equal(70, fields.LinkLevel);
    }

    [Fact]
    public void PositiveRandomProperty_KeepsItsSign()
    {
        // Regression test for a real rejection. Bloodstrike Dagger (15247) has
        // RandomProperty=5324 / RandomSuffix=0, so TrinityCore only accepts a positive id
        // for it — `if (randomPropertyId < 0) { if (!val.Item->RandomSuffix) return false; }`.
        // A 3.4.3 client linking "Bloodstrike Dagger of Healing" sends +2042. Negating it
        // produced Hitem:15247:0:0:0:0:0:-2042:0:80, which the server discarded.
        // Six colons after the itemID: five empty fields, then the value at index 5.
        Assert.True(ModernItemLinkCodec.Instance.TryParse("15247::::::2042::80", out var fields));
        Assert.Equal(2042, fields.RandomPropertyId);

        Assert.Equal("15247:0:0:0:0:0:2042:0:80", Format(WotLkItemLinkCodec.Instance, fields));
    }

    [Fact]
    public void NegativeRandomProperty_KeepsItsSign()
    {
        // Captured from a 2.5.3.42328 client. The modern client signs the field itself, so a
        // negative value means ItemRandomSuffix.dbc and must survive untouched.
        Assert.True(ModernItemLinkCodec.Instance.TryParse("25148::::::-1", out var fields));
        Assert.Equal(-1, fields.RandomPropertyId);

        Assert.Equal("25148:0:0:0:0:0:-1:0:0", Format(WotLkItemLinkCodec.Instance, fields));
    }

    [Fact]
    public void RandomProperty_RoundTripsBothSignsUnchanged()
    {
        foreach (int value in new[] { -2042, -1, 0, 1, 2042 })
        {
            string legacy = $"15479:0:0:0:0:0:{value}:0:80";
            Assert.True(WotLkItemLinkCodec.Instance.TryParse(legacy, out var fields));
            Assert.Equal(value, fields.RandomPropertyId);

            string modern = Format(ModernItemLinkCodec.Instance, fields);
            Assert.Equal($"15479:0:0:0:0:0:{value}:0:80:0:0:0", modern);

            Assert.True(ModernItemLinkCodec.Instance.TryParse(modern, out var reparsed));
            Assert.Equal(fields, reparsed);
        }
    }

    [Fact]
    public void ModernGem4_IsDroppedBecauseLegacyRequiresZero()
    {
        // StoreTo ends with `&& !dummy`, so a non-zero gem4 fails validation outright.
        // enchant, three gems, gem4=4, then empty suffix and uniqueID, level at index 7.
        Assert.True(ModernItemLinkCodec.Instance.TryParse("40000:100:1:2:3:4:::80", out var fields));
        Assert.Equal(4, fields.Gem4);

        string legacy = Format(WotLkItemLinkCodec.Instance, fields);
        Assert.Equal("40000:100:1:2:3:0:0:0:80", legacy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanumber")]
    [InlineData("0::::::::80")]        // itemId 0 is not a real item
    [InlineData("-5::::::::80")]
    public void ModernCodec_RejectsMalformedBodies(string body)
    {
        Assert.False(ModernItemLinkCodec.Instance.TryParse(body, out _));
    }

    [Theory]
    [InlineData("812:0:0:0:0:0:0:0")]          // too few
    [InlineData("812:0:0:0:0:0:0:0:70:0")]     // too many
    [InlineData("812:0:0:0:0:0:0:0:x")]        // non-numeric
    public void WotLkCodec_RejectsWrongTokenCounts(string body)
    {
        Assert.False(WotLkItemLinkCodec.Instance.TryParse(body, out _));
    }

    [Fact]
    public void EnchantAndGems_SurviveTranslation()
    {
        Assert.True(ModernItemLinkCodec.Instance.TryParse("32837:3789:41398:41398:0:0:0:0:80", out var fields));
        Assert.Equal(3789, fields.EnchantId);
        Assert.Equal(41398, fields.Gem1);
        Assert.Equal(41398, fields.Gem2);

        Assert.Equal("32837:3789:41398:41398:0:0:0:0:80", Format(WotLkItemLinkCodec.Instance, fields));
    }
}
