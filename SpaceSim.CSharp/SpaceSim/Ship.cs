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

    /// <summary>
    /// Speed through the three SPATIAL realms only (dims 0-2), excluding tuning drift in the higher realms.
    /// The tutorial's fly step keys off this so that ONLY the player's own WASD thrust counts as "flying"
    /// (a realm detuned by the tutorial moves the ship through dims 4-5, which is not propulsion the player
    /// chose). This is deliberately distinct from — and must not replace — the canonical full-5D ship speed
    /// (<see cref="Vec5.Norm"/> of <see cref="Velocity"/>) used by the status report, HUD, dwell/stillness
    /// test, and breathing, all of which intentionally account for all five realms.
    /// </summary>
    public float SpatialSpeed =>
        MathF.Sqrt(Velocity[0] * Velocity[0] + Velocity[1] * Velocity[1] + Velocity[2] * Velocity[2]);

    // Drive & target frequencies.
    // INVARIANT: the audio thread (AudioSystem.Read) reads these element-wise without a lock, so they must be
    // MUTATED IN PLACE (Array.Copy into them) and NEVER reassigned for the live ship — a reassignment would let
    // the audio thread observe a torn or null reference and crash. Save/load and preset recall copy into them.
    public float[] RDrive = new float[N];
    public float[] BaseFTarget = new float[N];
    public float[] FTarget = new float[N];

    /// <summary>Per-realm frequency the by-ear cue and the fine tuning rate steer toward right now: a nearby
    /// claimable objective's note (temple key / pyramid band) when one is in range, else the realm's still
    /// centre BaseFTarget. Computed each frame in Update; the audio thread snapshots it.</summary>
    public readonly float[] CueTargetFreqs = new float[N];

    // Tuning
    public int SelectedDim = 3;     // realm 4 — the first of the two hand-tunable higher realms
    public bool TuningMode;

    // First-rest tuning nudge (in-memory, per session): teach the by-ear verb once, the first time the
    // pilot comes to rest after flying — unless they have already tuned a higher realm themselves.
    private bool _tuningHintGiven;
    private bool _hasTunedHigherRealm;
    private bool _hasFlownThisSession;

    /// <summary>Sim-time of the last by-ear tuning action (realm select or Up/Down). The audio thread reads
    /// this so the tune-by-ear cue is present while tuning and fades out shortly after — keeping the flight
    /// soundscape calm when you are not tuning.</summary>
    public float LastTuneTime = -999f;

    /// <summary>True while the interactive tutorial is running, so retargeting the cue/tuning-rate onto
    /// objective notes is suppressed (the tutorial's tuning steps are centre-based and would otherwise fight it).</summary>
    public bool TutorialActive;

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
    public bool ByEarMode;          // when true, numeric tuning targets aren't spoken — tune by ear
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

    // Portal anchor selection (Shift+P opens a pick-list of your dropped anchors)
    public int PortalSelectionIndex;
    public List<PortalMenuItem> PortalItems = new();

    // Misc state
    private float[] _lastCursorPos = new float[2];
    private float _lastCursorSpeakTime;
    public CelestialBody? NearestBody;
    public float ShipHeading;
    public float Pitch;
    public int SpeedMode;  // 0 = Approach: a gentle first speed for new pilots (not persisted)

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

    // Water blessing — a timed protective + healing aura earned by holding perfect five-realm resonance
    // for the full ritual. Counts down in UpdatePlaying; grants damage immunity while active.
    public float WaterBlessingTimer;

    // Temple keys
    public HashSet<int> TempleKeys = new();
    public Temple? NearTemple;
    private bool _templeNearbyAnnounced;
    private bool _amentiSealedAnnounced;
    private float _templeDwell;   // seconds a realm has been held on the nearby temple's note (claim dwell)

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
    private bool _pyramidEngagedAnnounced;   // latch for the "pyramid resonance engaged" tuning confirmation

    // Consciousness value
    public float ConsciousnessValue = 0.3f;
    public ConsciousnessLevel ConsciousnessStage = ConsciousnessLevel.Awakening;

    // Dwelling / regeneration bath — rewards holding resonance while nearly still (the meditative heart).
    public float DwellTimer;
    public bool InRegeneration;
    private float _lastDwellSwell;

    // Throttle for the per-second diagnostic state log.
    private float _lastDiagLog;

    // Throttle for the "perfect resonance" click so near-perfect tuning can't spam it every frame.
    private float _lastPerfectClick;

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
    // Atlantean structures — also referenced here so the scanner can list them as objectives.
    public List<Temple> Temples = new();
    public List<Pyramid> Pyramids = new();

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

        // Initialize frequencies. The three spatial realms start detuned (you fly to tune them); the two
        // higher realms start locked to their targets, so a new pilot's first encounter with by-ear tuning
        // is gentle tending as they breathe, not a from-scratch hunt across the whole frequency band.
        for (int i = 0; i < N; i++)
        {
            BaseFTarget[i] = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);
            FTarget[i] = BaseFTarget[i];
            RDrive[i] = i >= 3
                ? BaseFTarget[i]
                : MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);
        }
        Array.Copy(BaseFTarget, CueTargetFreqs, N);   // cue starts at each realm's centre until an objective is near

        // Build upgrade list
        _upgrades = new UpgradeDef[]
        {
            new() { Name = "Resonance Width", Cost = GameConstants.UpgradeCosts[0], Effect = UpgradeWidth, Desc = "Increases tuning tolerance by golden increment." },
            new() { Name = "Integrity Repair", Cost = GameConstants.UpgradeCosts[1], Effect = UpgradeIntegrity, Desc = "Restores ship harmony." },
            new() { Name = "Max Velocity", Cost = GameConstants.UpgradeCosts[2], Effect = UpgradeVelocity, Desc = "Boosts top speed with divine proportion." },
            new() { Name = "Auto-Tune Helper", Cost = GameConstants.UpgradeCosts[3], Effect = AutoTune, Desc = "Nudges your drives once toward their targets." },
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

    /// <summary>The most recent in-game announcement, so the player can replay it (Tab key).</summary>
    private string _lastAnnouncement = "";

    /// <summary>The most recent tutorial line. Tab-repeat prefers this while the tutorial is running, so the
    /// player's repeat key replays the instruction rather than a transient gameplay line that overwrote it.</summary>
    private string _lastTutorialLine = "";

    /// <summary>Channels of recorded messages the player can cycle and browse with the buffer keys.</summary>
    private readonly SpeechBuffers _speechBuffers = new();

    /// <summary>
    /// Announces a message through the screen reader, but only if the same message hasn't been
    /// spoken within the cooldown window — this is what keeps per-frame alerts from spamming.
    /// The actual speaking is done by whoever is listening on the event bus.
    /// </summary>
    public void Speak(string msg) => Speak(msg, SpeechChannel.General);

    /// <summary>
    /// Announces a message on a given <see cref="SpeechChannel"/>, honoring the per-message cooldown.
    /// The message is always recorded in its buffer for later browsing, but is only spoken aloud now if
    /// the player's active buffer is All or matches this channel (see <see cref="SpeechBuffers"/>).
    /// </summary>
    public void Speak(string msg, SpeechChannel channel)
    {
        if (_lastSpoken.TryGetValue(msg, out float last) &&
            SimulationTime - last < GameConstants.SpeechCooldown)
            return;
        _lastSpoken[msg] = SimulationTime;
        _lastAnnouncement = msg;
        if (_speechBuffers.Record(channel, msg))
            GameEvents.RaiseSpeak(this, msg);
    }

    // Channel-tagged shorthands so call sites read naturally and group their messages into buffers.
    // (Tuning is deliberately NOT a channel — tuning feedback is essential, so it is always spoken via Speak.)
    private void SpeakNav(string msg) => Speak(msg, SpeechChannel.Navigation);
    private void SpeakAtlantean(string msg) => Speak(msg, SpeechChannel.Atlantean);
    private void SpeakSystem(string msg) => Speak(msg, SpeechChannel.System);

    /// <summary>
    /// Speak an interactive-tutorial line. Non-interrupting by default, so consecutive tutorial lines queue
    /// and play in sequence (properly spaced) instead of cutting each other off; pass interrupt:true for an
    /// on-demand repeat (Shift+T) that should cut through other speech. Always spoken (essential) and
    /// recorded so it can be browsed/repeated.
    /// </summary>
    public void SpeakTutorial(string msg, bool interrupt = false)
    {
        _lastAnnouncement = msg;
        _lastTutorialLine = msg;
        _speechBuffers.Record(SpeechChannel.System, msg);
        GameEvents.RaiseSpeak(this, msg, interrupt);
    }

    /// <summary>Replays the most recent announcement (Tab), bypassing the cooldown so it always speaks. While
    /// the tutorial is running, prefers the last tutorial line so the repeat key gives the instruction back,
    /// not whatever transient line happened to come after it.</summary>
    public void RepeatLastAnnouncement()
    {
        string line = TutorialActive && !string.IsNullOrEmpty(_lastTutorialLine) ? _lastTutorialLine : _lastAnnouncement;
        if (!string.IsNullOrEmpty(line))
            GameEvents.RaiseSpeak(this, line);
    }

    /// <summary>Clear transient held-key latches when the sim is left, so a key still held across the
    /// transition (e.g. Space mid water-blessing ritual) can't keep its timer running on resume.</summary>
    public void ResetHeldInput()
    {
        _spacebarPressed = false;
        _spacebarHoldTimer = 0f;
        RotatingLeft = false;
        RotatingRight = false;
    }

    /// <summary>
    /// Cycles the active speech buffer ('[' and ']'). The announcement bypasses the buffer filter so the
    /// player always hears which buffer they have moved to.
    /// </summary>
    public void CycleSpeechBuffer(int dir)
        => GameEvents.RaiseSpeak(this, $"Buffer: {_speechBuffers.CycleView(dir)}.");

    /// <summary>
    /// Browses the active buffer's recorded history (',' and '.'), re-reading older or newer messages.
    /// Speaks directly so the chosen message is heard regardless of the live filter.
    /// </summary>
    public void BrowseSpeechBuffer(int dir)
    {
        string? msg = _speechBuffers.Browse(dir);
        GameEvents.RaiseSpeak(this, msg ?? (dir < 0 ? "Start of buffer." : "End of buffer."));
    }

    /// <summary>Reorders the buffers (Ctrl + '[' / ']'), moving the focused buffer earlier or later in the list.</summary>
    public void MoveSpeechBuffer(int dir)
        => GameEvents.RaiseSpeak(this, _speechBuffers.MoveActiveView(dir));

    #endregion

    #region Damage

    /// <summary>
    /// Apply a hit to resonance integrity — halved by the Merkaba light-vehicle shield while it's
    /// active, and clamped at zero. Every integrity loss routes through here so the shield is honoured
    /// everywhere (this is what makes the Merkaba's protection actually do something).
    /// </summary>
    private void ApplyIntegrityDamage(float amount)
    {
        // The water blessing fully shields the light vehicle for its duration.
        if (WaterBlessingTimer > 0f) return;
        if (MerkabaActive) amount *= 1f - GameConstants.MerkabaShieldStrength;
        ResonanceIntegrity = MathF.Max(0f, ResonanceIntegrity - amount);
    }

    #endregion

    #region Utility methods

    /// <summary>A spoken-friendly word for how close a realm's drive is to its target — for by-ear tuning.</summary>
    private static string ResonanceWord(float resonance) =>
        resonance > 0.9f ? "locked" : resonance > 0.6f ? "very close" : resonance > 0.3f ? "near" : "far";

    /// <summary>Switches the Tuaoi crystal to a new mode and caches its info for fast per-frame lookups.</summary>
    public void SetTuaoiMode(TuaoiMode mode)
    {
        TuaoiMode = mode;
        _cachedTuaoiInfo = GameConstants.TuaoiModes[mode];
    }

    /// <summary>
    /// Reset the transient flight state the interactive tutorial depends on, so it always begins from a
    /// clean, known state regardless of any prior play session. Stale velocity, a lock, landed mode, an
    /// astral projection, etc. would otherwise auto-complete or hard-break the early steps. Also suppresses
    /// the first-rest tuning nudge, which the tutorial's own tuning steps make redundant.
    /// </summary>
    public void PrepareForTutorial()
    {
        ResetTransientState();
        _hasTunedHigherRealm = true;   // the tutorial teaches tuning; don't also fire the first-rest nudge
        // The two higher realms are deliberately left as-is here for Begin() to detune for the hands-on steps.
    }

    /// <summary>
    /// Enter NORMAL play from a clean, known state. Same transient reset as the tutorial, but the two higher
    /// realms start IN tune (a fresh new game) and tutorial mode is cleared — so a prior Begin-Tutorial session
    /// can't leak its detuned realms / tutorial state into Start Sim (which otherwise made normal play feel like
    /// the tutorial). Progress (crystals, keys, consciousness, position) is preserved; only transient flight
    /// state is reset.
    /// </summary>
    public void PrepareForPlay()
    {
        ResetTransientState();
        RDrive[3] = BaseFTarget[3];    // higher realms in tune for normal play (the tutorial detunes them instead)
        RDrive[4] = BaseFTarget[4];
        _hasTunedHigherRealm = false;  // allow the gentle first-rest tuning nudge during normal play
        TutorialActive = false;        // make sure no prior tutorial run leaves the cue/rate retarget suppressed
    }

    /// <summary>
    /// Shared transient-state reset used when (re)entering the sim or the tutorial: clears velocity, rests the
    /// three spatial realms at their still centre, and drops any lock / landing / orbit / astral / idle / menu /
    /// full-tuning state plus the dwell and rift-charge timers. Leaves progress and the higher realms alone;
    /// callers decide what to do with realms 4 &amp; 5. Uses BaseFTarget (fixed centre), not the breathing FTarget.
    /// </summary>
    private void ResetTransientState()
    {
        Array.Clear(Velocity);
        for (int i = 0; i < 3; i++) RDrive[i] = BaseFTarget[i];
        LandedMode = false;
        LandedPlanet = null;
        LandedPlanetBody = null;
        LockedTarget = null;
        LockedBody = null;
        LockedRift = null;
        LockedIsRift = false;
        IsOrbiting = false;
        AstralMode = false;
        AstralBodyPos = null;
        IdleMode = false;
        ActiveMenu = null;
        TuningMode = false;
        SelectedDim = 3;
        RiftChargeTimer = 0f;
        InRegeneration = false;
        DwellTimer = 0f;
        _lastInputTime = SimulationTime;
        StopLockSound();
        SilenceAllWorldSounds();
    }

    /// <summary>
    /// How far the ship can sense objects. Grows with consciousness (the universe "opens" as the pilot
    /// awakens) and is widened further by Communication mode (2x), letting the player hear ambients and
    /// detect temples, pyramids, and bodies from further away.
    /// </summary>
    public float GetEffectiveScanRange()
    {
        float range = GameConstants.InteractionDistance * ConsciousnessHearingMult();
        if (TuaoiMode == TuaoiMode.Communication)
            range *= _cachedTuaoiInfo.Rate; // 2.0x
        return range;
    }

    /// <summary>
    /// Perception/hearing multiplier that rises with consciousness (1.0 dormant, up to 2.0 fully
    /// awakened). As the pilot ascends, distant ambient voices and objects become audible — the
    /// soundscape opens. Drawn from <see cref="ConsciousnessValue"/>, which is clamped to 0..1.
    /// </summary>
    public float ConsciousnessHearingMult() => 1f + ConsciousnessValue;

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
        SpeakAtlantean("Golden Harmony activated. The universe sings in perfect proportion.");
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
        /// <summary>The live body behind this row (star/planet/nebula), so a lock can track it as it moves.</summary>
        public CelestialBody? ItemBody;
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

    /// <summary>One row in the portal-anchor pick-list (Shift+P): a label and the index of the anchor it
    /// teleports to (-1 for the placeholder "no anchors" row).</summary>
    public class PortalMenuItem
    {
        public string Label = "";
        public int AnchorIndex = -1;
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
