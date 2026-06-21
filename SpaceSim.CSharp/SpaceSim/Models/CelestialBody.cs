namespace SpaceSim.Models;

public class CelestialBody : WorldObject
{
    public CelestialBodyType BodyType;
    public float Frequency;

    // Star-specific
    public StellarType? StellarClass;

    // Nebula-specific
    public NebulaType? NebulaClass;
    public float Dissonance;

    // Planet-specific
    public ExoplanetType? ExoplanetClass;
    public float SizeMult = 1f;
    public float CrystalMult = 1f;
    public float Difficulty = 1f;
    public int ParentStarIdx = -1;
    public float OrbitRadius;
    public float OrbitSpeed;
    public float OrbitAngle;
    public float OrbitTilt;
    public float OrbitPhase;

    // Star wobble
    public float WobbleSpeed;
    public float WobbleRadius;
    public float WobblePhase;
    public float[]? BasePosition;

    // Nebula drift
    public float DriftSpeed;
    public float DriftAngle;
    public float RotationSpeed;
}
