namespace Preagonal.GServer.Game.Tests;

public sealed class CombatDropRuntimeTests
{
    private sealed class SequenceCombatRandom : ICombatRandom
    {
        private readonly int[] _values;
        private int _index;

        public SequenceCombatRandom(params int[] values)
        {
            _values = values;
            _index = 0;
        }

        public int Next(int maxExclusive)
        {
            var value = _values[_index % _values.Length];
            _index++;
            return value;
        }
    }

    [Fact]
    public void DecomposeGralatsUsesGreedyCppBuckets()
    {
        var drops = CombatDropRuntime.DecomposeGralats(134);

        Assert.Equal(
            [
                LevelItemType.GoldRupee,
                LevelItemType.RedRupee,
                LevelItemType.GreenRupee,
                LevelItemType.GreenRupee,
                LevelItemType.GreenRupee,
                LevelItemType.GreenRupee
            ],
            drops);
    }

    [Fact]
    public void ComputeDroppedArrowsAndBombsHonorInventoryCaps()
    {
        Assert.Equal(1, CombatDropRuntime.ComputeDroppedArrows(7, new SequenceCombatRandom(3)));
        Assert.Equal(1, CombatDropRuntime.ComputeDroppedBombs(9, new SequenceCombatRandom(3)));
        Assert.Equal(3, CombatDropRuntime.ComputeDroppedArrows(99, new SequenceCombatRandom(3)));
    }

    [Fact]
    public void ApplyPlayerDeathDropsMutatesInventoryAndBuildsItemPackets()
    {
        var player = new DurablePlayerInventoryState
        {
            Rupees = 41,
            Arrows = 9,
            Bombs = 9,
            MaxPower = 20,
            Hitpoints = 6.0f
        };

        var rng = new SequenceCombatRandom(75, 4, 0, 7, 1, 5, 0, 3, 2, 6, 7);
        var result = CombatDropRuntime.ApplyPlayerDeathDrops(
            player,
            dropItemsDead: true,
            minDeathGralats: 1,
            maxDeathGralats: 50,
            playerX: 10.0f,
            playerY: 20.0f,
            rng);

        Assert.Equal(16, player.Rupees);
        Assert.Equal(9, player.Arrows);
        Assert.Equal(9, player.Bombs);
        Assert.Equal(16, result.RemainingRupees);
        Assert.Equal(5, result.DropPackets.Count);
        Assert.True(result.DropPackets.Count >= 3);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(7, 7)]
    [InlineData(9, 9)]
    public void ComputeDroppedGralatsMatchesCppRangeAndInventoryClamp(int randomValue, int expected)
    {
        var result = CombatDropRuntime.ComputeDroppedGralats(
            maxDrop: 10,
            minDrop: 1,
            currentRupees: randomValue,
            new SequenceCombatRandom(randomValue));

        var expectedDrop = Math.Clamp(expected, 1, 10);
        Assert.Equal(Math.Min(expectedDrop, randomValue), result);
    }

    [Fact]
    public void BuildBaddyDropMappingFromObservedRolls()
    {
        var rng = new SequenceCombatRandom(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        var expected = new[]
        {
            LevelItemType.GreenRupee,
            LevelItemType.BlueRupee,
            LevelItemType.RedRupee,
            LevelItemType.Bombs,
            LevelItemType.Darts,
            LevelItemType.Heart,
            LevelItemType.GreenRupee,
            LevelItemType.GreenRupee,
            LevelItemType.GreenRupee,
            LevelItemType.GreenRupee,
            LevelItemType.Invalid,
            LevelItemType.Invalid
        };

        for (var i = 0; i < 11; i++)
        {
            var expectedItem = i switch
            {
                10 or 11 => LevelItemType.Invalid,
                _ => expected[i]
            };

            var hasDrop = CombatDropRuntime.TryRollBaddyDrop(rng, out var itemType);
            Assert.Equal(expectedItem != LevelItemType.Invalid, hasDrop);
            Assert.Equal(expectedItem, itemType);
        }
    }

    [Fact]
    public void BuildBaddyDropPacketUsesBaddyPositionScaling()
    {
        var packet = CombatDropRuntime.BuildBaddyDropPacket(1.25f, 4.75f, LevelItemType.RedRupee);

        Assert.Equal(
            [
                (byte)Preagonal.GServer.Protocol.ServerToPlayerPacketId.ItemAdd + 32,
                (byte)(1.25f * 2 + 32),
                (byte)(4.75f * 2 + 32),
                (byte)(LevelItemType.RedRupee + 32),
            (byte)'\n'
            ],
            packet);
    }

    [Fact]
    public void BuildBaddyDropPacketUsesCppMappedItemAndCoordinates()
    {
        var packet = CombatDropRuntime.BuildBaddyDropPacket(12.5f, 25.0f, LevelItemType.GreenRupee);

        Assert.Equal([54, 57, 82, 32, 10], packet);
    }

    [Fact]
    public void ApplyPlayerDeathDropsBlocksNoDropWhenDisabled()
    {
        var player = new DurablePlayerInventoryState
        {
            Rupees = 40,
            Arrows = 25,
            Bombs = 11
        };

        var result = CombatDropRuntime.ApplyPlayerDeathDrops(
            player,
            dropItemsDead: false,
            minDeathGralats: 1,
            maxDeathGralats: 50,
            playerX: 10.0f,
            playerY: 20.0f,
            rng: new SequenceCombatRandom(6, 3, 4, 2, 3, 7));

        Assert.Equal(40, result.RemainingRupees);
        Assert.Equal(25, result.RemainingArrows);
        Assert.Equal(11, result.RemainingBombs);
        Assert.Empty(result.DropPackets);
    }

    [Fact]
    public void ApplyPlayerDeathDropsProducesCppConfirmedGoldItemOrderAndGoldenBytes()
    {
        var player = new DurablePlayerInventoryState
        {
            Rupees = 30,
            Arrows = 25,
            Bombs = 15
        };

        // drop_gralats uses 6 -> blue rupee + green rupee
        // drop_arrows/drop_bombs consume randoms 0,0 before coordinates
        // then x/y coordinates consume (rand%8)=3,3 for both drops.
        var result = CombatDropRuntime.ApplyPlayerDeathDrops(
            player,
            dropItemsDead: true,
            minDeathGralats: 1,
            maxDeathGralats: 50,
            playerX: 10.0f,
            playerY: 20.0f,
            rng: new SequenceCombatRandom(6, 0, 0, 3, 3, 3, 3));

        Assert.Equal(24, result.RemainingRupees);
        Assert.Equal(25, result.RemainingArrows);
        Assert.Equal(15, result.RemainingBombs);
        Assert.Equal(
                [
                    54,
                    (byte)(12.5f * 2 + 32),
                    (byte)(23.0f * 2 + 32),
                    (byte)(LevelItemType.BlueRupee + 32),
                    10,
                    54,
                    (byte)(12.5f * 2 + 32),
                    (byte)(23.0f * 2 + 32),
                    (byte)(LevelItemType.GreenRupee + 32),
                    10
            ],
            result.DropPackets.SelectMany(x => x).ToArray());
    }

    [Fact]
    public void BuildBaddyDropBytesForNoDropRollsAreBlocked()
    {
        var rng = new SequenceCombatRandom(10, 11);
        Assert.False(CombatDropRuntime.TryRollBaddyDrop(rng, out _));
        Assert.False(CombatDropRuntime.TryRollBaddyDrop(rng, out _));
    }
}
