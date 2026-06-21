using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class LevelInteractionBoundaryTests
{
    [Fact]
    public void FindTouchedLinkUsesCppInclusiveTileBoundsAndLevelOrder()
    {
        var level = new NwLevelSnapshot(
            "GLEVNW01",
            [
                new NwLevelLink("first.nw", 1, 2, 3, 4, "5", "6"),
                new NwLevelLink("second.nw", 4, 6, 2, 2, "7", "8")
            ],
            [],
            [],
            [],
            []);

        Assert.Equal("first.nw", LevelInteraction.FindTouchedLink(level, 1, 2)?.NewLevel);
        Assert.Equal("first.nw", LevelInteraction.FindTouchedLink(level, 4, 6)?.NewLevel);
        Assert.Equal("second.nw", LevelInteraction.FindTouchedLink(level, 5, 6)?.NewLevel);
        Assert.Null(LevelInteraction.FindTouchedLink(level, 7, 6));
        Assert.Null(LevelInteraction.FindTouchedLink(level, 4, 9));
    }

    [Fact]
    public void BuildChestKeyUsesCppXColonYColonLevelNameFormat()
    {
        var chest = new NwLevelChest(10, 11, LevelItemType.RedRupee, 3);

        Assert.Equal("10:11:start.nw", LevelInteraction.BuildChestKey(chest, "start.nw"));
    }

    [Fact]
    public void TryOpenChestBuildsCppAckPacketAndRecordsUnopenedChest()
    {
        var level = new NwLevelSnapshot(
            "GLEVNW01",
            [],
            [],
            [],
            [],
            [new NwLevelChest(10, 11, LevelItemType.RedRupee, 3)]);
        var opened = new HashSet<string>(StringComparer.Ordinal);

        var result = LevelInteraction.TryOpenChest(level, "start.nw", 10, 11, opened);

        Assert.True(result.Opened);
        Assert.Equal("10:11:start.nw", result.ChestKey);
        Assert.Equal(LevelItemType.RedRupee, result.ItemType);
        Assert.Equal([36, 33, 42, 43, 10], result.Packet);
        Assert.Contains("10:11:start.nw", opened);
    }

    [Fact]
    public void TryOpenChestSkipsMissingOrAlreadyOpenedChest()
    {
        var level = new NwLevelSnapshot(
            "GLEVNW01",
            [],
            [],
            [],
            [],
            [new NwLevelChest(10, 11, LevelItemType.RedRupee, 3)]);
        var opened = new HashSet<string>(["10:11:start.nw"], StringComparer.Ordinal);

        var alreadyOpened = LevelInteraction.TryOpenChest(level, "start.nw", 10, 11, opened);
        var missing = LevelInteraction.TryOpenChest(level, "start.nw", 12, 13, opened);

        Assert.False(alreadyOpened.Opened);
        Assert.Empty(alreadyOpened.Packet);
        Assert.False(missing.Opened);
        Assert.Empty(missing.Packet);
        Assert.Single(opened);
    }

    [Fact]
    public void BuildTouchedSignPacketsRequiresServersideAndFacingUpSprite()
    {
        var level = LevelWithSigns(new NwLevelSign(10, 11, "Hello\n"));

        Assert.Empty(LevelInteraction.BuildTouchedSignPackets(level, serverside: false, sprite: 0, x: 10, y: 11));
        Assert.Empty(LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 1, x: 10, y: 11));
    }

    [Fact]
    public void BuildTouchedSignPacketsUsesCppInclusiveXRangeAndExactY()
    {
        var level = LevelWithSigns(new NwLevelSign(10, 11, "Hello\n"));

        Assert.Equal([185, 72, 101, 108, 108, 111, 35, 98, 10], LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 4, x: 8.5f, y: 11));
        Assert.Equal([185, 72, 101, 108, 108, 111, 35, 98, 10], LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 0, x: 10.5f, y: 11));
        Assert.Empty(LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 0, x: 8.49f, y: 11));
        Assert.Empty(LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 0, x: 10.51f, y: 11));
        Assert.Empty(LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 0, x: 10, y: 11.01f));
    }

    [Fact]
    public void BuildTouchedSignPacketsSendsEveryMatchingSignWithNewlinesReplacedByHashB()
    {
        var level = LevelWithSigns(
            new NwLevelSign(10, 11, "First\nLine\n"),
            new NwLevelSign(11, 11, "Second"));

        var packets = LevelInteraction.BuildTouchedSignPackets(level, serverside: true, sprite: 0, x: 10.0f, y: 11);

        Assert.Equal(
            [
                185, 70, 105, 114, 115, 116, 35, 98, 76, 105, 110, 101, 35, 98, 10,
                185, 83, 101, 99, 111, 110, 100, 10
            ],
            packets);
    }

    [Fact]
    public void BuildMovementTriggeredSignPacketsUsesRuntimePlayerPixelsAfterMovement()
    {
        var level = LevelWithSigns(new NwLevelSign(10, 11, "Hello\n"));
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate.GShort(Preagonal.GServer.Protocol.PlayerPropertyId.X2, 320),
                Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate.GShort(Preagonal.GServer.Protocol.PlayerPropertyId.Y2, 352)
            ]);

        var packets = LevelInteraction.BuildMovementTriggeredSignPackets(level, player, serverside: true);

        Assert.Equal([185, 72, 101, 108, 108, 111, 35, 98, 10], packets);
    }

    [Fact]
    public void BuildMovementTriggeredSignPacketsRequiresMovementTouchRequest()
    {
        var level = LevelWithSigns(new NwLevelSign(10, 11, "Hello\n"));
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [Preagonal.GServer.Protocol.IncomingPlayerPropertyUpdate.GChar(Preagonal.GServer.Protocol.PlayerPropertyId.Sprite, 0)]);

        Assert.Empty(LevelInteraction.BuildMovementTriggeredSignPackets(level, player, serverside: true));
    }

    private static NwLevelSnapshot LevelWithSigns(params NwLevelSign[] signs) =>
        new("GLEVNW01", [], signs, [], [], []);
}
