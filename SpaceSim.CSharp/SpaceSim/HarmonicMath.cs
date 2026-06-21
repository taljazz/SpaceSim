using System;

namespace SpaceSim;

/// <summary>
/// Pure helper for recognising the musical relationship between two drive frequencies.
///
/// <para>
/// Two callers need this answer: the audio thread (to add intermodulation tones when dimensions
/// align) and the <see cref="Ship"/> (to grant harmonic gameplay bonuses). They used to each carry
/// their own near-identical copy of the ratio test; centralising it here means the two can never
/// quietly drift apart.
/// </para>
/// </summary>
public static class HarmonicMath
{
    #region Ratio matching

    /// <summary>
    /// Try to match the ratio between two frequencies to a known harmonic interval
    /// (octave, perfect fifth, golden ratio, …) within <see cref="GameConstants.HarmonicTolerance"/>.
    /// The first matching interval wins, so the order of <see cref="GameConstants.HarmonicRatios"/>
    /// decides ties between intervals that sit close together.
    /// </summary>
    /// <param name="freqA">First frequency, in Hz.</param>
    /// <param name="freqB">Second frequency, in Hz.</param>
    /// <param name="type">The matched interval, when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> if the pair forms a recognised harmonic interval.</returns>
    public static bool TryMatchRatio(float freqA, float freqB, out HarmonicType type)
    {
        // Frequencies below 1 Hz are silent / uninitialised — never treat them as a harmonic.
        if (freqA < 1f || freqB < 1f)
        {
            type = default;
            return false;
        }

        // Compare larger over smaller so the ratio is always >= 1, regardless of argument order.
        float ratio = MathF.Max(freqA, freqB) / MathF.Min(freqA, freqB);

        foreach (var (hType, targetRatio) in GameConstants.HarmonicRatios)
        {
            // |ratio - target| < target * tolerance is the same test as the older
            // |ratio - target| / target < tolerance, just without the extra divide.
            if (MathF.Abs(ratio - targetRatio) < targetRatio * GameConstants.HarmonicTolerance)
            {
                type = hType;
                return true;
            }
        }

        type = default;
        return false;
    }

    #endregion
}
