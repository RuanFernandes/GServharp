using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class InventoryItemRulesTests
{
    [Fact]
    public void PickupPlayerPropsMatchCppLevelItemResourcePayloads()
    {
        var state = new DurablePlayerInventoryState
        {
            Rupees = 5,
            Bombs = 10,
            Arrows = 10,
            Hitpoints = 2.5f,
            MaxPower = 5,
            GlovePower = 1
        };

        Assert.Equal([35, 32, 32, 137], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.GoldRupee, state));
        Assert.Equal([37, 47], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Bombs, state));
        Assert.Equal([36, 47], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Darts, state));
        Assert.Equal([34, 39], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Heart, state));
        Assert.Equal([38, 34], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Glove1, state));
        Assert.Equal([38, 35], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Glove2, state));
    }

    [Fact]
    public void PickupPlayerPropsMatchCppEquipmentAndSpinPayloads()
    {
        var state = new DurablePlayerInventoryState
        {
            MaxPower = 5,
            Hitpoints = 1,
            ShieldPower = 2,
            SwordPower = 1,
            Status = 0
        };

        Assert.Equal([41, 34], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Shield, state));
        Assert.Equal([41, 35], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.LizardShield, state));
        Assert.Equal([40, 34], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.BattleAxe, state));
        Assert.Equal([40, 36], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.GoldenSword, state));
        Assert.Equal([33, 38, 34, 44], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.FullHeart, state));
        Assert.Equal([50, 96], InventoryItemRules.BuildPickupPlayerProps(LevelItemType.SpinAttack, state));

        state.Status = PlayerStatus.HasSpin;

        Assert.Empty(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.SpinAttack, state));
    }

    [Fact]
    public void PickupWeaponItemsAddDefaultWeaponAndReturnNoPlayerProps()
    {
        var state = new DurablePlayerInventoryState();

        Assert.Empty(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Bow, state));
        Assert.Empty(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Fireblast, state));

        Assert.Contains("bow", state.Weapons);
        Assert.Contains("fireblast", state.Weapons);
    }

    [Fact]
    public void InvalidPickupItemReturnsEmptyPayload()
    {
        Assert.Empty(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Invalid, new DurablePlayerInventoryState()));
    }

    [Fact]
    public void ApplyPickupPlayerPropsMutatesConfirmedRewardPropsLikeSetProps()
    {
        var state = new DurablePlayerInventoryState
        {
            Rupees = 5,
            Bombs = 10,
            Arrows = 10,
            Hitpoints = 2.5f,
            MaxPower = 5,
            GlovePower = 1,
            ShieldPower = 1,
            SwordPower = 1,
            Status = 0
        };

        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.GoldRupee, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Bombs, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Darts, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Heart, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.Glove2, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.MirrorShield, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.BattleAxe, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.FullHeart, state), state);
        InventoryItemRules.ApplyPickupPlayerProps(InventoryItemRules.BuildPickupPlayerProps(LevelItemType.SpinAttack, state), state);

        Assert.Equal(105, state.Rupees);
        Assert.Equal(15, state.Bombs);
        Assert.Equal(15, state.Arrows);
        Assert.Equal(6, state.MaxPower);
        Assert.Equal(6, state.Hitpoints);
        Assert.Equal(3, state.GlovePower);
        Assert.Equal(2, state.ShieldPower);
        Assert.Equal(2, state.SwordPower);
        Assert.True((state.Status & PlayerStatus.HasSpin) != 0);
    }

    [Fact]
    public void TryOpenChestAndApplyRewardRecordsChestAndMutatesState()
    {
        var level = new NwLevelSnapshot(
            "GLEVNW01",
            [],
            [],
            [],
            [],
            [new NwLevelChest(10, 11, LevelItemType.RedRupee, 3)]);
        var opened = new HashSet<string>(StringComparer.Ordinal);
        var state = new DurablePlayerInventoryState { Rupees = 5 };

        var result = LevelInteraction.TryOpenChestAndApplyReward(level, "start.nw", 10, 11, opened, state);

        Assert.True(result.Opened);
        Assert.Equal(LevelItemType.RedRupee, result.ItemType);
        Assert.Equal(35, state.Rupees);
        Assert.Equal([36, 33, 42, 43, 10], result.Packet);
        Assert.Contains("10:11:start.nw", opened);
    }

    [Fact]
    public void RemoveForPlayerDropMatchesCppResourceRequirements()
    {
        var state = new DurablePlayerInventoryState
        {
            Rupees = 100,
            Bombs = 5,
            Arrows = 5,
            Hitpoints = 1.5f,
            GlovePower = 2,
            Status = PlayerStatus.HasSpin
        };

        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.GoldRupee, state));
        Assert.Equal(0, state.Rupees);
        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.Bombs, state));
        Assert.Equal(0, state.Bombs);
        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.Darts, state));
        Assert.Equal(0, state.Arrows);
        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.Heart, state));
        Assert.Equal(0.5f, state.Hitpoints);
        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.Glove1, state));
        Assert.Equal(1, state.GlovePower);
        Assert.True(InventoryItemRules.TryRemoveForPlayerDrop(LevelItemType.SpinAttack, state));
        Assert.Equal((PlayerStatus)0, state.Status);
    }
}
