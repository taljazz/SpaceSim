using System;

namespace SpaceSim;

/// <summary>
/// Pure, allocation-free physics for the resonance drive — the heart of how the Light Vehicle
/// moves through 5D space.
///
/// <para>
/// The core idea: a dimension's <em>resonance</em> measures how perfectly the drive frequency
/// matches its target. A perfect match (delta = 0) gives resonance 1.0; the larger the gap, the
/// closer resonance falls toward 0. The <c>width</c> parameter controls how forgiving the tuning
/// is — a wider band makes it easier to stay "in tune."
/// </para>
///
/// <para>
/// This formula used to be copy-pasted into six different methods (and its inverse into two more).
/// It now lives here exactly once, so it can be unit-tested and reads identically whether you meet
/// it in the audio thread or the game-update loop. The operation order matches the original inline
/// code, so results are bit-for-bit the same and existing behaviour is preserved.
/// </para>
/// </summary>
public static class ResonancePhysics
{
    #region Resonance (frequency match -> 0..1)

    /// <summary>
    /// Resonance for a single dimension: <c>1 / (1 + (delta/width)^2)</c>.
    /// A Lorentzian curve — smooth, peaking at 1.0 when <paramref name="delta"/> is 0, and
    /// symmetric (the sign of <paramref name="delta"/> does not matter).
    /// </summary>
    /// <param name="delta">Difference between drive and target frequency, in Hz. Sign-agnostic.</param>
    /// <param name="width">Tuning tolerance in Hz (must be &gt; 0). Wider = easier to resonate.</param>
    /// <returns>Resonance level in the range (0, 1].</returns>
    public static float Resonance(float delta, float width)
    {
        // r is computed once and squared, which is identical in floating point to the original
        // "(delta / width) * (delta / width)" — a single deterministic divide, then a self-multiply.
        float r = delta / width;
        return 1f / (1f + r * r);
    }

    #endregion

    #region Inverse (desired resonance -> drive frequency)

    /// <summary>
    /// The inverse of <see cref="Resonance"/>: given the resonance we <em>want</em> in a dimension,
    /// return the drive frequency that produces it. Used by manual navigation and the autopilot to
    /// turn "how fast do I want to travel" into "what frequency should I tune to."
    /// </summary>
    /// <param name="fTarget">The dimension's target frequency, in Hz.</param>
    /// <param name="targetRes">Desired resonance (0..1). Values &lt;= 0 leave the drive at <paramref name="fTarget"/>.</param>
    /// <param name="width">Tuning tolerance in Hz.</param>
    /// <param name="sign">Which side of the target to tune toward (+1 / -1) — i.e. the travel direction.</param>
    /// <returns>The drive frequency to set for this dimension.</returns>
    public static float DriveForTargetResonance(float fTarget, float targetRes, float width, float sign)
    {
        // No motion requested (or impossible resonance) => sit exactly on the target frequency.
        if (targetRes <= 0f)
            return fTarget;

        // Solve res = 1 / (1 + (d/w)^2)  for d:   d = w * sqrt(1/res - 1)
        float dOverW = MathF.Sqrt(1f / targetRes - 1f);
        return fTarget + sign * width * dOverW;
    }

    #endregion
}
