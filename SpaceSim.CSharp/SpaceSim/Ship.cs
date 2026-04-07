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
    // --- Constants shorthand ---
    private const int N = GameConstants.NDimensions;
    private const float PHI = GameConstants.PHI;
    private const float DT = GameConstants.Dt;

    // --- External references ---
    private readonly AudioSystem _audio;
    private readonly TolkSpeechService _tolk;

    // =========================================================================
    //  PUBLIC STATE  (accessible by renderer / Game1)
    // =========================================================================

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

    // HUD
    public bool HudMode;
    public int HudIndex;
    public List<string> HudItems = new();

    // Planet exploration
    public float[] CursorPos = new float[2]; // x, y
    public List<float[]> CrystalPositions = new();
    public List<CrystalData> CrystalFreqs = new();
    public HashSet<int> LockedCrystals = new();
    public string PlanetBiome = "harmonic";
    private bool _approachingLockAnnounced;

    // Upgrades
    public bool UpgradeMode;
    public bool GoldenHarmonyActive;

    // Starmap
    public bool StarmapMode;
    public int StarmapIndex;
    public List<StarmapItem> StarmapItems = new();
    public float[]? LockedTarget;
    public GameSoundEffect? LockSound;
    public bool LockedIsRift;

    // Rift selection
    public bool RiftSelectionMode;
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

    // Proximity ambient sounds (references for stopping)
    private GameSoundEffect? _starSound;
    private GameSoundEffect? _nebulaSound;
    private GameSoundEffect? _planetSound;

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
    public Dictionary<string, (int[] Dims, float Expiry)> ActiveHarmonics = new();
    private float _lastHarmonicCheck;

    // ===== ATLANTEAN STATE =====
    public string TuaoiMode = "navigation";
    public int TuaoiModeIndex = 1;
    private float _lastTuaoiSwitch;

    public bool MerkabaActive;
    private bool _merkabaAnnounced;

    public Dictionary<int, (string Effect, float Expiry)> ActiveSolfeggio = new();
    private float _lastSolfeggioCheck;

    public bool InTempleResonance;
    private bool _templeAnnounced;

    public string ConsciousnessLevel = "beta";
    private bool _consciousnessAnnounced;

    // Sacred geometry
    public string? CurrentPattern;
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
    public string ConsciousnessName = "awakening";

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

    // Zoom (managed externally but stored here for convenience)
    public float ZoomLevel = 1f;

    // =========================================================================
    //  PROPERTIES
    // =========================================================================

    public bool IsInMenuMode => HudMode || UpgradeMode || StarmapMode || RiftSelectionMode;

    public bool IsChargingRift => RiftChargeTimer > 0;

    public float RiftChargeProgress =>
        RiftChargeTimer > 0 ? 1f - RiftChargeTimer / GameConstants.RiftChargeTime : 0f;

    public int MenuSelectedIndex
    {
        get
        {
            if (RiftSelectionMode) return RiftSelectionIndex;
            if (StarmapMode) return StarmapIndex;
            return HudIndex;
        }
    }

    public List<string> MenuItems
    {
        get
        {
            if (RiftSelectionMode) return RiftItems.Select(r => r.Label).ToList();
            if (StarmapMode) return StarmapItems.Select(s => s.Label).ToList();
            return HudItems;
        }
    }

    // =========================================================================
    //  UPGRADE DEFINITIONS
    // =========================================================================

    private struct UpgradeDef
    {
        public string Name;
        public int Cost;
        public Action Effect;
        public string Desc;
    }

    private UpgradeDef[] _upgrades;

    // =========================================================================
    //  CONSTRUCTOR
    // =========================================================================

    public Ship(AudioSystem audioSystem, TolkSpeechService tolk)
    {
        _audio = audioSystem;
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

        DebugLogger.Log("Ship", $"Ship created. RDrive=[{RDrive[0]:F1},{RDrive[1]:F1},{RDrive[2]:F1},{RDrive[3]:F1},{RDrive[4]:F1}]");
        DebugLogger.Log("Ship", $"  FTarget=[{FTarget[0]:F1},{FTarget[1]:F1},{FTarget[2]:F1},{FTarget[3]:F1},{FTarget[4]:F1}]");
    }

    // =========================================================================
    //  SPEECH HELPER
    // =========================================================================

    public void Speak(string msg)
    {
        if (_lastSpoken.TryGetValue(msg, out float last) &&
            SimulationTime - last < GameConstants.SpeechCooldown)
            return;
        _lastSpoken[msg] = SimulationTime;
        GameEvents.RaiseSpeak(this, msg);
    }

    // =========================================================================
    //  UTILITY METHODS
    // =========================================================================

    public float GetEffectiveScanRange()
    {
        float range = GameConstants.InteractionDistance;
        if (TuaoiMode == "communication")
            range *= GameConstants.TuaoiModes["communication"].Rate; // 2.0x
        return range;
    }

    public (string Name, CrystalSpectrumInfo Info) GetCrystalType(float frequency)
    {
        foreach (var (name, info) in GameConstants.CrystalSpectrum)
        {
            if (frequency >= info.FreqMin && frequency < info.FreqMax)
                return (name, info);
        }
        return ("quartz", GameConstants.CrystalSpectrum["quartz"]);
    }

    public string GetAtlanteanTerm(string term)
    {
        return GameConstants.AtlanteanTerms.TryGetValue(term.ToLower(), out var at) ? at : term;
    }

    // =========================================================================
    //  UPGRADE FUNCTIONS
    // =========================================================================

    public void UpgradeWidth() => ResonanceWidth += PHI * 0.5f;

    public void UpgradeIntegrity() =>
        ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + PHI * 0.2f);

    public void UpgradeVelocity() => MaxVelocity *= PHI;

    public void AutoTune()
    {
        for (int i = 0; i < N; i++)
            RDrive[i] += (FTarget[i] - RDrive[i]) * 0.1f;
    }

    public void UpgradeCrystalCount() => CrystalBonus += 1;

    public void ActivateGoldenHarmony()
    {
        GoldenHarmonyActive = true;
        MaxVelocity *= PHI;
        ResonanceWidth *= PHI;
        Speak("Golden Harmony activated. The universe sings in perfect proportion.");
    }

    // =========================================================================
    //  HELPER METHODS
    // =========================================================================

    private (float X, float Y) ProjectRelative(float[] targetPos)
    {
        float[] rel = Vec5.Subtract(targetPos, Position);
        float cosR = MathF.Cos(ViewRotation);
        float sinR = MathF.Sin(ViewRotation);
        float x = rel[0] * cosR + rel[3] * sinR;
        float y = rel[1] * cosR + rel[4] * sinR;
        return (x, y);
    }

    private static float Dist2D(float[] a, float[] b)
    {
        float dx = a[0] - b[0];
        float dy = a[1] - b[1];
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string FormatName(string s) =>
        string.Join(' ', s.Split('_').Select(Capitalize));

    private static string FormatFreqs(float[] freqs) =>
        string.Join(", ", freqs.Select(f => $"{f:F0}"));

    // =========================================================================
    //  INNER TYPES FOR MENUS
    // =========================================================================

    public class StarmapItem
    {
        public string Label = "";
        public float[]? Position;
        public string? ItemType;
        public Rift? ItemRift;
    }

    public class RiftMenuItem
    {
        public string Label = "";
        public float[]? Position;
        public string? RiftType;
        public Rift? Rift;
    }

    public class HarmonicInfo
    {
        public string Name = "";
        public int[] Dims = Array.Empty<int>();
        public float Ratio;
    }
}
