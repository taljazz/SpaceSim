using System;

namespace SpaceSim;

/// <summary>
/// Polymorphic crystal layouts for a landed planet. Each sacred-geometry pattern arranges the
/// planet's crystals differently; this logic used to be a chain of <c>if/else</c> branches inside
/// <c>GenerateCrystals</c>. Splitting it into a small class hierarchy keeps each layout
/// self-contained, individually unit-testable, and easy to extend with new patterns.
/// </summary>
public abstract class CrystalPattern
{
    #region Layout contract

    /// <summary>The sacred-geometry pattern this layout draws (or <c>null</c> for the generic fallback).</summary>
    public abstract SacredGeometryPattern? Kind { get; }

    /// <summary>
    /// The 2D position (px, py) for the crystal at <paramref name="index"/> out of
    /// <paramref name="count"/> total, scaled by <paramref name="scaleFactor"/>.
    /// </summary>
    public abstract (float X, float Y) PositionFor(int index, int count, float scaleFactor);

    #endregion
}

/// <summary>Seed of Life — one crystal at the centre ringed by six on a hexagon (used at 7 crystals).</summary>
public sealed class SeedOfLifePattern : CrystalPattern
{
    public override SacredGeometryPattern? Kind => SacredGeometryPattern.SeedOfLife;

    public override (float X, float Y) PositionFor(int index, int count, float scaleFactor)
    {
        if (index == 0)
            return (0f, 0f); // the heart of the flower

        float angle = (index - 1) * (MathF.Tau / 6f);
        float r = scaleFactor / 10f;
        return (r * MathF.Cos(angle), r * MathF.Sin(angle));
    }
}

/// <summary>Merkaba — interlocked tetrahedra: an inner square and an outer, PHI-scaled one (used at 8 crystals).</summary>
public sealed class MerkabaPattern : CrystalPattern
{
    public override SacredGeometryPattern? Kind => SacredGeometryPattern.Merkaba;

    public override (float X, float Y) PositionFor(int index, int count, float scaleFactor)
    {
        if (index < 4)
        {
            // Inner tetrahedron, rotated 45 degrees.
            float angle = index * (MathF.Tau / 4f) + MathF.PI / 4f;
            float r = scaleFactor / 10f;
            return (r * MathF.Cos(angle), r * MathF.Sin(angle));
        }

        // Outer tetrahedron, expanded by the golden ratio.
        float outerAngle = (index - 4) * (MathF.Tau / 4f);
        float outerR = scaleFactor / 10f * GameConstants.PHI;
        return (outerR * MathF.Cos(outerAngle), outerR * MathF.Sin(outerAngle));
    }
}

/// <summary>
/// Golden spiral — the Fibonacci/PHI layout. Used both for the explicit Golden Spiral pattern and
/// as the fallback whenever no special pattern applies (identical to the original "else" branch).
/// </summary>
public sealed class SpiralPattern : CrystalPattern
{
    public override SacredGeometryPattern? Kind => null; // generic fallback layout

    public override (float X, float Y) PositionFor(int index, int count, float scaleFactor)
    {
        float theta = index * MathF.Tau * GameConstants.PHI;
        int fibIdx = index % GameConstants.FibSeq.Length;
        float r = GameConstants.FibSeq[fibIdx] * (scaleFactor / 10f);
        return (r * MathF.Cos(theta), r * MathF.Sin(theta));
    }
}

/// <summary>
/// Chooses the <see cref="CrystalPattern"/> for a detected sacred pattern and crystal count. The
/// special layouts only apply at their exact crystal counts; everything else uses the golden
/// <see cref="Spiral"/> — exactly mirroring the original selection rules.
/// </summary>
public static class CrystalPatterns
{
    #region Shared instances (patterns are stateless, so a single instance is reused)

    public static readonly CrystalPattern SeedOfLife = new SeedOfLifePattern();
    public static readonly CrystalPattern Merkaba = new MerkabaPattern();
    public static readonly CrystalPattern Spiral = new SpiralPattern();

    #endregion

    #region Selection

    /// <summary>
    /// Pick the layout for the detected <paramref name="pattern"/> at the given crystal
    /// <paramref name="count"/>, falling back to the golden <see cref="Spiral"/>.
    /// </summary>
    public static CrystalPattern Select(SacredGeometryPattern? pattern, int count) => pattern switch
    {
        SacredGeometryPattern.SeedOfLife when count == 7 => SeedOfLife,
        SacredGeometryPattern.Merkaba when count == 8 => Merkaba,
        _ => Spiral,
    };

    #endregion
}
