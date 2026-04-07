using Microsoft.Xna.Framework;

namespace SpaceSim.Models;

public record StellarTypeInfo(Color Color, float FreqMult, string Desc, float AudioRangeMin, float AudioRangeMax);
public record NebulaTypeInfo(Color Color, float FreqRangeMin, float FreqRangeMax, float Dissonance, string Desc);
public record ExoplanetTypeInfo(float SizeMult, float CrystalMult, float Difficulty, string Desc);
public record SolfeggioInfo(string Name, string Effect, string Desc, string Bonus, float Mult);
public record CrystalSpectrumInfo(float FreqMin, float FreqMax, Color Color, string Chakra, string Bonus, float Mult);
public record TuaoiModeInfo(float FreqBase, Color Color, string Effect, float Rate, string Desc);
public record SacredPatternInfo(int Points, string Bonus, float Mult);
public record BrainwaveStateInfo(float FreqMin, float FreqMax, string State, string Effect, float Mult);
public record ConsciousnessLevelInfo(float Threshold, float Mult, string Desc);
public record AtlanteanCrystalTypeInfo(Color Color, float FreqMin, float FreqMax, string Effect, float Mult, string Desc);
public record CymaticsPatternInfo(float FreqMin, float FreqMax, float Complexity);
