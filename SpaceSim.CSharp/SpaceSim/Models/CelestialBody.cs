namespace SpaceSim.Models;

public class CelestialBody
{
    public float[] Position = Vec5.Zero();
    public float Frequency;
    public string Type = "star"; // "star", "planet", "nebula"

    // Star-specific
    public string? StellarType;

    // Nebula-specific
    public string? NebulaType;
    public float Dissonance;

    // Planet-specific
    public string? ExoplanetType;
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
