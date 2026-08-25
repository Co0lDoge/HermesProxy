using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class SpellCastResultMappingTests
{
    [Fact]
    public void Classic_SpellInProgress_CollidesWith_V343_RequiresSpellFocus()
    {
        // SendCastRequestFailed must not emit Classic 123 at a 3.4.3 client.
        Assert.Equal(123u, (uint)SpellCastResultClassic.SpellInProgress);
        Assert.Equal(123u, (uint)SpellCastResultV343.RequiresSpellFocus);
        Assert.Equal(126u, (uint)SpellCastResultV343.SpellInProgress);
    }
}
