using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// Ship class managing all game state and logic including physics, navigation,
/// upgrades, landing, rift interaction, and UI. Converted from Python ship.py (~2400 lines).
/// </summary>
public partial class Ship
{
    #region Constants & external references

    // --- Constants shorthand ---
    // Short local aliases for the handful of game constants used all over this class,
    // so the physics reads cleanly (N instead of GameConstants.NDimensions, etc.).
    private const int N = GameConstants.NDimensions;
    private const float PHI = GameConstants.PHI;
    private const float DT = GameConstants.Dt;

    // --- External references ---
    // The services the ship leans on: the NAudio engine (sounds/waveforms), the OpenAL spatial
    // engine (positioned world sounds), and the screen reader (reached via the event bus in Speak()).
    private readonly AudioSystem _audio;
    private readonly OpenAlAudio _openAl;
    private readonly TolkSpeechService _tolk;

    #endregion

    #region Public state (accessible by renderer / Game1)

    // Core position & movement
    public float[] Position = Vec5.Zero();
    public float[] Velocity = Vec5.Zero();
    public float Heading;

    // Drive & target frequencies
    public float[] RDrive = new float[N];
    public float[] BaseFTarget = new float[N];
    public float[] FTarget = new float[N];

    // Tuning
    public int SelectedDim;
    public bool TuningMode;

    // Proximity & resonance
    public bool NearObject;
    public float[] ResonanceLevels = new float[N];

    // View & rotation
    public float ViewRotation;
    public bool RotatingLeft;
    public bool RotatingRight;
    private float _lastRotationSoundTime;
    private float _lastLandmarkSpeakTime;
    private float _lastApproachingBeepTime;

    // Landing & exploration
    public bool LandedMode;
    public float[]? LandedPlanet;
    public CelestialBody? LandedPlanetBody;
    public float LandingTimer;
    public float ResonanceIntegrity = 1f;
    public int CrystalsCollected;

    // Power & dissonance
    public float[] ResonancePower = new float[N];
    public float DissonanceTimer;

    // UI settings
    public int VerboseMode = 1;
    public int HudTextSize = GameConstants.HudTextSizeBase;
    public bool HighContrast;
    public bool AutosaveEnabled = true;
    public bool AmbientSoundsEnabled = true;
    public bool NebulaDissonanceEnabled = true;
    private float _lastAutosaveTime;

    // Upgradable attributes
    public float ResonanceWidth = GameConstants.ResonanceWidthBase;
    public float MaxVelocity = GameConstants.MaxVelocityBase;
    public int CrystalCount = GameConstants.CrystalCountBase;
    public int CrystalBonus;

    // Previous resonance
    private float[] _prevResonanceLevels = new float[N];

    // Rifts
    public List<Rift> Rifts = new();

    // Debounce flags (replaced by edge-detection via prevKeys)
    // (No longer needed as individual flags; we use IsKeyPressed pattern)

    // Active menu (polymorphic — null when no menu is open). See MenuMode.
    public MenuMode? ActiveMenu;

    // HUD menu state (HudItems doubles as the persistent gameplay HUD read-out).
    public int HudIndex;
    public List<string> HudItems = new();

    // Planet exploration
    public float[] CursorPos = new float[2]; // x, y
    public List<float[]> CrystalPositions = new();
    public List<CrystalData> CrystalFreqs = new();
    public HashSet<int> LockedCrystals = new();
    public PlanetBiome Biome = PlanetBiome.Harmonic;
    private bool _approachingLockAnnounced;

    // Upgrades
    public bool GoldenHarmonyActive;

    // Starmap
    public int StarmapIndex;
    public List<StarmapItem> StarmapItems = new();
    public float[]? LockedTarget;
    public WorldSound? LockSound;   // target-lock homing beacon (positional world sound)
    public bool LockedIsRift;

    // Rift selection
    public int RiftSelectionIndex;
    public List<RiftMenuItem> RiftItems = new();
    public Rift? LockedRift;

    // Misc state
    private float[] _lastCursorPos = new float[2];
    private float _lastCursorSpeakTime;
    public CelestialBody? NearestBody;
    public float ShipHeading;
    public float Pitch;
    public int SpeedMode = 2;

    // Rift charge & guidance
    public float RiftChargeTimer;
    private float _lastGuidanceTime;
    private bool _approachedRiftAnnounced;
    private float _prevRiftDist = float.MaxValue;
    private float _prevRiftAlign;
    private float _prevRiftRes;

    // Proximity ambient sounds — positional world sounds (OpenAL/HRTF when available, NAudio fallback).
    private WorldSound? _starSound;
    private WorldSound? _nebulaSound;
    private WorldSound? _planetSound;

    // Idle mode
    private float _lastInputTime;
    public bool IdleMode;

    // Biome sound
    private GameSoundEffect? _biomeSound;

    // Water blessing
    private float _spacebarHoldTimer;
    private bool _spacebarPressed;

    // Speech cooldown tracking
    private readonly Dictionary<string, float> _lastSpoken = new();
    public float SimulationTime;
    private float _lastBeepTime = -1f;
    public bool EasterEggAnnounced;

    // Universe regeneration flag
    public bool NeedsUniverseRegeneration;

    // Harmonic tracking
    public Dictionary<string, (HarmonicType Type, int[] Dims, float Expiry)> ActiveHarmonics = new();
    private float _lastHarmonicCheck;

    #region Atlantean state

    // Tuaoi crystal: the six-sided stone the player retunes (press G) for different tactical bonuses.
    public TuaoiMode TuaoiMode = TuaoiMode.Navigation;
    public int TuaoiModeIndex = 1;
    private TuaoiModeInfo _cachedTuaoiInfo = null!;
    private float _lastTuaoiSwitch;

    // Merkaba: the protective light-vehicle field that switches on when every dimension is in tune.
    public bool MerkabaActive;
    private bool _merkabaAnnounced;

    // Solfeggio: sacred frequencies the drive can land on, each granting a timed effect.
    public Dictionary<int, (SolfeggioEffect Effect, float Expiry)> ActiveSolfeggio = new();
    private float _lastSolfeggioCheck;

    // Temple resonance: the 110 Hz ancient-healing band.
    public bool InTempleResonance;
    private bool _templeAnnounced;

    public BrainwaveState CurrentBrainwave = BrainwaveState.Beta;
    private bool _consciousnessAnnounced;

    // Sacred geometry — which pattern (if any) the current planet's crystals form.
    public SacredGeometryPattern? CurrentPattern;
    public List<int> PatternProgress = new();
    public float PatternBonusTimer;

    // Temple keys
    public HashSet<int> TempleKeys = new();
    private float _lastTempleCheck;
    public Temple? NearTemple;
    private bool _templeNearbyAnnounced;
    private bool _amentiSealedAnnounced;

    // Ley lines
    public bool OnLeyLine;
    public LeyLine? CurrentLeyLine;
    private bool _leyLineAnnounced;

    // Portal anchors
    public List<PortalAnchor> PortalAnchors = new();
    private float _lastPortalUse;

    // Pyramid
    public Pyramid? NearPyramid;
    private bool _pyramidAnnounced;

    // Consciousness value
    public float ConsciousnessValue = 0.3f;
    public ConsciousnessLevel ConsciousnessStage = ConsciousnessLevel.Awakening;

    // Astral projection
    public bool AstralMode;
    public float[]? AstralBodyPos;
    public float AstralTimer;
    private float _lastAstralReturn;
    private bool _astralTooFar;

    // Intention navigation
    public bool IntentionActive;
    public float IntentionTimer;
    public float[]? IntentionTarget;

    // Halls of Amenti
    public bool VisitedAmenti;
    public bool AmentiBlessingActive;

    // Frequency presets
    public Dictionary<int, float[]> FrequencyPresets = new();
    private int? _pendingPresetOverwrite;
    private float _pendingPresetTime;

    // Celestial body references (set by Game1 after universe generation)
    public List<CelestialBody> Stars = new();
    public List<CelestialBody> Planets = new();
    public List<CelestialBody> Nebulae = new();

    // Nebula dissonance announcement
    private bool _nebulaDissonanceAnnounced;

    // Pre-allocated buffers for Update() to avoid per-frame GC pressure
    private readonly float[] _envInfluence = new float[N];
    private readonly float[] _dists = new float[N];
    private readonly float[] _dirVecBuffer = new float[N];
    private readonly List<int> _expiredKeys = new();
    private readonly List<CelestialBody> _nearbyBuffer = new();

    // Spatial grid for fast proximity queries (set by SpaceSimGame each frame)
    public SpatialGrid? SpatialGrid;

    // Zoom (managed externally but stored here for convenience)
    public float ZoomLevel = 1f;

    #endregion

    #endregion

    #region Properties

    /// <summary>True while any screen-reader menu (HUD, upgrades, starmap, rifts) is open.</summary>
    public bool IsInMenuMode => ActiveMenu != null;

    /// <summary>True while the player is holding the charge to enter a locked rift.</summary>
    public bool IsChargingRift => RiftChargeTimer > 0;

    /// <summary>Charge completion as 0..1, for the renderer to show a progress indicator.</summary>
    public float RiftChargeProgress =>
        RiftChargeTimer > 0 ? 1f - RiftChargeTimer / GameConstants.RiftChargeTime : 0f;

    /// <summary>Index of the highlighted row in the active menu (0 when no menu is open).</summary>
    public int MenuSelectedIndex => ActiveMenu?.SelectedIndex ?? 0;

    /// <summary>Row labels of the active menu (empty when no menu is open).</summary>
    public IReadOnlyList<string> MenuItems => ActiveMenu?.ItemLabels ?? Array.Empty<string>();

    #endregion

    #region Upgrade definitions

    /// <summary>One purchasable upgrade: its name, crystal cost, the action that applies it, and a description.</summary>
    private struct UpgradeDef
    {
        public string Name;
        public int Cost;
        public Action Effect;
        public string Desc;
    }

    private UpgradeDef[] _upgrades;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a ship: wires up the audio and speech services, seeds the drive and target
    /// frequencies to random starting values, builds the upgrade list, and selects the
    /// default Tuaoi mode.
    /// </summary>
    public Ship(AudioSystem audioSystem, OpenAlAudio openAl, TolkSpeechService tolk)
    {
        _audio = audioSystem;
        _openAl = openAl;
        _tolk = tolk;

        // Initialize frequencies
        for (int i = 0; i < N; i++)
        {
            RDrive[i] = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);
            BaseFTarget[i] = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);
            FTarget[i] = BaseFTarget[i];
        }

        // Build upgrade list
        _upgrades = new UpgradeDef[]
        {
            new() { Name = "Resonance Width", Cost = GameConstants.UpgradeCosts[0], Effect = UpgradeWidth, Desc = "Increases tuning tolerance by golden increment." },
            new() { Name = "Integrity Repair", Cost = GameConstants.UpgradeCosts[1], Effect = UpgradeIntegrity, Desc = "Restores ship harmony." },
            new() { Name = "Max Velocity", Cost = GameConstants.UpgradeCosts[2], Effect = UpgradeVelocity, Desc = "Boosts top speed with divine proportion." },
            new() { Name = "Auto-Tune Helper", Cost = GameConstants.UpgradeCosts[3], Effect = AutoTune, Desc = "Subtly aligns frequencies automatically." },
            new() { Name = "Crystal Growth", Cost = GameConstants.UpgradeCosts[4], Effect = UpgradeCrystalCount, Desc = "Increases crystals per planet." },
            new() { Name = "Golden Harmony Mode", Cost = GameConstants.UpgradeCosts[5], Effect = ActivateGoldenHarmony, Desc = "Permanent PHI multiplier to all stats for ascension prep." },
        };

        _lastInputTime = SimulationTime;
        SetTuaoiMode(TuaoiMode.Navigation);

        DebugLogger.Log("Ship", $"Ship created. RDrive=[{RDrive[0]:F1},{RDrive[1]:F1},{RDrive[2]:F1},{RDrive[3]:F1},{RDrive[4]:F1}]");
        DebugLogger.Log("Ship", $"  FTarget=[{FTarget[0]:F1},{FTarget[1]:F1},{FTarget[2]:F1},{FTarget[3]:F1},{FTarget[4]:F1}]");
    }

    #endregion

    #region Speech helper

    /// <summary>
    /// Announces a message through the screen reader, but only if the same message hasn't been
    /// spoken within the cooldown window — this is what keeps per-frame alerts from spamming.
    /// The actual speaking is done by whoever is listening on the event bus.
    /// </summary>
    public void Speak(string msg)
    {
        if (_lastSpoken.TryGetValue(msg, out float last) &&
            SimulationTime - last < GameConstants.SpeechCooldown)
            return;
        _lastSpoken[msg] = SimulationTime;
        GameEvents.RaiseSpeak(this, msg);
    }

    #endregion

    #region Utility methods

    /// <summary>Switches the Tuaoi crystal to a new mode and caches its info for fast per-frame lookups.</summary>
    public void SetTuaoiMode(TuaoiMode mode)
    {
        TuaoiMode = mode;
        _cachedTuaoiInfo = GameConstants.TuaoiModes[mode];
    }

    /// <summary>
    /// How far the ship can sense objects. Communication mode widens this (2x), letting the
    /// player detect temples, pyramids and bodies from further away.
    /// </summary>
    public float GetEffectiveScanRange()
    {
        float range = GameConstants.InteractionDistance;
        if (TuaoiMode == TuaoiMode.Communication)
            range *= _cachedTuaoiInfo.Rate; // 2.0x
        return range;
    }

    /// <summary>
    /// Maps a frequency to its crystal type (Ruby through Quartz, by chakra band).
    /// Falls back to quartz for anything above the top of the spectrum.
    /// </summary>
    public (string Name, CrystalSpectrumInfo Info) GetCrystalType(float frequency)
    {
        foreach (var (name, info) in GameConstants.CrystalSpectrum)
        {
            if (frequency >= info.FreqMin && frequency < info.FreqMax)
                return (name, info);
        }
        return ("quartz", GameConstants.CrystalSpectrum["quartz"]);
    }

    /// <summary>Looks up the Atlantean-flavoured name for a plain term (e.g. "rift" -> "Harmonic Chamber").</summary>
    public string GetAtlanteanTerm(string term)
    {
        return GameConstants.AtlanteanTerms.TryGetValue(term.ToLower(), out var at) ? at : term;
    }

    #endregion

    #region Upgrade functions

    /// <summary>Widens the tuning tolerance by a golden-ratio increment, making frequencies easier to match.</summary>
    public void UpgradeWidth() => ResonanceWidth += PHI * 0.5f;

    /// <summary>Restores ship harmony (integrity) by a golden-ratio amount, capped at full.</summary>
    public void UpgradeIntegrity() =>
        ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + PHI * 0.2f);

    /// <summary>Boosts top speed by the golden ratio.</summary>
    public void UpgradeVelocity() => MaxVelocity *= PHI;

    /// <summary>Nudges every drive frequency a tenth of the way toward its target — a gentle auto-align.</summary>
    public void AutoTune()
    {
        for (int i = 0; i < N; i++)
            RDrive[i] += (FTarget[i] - RDrive[i]) * 0.1f;
    }

    /// <summary>Increases the number of crystals each planet yields.</summary>
    public void UpgradeCrystalCount() => CrystalBonus += 1;

    /// <summary>The capstone upgrade: a permanent golden-ratio multiplier on speed and tuning width.</summary>
    public void ActivateGoldenHarmony()
    {
        GoldenHarmonyActive = true;
        MaxVelocity *= PHI;
        ResonanceWidth *= PHI;
        Speak("Golden Harmony activated. The universe sings in perfect proportion.");
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Projects a target's 5D position into the ship-relative 2D plane the player "sees", applying
    /// the current view rotation. The higher dimensions (indices 3 and 4) fold into the same x/y
    /// so they can be heard/seen as direction. Used for panning sounds and angle announcements.
    /// </summary>
    private (float X, float Y) ProjectRelative(float[] targetPos)
    {
        float[] rel = Vec5.Subtract(targetPos, Position);
        float cosR = MathF.Cos(ViewRotation);
        float sinR = MathF.Sin(ViewRotation);
        float x = rel[0] * cosR + rel[3] * sinR;
        float y = rel[1] * cosR + rel[4] * sinR;
        return (x, y);
    }

    /// <summary>Plain 2D (x/y only) distance between two 5D points.</summary>
    private static float Dist2D(float[] a, float[] b)
    {
        float dx = a[0] - b[0];
        float dy = a[1] - b[1];
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Upper-cases the first letter of a string (leaving the rest as-is).</summary>
    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    /// <summary>Turns a snake_case identifier into spaced, Capitalised words (e.g. "ocean_world" -> "Ocean World").</summary>
    private static string FormatName(string s) =>
        string.Join(' ', s.Split('_').Select(Capitalize));

    /// <summary>Formats a frequency array as a comma-separated, whole-number list for speech/HUD.</summary>
    private static string FormatFreqs(float[] freqs) =>
        string.Join(", ", freqs.Select(f => $"{f:F0}"));

    #endregion

    #region Inner types for menus

    /// <summary>One row in the starmap scanner: a label, an optional target position, what kind of object it is, and any rift it points at.</summary>
    public class StarmapItem
    {
        public string Label = "";
        public float[]? Position;
        /// <summary>What kind of object this row represents (null for non-targetable rows).</summary>
        public StarmapItemKind? Kind;
        /// <summary>True for the synthetic "Unlock target" row that clears the current lock.</summary>
        public bool IsUnlockAction;
        public Rift? ItemRift;
    }

    /// <summary>One row in the rift-selection menu: a label, the rift's position, and the rift itself.</summary>
    public class RiftMenuItem
    {
        public string Label = "";
        public float[]? Position;
        /// <summary>True for the synthetic "Unlock rift" row that clears the current lock.</summary>
        public bool IsUnlockAction;
        public Rift? Rift;
    }

    /// <summary>A detected harmonic relationship: its interval type, the two dimensions involved, and their frequency ratio.</summary>
    public class HarmonicInfo
    {
        public HarmonicType HType;
        public int[] Dims = Array.Empty<int>();
        public float Ratio;
    }

    #endregion
}
