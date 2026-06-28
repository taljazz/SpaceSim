using System;

namespace SpaceSim;

/// <summary>
/// Pure shaping for the resonance-driven vibrato — kept here (like <see cref="ResonancePhysics"/> and
/// <see cref="TuningDynamics"/>) so it can be unit-tested. Higher resonance gives a deeper, slightly
/// faster warble; the synthesis feeds the result into phase accumulators so it stays click-free.
/// </summary>
public static class VibratoShape
{
    /// <summary>Vibrato depth (phase-offset amplitude, radians) at zero and full resonance.</summary>
    public const float DepthMin = 0.25f, DepthMax = 1.1f;

    /// <summary>Vibrato LFO rate (Hz) at zero and full resonance.</summary>
    public const float RateMin = 3.4f, RateMax = 4.3f;

    /// <summary>
    /// Map a resonance level (0..1) to the vibrato (depth, rate). Linear lerp between the min/max
    /// endpoints, clamped — identical to the original inline mapping, now isolated and testable.
    /// </summary>
    public static (float Depth, float Rate) DepthRate(float resLevel)
    {
        float r = Math.Clamp(resLevel, 0f, 1f);
        return (DepthMin + (DepthMax - DepthMin) * r, RateMin + (RateMax - RateMin) * r);
    }
}
