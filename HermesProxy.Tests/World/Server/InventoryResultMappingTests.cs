using System;
using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class InventoryResultMappingTests
{
    [Fact]
    public void WotlkInvFull_IsAlreadyLooted_IfPassedThrough()
    {
        Assert.Equal(50, (int)InventoryResultWotLK.InvFull);
        Assert.Equal(50, (int)InventoryResult.LootGone);
        Assert.Equal(51, (int)InventoryResult.InvFull);
    }

    [Theory]
    [InlineData(InventoryResultWotLK.InvFull, InventoryResult.InvFull)]
    [InlineData(InventoryResultWotLK.LootGone, InventoryResult.LootGone)]
    [InlineData(InventoryResultWotLK.NotEnoughMoney, InventoryResult.NotEnoughMoney)]
    [InlineData(InventoryResultWotLK.BagFull, InventoryResult.BagFull)]
    [InlineData(InventoryResultWotLK.ItemMaxCount, InventoryResult.ItemMaxCount)]
    [InlineData(InventoryResultWotLK.BankFull, InventoryResult.BankFull)]
    public void WotlkCastEnum_MapsByName(InventoryResultWotLK ac, InventoryResult expected)
    {
        Assert.Equal(expected, ac.CastEnum<InventoryResult>());
    }
}
