namespace SpaceSim.Models;

/// <summary>
/// A star, planet, or nebula floating in the 5D universe. One flexible class covers all three —
/// only the fields relevant to <see cref="BodyType"/> are meaningful (the rest stay at their
/// defaults), which keeps the procedurally-generated universe in a single list.
/// </summary>
public class CelestialBody : WorldObject
{
    /// <summary>Which of the three kinds this body is (star / planet / nebula).</summary>
    public CelestialBodyType BodyType;

    /// <summary>The body's resonant frequency, in Hz — what the player tunes a drive toward to interact with it.</summary>
    public float Frequency;

    // Star-specific
    /// <summary>Evolution class for stars (main sequence, red giant, …); null for non-stars.</summary>
    public StellarType? StellarClass;

    // Nebula-specific
    /// <summary>Classification for nebulae (emission, reflection, …); null for non-nebulae.</summary>
    public NebulaType? NebulaClass;

    /// <summary>How chaotic this nebula's dissonance field is (0..1) — higher values drift the player's tuning harder.</summary>
    public float Dissonance;

    // Planet-specific
    /// <summary>Exoplanet classification (hot Jupiter, ocean world, …); null for non-planets.</summary>
    public ExoplanetType? ExoplanetClass;

    /// <summary>Visual size multiplier for the body.</summary>
    public float SizeMult = 1f;

    /// <summary>Multiplier on the number of crystals this planet yields when explored.</summary>
    public float CrystalMult = 1f;

    /// <summary>Landing-difficulty multiplier — scales the resonance required to anchor here.</summary>
    public float Difficulty = 1f;

    /// <summary>Index of the star this planet orbits (-1 if none).</summary>
    public int ParentStarIdx = -1;

    /// <summary>Orbital radius around the parent star.</summary>
    public float OrbitRadius;

    /// <summary>Angular orbital speed (closer planets orbit faster, Kepler-style).</summary>
    public float OrbitSpeed;

    /// <summary>Current orbital angle, advanced each frame.</summary>
    public float OrbitAngle;

    /// <summary>Tilt of the orbital plane, for a little 3D variety.</summary>
    public float OrbitTilt;

    /// <summary>Starting phase offset so planets don't all line up.</summary>
    public float OrbitPhase;

    // Star wobble
    /// <summary>Speed of the star's gravitational wobble from its planets.</summary>
    public float WobbleSpeed;

    /// <summary>Amplitude of the star's wobble.</summary>
    public float WobbleRadius;

    /// <summary>Phase offset for the wobble.</summary>
    public float WobblePhase;

    /// <summary>The star's true (un-wobbled) anchor position; null for non-stars.</summary>
    public float[]? BasePosition;

    // Nebula drift
    /// <summary>Speed at which a nebula slowly drifts through space.</summary>
    public float DriftSpeed;

    /// <summary>Direction of the nebula's drift.</summary>
    public float DriftAngle;

    /// <summary>How fast the nebula rotates in place.</summary>
    public float RotationSpeed;
}
