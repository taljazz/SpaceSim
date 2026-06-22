using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// All game constants, mirroring constants.py from the Python version.
///
/// <para>
/// This is the single source of truth for every tunable number in the simulator — physics,
/// audio, celestial generation, and the whole Atlantean layer (Solfeggio tones, crystals,
/// temples, ley lines, pyramids, consciousness). Nothing here is hardcoded elsewhere, so a
/// value changed here changes it everywhere. The members are grouped into <c>#region</c>
/// blocks by theme so you can find a knob without scrolling the whole file.
/// </para>
///
/// <para>
/// The golden ratio <see cref="PHI"/> threads through nearly every section — positioning,
/// audio overtones, upgrade costs, sacred geometry — because resonance and proportion are the
/// game's core conceit.
/// </para>
/// </summary>
public static class GameConstants
{
    #region Core dimensions and display

    public const int NDimensions = 5;
    public const int ScreenWidth = 800;
    public const int ScreenHeight = 600;
    public const int Fps = 60;
    public const float Dt = 1f / Fps;

    #endregion

    #region Physics constants

    public const float MaxVelocityBase = 10f;
    public const float ResonanceWidthBase = 10f;
    public const float FrequencyMin = 110f;
    public const float FrequencyMax = 963f;
    public const float PHI = 1.6180339887f; // Golden ratio

    #endregion

    #region Audio settings

    public const int SampleRate = 44100;
    public const float SchumannFreq = 7.83f;
    public const float SchumannVolume = 0.01f;

    // --- Proximity ambient loudness ---
    // The normalized star/planet/nebula ambient waveforms peak very low (~0.1), so on their own they
    // were nearly inaudible once distance, EffectVolume, and MasterVolume stacked on top. These lift
    // them to a clearly audible level (gains above 1 amplify the quiet buffers), with a per-voice
    // ceiling so several overlapping ambients can't clip the mix.

    /// <summary>Overall boost applied to proximity-ambient voices so the universe is audible up close.</summary>
    public const float AmbientGain = 3.5f;

    /// <summary>Per-voice ceiling for a proximity ambient, keeping overlapping ambients from clipping.</summary>
    public const float AmbientMaxVoiceGain = 2.8f;

    /// <summary>Playback gain for the "Learn Sounds" sound dictionary, so demos are clearly audible.</summary>
    public const float LearnSoundGain = 2.5f;

    // --- Tuning-by-ear beat cue ---
    // A reference tone at the selected realm's target pitch, tremolo'd at the detuning rate so the
    // player can tune by ear: it pulses fast when far off, slows as they approach, and goes steady
    // at perfect resonance.

    /// <summary>Detuning (Hz) within which the beat-frequency tuning cue emerges (it stays silent beyond this).</summary>
    public const float BeatCueRange = 25f;

    /// <summary>Amplitude of the tuning cue — subtle; further scaled by closeness and the tremolo envelope.</summary>
    public const float BeatCueVolume = 0.07f;

    /// <summary>Detuning (Hz) within which the tremolo smoothly fills up to a full, steady "locked" tone.</summary>
    public const float BeatLockZone = 3f;

    // --- Doppler (positional world voices) ---
    // Approaching a world sound shifts its pitch up, receding shifts it down (a simple radial-speed
    // approximation). Applied to OpenAL voices only; the NAudio fallback has no pitch control.

    /// <summary>Pitch shift per unit of radial speed toward a source.</summary>
    public const float DopplerScale = 0.02f;

    /// <summary>Lower clamp on Doppler pitch (receding fast).</summary>
    public const float DopplerMinPitch = 0.7f;

    /// <summary>Upper clamp on Doppler pitch (approaching fast).</summary>
    public const float DopplerMaxPitch = 1.4f;

    #endregion

    #region Celestial body generation

    public const int NStars = 200;
    public const int NPlanetsPerStar = 3;
    public const int NNebulae = 10;
    public const float OrbitRadius = 5f;
    public const float PlanetRadius = 10f;
    public const float InteractionDistance = 15f;

    #endregion

    #region Fibonacci sequence and derived costs

    // Fibonacci sequence (F(0) through F(NFibonacci-1))
    // FibSeq is used for celestial/crystal positioning; UpgradeCosts is derived from it (F(1)..F(8))
    public const int NFibonacci = 9; // 0,1,1,2,3,5,8,13,21
    /// <summary>Fibonacci sequence F(0)..F(NFibonacci-1), built in the static constructor. Drives spiral positioning scales.</summary>
    public static readonly int[] FibSeq;
    /// <summary>Scale factor that maps the largest Fibonacci term onto ~100 world units. Built in the static constructor.</summary>
    public static readonly float ScaleFactor;

    #endregion

    #region Speech and audio feedback

    public const float SpeechCooldown = 0.5f;
    public const float ViewLandmarkThreshold = 10f;
    public const float RotationSoundDuration = 0.2f;
    public const float LandmarkSpeechCooldown = 1f;
    public const float CursorSpeechCooldown = 0.2f;

    #endregion

    #region Landing and planet exploration

    public const float LandingThreshold = 0.8f;
    public const float LandingTime = 3f;
    public const int CrystalCountBase = 3;
    public const int GridSize = 10;
    public const float CrystalCollectionThreshold = 0.8f;

    #endregion

    #region Resonance and power mechanics

    public const float PowerBuildThreshold = 0.8f;
    public const float PowerBuildTime = 5f;
    public const float DissonanceThreshold = 0.2f;
    public const float DissonanceDuration = 10f;
    public const float PerfectResonanceThreshold = 0.999f;

    #endregion

    #region Rift mechanics

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

    #endregion

    #region UI and display

    public const int HudTextSizeBase = 24;
    public const float ClickInterval = 0.5f;

    #endregion

    #region Upgrades and progression

    // Upgrades and progression — derived from FibSeq: F(1) through F(8)
    /// <summary>Crystal cost per upgrade tier, F(1)..F(8) = {1,1,2,3,5,8,13,21}. Built in the static constructor.</summary>
    public static readonly int[] UpgradeCosts;
    public const int AscensionCrystalThreshold = 21;

    #endregion

    #region Navigation and tuning

    public const float RotationSpeed = 3f;
    public const float TuningRate = 100f;
    public const float TuningRatePlanet = 20f;
    public const float ScannerRange = 50f;
    public const float SlowdownDist = 20f;
    public const float AutoSnapThreshold = 0.5f;
    public const float ApproachingLockThreshold = 10f;

    #endregion

    #region Speed modes

    /// <summary>Velocity scaling for each speed mode (Approach / Cruise / Quantum).</summary>
    public static readonly float[] SpeedFactors = { 0.3f, 0.6f, 1f };
    /// <summary>Display names for the speed modes, indexed in lockstep with <see cref="SpeedFactors"/>.</summary>
    public static readonly string[] SpeedModeNames = { "Approach", "Cruise", "Quantum" };

    #endregion

    #region Celestial body effects

    public const float StarHarmonyRadius = 12f;
    public const float StarMaxBenefitRadius = 6f;
    public const float NebulaDissonanceRadius = 10f;

    #endregion

    #region Special mechanics

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

    #endregion

    #region Zoom

    public const float ZoomMin = 0.2f;
    public const float ZoomMax = 5f;
    public const float ZoomStep = 0.1f;

    #endregion

    #region Harmonic relationship system

    public const float HarmonicTolerance = 0.02f;
    /// <summary>Frequency ratios for each detectable musical interval. A pair of dimensions whose frequencies match one of these (within <see cref="HarmonicTolerance"/>) triggers that harmonic's bonus.</summary>
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

    #endregion

    #region Harmonic series settings

    public const int NHarmonics = 7;
    public const float HarmonicFalloff = 1.5f;
    public const float SubharmonicDepth = 0.15f;
    public const float IntermodDepth = 0.08f;

    #endregion

    #region Stellar types

    /// <summary>Per-stellar-class data (color, frequency multiplier, description, ambient audio range).</summary>
    public static readonly Dictionary<StellarType, StellarTypeInfo> StellarTypes = new()
    {
        [StellarType.MainSequence] = new(new Color(255, 255, 200), 1f, "stable hydrogen-burning star", 200f, 400f),
        [StellarType.RedGiant] = new(new Color(255, 100, 50), 0.7f, "ancient bloated star", 30f, 50f),
        [StellarType.WhiteDwarf] = new(new Color(200, 220, 255), 1.8f, "dense stellar core", 1200f, 1500f),
        [StellarType.BrownDwarf] = new(new Color(100, 50, 30), 0.3f, "failed star", 20f, 30f),
    };
    /// <summary>Spawn weights for each stellar class (sum to 1.0).</summary>
    public static readonly Dictionary<StellarType, float> StellarTypeProbabilities = new()
    {
        [StellarType.MainSequence] = 0.70f,
        [StellarType.RedGiant] = 0.15f,
        [StellarType.WhiteDwarf] = 0.10f,
        [StellarType.BrownDwarf] = 0.05f,
    };

    #endregion

    #region Nebula types

    /// <summary>Per-nebula-class data (color, frequency range, dissonance level, description).</summary>
    public static readonly Dictionary<NebulaType, NebulaTypeInfo> NebulaTypes = new()
    {
        [NebulaType.Emission] = new(new Color(255, 50, 50), 200f, 300f, 0.5f, "ionized gas cloud"),
        [NebulaType.Reflection] = new(new Color(50, 150, 255), 600f, 800f, 0.3f, "dust reflecting starlight"),
        [NebulaType.Planetary] = new(new Color(150, 255, 150), 400f, 600f, 0.4f, "dying star shell"),
        [NebulaType.SupernovaRemnant] = new(new Color(255, 150, 100), 100f, 900f, 0.9f, "expanding blast wave"),
    };
    /// <summary>Spawn weights for each nebula class (sum to 1.0).</summary>
    public static readonly Dictionary<NebulaType, float> NebulaTypeProbabilities = new()
    {
        [NebulaType.Emission] = 0.40f,
        [NebulaType.Reflection] = 0.30f,
        [NebulaType.Planetary] = 0.20f,
        [NebulaType.SupernovaRemnant] = 0.10f,
    };

    #endregion

    #region Exoplanet types

    /// <summary>Per-exoplanet-class data (size multiplier, crystal multiplier, landing difficulty, description).</summary>
    public static readonly Dictionary<ExoplanetType, ExoplanetTypeInfo> ExoplanetTypes = new()
    {
        [ExoplanetType.HotJupiter] = new(3f, 0.5f, 1.5f, "scorching gas giant"),
        [ExoplanetType.SuperEarth] = new(1.5f, 1.2f, 1f, "massive rocky world"),
        [ExoplanetType.OceanWorld] = new(1.2f, 1.5f, 0.8f, "water-covered planet"),
        [ExoplanetType.RoguePlanet] = new(1f, 2f, 2f, "sunless wanderer"),
        [ExoplanetType.IceGiant] = new(2.5f, 0.8f, 1.3f, "frozen methane world"),
    };
    /// <summary>Spawn weights for each exoplanet class (sum to 1.0).</summary>
    public static readonly Dictionary<ExoplanetType, float> ExoplanetTypeProbabilities = new()
    {
        [ExoplanetType.SuperEarth] = 0.35f,
        [ExoplanetType.HotJupiter] = 0.25f,
        [ExoplanetType.IceGiant] = 0.20f,
        [ExoplanetType.OceanWorld] = 0.15f,
        [ExoplanetType.RoguePlanet] = 0.05f,
    };

    #endregion

    #region Solfeggio Frequencies

    /// <summary>The ten sacred Solfeggio tones (keyed by Hz). Tuning a drive within <see cref="SolfeggioTolerance"/> of one grants its effect.</summary>
    public static readonly Dictionary<int, SolfeggioInfo> SolfeggioFrequencies = new()
    {
        [174] = new("Foundation", SolfeggioEffect.PainRelief, "natural anesthetic", "shield", 1.1f),
        [285] = new("Quantum", SolfeggioEffect.TissueHealing, "cellular regeneration", "minor_heal", 0.5f),
        [396] = new("Liberation", SolfeggioEffect.ReleaseFear, "liberating guilt and fear", "stability", 1.2f),
        [417] = new("Transmutation", SolfeggioEffect.FacilitateChange, "undoing situations", "rift_assist", 1.15f),
        [432] = new("Natural Harmony", SolfeggioEffect.UniversalTuning, "cosmic frequency", "base_heal", 1f),
        [528] = new("Miracle", SolfeggioEffect.Transformation, "DNA repair, love frequency", "major_heal", 2f),
        [639] = new("Connection", SolfeggioEffect.Relationships, "harmonizing connections", "comm_boost", 1.3f),
        [741] = new("Awakening", SolfeggioEffect.Expression, "awakening intuition", "rift_detect", 1.4f),
        [852] = new("Intuition", SolfeggioEffect.SpiritualOrder, "returning to spiritual order", "third_eye", 1.25f),
        [963] = new("Divine", SolfeggioEffect.Oneness, "connection to Source", "transcend", 1.5f),
    };
    public const float SolfeggioTolerance = 5f;

    #endregion

    #region Crystal Color Spectrum

    /// <summary>Crystal types keyed by name, each mapping a frequency band to a chakra color and gameplay bonus.</summary>
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

    #endregion

    #region Temple Resonance

    public const float TempleResonanceFreq = 110f;
    public static readonly (float Min, float Max) TempleResonanceRange = (95f, 120f);
    public const int TempleCount = 7;
    public const float TempleHealingRate = 0.02f;
    public const float TempleConsciousnessBoost = 1.5f;

    #endregion

    #region Merkaba Activation

    public const float MerkabaActivationThreshold = 0.9f;
    public const float MerkabaShieldStrength = 0.5f;
    public const float MerkabaVelocityBoost = 1.3f;
    public const float MerkabaDetectionRange = 2f;

    #endregion

    #region Tuaoi Crystal Modes

    /// <summary>The six faces of the Tuaoi Stone, each a selectable drive mode with its own frequency, color, and effect.</summary>
    public static readonly Dictionary<TuaoiMode, TuaoiModeInfo> TuaoiModes = new()
    {
        [TuaoiMode.Healing] = new(432f, new Color(0, 255, 128), "integrity_regen", 0.01f, "Atlantean healing frequency"),
        [TuaoiMode.Navigation] = new(PHI * 256f, new Color(100, 150, 255), "enhanced_autopilot", 1.5f, "Golden ratio navigation"),
        [TuaoiMode.Communication] = new(7.83f, new Color(255, 200, 100), "expanded_scan", 2f, "Earth resonance connection"),
        [TuaoiMode.Power] = new(528f, new Color(255, 100, 100), "velocity_boost", 1.25f, "Miracle frequency power"),
        [TuaoiMode.Regeneration] = new(285f, new Color(200, 100, 255), "resonance_recovery", 1.3f, "Cellular regeneration frequency"),
        [TuaoiMode.Transcendence] = new(963f, new Color(255, 255, 200), "higher_dim_sensitivity", 1.4f, "Divine connection frequency"),
    };
    /// <summary>Cycle order for the G key, defining the index that <see cref="TuaoiModeInfoByIndex"/> follows.</summary>
    public static readonly TuaoiMode[] TuaoiModeOrder = { TuaoiMode.Healing, TuaoiMode.Navigation, TuaoiMode.Communication, TuaoiMode.Power, TuaoiMode.Regeneration, TuaoiMode.Transcendence };
    /// <summary>Tuaoi mode info pre-flattened into <see cref="TuaoiModeOrder"/> order for fast indexed lookup. Built in the static constructor.</summary>
    public static readonly TuaoiModeInfo[] TuaoiModeInfoByIndex;
    public const float TuaoiModeSwitchCooldown = 2f;

    #endregion

    #region Halls of Amenti

    public const float AmentiResonanceThreshold = 0.95f;
    public const float AmentiTimeDilation = 0.5f;
    public const float AmentiWisdomBonus = 2f;
    /// <summary>World position of the master temple — the universe center (origin).</summary>
    public static readonly float[] HallsOfAmentiPos = Vec5.Zero();

    #endregion

    #region Sacred Geometry Patterns

    /// <summary>Crystal-arrangement patterns keyed by name, each with its point count and completion bonus.</summary>
    public static readonly Dictionary<SacredGeometryPattern, SacredPatternInfo> SacredPatterns = new()
    {
        [SacredGeometryPattern.VesicaPiscis] = new(2, "creation", 1.2f),
        [SacredGeometryPattern.SeedOfLife] = new(7, "crystal_regen", 1.5f),
        [SacredGeometryPattern.FlowerOfLife] = new(19, "all_harmonics", 2f),
        [SacredGeometryPattern.MetatronsCube] = new(13, "max_resonance", 1.8f),
        [SacredGeometryPattern.Merkaba] = new(8, "protection", 1.6f),
        [SacredGeometryPattern.GoldenSpiral] = new(5, "phi_stacking", PHI),
    };

    #endregion

    #region Brainwave States

    /// <summary>Consciousness states keyed by brainwave band, each detected by frequency and granting an effect.</summary>
    public static readonly Dictionary<BrainwaveState, BrainwaveStateInfo> BrainwaveStates = new()
    {
        [BrainwaveState.Delta] = new(0.5f, 4f, "deep_healing", BrainwaveEffect.AutoRepair, 2f),
        [BrainwaveState.Theta] = new(4f, 8f, "meditation", BrainwaveEffect.RiftVision, 1.5f),
        [BrainwaveState.Alpha] = new(8f, 13f, "relaxed_focus", BrainwaveEffect.EnhancedScan, 1.3f),
        [BrainwaveState.Beta] = new(13f, 30f, "active", BrainwaveEffect.FastTuning, 1.2f),
        [BrainwaveState.Gamma] = new(30f, 100f, "transcendence", BrainwaveEffect.AllBonus, 1.4f),
    };

    #endregion

    #region Ley Line Highways

    public const int LeyLineCount = 12;
    public const float LeyLineSpeedMult = 3f;
    public const float LeyLineWidth = 8f;
    public const float LeyLineDetectionRange = 25f;
    public const float LeyLineFreq = 432f;

    #endregion

    #region Portal Anchor System

    public const int MaxPortalAnchors = 7;
    public const int PortalAnchorCost = 3;
    public const float PortalTravelResonance = 0.85f;
    public const float PortalCooldown = 30f;

    #endregion

    #region Crystal Activation Sequences

    public const int ActivationSequenceLength = 5;
    /// <summary>The frequency sequence (Hz) that must be played in order to trigger a crystal activation bonus.</summary>
    public static readonly int[] ActivationFrequencies = { 396, 417, 528, 639, 741 };
    public const float ActivationTolerance = 8f;
    public const float ActivationTimeLimit = 30f;
    public const float ActivationRewardMult = 2f;

    #endregion

    #region 12+1 Temple System

    public const int MinorTempleCount = 12;
    /// <summary>Zodiac names for the 12 minor temple keys, indexed in lockstep with <see cref="TempleKeyFrequencies"/>.</summary>
    public static readonly string[] TempleKeyNames =
    {
        "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
        "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces"
    };
    /// <summary>Resonance frequency (Hz) for each zodiac temple, indexed in lockstep with <see cref="TempleKeyNames"/>.</summary>
    public static readonly int[] TempleKeyFrequencies =
    {
        396, 417, 432, 444, 480, 512, 528, 576, 594, 639, 672, 741
    };
    public const int MasterTempleUnlockKeys = 12;

    #endregion

    #region Pyramid Resonance Chambers

    public const float PyramidResonanceFreq = 118f;
    public static readonly (float Min, float Max) PyramidResonanceRange = (117f, 121f);
    public const float PyramidHealingMult = 3f;
    public const float PyramidConsciousnessBoost = 2f;
    public const int PyramidCount = 3;

    #endregion

    #region Atlantean Crystal Types

    /// <summary>Rare special crystals keyed by name, each with a color, frequency band, unique effect, and lore description.</summary>
    public static readonly Dictionary<string, AtlanteanCrystalTypeInfo> AtlanteanCrystalTypes = new()
    {
        ["fire_crystal"] = new(new Color(255, 69, 0), 200f, 300f, CrystalEffect.VelocityBurst, 2f, "Volcanic energy crystal from Atlantean forges"),
        ["aquamarine"] = new(new Color(127, 255, 212), 300f, 400f, CrystalEffect.ShieldBoost, 1.5f, "Ocean-born crystal of protection"),
        ["larimar"] = new(new Color(135, 206, 235), 400f, 500f, CrystalEffect.Communication, 1.8f, "Dolphin stone of ancient Atlantean wisdom"),
        ["moldavite"] = new(new Color(154, 205, 50), 500f, 600f, CrystalEffect.Transformation, 2.5f, "Extraterrestrial glass of rapid evolution"),
        ["lemurian_seed"] = new(new Color(255, 182, 193), 600f, 700f, CrystalEffect.MemoryUnlock, 2f, "Ancient knowledge carrier from Lemuria"),
        ["black_tourmaline"] = new(new Color(47, 79, 79), 100f, 200f, CrystalEffect.Purification, 1.3f, "Protective stone against negative frequencies"),
        ["celestite"] = new(new Color(176, 224, 230), 700f, 800f, CrystalEffect.AngelicConnection, 1.7f, "Bridge to higher realms and celestial beings"),
    };
    public const float AtlanteanCrystalChance = 0.15f;

    #endregion

    #region Consciousness Level System

    /// <summary>The six consciousness tiers, each with the resonance threshold to reach it and a stat multiplier.</summary>
    public static readonly Dictionary<ConsciousnessLevel, ConsciousnessLevelInfo> ConsciousnessLevels = new()
    {
        [ConsciousnessLevel.Dormant] = new(0f, 1f, "Unawakened state"),
        [ConsciousnessLevel.Awakening] = new(0.3f, 1.2f, "Beginning to sense the harmonics"),
        [ConsciousnessLevel.Aware] = new(0.5f, 1.4f, "Consciously navigating frequencies"),
        [ConsciousnessLevel.Attuned] = new(0.7f, 1.6f, "Deeply connected to cosmic vibrations"),
        [ConsciousnessLevel.Enlightened] = new(0.85f, 1.8f, "Mastery of harmonic navigation"),
        [ConsciousnessLevel.Ascended] = new(0.95f, 2f, "One with the universal frequency"),
    };
    /// <summary>Low-to-high ordering of consciousness tiers, used to detect level-up/level-down transitions.</summary>
    public static readonly ConsciousnessLevel[] ConsciousnessLevelOrder = { ConsciousnessLevel.Dormant, ConsciousnessLevel.Awakening, ConsciousnessLevel.Aware, ConsciousnessLevel.Attuned, ConsciousnessLevel.Enlightened, ConsciousnessLevel.Ascended };
    public const float ConsciousnessGainRate = 0.001f;
    public const float ConsciousnessDecayRate = 0.0005f;

    #endregion

    #region Astral Projection Mode

    public const float AstralProjectionResonance = 0.9f;
    public const float AstralProjectionRange = 200f;
    public const float AstralSpeedMult = 5f;
    public const float AstralDuration = 30f;
    public const float AstralCooldown = 60f;

    #endregion

    #region Intention-Based Navigation

    public const float IntentionActivationTime = 5f;
    public const float IntentionResonanceThreshold = 0.8f;
    public const float IntentionRange = 100f;
    public const float IntentionPrecision = 0.9f;

    #endregion

    #region Cymatics Visualization

    public const bool CymaticsEnabled = true;
    /// <summary>Cymatic figures keyed by name, each mapping a frequency band to a symmetry value for visualization.</summary>
    public static readonly Dictionary<CymaticsPattern, CymaticsPatternInfo> CymaticsPatterns = new()
    {
        [CymaticsPattern.Hexagon] = new(200f, 300f, 6f),
        [CymaticsPattern.Star] = new(300f, 400f, 5f),
        [CymaticsPattern.Flower] = new(400f, 500f, 12f),
        [CymaticsPattern.Mandala] = new(500f, 600f, 8f),
        [CymaticsPattern.Spiral] = new(600f, 700f, PHI),
        [CymaticsPattern.Merkaba] = new(700f, 800f, 24f),
    };

    #endregion

    #region Static initialization

    /// <summary>
    /// Static constructor — builds the runtime-computed tables: the Fibonacci sequence and the
    /// arrays derived from it (<see cref="UpgradeCosts"/>, <see cref="ScaleFactor"/>), plus the
    /// flattened <see cref="TuaoiModeInfoByIndex"/> lookup.
    /// </summary>
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

    #endregion
}
