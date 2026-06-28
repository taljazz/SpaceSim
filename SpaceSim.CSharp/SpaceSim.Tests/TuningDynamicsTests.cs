using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="TuningDynamics"/> — the pure curves behind the tune-by-ear feel: Model B
/// breathing (full when still, fading with speed and with rest) and the self-fining tuning rate
/// (full when far, easing to a crawl at the lock).
/// </summary>
public class TuningDynamicsTests
{
    private const float MaxVel = GameConstants.MaxVelocityBase; // 10

    #region BreathScale

    [Fact]
    public void BreathScale_Suppressed_IsZero()
    {
        // Suppressed (ship-managed / grounded / idle) silences breathing regardless of speed or rest.
        Assert.Equal(0f, TuningDynamics.BreathScale(suppressed: true, speed: 0f, MaxVel, inRegeneration: false, dwellTimer: 0f), 4);
        Assert.Equal(0f, TuningDynamics.BreathScale(suppressed: true, speed: 5f, MaxVel, inRegeneration: true, dwellTimer: 99f), 4);
    }

    [Fact]
    public void BreathScale_StillAndFree_IsFull()
    {
        // Standing still in free flight = full breathing (1.0), the richest listening state.
        Assert.Equal(1f, TuningDynamics.BreathScale(false, speed: 0f, MaxVel, false, 0f), 4);
    }

    [Fact]
    public void BreathScale_AtCruise_FallsToCruiseFloor()
    {
        // At/over the stillness-speed threshold, breathing eases to the faint cruise floor.
        float atThreshold = MaxVel * GameConstants.BreathStillSpeed;
        Assert.Equal(GameConstants.BreathCruiseFloor, TuningDynamics.BreathScale(false, atThreshold, MaxVel, false, 0f), 4);
        // Beyond the threshold it stays clamped at the floor (never negative).
        Assert.Equal(GameConstants.BreathCruiseFloor, TuningDynamics.BreathScale(false, atThreshold * 3f, MaxVel, false, 0f), 4);
    }

    [Fact]
    public void BreathScale_Midspeed_IsBetweenFloorAndFull()
    {
        // Half the stillness speed => halfway up the stillness curve.
        float halfSpeed = MaxVel * GameConstants.BreathStillSpeed * 0.5f;
        float expected = GameConstants.BreathCruiseFloor + (1f - GameConstants.BreathCruiseFloor) * 0.5f;
        Assert.Equal(expected, TuningDynamics.BreathScale(false, halfSpeed, MaxVel, false, 0f), 4);
    }

    [Fact]
    public void BreathScale_SlowerIsAlwaysBreathierThanFaster()
    {
        float slow = TuningDynamics.BreathScale(false, 0.5f, MaxVel, false, 0f);
        float fast = TuningDynamics.BreathScale(false, 2.5f, MaxVel, false, 0f);
        Assert.True(slow > fast);
    }

    [Fact]
    public void BreathScale_InBath_SettlesToStillnessOverTime()
    {
        // At bath entry (dwellTimer == DwellEnterTime) there is no fade yet — equals the still value.
        float entry = TuningDynamics.BreathScale(false, 0f, MaxVel, inRegeneration: true, dwellTimer: GameConstants.DwellEnterTime);
        Assert.Equal(1f, entry, 4);

        // Halfway through the settle window, breathing is halved.
        float half = TuningDynamics.BreathScale(false, 0f, MaxVel, true, GameConstants.DwellEnterTime + GameConstants.BreathDwellSettle * 0.5f);
        Assert.Equal(0.5f, half, 4);

        // Past the settle window, the tones are fully still (clamped at zero, never negative).
        float settled = TuningDynamics.BreathScale(false, 0f, MaxVel, true, GameConstants.DwellEnterTime + GameConstants.BreathDwellSettle * 2f);
        Assert.Equal(0f, settled, 4);
    }

    [Fact]
    public void BreathScale_ZeroMaxVelocity_DoesNotDivideByZero()
    {
        // The 1e-6 denominator guard means maxVelocity=0 yields a sane result, never NaN/Infinity.
        float still = TuningDynamics.BreathScale(false, speed: 0f, maxVelocity: 0f, false, 0f);
        Assert.False(float.IsNaN(still) || float.IsInfinity(still));
        Assert.Equal(1f, still, 4); // speed 0 => fully still => full breathing

        float moving = TuningDynamics.BreathScale(false, speed: 1f, maxVelocity: 0f, false, 0f);
        Assert.False(float.IsNaN(moving) || float.IsInfinity(moving));
        Assert.Equal(GameConstants.BreathCruiseFloor, moving, 4); // any motion => clamped to the floor
    }

    [Fact]
    public void BreathScale_InBathBelowEnterTime_NeverExceedsStillValue()
    {
        // Defensive: even if called with dwellTimer below DwellEnterTime, the clamped fade keeps the
        // result from rising above the still value, so the documented 0..1 contract always holds.
        float s = TuningDynamics.BreathScale(false, speed: 0f, MaxVel, inRegeneration: true, dwellTimer: 0f);
        Assert.True(s <= 1f);
        Assert.Equal(1f, s, 4);
    }

    #endregion

    #region TuningRate

    [Fact]
    public void TuningRate_AtLock_IsFineMinimum()
    {
        Assert.Equal(GameConstants.TuningFineMin, TuningDynamics.TuningRate(0f), 4);
    }

    [Fact]
    public void TuningRate_FarOff_IsFullRate()
    {
        Assert.Equal(GameConstants.TuningRate, TuningDynamics.TuningRate(GameConstants.TuningCoarseDelta), 4);
        // Beyond the coarse threshold it stays clamped at the full rate.
        Assert.Equal(GameConstants.TuningRate, TuningDynamics.TuningRate(GameConstants.TuningCoarseDelta * 5f), 4);
    }

    [Fact]
    public void TuningRate_IsSignAgnostic()
    {
        Assert.Equal(TuningDynamics.TuningRate(20f), TuningDynamics.TuningRate(-20f), 4);
    }

    [Fact]
    public void TuningRate_Midway_IsHalfwayBetweenFineAndFull()
    {
        float expected = GameConstants.TuningFineMin + (GameConstants.TuningRate - GameConstants.TuningFineMin) * 0.5f;
        Assert.Equal(expected, TuningDynamics.TuningRate(GameConstants.TuningCoarseDelta * 0.5f), 4);
    }

    #endregion
}
