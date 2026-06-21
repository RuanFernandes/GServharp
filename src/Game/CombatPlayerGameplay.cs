namespace Preagonal.GServer.Game;

[Flags]
public enum PlayerStatus : byte
{
    Paused = 0x01,
    Hidden = 0x02,
    Male = 0x04,
    Dead = 0x08,
    AllowWeapons = 0x10,
    HideSword = 0x20,
    HasSpin = 0x40
}

public sealed class CombatPlayerState
{
    public CombatPlayerState(byte maxPower, float hitpoints, byte alignment, byte arrows, byte bombs)
    {
        MaxPower = maxPower;
        Hitpoints = hitpoints;
        Alignment = alignment;
        Arrows = arrows;
        Bombs = bombs;
    }

    public byte MaxPower { get; set; }
    public float Hitpoints { get; set; }
    public byte Alignment { get; set; }
    public byte Arrows { get; set; }
    public byte Bombs { get; set; }
    public byte BombPower { get; set; }
    public byte GlovePower { get; set; }
    public byte MagicPoints { get; set; }
    public ushort ApCounter { get; set; }
    public int Deaths { get; set; }
}

public sealed record AlignmentTimerSettings(
    ushort ApTime0,
    ushort ApTime1,
    ushort ApTime2,
    ushort ApTime3,
    ushort ApTime4);

public sealed record NonSparKillClaimAlignmentPenalty(byte Alignment, ushort ApCounter);

public static class CombatPlayerGameplay
{
    public static void ApplyMaxPower(CombatPlayerState state, byte newMaxPower, int heartLimit)
    {
        state.MaxPower = (byte)Math.Clamp(newMaxPower, 0, Math.Min(heartLimit, 20));
        SetPower(state, state.MaxPower);
    }

    public static void ApplyCurrentPower(CombatPlayerState state, byte encodedHalfHearts)
    {
        var power = encodedHalfHearts / 2.0f;
        if (state.Alignment < 40 && power > state.Hitpoints)
            return;

        SetPower(state, power);
    }

    public static void ApplyArrows(CombatPlayerState state, byte arrows) =>
        state.Arrows = (byte)Math.Clamp((int)arrows, 0, 99);

    public static void ApplyBombs(CombatPlayerState state, byte bombs) =>
        state.Bombs = (byte)Math.Clamp((int)bombs, 0, 99);

    public static void ApplyBombPower(CombatPlayerState state, byte bombPower) =>
        state.BombPower = (byte)Math.Clamp((int)bombPower, 0, 3);

    public static void ApplyGlovePower(CombatPlayerState state, byte glovePower) =>
        state.GlovePower = (byte)Math.Clamp((int)glovePower, 0, 3);

    public static void ApplyMagicPoints(CombatPlayerState state, byte magicPoints) =>
        state.MagicPoints = (byte)Math.Clamp((int)magicPoints, 0, 100);

    public static void ApplyAlignment(CombatPlayerState state, byte alignment) =>
        state.Alignment = (byte)Math.Clamp((int)alignment, 0, 100);

    public static void ApplyStatusTransition(
        CombatPlayerState state,
        PlayerStatus oldStatus,
        PlayerStatus newStatus,
        bool isSparringZone = false)
    {
        var wasDead = (oldStatus & PlayerStatus.Dead) != 0;
        var isDead = (newStatus & PlayerStatus.Dead) != 0;

        if (wasDead && !isDead)
        {
            var revivedPower = state.Alignment < 20 ? 3.0f : state.Alignment < 40 ? 5.0f : state.MaxPower;
            SetPower(state, Math.Clamp(revivedPower, 0.5f, state.MaxPower));
        }

        if (!wasDead && isDead && !isSparringZone)
            state.Deaths++;
    }

    public static bool TickAlignment(
        CombatPlayerState state,
        AlignmentTimerSettings timers,
        bool isPaused = false,
        bool isSparringZone = false)
    {
        if (isPaused || isSparringZone)
            return false;

        if (state.ApCounter > 0)
            state.ApCounter--;

        if (state.ApCounter > 0)
            return false;

        var changed = false;
        if (state.Alignment < 100)
        {
            state.Alignment++;
            changed = true;
        }

        state.ApCounter = state.Alignment < 20
            ? timers.ApTime0
            : state.Alignment < 40
                ? timers.ApTime1
                : state.Alignment < 60
                    ? timers.ApTime2
                    : state.Alignment < 80
                        ? timers.ApTime3
                        : timers.ApTime4;

        return changed;
    }

    public static NonSparKillClaimAlignmentPenalty ApplyNonSparKillClaimAlignmentPenalty(
        byte killerAlignment,
        byte loserAlignment,
        AlignmentTimerSettings timers)
    {
        if (killerAlignment == 0 || loserAlignment < 20)
            return new NonSparKillClaimAlignmentPenalty(killerAlignment, 0);

        var newAlignment = killerAlignment - (((killerAlignment / 20) + 1) * (loserAlignment / 20));
        if (newAlignment < 0)
            newAlignment = 0;

        var alignment = (byte)newAlignment;
        return new NonSparKillClaimAlignmentPenalty(alignment, SelectApTimer(alignment, timers));
    }

    private static void SetPower(CombatPlayerState state, float power) =>
        state.Hitpoints = Math.Clamp(power, 0.0f, state.MaxPower);

    private static ushort SelectApTimer(byte alignment, AlignmentTimerSettings timers) =>
        alignment < 20
            ? timers.ApTime0
            : alignment < 40
                ? timers.ApTime1
                : alignment < 60
                    ? timers.ApTime2
                    : alignment < 80
                        ? timers.ApTime3
                        : timers.ApTime4;
}
