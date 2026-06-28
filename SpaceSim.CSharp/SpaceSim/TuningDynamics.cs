using System;

namespace SpaceSim;

/// <summary>
/// Pure, allocation-free curves for the tune-by-ear feel — kept here (like <see cref="ResonancePhysics"/>)
/// so they can be unit-tested and read identically wherever they are used.
/// </summary>
public static class TuningDynamics
{
    #region Breathing scale (Model B)

    /// <summary>
    /// How strongly the higher realms' target tones breathe, 0..1. Full when still and free, fading as you
    /// speed up and as you settle into the regeneration bath, and zero when breathing is suppressed (the
    /// ship is flying itself, you are landed, projecting astrally, or idle).
    /// </summary>
    /// <param name="suppressed">True when breathing should be silenced entirely (ship-managed / grounded / idle).</param>
    /// <param name="speed">Current ship speed (units/sec).</param>
    /// <param name="maxVelocity">The ship's current max velocity, for the stillness ratio.</param>
    /// <param name="inRegeneration">True while resting in the dwelling bath.</param>
    /// <param name="dwellTimer">Seconds of sustained eligible dwelling (drives the in-bath settle).</param>
    public static float BreathScale(bool suppressed, float speed, float maxVelocity, bool inRegeneration, float dwellTimer)
    {
        if (suppressed) return 0f;

        // Stillness: 0 at cruise, rising to 1 as you slow past BreathStillSpeed of max velocity.
        float denom = MathF.Max(1e-6f, maxVelocity * GameConstants.BreathStillSpeed);
        float stillness = 1f - Math.Clamp(speed / denom, 0f, 1f);

        // A faint floor remains even at full cruise, so the universe never goes fully dead in transit.
        float scale = GameConstants.BreathCruiseFloor + (1f - GameConstants.BreathCruiseFloor) * stillness;

        // Once the bath has formed, the tones settle with you — breathing eases to stillness over time.
        // The fade is clamped to [0,1] so the result never exceeds the still value, even if this is ever
        // called with dwellTimer below DwellEnterTime (keeps the documented 0..1 contract intact).
        if (inRegeneration)
        {
            float fade = 1f - (dwellTimer - GameConstants.DwellEnterTime) / GameConstants.BreathDwellSettle;
            scale *= Math.Clamp(fade, 0f, 1f);
        }

        return scale;
    }

    #endregion

    #region Self-fining tuning rate

    /// <summary>
    /// The by-ear tuning rate (Hz/sec) for a given detuning: full <see cref="GameConstants.TuningRate"/>
    /// when far, easing to <see cref="GameConstants.TuningFineMin"/> at the lock so you settle gently onto
    /// the still center.
    /// </summary>
    /// <param name="delta">Detuning between the drive and its target, in Hz (sign-agnostic).</param>
    public static float TuningRate(float delta)
    {
        float frac = Math.Clamp(MathF.Abs(delta) / GameConstants.TuningCoarseDelta, 0f, 1f);
        return GameConstants.TuningFineMin + (GameConstants.TuningRate - GameConstants.TuningFineMin) * frac;
    }

    #endregion
}
