using System;
using System.Collections.Generic;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// Procedural generation of celestial bodies, temples, ley lines, and pyramids
/// using golden spiral mathematics and Fibonacci sequences.
/// Converted from Python celestial.py.
/// </summary>
public static class CelestialGenerator
{
    /// <summary>
    /// Generate celestial bodies procedurally using golden spiral positioning.
    /// Higher dimensions are derived from spatial dimensions with PHI relationships
    /// plus random variation.
    /// </summary>
    /// <param name="n">Number of bodies to generate.</param>
    /// <param name="bodyType">Type of celestial body: "star", "planet", or "nebula".</param>
    /// <returns>List of generated celestial bodies.</returns>
    public static List<CelestialBody> GenerateCelestial(int n, CelestialBodyType bodyType = CelestialBodyType.Star)
    {
        var bodies = new List<CelestialBody>(n);

        for (int i = 0; i < n; i++)
        {
            float theta = i * 2f * MathF.PI * GameConstants.PHI;
            float r = GameConstants.FibSeq[i % GameConstants.FibSeq.Length] * GameConstants.ScaleFactor;

            float[] pos = Vec5.Zero();
            pos[0] = r * MathF.Cos(theta);
            pos[1] = r * MathF.Sin(theta);
            // Higher dimensions derived from spatial dims with PHI relationship
            pos[2] = pos[0] * GameConstants.PHI + MathHelpers.RandomRange(-10f, 10f);
            pos[3] = pos[1] * GameConstants.PHI + MathHelpers.RandomRange(-10f, 10f);
            pos[4] = (pos[0] + pos[1]) * 0.5f * GameConstants.PHI + MathHelpers.RandomRange(-10f, 10f);

            float freq = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);

            var body = new CelestialBody
            {
                Position = pos,
                Frequency = freq,
                BodyType = bodyType
            };

            // Assign stellar type for stars
            if (bodyType == CelestialBodyType.Star)
            {
                var stellarType = MathHelpers.WeightedRandomChoice(GameConstants.StellarTypeProbabilities);
                body.StellarClass = stellarType;
                // Multiply frequency by stellar type multiplier
                body.Frequency *= GameConstants.StellarTypes[stellarType].FreqMult;
            }
            // Assign nebula type for nebulae
            else if (bodyType == CelestialBodyType.Nebula)
            {
                var nebulaType = MathHelpers.WeightedRandomChoice(GameConstants.NebulaTypeProbabilities);
                body.NebulaClass = nebulaType;
                var info = GameConstants.NebulaTypes[nebulaType];
                // Adjust frequency to nebula type range
                body.Frequency = MathHelpers.RandomRange(info.FreqRangeMin, info.FreqRangeMax);
                body.Dissonance = info.Dissonance;
            }

            bodies.Add(body);
        }

        DebugLogger.Log("Celestial", $"GenerateCelestial: {n} {bodyType}(s) generated");
        return bodies;
    }

    /// <summary>
    /// Generate the complete universe of celestial bodies: stars with wobble,
    /// planets with orbital mechanics, and nebulae with drift.
    /// </summary>
    /// <returns>Tuple of (stars, planets, nebulae, celestialBodies).</returns>
    public static (List<CelestialBody> Stars, List<CelestialBody> Planets, List<CelestialBody> Nebulae, List<CelestialBody> CelestialBodies) GenerateAllCelestialBodies()
    {
        // Generate stars using golden spiral
        var stars = GenerateCelestial(GameConstants.NStars, CelestialBodyType.Star);

        // Add subtle movement properties to stars (wobble from planetary gravity)
        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            star.WobbleSpeed = MathHelpers.RandomRange(0.05f, 0.2f);
            star.WobbleRadius = MathHelpers.RandomRange(0.5f, 2f);
            star.WobblePhase = MathHelpers.RandomRange(0f, 2f * MathF.PI);
            star.BasePosition = Vec5.Clone(star.Position);
        }

        // Generate planets orbiting each star
        var planets = new List<CelestialBody>(stars.Count * GameConstants.NPlanetsPerStar);
        for (int starIdx = 0; starIdx < stars.Count; starIdx++)
        {
            var star = stars[starIdx];
            for (int planetI = 0; planetI < GameConstants.NPlanetsPerStar; planetI++)
            {
                // Calculate orbital parameters
                float orbitRadius = MathHelpers.RandomRange(GameConstants.OrbitRadius * 0.3f, GameConstants.OrbitRadius);
                // Kepler-ish: closer = faster
                float orbitSpeed = MathHelpers.RandomRange(0.1f, 0.5f) / (orbitRadius / GameConstants.OrbitRadius);
                float orbitAngle = MathHelpers.RandomRange(0f, 2f * MathF.PI);
                float orbitTilt = MathHelpers.RandomRange(-0.3f, 0.3f);

                // Initial position offset from parent star
                float[] pos = Vec5.Clone(star.Position);
                pos[0] += orbitRadius * MathF.Cos(orbitAngle);
                pos[1] += orbitRadius * MathF.Sin(orbitAngle);
                pos[2] += orbitRadius * orbitTilt * MathF.Sin(orbitAngle);

                float freq = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);

                // Assign exoplanet type
                var exoplanetType = MathHelpers.WeightedRandomChoice(GameConstants.ExoplanetTypeProbabilities);
                var typeInfo = GameConstants.ExoplanetTypes[exoplanetType];

                var planet = new CelestialBody
                {
                    Position = pos,
                    Frequency = freq,
                    BodyType = CelestialBodyType.Planet,
                    ExoplanetClass = exoplanetType,
                    SizeMult = typeInfo.SizeMult,
                    CrystalMult = typeInfo.CrystalMult,
                    Difficulty = typeInfo.Difficulty,
                    ParentStarIdx = starIdx,
                    OrbitRadius = orbitRadius,
                    OrbitSpeed = orbitSpeed,
                    OrbitAngle = orbitAngle,
                    OrbitTilt = orbitTilt,
                    OrbitPhase = MathHelpers.RandomRange(0f, 2f * MathF.PI)
                };
                planets.Add(planet);
            }
        }

        // Generate nebulae with drift/rotation properties
        var nebulae = GenerateCelestial(GameConstants.NNebulae, CelestialBodyType.Nebula);
        for (int i = 0; i < nebulae.Count; i++)
        {
            var nebula = nebulae[i];
            nebula.DriftSpeed = MathHelpers.RandomRange(0.02f, 0.1f);
            nebula.DriftAngle = MathHelpers.RandomRange(0f, 2f * MathF.PI);
            nebula.RotationSpeed = MathHelpers.RandomRange(0.01f, 0.05f);
            nebula.BasePosition = Vec5.Clone(nebula.Position);
        }

        // Combined list for collision/proximity checks
        var celestialBodies = new List<CelestialBody>(stars.Count + planets.Count + nebulae.Count);
        celestialBodies.AddRange(stars);
        celestialBodies.AddRange(planets);
        celestialBodies.AddRange(nebulae);

        DebugLogger.Log("Celestial", $"GenerateAllCelestialBodies: {stars.Count} stars, {planets.Count} planets, {nebulae.Count} nebulae");
        return (stars, planets, nebulae, celestialBodies);
    }

    /// <summary>
    /// Update celestial body positions based on orbital mechanics and drift.
    /// Called each frame to animate star wobble, planetary orbits, and nebula drift.
    /// </summary>
    /// <param name="stars">List of star bodies.</param>
    /// <param name="planets">List of planet bodies.</param>
    /// <param name="nebulae">List of nebula bodies.</param>
    /// <param name="time">Current simulation time in seconds.</param>
    private static bool _warnedNullBasePos;
    private static bool _warnedBadParentIdx;

    public static void UpdateCelestialPositions(List<CelestialBody> stars, List<CelestialBody> planets, List<CelestialBody> nebulae, float time)
    {
        // Update star positions (subtle wobble from planetary gravity)
        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            if (star.BasePosition == null)
            {
                if (!_warnedNullBasePos) { DebugLogger.Log("Celestial", $"WARNING: Star {i} has null BasePosition"); _warnedNullBasePos = true; }
                continue;
            }

            float wobbleX = star.WobbleRadius * MathF.Cos(time * star.WobbleSpeed + star.WobblePhase);
            float wobbleY = star.WobbleRadius * MathF.Sin(time * star.WobbleSpeed + star.WobblePhase);
            star.Position[0] = star.BasePosition[0] + wobbleX;
            star.Position[1] = star.BasePosition[1] + wobbleY;
        }

        // Update planet orbital positions
        for (int i = 0; i < planets.Count; i++)
        {
            var planet = planets[i];
            if (planet.ParentStarIdx < 0 || planet.ParentStarIdx >= stars.Count)
            {
                if (!_warnedBadParentIdx) { DebugLogger.Log("Celestial", $"WARNING: Planet {i} has invalid ParentStarIdx={planet.ParentStarIdx}"); _warnedBadParentIdx = true; }
                continue;
            }

            var star = stars[planet.ParentStarIdx];
            float angle = planet.OrbitAngle + time * planet.OrbitSpeed;
            float radius = planet.OrbitRadius;
            float tilt = planet.OrbitTilt;

            // Calculate orbital position relative to parent star
            planet.Position[0] = star.Position[0] + radius * MathF.Cos(angle);
            planet.Position[1] = star.Position[1] + radius * MathF.Sin(angle);
            planet.Position[2] = star.Position[2] + radius * tilt * MathF.Sin(angle + planet.OrbitPhase);
            // Higher dimensions follow with PHI relationship
            planet.Position[3] = star.Position[3] + radius * 0.5f * MathF.Cos(angle * GameConstants.PHI);
            planet.Position[4] = star.Position[4] + radius * 0.5f * MathF.Sin(angle * GameConstants.PHI);
        }

        // Update nebula drift
        for (int i = 0; i < nebulae.Count; i++)
        {
            var nebula = nebulae[i];
            if (nebula.BasePosition == null) continue;

            float driftX = MathF.Sin(time * nebula.DriftSpeed) * 5f;
            float driftY = MathF.Cos(time * nebula.DriftSpeed + nebula.DriftAngle) * 5f;
            nebula.Position[0] = nebula.BasePosition[0] + driftX;
            nebula.Position[1] = nebula.BasePosition[1] + driftY;
        }
    }

    /// <summary>
    /// Generate the 12 minor zodiac temples plus the Halls of Amenti master temple.
    /// Temples are placed in a sacred dodecagon pattern around the universe center,
    /// each at golden ratio distances.
    /// </summary>
    /// <returns>List of 13 temples (12 minor + 1 master).</returns>
    public static List<Temple> GenerateTemples()
    {
        var temples = new List<Temple>(GameConstants.MinorTempleCount + 1);

        // Generate 12 minor temples in a sacred dodecagon pattern
        for (int i = 0; i < GameConstants.MinorTempleCount; i++)
        {
            // Position temples with zodiac spacing (30-degree offset for zodiac alignment)
            float angle = i * (2f * MathF.PI / 12f) + (MathF.PI / 6f);
            float radius = GameConstants.FibSeq[Math.Min(i + 3, GameConstants.FibSeq.Length - 1)]
                           * GameConstants.ScaleFactor * GameConstants.PHI;

            float[] pos = Vec5.Zero();
            pos[0] = radius * MathF.Cos(angle);
            pos[1] = radius * MathF.Sin(angle);
            // Higher dimensions follow golden ratio relationships
            pos[2] = radius * MathF.Sin(angle * GameConstants.PHI) * 0.5f;
            pos[3] = pos[0] * GameConstants.PHI;
            pos[4] = pos[1] * GameConstants.PHI;

            var temple = new Temple
            {
                Position = pos,
                Frequency = GameConstants.TempleKeyFrequencies[i],
                Kind = TempleType.Minor,
                KeyIndex = i,
                KeyName = GameConstants.TempleKeyNames[i]
            };
            temples.Add(temple);
        }

        // Add Halls of Amenti (Master Temple) at universe center
        var hallsOfAmenti = new Temple
        {
            Position = Vec5.Clone(GameConstants.HallsOfAmentiPos),
            Frequency = GameConstants.TempleResonanceFreq, // 110 Hz ancient healing frequency
            Kind = TempleType.Master,
            KeyIndex = -1, // Special index for master temple
            KeyName = "Amenti"
        };
        temples.Add(hallsOfAmenti);

        DebugLogger.Log("Celestial", $"GenerateTemples: {temples.Count} temples created");
        return temples;
    }

    /// <summary>
    /// Generate ley lines connecting temples in a sacred energy grid.
    /// Creates ring connections (12), star connections (6), and Amenti paths (12).
    /// </summary>
    /// <param name="temples">List of temples (12 minor + 1 master).</param>
    /// <returns>List of 30 ley lines.</returns>
    public static List<LeyLine> GenerateLeyLines(List<Temple> temples)
    {
        var leyLines = new List<LeyLine>(30);

        // Ring lines: connect each temple to the next in sequence (12 lines)
        for (int i = 0; i < GameConstants.MinorTempleCount; i++)
        {
            int nextI = (i + 1) % GameConstants.MinorTempleCount;

            var leyLine = new LeyLine
            {
                Start = Vec5.Clone(temples[i].Position),
                End = Vec5.Clone(temples[nextI].Position),
                Frequency = GameConstants.LeyLineFreq,

                Name = $"Ley Line: {temples[i].KeyName} to {temples[nextI].KeyName}",
                TempleIndex1 = i,
                TempleIndex2 = nextI,
                Major = false,
                AmentiPath = false
            };
            leyLines.Add(leyLine);
        }

        // Star lines: connect opposite temples (6 lines forming a star pattern)
        for (int i = 0; i < 6; i++)
        {
            int oppositeI = i + 6;

            var leyLine = new LeyLine
            {
                Start = Vec5.Clone(temples[i].Position),
                End = Vec5.Clone(temples[oppositeI].Position),
                Frequency = GameConstants.LeyLineFreq * GameConstants.PHI, // Higher frequency for major ley lines

                Name = $"Major Ley Line: {temples[i].KeyName} to {temples[oppositeI].KeyName}",
                TempleIndex1 = i,
                TempleIndex2 = oppositeI,
                Major = true,
                AmentiPath = false
            };
            leyLines.Add(leyLine);
        }

        // Amenti paths: connect all temples to Halls of Amenti (12 radial lines)
        var amenti = temples[^1]; // Master temple is last in list
        for (int i = 0; i < GameConstants.MinorTempleCount; i++)
        {
            var leyLine = new LeyLine
            {
                Start = Vec5.Clone(temples[i].Position),
                End = Vec5.Clone(amenti.Position),
                Frequency = GameConstants.TempleResonanceFreq, // 110 Hz for Amenti connections

                Name = $"Amenti Path: {temples[i].KeyName} to Halls of Amenti",
                TempleIndex1 = i,
                TempleIndex2 = -1,
                Major = false,
                AmentiPath = true
            };
            leyLines.Add(leyLine);
        }

        DebugLogger.Log("Celestial", $"GenerateLeyLines: {leyLines.Count} ley lines created");
        return leyLines;
    }

    /// <summary>
    /// Generate pyramid resonance chambers at sacred locations.
    /// Pyramids are placed at golden ratio distances in key energy intersection points.
    /// </summary>
    /// <returns>List of 3 pyramids.</returns>
    public static List<Pyramid> GeneratePyramids()
    {
        float phi = GameConstants.PHI;
        float phi2 = phi * phi;
        float phi3 = phi * phi * phi;

        var pyramidPositions = new[]
        {
            // Pyramid 1: Giza alignment (Earth reference point)
            Vec5.Create(phi * 50f, 0f, phi * 30f, phi2 * 50f, 0f),
            // Pyramid 2: Stellar alignment (star grid nexus)
            Vec5.Create(-phi * 40f, phi * 40f, -phi * 20f, 0f, phi2 * 40f),
            // Pyramid 3: Dimensional gateway (higher dim focus)
            Vec5.Create(0f, -phi * 60f, phi * 40f, phi3 * 30f, phi3 * 30f)
        };

        var pyramidNames = new[]
        {
            "Pyramid of Giza Resonance",
            "Pyramid of Stellar Alignment",
            "Pyramid of Dimensional Gateway"
        };

        var pyramids = new List<Pyramid>(GameConstants.PyramidCount);
        for (int i = 0; i < GameConstants.PyramidCount; i++)
        {
            var pyramid = new Pyramid
            {
                Position = pyramidPositions[i],
                Frequency = GameConstants.PyramidResonanceFreq, // 118 Hz
                Name = pyramidNames[i],
                Index = i,
                Desc = $"{pyramidNames[i]} - sacred resonance chamber at 118 Hz"
            };
            pyramids.Add(pyramid);
        }

        DebugLogger.Log("Celestial", $"GeneratePyramids: {pyramids.Count} pyramids created");
        return pyramids;
    }

    /// <summary>
    /// Generate the complete Atlantean universe including all celestial bodies,
    /// temples, ley lines, and pyramids.
    /// </summary>
    /// <returns>Tuple of all universe components.</returns>
    public static (List<CelestialBody> Stars, List<CelestialBody> Planets, List<CelestialBody> Nebulae, List<CelestialBody> CelestialBodies, List<Temple> Temples, List<LeyLine> LeyLines, List<Pyramid> Pyramids) GenerateCompleteUniverse()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Generate base celestial bodies
        var (stars, planets, nebulae, celestialBodies) = GenerateAllCelestialBodies();

        // Generate Atlantean structures
        var temples = GenerateTemples();
        var leyLines = GenerateLeyLines(temples);
        var pyramids = GeneratePyramids();

        sw.Stop();
        DebugLogger.Log("Celestial", $"Complete universe generated in {sw.ElapsedMilliseconds}ms");

        return (stars, planets, nebulae, celestialBodies, temples, leyLines, pyramids);
    }
}
