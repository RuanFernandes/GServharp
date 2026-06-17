using GServ.Game;

namespace GServ.Game.Tests;

public sealed class LevelItemCatalogTests
{
    [Fact]
    public void GetItemIdMatchesCppItemListNamesAndIds()
    {
        Assert.Equal(LevelItemType.GreenRupee, LevelItemCatalog.GetItemId("greenrupee"));
        Assert.Equal(LevelItemType.Bombs, LevelItemCatalog.GetItemId("bombs"));
        Assert.Equal(LevelItemType.SpinAttack, LevelItemCatalog.GetItemId("spinattack"));
    }

    [Fact]
    public void GetItemIdRejectsUnknownNamesAndOutOfRangeIds()
    {
        Assert.Equal(LevelItemType.Invalid, LevelItemCatalog.GetItemId("GREENRUPEE"));
        Assert.Equal(LevelItemType.Invalid, LevelItemCatalog.GetItemId("missing"));
        Assert.Equal(LevelItemType.Invalid, LevelItemCatalog.GetItemId(-1));
        Assert.Equal(LevelItemType.Invalid, LevelItemCatalog.GetItemId(25));
    }

    [Fact]
    public void GetItemNameReturnsEmptyForInvalidIds()
    {
        Assert.Equal("redrupee", LevelItemCatalog.GetItemName(LevelItemType.RedRupee));
        Assert.Equal("", LevelItemCatalog.GetItemName(LevelItemType.Invalid));
    }
}
