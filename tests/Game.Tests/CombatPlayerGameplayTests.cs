using GServ.Game;

namespace GServ.Game.Tests;

public sealed class CombatPlayerGameplayTests
{
    [Fact]
    public void AppliesConfirmedResourceAndPowerClampsFromPlayerProps()
    {
        var state = new CombatPlayerState(maxPower: 3, hitpoints: 1.5f, alignment: 40, arrows: 0, bombs: 0);

        CombatPlayerGameplay.ApplyMaxPower(state, newMaxPower: 25, heartLimit: 30);
        CombatPlayerGameplay.ApplyCurrentPower(state, encodedHalfHearts: 99);
        CombatPlayerGameplay.ApplyArrows(state, 255);
        CombatPlayerGameplay.ApplyBombs(state, 255);
        CombatPlayerGameplay.ApplyBombPower(state, 255);
        CombatPlayerGameplay.ApplyGlovePower(state, 255);
        CombatPlayerGameplay.ApplyMagicPoints(state, 255);
        CombatPlayerGameplay.ApplyAlignment(state, 255);

        Assert.Equal(20, state.MaxPower);
        Assert.Equal(20, state.Hitpoints);
        Assert.Equal(99, state.Arrows);
        Assert.Equal(99, state.Bombs);
        Assert.Equal(3, state.BombPower);
        Assert.Equal(3, state.GlovePower);
        Assert.Equal(100, state.MagicPoints);
        Assert.Equal(100, state.Alignment);
    }

    [Fact]
    public void CurrentPowerIncreaseIsIgnoredWhenAlignmentIsBelowForty()
    {
        var state = new CombatPlayerState(maxPower: 10, hitpoints: 2.5f, alignment: 39, arrows: 0, bombs: 0);

        CombatPlayerGameplay.ApplyCurrentPower(state, encodedHalfHearts: 10);

        Assert.Equal(2.5f, state.Hitpoints);
    }

    [Theory]
    [InlineData(10, 3)]
    [InlineData(20, 5)]
    [InlineData(39, 5)]
    [InlineData(40, 12)]
    [InlineData(100, 12)]
    public void RevivePowerUsesCppAlignmentThresholds(byte alignment, float expectedPower)
    {
        var state = new CombatPlayerState(maxPower: 12, hitpoints: 0, alignment: alignment, arrows: 0, bombs: 0);

        CombatPlayerGameplay.ApplyStatusTransition(state, oldStatus: PlayerStatus.Dead, newStatus: 0);

        Assert.Equal(expectedPower, state.Hitpoints);
    }

    [Fact]
    public void DeathTransitionIncrementsDeathsOnlyOutsideSparringLevels()
    {
        var normal = new CombatPlayerState(maxPower: 3, hitpoints: 0, alignment: 50, arrows: 0, bombs: 0);
        var spar = new CombatPlayerState(maxPower: 3, hitpoints: 0, alignment: 50, arrows: 0, bombs: 0);

        CombatPlayerGameplay.ApplyStatusTransition(normal, oldStatus: 0, newStatus: PlayerStatus.Dead, isSparringZone: false);
        CombatPlayerGameplay.ApplyStatusTransition(spar, oldStatus: 0, newStatus: PlayerStatus.Dead, isSparringZone: true);

        Assert.Equal(1, normal.Deaths);
        Assert.Equal(0, spar.Deaths);
    }

    [Fact]
    public void ApTickMatchesCppThresholdsAndSkipsPausedOrSparringLevels()
    {
        var state = new CombatPlayerState(maxPower: 3, hitpoints: 3, alignment: 19, arrows: 0, bombs: 0)
        {
            ApCounter = 1
        };
        var timers = new AlignmentTimerSettings(30, 90, 300, 600, 1200);

        var changed = CombatPlayerGameplay.TickAlignment(state, timers);

        Assert.True(changed);
        Assert.Equal(20, state.Alignment);
        Assert.Equal(90, state.ApCounter);

        changed = CombatPlayerGameplay.TickAlignment(state, timers, isPaused: true);

        Assert.False(changed);
        Assert.Equal(90, state.ApCounter);
    }

    [Theory]
    [InlineData(80, 20, 75, 600)]
    [InlineData(10, 20, 9, 30)]
    [InlineData(1, 99, 0, 30)]
    [InlineData(90, 19, 90, 0)]
    public void NonSparKillClaimApLossMatchesCppFormula(
        byte killerAlignment,
        byte loserAlignment,
        byte expectedAlignment,
        ushort expectedCounter)
    {
        var result = CombatPlayerGameplay.ApplyNonSparKillClaimAlignmentPenalty(
            killerAlignment,
            loserAlignment,
            new AlignmentTimerSettings(30, 90, 300, 600, 1200));

        Assert.Equal(expectedAlignment, result.Alignment);
        Assert.Equal(expectedCounter, result.ApCounter);
    }
}
