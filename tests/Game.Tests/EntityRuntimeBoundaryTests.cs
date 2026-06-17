using GServ.Game;
using GServ.Protocol;

namespace GServ.Game.Tests;

public sealed class EntityRuntimeBoundaryTests
{
    [Fact]
    public void RuntimeLevelItemsAppendRemoveFirstMatchingAndReportInvalidWhenMissing()
    {
        var level = new RuntimeLevel("start.nw");

        Assert.True(level.AddItem(10.5f, 11.5f, LevelItemType.RedRupee));
        Assert.True(level.AddItem(12.5f, 13.5f, LevelItemType.Bombs));
        Assert.Equal([LevelItemType.RedRupee, LevelItemType.Bombs], level.Items.Select(item => item.ItemType));

        Assert.Equal(LevelItemType.RedRupee, level.RemoveItem(10.5f, 11.5f));
        Assert.Equal([LevelItemType.Bombs], level.Items.Select(item => item.ItemType));
        Assert.Equal(LevelItemType.Invalid, level.RemoveItem(10.5f, 11.5f));
    }

    [Fact]
    public void SpawnLevelItemForPlayerDropRemovesPlayerResourceAddsItemAndBuildsForwardPacket()
    {
        var level = new RuntimeLevel("start.nw");
        var state = new DurablePlayerInventoryState { Rupees = 30 };

        var result = LevelItemRuntime.SpawnLevelItem(level, encodedX: 21, encodedY: 23, itemId: 2, playerDrop: true, state);

        Assert.True(result.ChangedLevel);
        Assert.Equal(0, state.Rupees);
        Assert.Equal([LevelItemType.RedRupee], level.Items.Select(item => item.ItemType));
        Assert.Equal([54, 53, 55, 34, 10], result.ForwardPacket);
        Assert.Empty(result.SelfPacket);
    }

    [Fact]
    public void SpawnLevelItemForPlayerDropWithoutResourceDoesNothing()
    {
        var level = new RuntimeLevel("start.nw");
        var state = new DurablePlayerInventoryState { Rupees = 0 };

        var result = LevelItemRuntime.SpawnLevelItem(level, encodedX: 21, encodedY: 23, itemId: 2, playerDrop: true, state);

        Assert.False(result.ChangedLevel);
        Assert.Empty(level.Items);
        Assert.Empty(result.ForwardPacket);
        Assert.Empty(result.SelfPacket);
    }

    [Fact]
    public void TakeLevelItemForwardsDeleteRemovesItemAndAppliesReward()
    {
        var level = new RuntimeLevel("start.nw");
        var state = new DurablePlayerInventoryState { Rupees = 5 };
        level.AddItem(10.5f, 11.5f, LevelItemType.RedRupee);

        var result = LevelItemRuntime.DeleteOrTakeLevelItem(level, encodedX: 21, encodedY: 23, takeItem: true, state);

        Assert.True(result.ChangedLevel);
        Assert.Empty(level.Items);
        Assert.Equal(35, state.Rupees);
        Assert.Equal([55, 53, 55, 10], result.ForwardPacket);
        Assert.Empty(result.SelfPacket);
    }

    [Fact]
    public void DeleteLevelItemDoesNotApplyReward()
    {
        var level = new RuntimeLevel("start.nw");
        var state = new DurablePlayerInventoryState { Rupees = 5 };
        level.AddItem(10.5f, 11.5f, LevelItemType.RedRupee);

        var result = LevelItemRuntime.DeleteOrTakeLevelItem(level, encodedX: 21, encodedY: 23, takeItem: false, state);

        Assert.True(result.ChangedLevel);
        Assert.Empty(level.Items);
        Assert.Equal(5, state.Rupees);
        Assert.Equal([55, 53, 55, 10], result.ForwardPacket);
    }

    [Fact]
    public void RuntimeLevelHorsesAppendAndRemoveFirstMatchingCoordinates()
    {
        var level = new RuntimeLevel("start.nw");

        level.AddHorse("horse.png", 30.5f, 31.0f, direction: 2, bushes: 1);
        level.AddHorse("other.png", 10.0f, 11.0f, direction: 0, bushes: 0);

        level.RemoveHorse(30.5f, 31.0f);

        Assert.Equal(["other.png"], level.Horses.Select(horse => horse.Image));
    }

    [Fact]
    public void RuntimeLevelNpcsUseSetSemanticsAndSortedIteration()
    {
        var level = new RuntimeLevel("start.nw");

        Assert.True(level.AddNpc(9));
        Assert.True(level.AddNpc(3));
        Assert.False(level.AddNpc(9));

        Assert.Equal([3u, 9u], level.NpcIds);

        level.RemoveNpc(3);

        Assert.Equal([9u], level.NpcIds);
    }

    [Fact]
    public void RuntimeBaddiesStartAtOneReuseFreedIdsAndKeepCppFiftyOneBaddyBoundary()
    {
        var level = new RuntimeLevel("start.nw");

        var first = level.AddBaddy(10, 11, type: 2);
        var second = level.AddBaddy(12, 13, type: 3);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);

        level.RemoveBaddy(1);
        var reused = level.AddBaddy(14, 15, type: 4);

        Assert.NotNull(reused);
        Assert.Equal(1, reused.Id);

        while (level.Baddies.Count <= 50)
        {
            Assert.NotNull(level.AddBaddy(1, 1, type: 0));
        }

        Assert.Equal(51, level.Baddies.Count);
        Assert.Null(level.AddBaddy(1, 1, type: 0));
    }

    [Fact]
    public void LevelItemRupeeCountsMatchCppTable()
    {
        Assert.Equal(1, LevelItemCatalog.GetRupeeCount(LevelItemType.GreenRupee));
        Assert.Equal(5, LevelItemCatalog.GetRupeeCount(LevelItemType.BlueRupee));
        Assert.Equal(30, LevelItemCatalog.GetRupeeCount(LevelItemType.RedRupee));
        Assert.Equal(100, LevelItemCatalog.GetRupeeCount(LevelItemType.GoldRupee));
        Assert.Equal(0, LevelItemCatalog.GetRupeeCount(LevelItemType.Bombs));
    }

    [Fact]
    public void BaddyPropsMatchCppResetDefaultsAndPropertyOrder()
    {
        var baddy = RuntimeBaddy.Create(id: 1, x: 10, y: 11, type: 2);

        Assert.Equal(
            new byte[]
            {
                34, 33,
                33, 52,
                34, 54,
                35, 34,
                36, 36, 44, 98, 97, 100, 100, 121, 114, 101, 100, 46, 112, 110, 103,
                37, 32,
                38, 32,
                39, 42,
                40, 32,
                41, 32,
                42, 32,
                10
            },
            EntityRuntimePackets.BaddyProps(baddy, clientVersion: 217));
    }

    [Fact]
    public void TickBaddyTimeoutsSendsSwampShotModeOnlyToNonLeader()
    {
        var level = new RuntimeLevel("start.nw");
        var baddy = level.AddBaddy(10, 11, type: 4);
        Assert.NotNull(baddy);

        level.AddPlayer(10);
        level.AddPlayer(11);

        baddy.SetProps(
            [(byte)(5 + 32), (byte)(BaddyMode.Hurt + 32)],
            baddyItemsEnabled: false,
            baddyRespawnTime: 60,
            rng: null,
            out _);
        baddy.Timeout.SetTimeout(1);

        var result = level.TickBaddyTimeouts();

        Assert.Single(result.Packets);
        Assert.Equal(11, result.Packets.Single().RecipientId);
        Assert.Equal([34, 33, 37, 38, 10], result.Packets.Single().Packet);
        Assert.Empty(result.DropPackets);
    }

    [Fact]
    public void TickBaddyTimeoutsSendsDieModeOnlyToNonLeaderAndSwitchesToDeadMode()
    {
        var level = new RuntimeLevel("start.nw");
        var baddy = level.AddBaddy(10, 11, type: 2);
        Assert.NotNull(baddy);

        level.AddPlayer(12);
        level.AddPlayer(13);

        baddy.SetProps(
            [(byte)(5 + 32), (byte)(BaddyMode.Die + 32)],
            baddyItemsEnabled: false,
            baddyRespawnTime: 60,
            rng: null,
            out _);
        baddy.Timeout.SetTimeout(1);

        var result = level.TickBaddyTimeouts(clientVersion: 217, baddyRespawnTime: 60);

        Assert.Single(result.Packets);
        Assert.Equal(13, result.Packets[0].RecipientId);
        Assert.Equal([34, 33, 37, 41, 10], result.Packets[0].Packet);
        Assert.Equal((byte)BaddyMode.Dead, baddy.Mode);
        Assert.Equal(60, baddy.Timeout.GetTimeout());
    }

    [Fact]
    public void TickBaddyTimeoutsResetsAndSendsBaddyPropsToAllPlayers()
    {
        var level = new RuntimeLevel("start.nw");
        var baddy = level.AddBaddy(10, 11, type: 2);
        Assert.NotNull(baddy);

        level.AddPlayer(20);
        level.AddPlayer(21);
        level.AddPlayer(22);

        baddy.SetProps(
            [(byte)(1 + 32), (byte)(20 + 32), (byte)(2 + 32), (byte)(30 + 32), (byte)(5 + 32), (byte)(BaddyMode.Walk + 32)],
            baddyItemsEnabled: false,
            baddyRespawnTime: 60,
            rng: null,
            out _);
        baddy.Timeout.SetTimeout(1);

        var result = level.TickBaddyTimeouts();
        var expected = EntityRuntimePackets.BaddyProps(baddy, clientVersion: 217);

        Assert.Equal(3, result.Packets.Count);
        Assert.Equal(new ushort[] { 20, 21, 22 }, result.Packets.Select(p => p.RecipientId).ToArray());
        Assert.All(result.Packets, packet => Assert.Equal(expected, packet.Packet));
    }

    [Fact]
    public void TickBaddyTimeoutsRemovesBaddyWhenRespawnDisabled()
    {
        var level = new RuntimeLevel("start.nw");
        var baddy = level.AddBaddy(10, 11, type: 2);
        Assert.NotNull(baddy);

        level.AddPlayer(40);
        level.AddPlayer(41);
        baddy.SetRespawn(false);

        baddy.SetProps(
            [(byte)(5 + 32), (byte)(BaddyMode.Die + 32)],
            baddyItemsEnabled: false,
            baddyRespawnTime: 60,
            rng: null,
            out _);
        baddy.Timeout.SetTimeout(1);

        var result = level.TickBaddyTimeouts();

        Assert.Single(result.Packets);
        Assert.Empty(level.Baddies);
        Assert.Single(result.Packets);
    }
}
