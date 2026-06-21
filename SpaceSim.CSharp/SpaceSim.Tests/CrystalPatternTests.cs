using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for the polymorphic <see cref="CrystalPattern"/> layouts. Each layout is asserted against
/// the original inline crystal-positioning formula it replaced, so the refactor is provably
/// behaviour-preserving.
/// </summary>
public class CrystalPatternTests
{
    private const float SF = 100f; // arbitrary scale factor for the assertions

    #region Layout math (vs legacy formulas)

    [Fact]
    public void SeedOfLife_CentreThenHexagon()
    {
        var p = CrystalPatterns.SeedOfLife;
        Assert.Equal((0f, 0f), p.PositionFor(0, 7, SF));

        for (int i = 1; i < 7; i++)
        {
            float angle = (i - 1) * (MathF.Tau / 6f);
            float r = SF / 10f;
            var (x, y) = p.PositionFor(i, 7, SF);
            Assert.Equal(r * MathF.Cos(angle), x, precision: 4);
            Assert.Equal(r * MathF.Sin(angle), y, precision: 4);
        }
    }

    [Fact]
    public void Merkaba_InnerAndOuterTetrahedra()
    {
        var p = CrystalPatterns.Merkaba;
        for (int i = 0; i < 8; i++)
        {
            float angle, r;
            if (i < 4) { angle = i * (MathF.Tau / 4f) + MathF.PI / 4f; r = SF / 10f; }
            else { angle = (i - 4) * (MathF.Tau / 4f); r = SF / 10f * GameConstants.PHI; }

            var (x, y) = p.PositionFor(i, 8, SF);
            Assert.Equal(r * MathF.Cos(angle), x, precision: 4);
            Assert.Equal(r * MathF.Sin(angle), y, precision: 4);
        }
    }

    [Fact]
    public void Spiral_MatchesLegacyFibonacciFormula()
    {
        var p = CrystalPatterns.Spiral;
        for (int i = 0; i < 6; i++)
        {
            float theta = i * MathF.Tau * GameConstants.PHI;
            int fibIdx = i % GameConstants.FibSeq.Length;
            float r = GameConstants.FibSeq[fibIdx] * (SF / 10f);

            var (x, y) = p.PositionFor(i, 5, SF);
            Assert.Equal(r * MathF.Cos(theta), x, precision: 3);
            Assert.Equal(r * MathF.Sin(theta), y, precision: 3);
        }
    }

    #endregion

    #region Selection rules

    [Theory]
    [InlineData(SacredGeometryPattern.SeedOfLife, 7, SacredGeometryPattern.SeedOfLife)]
    [InlineData(SacredGeometryPattern.Merkaba, 8, SacredGeometryPattern.Merkaba)]
    public void Select_ReturnsSpecialLayout_AtMatchingCount(SacredGeometryPattern pattern, int count, SacredGeometryPattern expectedKind)
    {
        Assert.Equal(expectedKind, CrystalPatterns.Select(pattern, count).Kind);
    }

    [Theory]
    [InlineData(SacredGeometryPattern.SeedOfLife, 5)]   // count mismatch -> spiral fallback
    [InlineData(SacredGeometryPattern.GoldenSpiral, 5)] // golden spiral always uses the spiral layout
    [InlineData(SacredGeometryPattern.FlowerOfLife, 19)]
    public void Select_FallsBackToSpiral(SacredGeometryPattern pattern, int count)
    {
        Assert.Same(CrystalPatterns.Spiral, CrystalPatterns.Select(pattern, count));
    }

    [Fact]
    public void Select_NullPattern_IsSpiral()
    {
        Assert.Same(CrystalPatterns.Spiral, CrystalPatterns.Select(null, 3));
    }

    #endregion
}
