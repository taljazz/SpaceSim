namespace SpaceSim;

/// <summary>
/// Type-safe enums replacing string-based type identifiers throughout the codebase.
/// Eliminates ~100+ string comparisons and dictionary lookups for better performance
/// and compile-time safety.
/// </summary>

public enum CelestialBodyType
{
    Star,
    Planet,
    Nebula
}

public enum StellarType
{
    MainSequence,
    RedGiant,
    WhiteDwarf,
    BrownDwarf
}

public enum NebulaType
{
    Emission,
    Reflection,
    Planetary,
    SupernovaRemnant
}

public enum ExoplanetType
{
    HotJupiter,
    SuperEarth,
    OceanWorld,
    RoguePlanet,
    IceGiant
}

public enum TuaoiMode
{
    Healing,
    Navigation,
    Communication,
    Power,
    Regeneration,
    Transcendence
}

public enum TempleType
{
    Minor,
    Master
}

public enum RiftType
{
    Normal,
    Boost,
    Crystal,
    Hazard,
    PerfectFifth
}

public enum ConsciousnessLevel
{
    Dormant,
    Awakening,
    Aware,
    Attuned,
    Enlightened,
    Ascended
}

public enum BrainwaveState
{
    Delta,
    Theta,
    Alpha,
    Beta,
    Gamma
}

public enum PlanetBiome
{
    Harmonic,
    Dissonant
}

public enum HarmonicType
{
    Octave,
    PerfectFifth,
    PerfectFourth,
    MajorThird,
    MinorThird,
    MajorSixth,
    MinorSixth,
    Tritone,
    Golden
}
