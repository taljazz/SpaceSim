using Microsoft.Xna.Framework;

namespace SpaceSim.Models;

// Immutable lookup records describing the fixed properties of each game "type". The enums in
// Enums.cs name the variants; these records hold the data (colors, frequencies, multipliers,
// descriptions) that GameConstants maps each enum value to.

/// <summary>Visual color, frequency multiplier, blurb, and ambient-audio range for a stellar class.</summary>
public record StellarTypeInfo(Color Color, float FreqMult, string Desc, float AudioRangeMin, float AudioRangeMax);

/// <summary>Color, frequency band, dissonance level, and blurb for a nebula class.</summary>
public record NebulaTypeInfo(Color Color, float FreqRangeMin, float FreqRangeMax, float Dissonance, string Desc);

/// <summary>Size, crystal-yield, and difficulty multipliers (plus blurb) for an exoplanet class.</summary>
public record ExoplanetTypeInfo(float SizeMult, float CrystalMult, float Difficulty, string Desc);

/// <summary>Name, effect, description, and bonus details for one sacred Solfeggio frequency.</summary>
public record SolfeggioInfo(string Name, SolfeggioEffect Effect, string Desc, string Bonus, float Mult);

/// <summary>Frequency band, color, chakra, and bonus for one crystal-spectrum tier.</summary>
public record CrystalSpectrumInfo(float FreqMin, float FreqMax, Color Color, string Chakra, string Bonus, float Mult);

/// <summary>Base frequency, color, effect, and rate for one Tuaoi Crystal mode.</summary>
public record TuaoiModeInfo(float FreqBase, Color Color, string Effect, float Rate, string Desc);

/// <summary>Number of crystal points, bonus description, and multiplier for one sacred-geometry pattern.</summary>
public record SacredPatternInfo(int Points, string Bonus, float Mult);

/// <summary>Frequency band, name, effect, and multiplier for one brainwave state.</summary>
public record BrainwaveStateInfo(float FreqMin, float FreqMax, string State, BrainwaveEffect Effect, float Mult);

/// <summary>Resonance threshold, stat multiplier, and blurb for one consciousness level.</summary>
public record ConsciousnessLevelInfo(float Threshold, float Mult, string Desc);

/// <summary>Color, frequency band, effect, and multiplier for one rare Atlantean crystal type.</summary>
public record AtlanteanCrystalTypeInfo(Color Color, float FreqMin, float FreqMax, CrystalEffect Effect, float Mult, string Desc);

/// <summary>Frequency band and pattern complexity for one cymatics figure (reserved for the planned visualizer).</summary>
public record CymaticsPatternInfo(float FreqMin, float FreqMax, float Complexity);
