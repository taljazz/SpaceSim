using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// All game constants, mirroring constants.py from the Python version.
/// </summary>
public static class GameConstants
{
    // Core dimensions and display
    public const int NDimensions = 5;
    public const int ScreenWidth = 800;
    public const int ScreenHeight = 600;
    public const int Fps = 60;
    public const float Dt = 1f / Fps;

    // Physics constants
    public const float MaxVelocityBase = 10f;
    public const float ResonanceWidthBase = 10f;
    public const float FrequencyMin = 110f;
    public const float FrequencyMax = 963f;
    public const float PHI = 1.6180339887f; // Golden ratio

    // Audio settings
    public const int SampleRate = 44100;
    public const float SchumannFreq = 7.83f;
    public const float SchumannVolume = 0.01f;

    // Celestial body generation
    public const int NStars = 200;
    public const int NPlanetsPerStar = 3;
    public const int NNebulae = 10;
    public const float OrbitRadius = 5f;
    public const float PlanetRadius = 10f;
    public const float InteractionDistance = 15f;

    // Fibonacci sequence (F(0) through F(NFibonacci-1))
    // FibSeq is used for celestial/crystal positioning; UpgradeCosts is derived from it (F(1)..F(8))
    public const int NFibonacci = 9; // 0,1,1,2,3,5,8,13,21
    public static readonly int[] FibSeq;
    public static readonly float ScaleFactor;

    // Speech and audio feedback
    public const float SpeechCooldown = 0.5f;
    public const float ViewLandmarkThreshold = 10f;
    public const float RotationSoundDuration = 0.2f;
    public const float LandmarkSpeechCooldown = 1f;
    public const float CursorSpeechCooldown = 0.2f;

    // Landing and planet exploration
    public const float LandingThreshold = 0.8f;
    public const float LandingTime = 3f;
    public const int CrystalCountBase = 3;
    public const int GridSize = 10;
    public const float CrystalCollectionThreshold = 0.8f;

    // Resonance and power mechanics
    public const float PowerBuildThreshold = 0.8f;
    public const float PowerBuildTime = 5f;
    public const float DissonanceThreshold = 0.2f;
    public const float DissonanceDuration = 10f;
    public const float PerfectResonanceThreshold = 0.999f;

    // Rift mechanics
    public const float RiftAlignmentTolerance = 20f;
    public const float RiftFadeTime = 30f;
    public const float RiftEntryResThreshold = 0.6f;
    public const float RiftMaxDist = 20f;
    public const float RiftFocusThreshold = 90f;
    public const float RiftEntryAlignmentAngle = 20f;
    public const float RiftChargeTime = 4f;
    public const float RiftNudgeRate = 0.2f;
    public const float PerfectFifthTolerance = 0.5f;
    public const float PerfectFifthProb = 0.0001f;

    // UI and display
    public const int HudTextSizeBase = 24;
    public const float ClickInterval = 0.5f;

    // Upgrades and progression — derived from FibSeq: F(1) through F(8)
    public static readonly int[] UpgradeCosts;
    public const int AscensionCrystalThreshold = 21;

    // Navigation and tuning
    public const float RotationSpeed = 3f;
    public const float TuningRate = 100f;
    public const float TuningRatePlanet = 20f;
    public const float ScannerRange = 50f;
    public const float SlowdownDist = 20f;
    public const float AutoSnapThreshold = 0.5f;
    public const float ApproachingLockThreshold = 10f;

    // Speed modes
    public static readonly float[] SpeedFactors = { 0.3f, 0.6f, 1f };
    public static readonly string[] SpeedModeNames = { "Approach", "Cruise", "Quantum" };

    // Celestial body effects
    public const float StarHarmonyRadius = 12f;
    public const float StarMaxBenefitRadius = 6f;
    public const float NebulaDissonanceRadius = 10f;

    // Special mechanics
    public const float IdleTimeThreshold = 120f;
    public const float PitchRecordDuration = 1f;
    public const float EasterEggFreq = 432f;
    public const float EasterEggTolerance = 0.1f;
    public const float AutosaveInterval = 300f;
    public const float WaterBlessingHoldTime = 33f;
    public const float WaterBlessingResThreshold = 0.999f;
    public const float WaterBlessingFreq = 432f;
    public const float WaterBlessingDuration = 60f;
    public const float SingSilenceThreshold = 4f;
    public const float HeartbeatVolume = 0.1f;

    // Zoom
    public const float ZoomMin = 0.2f;
    public const float ZoomMax = 5f;
    public const float ZoomStep = 0.1f;

    // Harmonic relationship system
    public const float HarmonicTolerance = 0.02f;
    public static readonly Dictionary<HarmonicType, float> HarmonicRatios = new()
    {
        [HarmonicType.Octave] = 2f,
        [HarmonicType.PerfectFifth] = 1.5f,
        [HarmonicType.PerfectFourth] = 4f / 3f,       // 1.333333...
        [HarmonicType.MajorThird] = 1.25f,
        [HarmonicType.MinorThird] = 1.2f,
        [HarmonicType.MajorSixth] = 5f / 3f,         // 1.666667...
        [HarmonicType.MinorSixth] = 1.6f,
        [HarmonicType.Tritone] = 1.41421356f,          // √2
        [HarmonicType.Golden] = PHI,
    };
    public const float HarmonicDetectionInterval = 0.5f;
    public const float HarmonicBonusDuration = 2f;
    public const float HarmonicBonusMultiplier = 1.15f;

    // Harmonic series settings
    public const int NHarmonics = 7;
    public const float HarmonicFalloff = 1.5f;
    public const float SubharmonicDepth = 0.15f;
    public const float IntermodDepth = 0.08f;

    // Stellar types
    public static readonly Dictionary<StellarType, StellarTypeInfo> StellarTypes = new()
    {
        [StellarType.MainSequence] = new(new Color(255, 255, 200), 1f, "stable hydrogen-burning star", 200f, 400f),
        [StellarType.RedGiant] = new(new Color(255, 100, 50), 0.7f, "ancient bloated star", 30f, 50f),
        [StellarType.WhiteDwarf] = new(new Color(200, 220, 255), 1.8f, "dense stellar core", 1200f, 1500f),
        [StellarType.BrownDwarf] = new(new Color(100, 50, 30), 0.3f, "failed star", 20f, 30f),
    };
    public static readonly Dictionary<StellarType, float> StellarTypeProbabilities = new()
    {
        [StellarType.MainSequence] = 0.70f,
        [StellarType.RedGiant] = 0.15f,
        [StellarType.WhiteDwarf] = 0.10f,
        [StellarType.BrownDwarf] = 0.05f,
    };

    // Nebula types
    public static readonly Dictionary<NebulaType, NebulaTypeInfo> NebulaTypes = new()
    {
        [NebulaType.Emission] = new(new Color(255, 50, 50), 200f, 300f, 0.5f, "ionized gas cloud"),
        [NebulaType.Reflection] = new(new Color(50, 150, 255), 600f, 800f, 0.3f, "dust reflecting starlight"),
        [NebulaType.Planetary] = new(new Color(150, 255, 150), 400f, 600f, 0.4f, "dying star shell"),
        [NebulaType.SupernovaRemnant] = new(new Color(255, 150, 100), 100f, 900f, 0.9f, "expanding blast wave"),
    };
    public static readonly Dictionary<NebulaType, float> NebulaTypeProbabilities = new()
    {
        [NebulaType.Emission] = 0.40f,
        [NebulaType.Reflection] = 0.30f,
        [NebulaType.Planetary] = 0.20f,
        [NebulaType.SupernovaRemnant] = 0.10f,
    };

    // Exoplanet types
    public static readonly Dictionary<ExoplanetType, ExoplanetTypeInfo> ExoplanetTypes = new()
    {
        [ExoplanetType.HotJupiter] = new(3f, 0.5f, 1.5f, "scorching gas giant"),
        [ExoplanetType.SuperEarth] = new(1.5f, 1.2f, 1f, "massive rocky world"),
        [ExoplanetType.OceanWorld] = new(1.2f, 1.5f, 0.8f, "water-covered planet"),
        [ExoplanetType.RoguePlanet] = new(1f, 2f, 2f, "sunless wanderer"),
        [ExoplanetType.IceGiant] = new(2.5f, 0.8f, 1.3f, "frozen methane world"),
    };
    public static readonly Dictionary<ExoplanetType, float> ExoplanetTypeProbabilities = new()
    {
        [ExoplanetType.SuperEarth] = 0.35f,
        [ExoplanetType.HotJupiter] = 0.25f,
        [ExoplanetType.IceGiant] = 0.20f,
        [ExoplanetType.OceanWorld] = 0.15f,
        [ExoplanetType.RoguePlanet] = 0.05f,
    };

    // Solfeggio Frequencies
    public static readonly Dictionary<int, SolfeggioInfo> SolfeggioFrequencies = new()
    {
        [174] = new("Foundation", "pain_relief", "natural anesthetic", "shield", 1.1f),
        [285] = new("Quantum", "tissue_healing", "cellular regeneration", "minor_heal", 0.5f),
        [396] = new("Liberation", "release_fear", "liberating guilt and fear", "stability", 1.2f),
        [417] = new("Transmutation", "facilitate_change", "undoing situations", "rift_assist", 1.15f),
        [432] = new("Natural Harmony", "universal_tuning", "cosmic frequency", "base_heal", 1f),
        [528] = new("Miracle", "transformation", "DNA repair, love frequency", "major_heal", 2f),
        [639] = new("Connection", "relationships", "harmonizing connections", "comm_boost", 1.3f),
        [741] = new("Awakening", "expression", "awakening intuition", "rift_detect", 1.4f),
        [852] = new("Intuition", "spiritual_order", "returning to spiritual order", "third_eye", 1.25f),
        [963] = new("Divine", "oneness", "connection to Source", "transcend", 1.5f),
    };
    public const float SolfeggioTolerance = 5f;

    // Crystal Color Spectrum
    public static readonly Dictionary<string, CrystalSpectrumInfo> CrystalSpectrum = new()
    {
        ["ruby"] = new(110f, 285f, new Color(220, 20, 60), "root", "stability", 1.2f),
        ["carnelian"] = new(285f, 350f, new Color(255, 127, 80), "sacral", "crystal_find", 1.3f),
        ["citrine"] = new(350f, 417f, new Color(255, 215, 0), "solar_plexus", "velocity", 1.15f),
        ["emerald"] = new(417f, 528f, new Color(0, 201, 87), "heart", "integrity", 1.25f),
        ["lapis"] = new(528f, 639f, new Color(38, 97, 156), "throat", "scan_range", 1.4f),
        ["amethyst"] = new(639f, 741f, new Color(153, 102, 204), "third_eye", "rift_detect", 1.35f),
        ["quartz"] = new(741f, 963f, new Color(255, 255, 255), "crown", "universal", 1.1f),
    };

    // Temple Resonance
    public const float TempleResonanceFreq = 110f;
    public static readonly (float Min, float Max) TempleResonanceRange = (95f, 120f);
    public const int TempleCount = 7;
    public const float TempleHealingRate = 0.02f;
    public const float TempleConsciousnessBoost = 1.5f;

    // Merkaba Activation
    public const float MerkabaActivationThreshold = 0.9f;
    public const float MerkabaShieldStrength = 0.5f;
    public const float MerkabaVelocityBoost = 1.3f;
    public const float MerkabaDetectionRange = 2f;

    // Tuaoi Crystal Modes
    public static readonly Dictionary<TuaoiMode, TuaoiModeInfo> TuaoiModes = new()
    {
        [TuaoiMode.Healing] = new(432f, new Color(0, 255, 128), "integrity_regen", 0.01f, "Atlantean healing frequency"),
        [TuaoiMode.Navigation] = new(PHI * 256f, new Color(100, 150, 255), "enhanced_autopilot", 1.5f, "Golden ratio navigation"),
        [TuaoiMode.Communication] = new(7.83f, new Color(255, 200, 100), "expanded_scan", 2f, "Earth resonance connection"),
        [TuaoiMode.Power] = new(528f, new Color(255, 100, 100), "velocity_boost", 1.25f, "Miracle frequency power"),
        [TuaoiMode.Regeneration] = new(285f, new Color(200, 100, 255), "resonance_recovery", 1.3f, "Cellular regeneration frequency"),
        [TuaoiMode.Transcendence] = new(963f, new Color(255, 255, 200), "higher_dim_sensitivity", 1.4f, "Divine connection frequency"),
    };
    public static readonly TuaoiMode[] TuaoiModeOrder = { TuaoiMode.Healing, TuaoiMode.Navigation, TuaoiMode.Communication, TuaoiMode.Power, TuaoiMode.Regeneration, TuaoiMode.Transcendence };
    public static readonly TuaoiModeInfo[] TuaoiModeInfoByIndex;
    public const float TuaoiModeSwitchCooldown = 2f;

    // Halls of Amenti
    public const float AmentiResonanceThreshold = 0.95f;
    public const float AmentiTimeDilation = 0.5f;
    public const float AmentiWisdomBonus = 2f;
    public static readonly float[] HallsOfAmentiPos = Vec5.Zero();

    // Sacred Geometry Patterns
    public static readonly Dictionary<string, SacredPatternInfo> SacredPatterns = new()
    {
        ["vesica_piscis"] = new(2, "creation", 1.2f),
        ["seed_of_life"] = new(7, "crystal_regen", 1.5f),
        ["flower_of_life"] = new(19, "all_harmonics", 2f),
        ["metatrons_cube"] = new(13, "max_resonance", 1.8f),
        ["merkaba"] = new(8, "protection", 1.6f),
        ["golden_spiral"] = new(5, "phi_stacking", PHI),
    };

    // Brainwave States
    public static readonly Dictionary<BrainwaveState, BrainwaveStateInfo> BrainwaveStates = new()
    {
        [BrainwaveState.Delta] = new(0.5f, 4f, "deep_healing", "auto_repair", 2f),
        [BrainwaveState.Theta] = new(4f, 8f, "meditation", "rift_vision", 1.5f),
        [BrainwaveState.Alpha] = new(8f, 13f, "relaxed_focus", "enhanced_scan", 1.3f),
        [BrainwaveState.Beta] = new(13f, 30f, "active", "fast_tuning", 1.2f),
        [BrainwaveState.Gamma] = new(30f, 100f, "transcendence", "all_bonus", 1.4f),
    };

    // Atlantean Terminology
    public static readonly Dictionary<string, string> AtlanteanTerms = new()
    {
        ["rift"] = "Harmonic Chamber",
        ["rifts"] = "Harmonic Chambers",
        ["crystal"] = "Atlantean Crystal",
        ["crystals"] = "Atlantean Crystals",
        ["upgrade"] = "Attunement",
        ["upgrades"] = "Attunements",
        ["landed"] = "Anchored",
        ["landing"] = "Anchoring",
        ["takeoff"] = "Ascension",
        ["meditation"] = "Atla-Ra Meditation",
        ["resonance"] = "Harmonic Alignment",
        ["frequency"] = "Vibrational Tone",
        ["dimension"] = "Realm",
        ["ship"] = "Light Vehicle",
    };

    // Ley Line Highways
    public const int LeyLineCount = 12;
    public const float LeyLineSpeedMult = 3f;
    public const float LeyLineWidth = 8f;
    public const float LeyLineDetectionRange = 25f;
    public const float LeyLineFreq = 432f;

    // Portal Anchor System
    public const int MaxPortalAnchors = 7;
    public const int PortalAnchorCost = 3;
    public const float PortalTravelResonance = 0.85f;
    public const float PortalCooldown = 30f;

    // Crystal Activation Sequences
    public const int ActivationSequenceLength = 5;
    public static readonly int[] ActivationFrequencies = { 396, 417, 528, 639, 741 };
    public const float ActivationTolerance = 8f;
    public const float ActivationTimeLimit = 30f;
    public const float ActivationRewardMult = 2f;

    // 12+1 Temple System
    public const int MinorTempleCount = 12;
    public static readonly string[] TempleKeyNames =
    {
        "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
        "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces"
    };
    public static readonly int[] TempleKeyFrequencies =
    {
        396, 417, 432, 444, 480, 512, 528, 576, 594, 639, 672, 741
    };
    public const int MasterTempleUnlockKeys = 12;

    // Pyramid Resonance Chambers
    public const float PyramidResonanceFreq = 118f;
    public static readonly (float Min, float Max) PyramidResonanceRange = (117f, 121f);
    public const float PyramidHealingMult = 3f;
    public const float PyramidConsciousnessBoost = 2f;
    public const int PyramidCount = 3;

    // Atlantean Crystal Types
    public static readonly Dictionary<string, AtlanteanCrystalTypeInfo> AtlanteanCrystalTypes = new()
    {
        ["fire_crystal"] = new(new Color(255, 69, 0), 200f, 300f, "velocity_burst", 2f, "Volcanic energy crystal from Atlantean forges"),
        ["aquamarine"] = new(new Color(127, 255, 212), 300f, 400f, "shield_boost", 1.5f, "Ocean-born crystal of protection"),
        ["larimar"] = new(new Color(135, 206, 235), 400f, 500f, "communication", 1.8f, "Dolphin stone of ancient Atlantean wisdom"),
        ["moldavite"] = new(new Color(154, 205, 50), 500f, 600f, "transformation", 2.5f, "Extraterrestrial glass of rapid evolution"),
        ["lemurian_seed"] = new(new Color(255, 182, 193), 600f, 700f, "memory_unlock", 2f, "Ancient knowledge carrier from Lemuria"),
        ["black_tourmaline"] = new(new Color(47, 79, 79), 100f, 200f, "purification", 1.3f, "Protective stone against negative frequencies"),
        ["celestite"] = new(new Color(176, 224, 230), 700f, 800f, "angelic_connection", 1.7f, "Bridge to higher realms and celestial beings"),
    };
    public const float AtlanteanCrystalChance = 0.15f;

    // Consciousness Level System
    public static readonly Dictionary<ConsciousnessLevel, ConsciousnessLevelInfo> ConsciousnessLevels = new()
    {
        [ConsciousnessLevel.Dormant] = new(0f, 1f, "Unawakened state"),
        [ConsciousnessLevel.Awakening] = new(0.3f, 1.2f, "Beginning to sense the harmonics"),
        [ConsciousnessLevel.Aware] = new(0.5f, 1.4f, "Consciously navigating frequencies"),
        [ConsciousnessLevel.Attuned] = new(0.7f, 1.6f, "Deeply connected to cosmic vibrations"),
        [ConsciousnessLevel.Enlightened] = new(0.85f, 1.8f, "Mastery of harmonic navigation"),
        [ConsciousnessLevel.Ascended] = new(0.95f, 2f, "One with the universal frequency"),
    };
    public static readonly ConsciousnessLevel[] ConsciousnessLevelOrder = { ConsciousnessLevel.Dormant, ConsciousnessLevel.Awakening, ConsciousnessLevel.Aware, ConsciousnessLevel.Attuned, ConsciousnessLevel.Enlightened, ConsciousnessLevel.Ascended };
    public const float ConsciousnessGainRate = 0.001f;
    public const float ConsciousnessDecayRate = 0.0005f;

    // Astral Projection Mode
    public const float AstralProjectionResonance = 0.9f;
    public const float AstralProjectionRange = 200f;
    public const float AstralSpeedMult = 5f;
    public const float AstralDuration = 30f;
    public const float AstralCooldown = 60f;

    // Intention-Based Navigation
    public const float IntentionActivationTime = 5f;
    public const float IntentionResonanceThreshold = 0.8f;
    public const float IntentionRange = 100f;
    public const float IntentionPrecision = 0.9f;

    // Cymatics Visualization
    public const bool CymaticsEnabled = true;
    public static readonly Dictionary<string, CymaticsPatternInfo> CymaticsPatterns = new()
    {
        ["hexagon"] = new(200f, 300f, 6f),
        ["star"] = new(300f, 400f, 5f),
        ["flower"] = new(400f, 500f, 12f),
        ["mandala"] = new(500f, 600f, 8f),
        ["spiral"] = new(600f, 700f, PHI),
        ["merkaba"] = new(700f, 800f, 24f),
    };

    // Static constructor to generate Fibonacci sequence and derived arrays
    static GameConstants()
    {
        FibSeq = new int[NFibonacci];
        FibSeq[0] = 0;
        FibSeq[1] = 1;
        for (int i = 2; i < NFibonacci; i++)
            FibSeq[i] = FibSeq[i - 1] + FibSeq[i - 2];
        ScaleFactor = 100f / FibSeq[^1];

        // UpgradeCosts = F(1)..F(8) = {1, 1, 2, 3, 5, 8, 13, 21}
        UpgradeCosts = new int[NFibonacci - 1];
        for (int i = 0; i < UpgradeCosts.Length; i++)
            UpgradeCosts[i] = FibSeq[i + 1];

        // Build TuaoiModeInfoByIndex for fast indexed lookup
        TuaoiModeInfoByIndex = new TuaoiModeInfo[TuaoiModeOrder.Length];
        for (int i = 0; i < TuaoiModeOrder.Length; i++)
            TuaoiModeInfoByIndex[i] = TuaoiModes[TuaoiModeOrder[i]];
    }
}
