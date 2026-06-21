using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="HarmonicMath.TryMatchRatio"/> — recognising musical intervals between two
/// drive frequencies. Only intervals that are well separated from their neighbours are asserted by
/// exact type; intervals that overlap within tolerance (e.g. golden ratio vs minor sixth) are only
/// checked for "a match exists," since which one wins depends on table order.
/// </summary>
public class HarmonicMathTests
{
    #region Exact interval matches (well-separated ratios)

    [Fact]
    public void Octave_IsDetected()
    {
        Assert.True(HarmonicMath.TryMatchRatio(400f, 800f, out var type));
        Assert.Equal(HarmonicType.Octave, type);
    }

    [Fact]
    public void PerfectFifth_IsDetected()
    {
        Assert.True(HarmonicMath.TryMatchRatio(400f, 600f, out var type));
        Assert.Equal(HarmonicType.PerfectFifth, type);
    }

    [Fact]
    public void PerfectFourth_IsDetected()
    {
        // 4:3 ratio -> 400 * 4/3 ≈ 533.33
        Assert.True(HarmonicMath.TryMatchRatio(400f, 533.33f, out var type));
        Assert.Equal(HarmonicType.PerfectFourth, type);
    }

    [Fact]
    public void MajorThird_IsDetected()
    {
        // 5:4 ratio -> 400 * 1.25 = 500
        Assert.True(HarmonicMath.TryMatchRatio(400f, 500f, out var type));
        Assert.Equal(HarmonicType.MajorThird, type);
    }

    #endregion

    #region General behaviour

    [Fact]
    public void GoldenRatio_ProducesAMatch()
    {
        // φ overlaps the minor sixth within tolerance, so only assert that *some* interval matches.
        Assert.True(HarmonicMath.TryMatchRatio(400f, 400f * GameConstants.PHI, out _));
    }

    [Fact]
    public void ArgumentOrder_DoesNotMatter()
    {
        bool a = HarmonicMath.TryMatchRatio(400f, 800f, out var typeA);
        bool b = HarmonicMath.TryMatchRatio(800f, 400f, out var typeB);
        Assert.True(a);
        Assert.True(b);
        Assert.Equal(typeA, typeB);
    }

    [Fact]
    public void UnrelatedRatio_IsNotAHarmonic()
    {
        // 400:440 = 1.1, which sits in the gap between unison and the minor third (1.2).
        Assert.False(HarmonicMath.TryMatchRatio(400f, 440f, out _));
    }

    [Fact]
    public void SubAudibleFrequency_IsNeverAHarmonic()
    {
        Assert.False(HarmonicMath.TryMatchRatio(0.5f, 800f, out _));
        Assert.False(HarmonicMath.TryMatchRatio(400f, 0f, out _));
    }

    #endregion
}
