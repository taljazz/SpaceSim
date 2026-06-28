using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>Tests for <see cref="CueShape.Compute"/> — the pure tune-by-ear cue shaping.</summary>
public class CueShapeTests
{
    [Fact]
    public void AtLock_IsClosestSlowestSteadyAndCentred()
    {
        var c = CueShape.Compute(0f);
        Assert.Equal(1f, c.Closeness, 4);
        Assert.Equal(GameConstants.TremoloMin, c.PulseRate, 4);
        Assert.Equal(1f, c.LockBlend, 4);
        Assert.Equal(0, c.Direction);
    }

    [Fact]
    public void FarOff_ClosenessReachesZero()
    {
        Assert.Equal(0f, CueShape.Compute(GameConstants.CueCoarseRange).Closeness, 4);
        Assert.Equal(0f, CueShape.Compute(GameConstants.CueCoarseRange * 2f).Closeness, 4); // clamped, never negative
    }

    [Fact]
    public void Closeness_IsMonotonicDecreasingInDistance()
    {
        Assert.True(CueShape.Compute(10f).Closeness > CueShape.Compute(50f).Closeness);
        Assert.True(CueShape.Compute(50f).Closeness > CueShape.Compute(150f).Closeness);
    }

    [Fact]
    public void PulseRate_StaysCountableAndInRange()
    {
        foreach (float sd in new[] { 0f, 5f, 20f, 39f, 41f, 100f, 500f })
        {
            float r = CueShape.Compute(sd).PulseRate;
            Assert.InRange(r, GameConstants.TremoloMin, GameConstants.TremoloMax);
            Assert.True(r < 12f, "pulse must stay below the flutter-fusion limit");
        }
        Assert.Equal(GameConstants.TremoloMax, CueShape.Compute(GameConstants.BeatCueRange).PulseRate, 4);
    }

    [Fact]
    public void Direction_IsSignedOutsideDeadbandAndCentredInside()
    {
        Assert.Equal(1, CueShape.Compute(10f).Direction);    // sharp
        Assert.Equal(-1, CueShape.Compute(-10f).Direction);  // flat
        Assert.Equal(0, CueShape.Compute(1f).Direction);     // inside deadband
        Assert.Equal(0, CueShape.Compute(-1f).Direction);
    }

    [Fact]
    public void ClosenessAndPulse_AreSignSymmetric()
    {
        Assert.Equal(CueShape.Compute(7f).Closeness, CueShape.Compute(-7f).Closeness, 4);
        Assert.Equal(CueShape.Compute(7f).PulseRate, CueShape.Compute(-7f).PulseRate, 4);
    }

    [Fact]
    public void Closeness_IsContinuousAcrossTheOldBandEdge()
    {
        // The old cue hard-gated at BeatCueRange (40 Hz); the coarse closeness must not jump there.
        float below = CueShape.Compute(GameConstants.BeatCueRange - 1f).Closeness;
        float above = CueShape.Compute(GameConstants.BeatCueRange + 1f).Closeness;
        Assert.True(MathF.Abs(below - above) < 0.05f);
    }
}
