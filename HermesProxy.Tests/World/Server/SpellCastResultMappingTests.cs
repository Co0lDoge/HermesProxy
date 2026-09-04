using System;
using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class SpellCastResultMappingTests
{
    [Fact]
    public void WotLK_NoDueling_MapsToV343_NoDueling()
    {
        // AC EffectDuel sends SPELL_FAILED_NO_DUELING (79). The V3_4_3
        // client has that string at 102. Name-map must not land on NeedMoreItems (79).
        Assert.Equal(79u, (uint)SpellCastResultWotLK.NoDueling);
        Assert.Equal(102u, (uint)SpellCastResultV343.NoDueling);
        Assert.Equal(
            SpellCastResultV343.NoDueling,
            SpellCastResultWotLK.NoDueling.CastEnum<SpellCastResultV343>());
    }

    [Fact]
    public void Classic_SpellInProgress_CollidesWith_V343_RequiresSpellFocus()
    {
        // SendCastRequestFailed must not emit Classic 123 at a 3.4.3 client.
        Assert.Equal(123u, (uint)SpellCastResultClassic.SpellInProgress);
        Assert.Equal(123u, (uint)SpellCastResultV343.RequiresSpellFocus);
        Assert.Equal(126u, (uint)SpellCastResultV343.SpellInProgress);
    }
}
