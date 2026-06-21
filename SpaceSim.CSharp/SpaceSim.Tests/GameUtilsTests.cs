using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="GameUtils"/> string/speech helpers. (The projection and colour helpers are
/// exercised through the renderer at runtime; here we cover the pure, screen-reader-facing logic.)
/// </summary>
public class GameUtilsTests
{
    #region SpacePascalCase

    [Theory]
    [InlineData("PerfectFifth", "Perfect Fifth")]
    [InlineData("MajorThird", "Major Third")]
    [InlineData("MinorSixth", "Minor Sixth")]
    [InlineData("Octave", "Octave")]   // single word unchanged
    [InlineData("Golden", "Golden")]
    [InlineData("Tritone", "Tritone")]
    public void SpacePascalCase_SeparatesWordsForSpeech(string input, string expected)
    {
        Assert.Equal(expected, GameUtils.SpacePascalCase(input));
    }

    [Fact]
    public void SpacePascalCase_EmptyOrNull_ReturnedAsIs()
    {
        Assert.Equal("", GameUtils.SpacePascalCase(""));
        Assert.Null(GameUtils.SpacePascalCase(null!));
    }

    // Guards the de-stringify refactor: spoken sacred-pattern names must still match what the old
    // snake_case FormatName produced (e.g. "seed_of_life" -> "Seed Of Life").
    [Theory]
    [InlineData(SacredGeometryPattern.VesicaPiscis, "Vesica Piscis")]
    [InlineData(SacredGeometryPattern.SeedOfLife, "Seed Of Life")]
    [InlineData(SacredGeometryPattern.FlowerOfLife, "Flower Of Life")]
    [InlineData(SacredGeometryPattern.MetatronsCube, "Metatrons Cube")]
    [InlineData(SacredGeometryPattern.Merkaba, "Merkaba")]
    [InlineData(SacredGeometryPattern.GoldenSpiral, "Golden Spiral")]
    public void SpacePascalCase_SacredPatternNames_MatchLegacyDisplay(SacredGeometryPattern pattern, string expected)
    {
        Assert.Equal(expected, GameUtils.SpacePascalCase(pattern.ToString()));
    }

    #endregion
}
