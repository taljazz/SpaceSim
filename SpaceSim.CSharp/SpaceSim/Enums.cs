namespace SpaceSim;

/// <summary>
/// Type-safe enums replacing string-based type identifiers throughout the codebase.
/// Eliminates ~100+ string comparisons and dictionary lookups for better performance
/// and compile-time safety.
/// </summary>

#region Celestial bodies

/// <summary>The three kinds of object that make up the procedural universe.</summary>
public enum CelestialBodyType
{
    Star,
    Planet,
    Nebula
}

/// <summary>Stellar evolution class — drives a star's color, frequency, and ambient sound.</summary>
public enum StellarType
{
    MainSequence,
    RedGiant,
    WhiteDwarf,
    BrownDwarf
}

/// <summary>Nebula classification — drives color, frequency band, and dissonance.</summary>
public enum NebulaType
{
    Emission,
    Reflection,
    Planetary,
    SupernovaRemnant
}

/// <summary>Exoplanet classification — drives size, crystal yield, and landing difficulty.</summary>
public enum ExoplanetType
{
    HotJupiter,
    SuperEarth,
    OceanWorld,
    RoguePlanet,
    IceGiant
}

#endregion

#region Atlantean systems & navigation

/// <summary>The six faces of the Tuaoi Crystal — press G to cycle; each grants a different tactical bonus.</summary>
public enum TuaoiMode
{
    Healing,
    Navigation,
    Communication,
    Power,
    Regeneration,
    Transcendence
}

/// <summary>Whether a temple is one of the 12 zodiac (minor) temples or the central master temple.</summary>
public enum TempleType
{
    Minor,
    Master
}

/// <summary>What a rift does when the player charges into it (a plain warp, a reward, a hazard, …).</summary>
public enum RiftType
{
    Normal,
    Boost,
    Crystal,
    Hazard,
    PerfectFifth
}

/// <summary>The player's awareness tier — rises with sustained high resonance and scales all stats.</summary>
public enum ConsciousnessLevel
{
    Dormant,
    Awakening,
    Aware,
    Attuned,
    Enlightened,
    Ascended
}

/// <summary>Consciousness state inferred from drive frequency, from deep delta to transcendent gamma.</summary>
public enum BrainwaveState
{
    Delta,
    Theta,
    Alpha,
    Beta,
    Gamma
}

/// <summary>Whether a planet's surface is welcoming (harmonic) or hostile (dissonant).</summary>
public enum PlanetBiome
{
    Harmonic,
    Dissonant
}

/// <summary>The musical interval detected between two dimensions' frequencies, each with its own bonus.</summary>
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

#endregion

#region Patterns, UI & reserved

/// <summary>Sacred-geometry crystal layouts that can appear on a landed planet.</summary>
public enum SacredGeometryPattern
{
    VesicaPiscis,
    SeedOfLife,
    FlowerOfLife,
    MetatronsCube,
    Merkaba,
    GoldenSpiral
}

/// <summary>Kind of object shown as a row in the starmap scanner menu.</summary>
public enum StarmapItemKind
{
    Star,
    Planet,
    Nebula,
    Rift,
    Temple,
    Pyramid
}

/// <summary>The gameplay effect granted while a given brainwave state is active.</summary>
public enum BrainwaveEffect
{
    AutoRepair,
    RiftVision,
    EnhancedScan,
    FastTuning,
    AllBonus
}

/// <summary>The special effect applied when a rare Atlantean crystal is collected.</summary>
public enum CrystalEffect
{
    VelocityBurst,
    ShieldBoost,
    Communication,
    Transformation,
    MemoryUnlock,
    Purification,
    AngelicConnection
}

/// <summary>The healing/awakening influence associated with each Solfeggio frequency.</summary>
public enum SolfeggioEffect
{
    PainRelief,
    TissueHealing,
    ReleaseFear,
    FacilitateChange,
    UniversalTuning,
    Transformation,
    Relationships,
    Expression,
    SpiritualOrder,
    Oneness
}

/// <summary>Sacred cymatics (sound-into-form) patterns, keyed by frequency band. Reserved for the planned cymatics visualizer.</summary>
public enum CymaticsPattern
{
    Hexagon,
    Star,
    Flower,
    Mandala,
    Spiral,
    Merkaba
}

#endregion

#region Top-level game screens

/// <summary>Which top-level screen the game is showing: the main menu, the live sim, the sound dictionary, or help.</summary>
public enum GameScreen
{
    MainMenu,
    Playing,
    LearnSounds,
    Help
}

/// <summary>A screen change requested by a menu, applied by the game host (SpaceSimGame).</summary>
public enum ScreenTransition
{
    None,
    StartSim,
    StartTutorial,
    OpenLearnSounds,
    OpenHelp,
    CloseHelp,
    BackToMainMenu,
    Quit
}

#endregion
