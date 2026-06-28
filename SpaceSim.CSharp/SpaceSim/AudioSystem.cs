using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpaceSim;

/// <summary>
/// Real-time audio synthesis engine using NAudio.
/// Implements ISampleProvider to generate audio in its Read method (the audio callback).
/// Converts the Python sounddevice-based system to NAudio's pull model.
/// </summary>
public partial class AudioSystem : ISampleProvider, IDisposable
{
    #region Constants

    // --- Constants ---
    private const int SampleRate = GameConstants.SampleRate;
    private const int Channels = 2;
    private const float PHI = GameConstants.PHI;
    private const int NDimensions = GameConstants.NDimensions;
    private const float TwoPi = MathF.PI * 2f;

    #endregion

    #region NAudio plumbing

    // --- NAudio plumbing ---
    private WaveOutEvent? _waveOut;

    /// <summary>Chosen output device (NAudio MME device number; -1 = system default).</summary>
    private int _deviceNumber = -1;

    /// <summary>True while the output device is being swapped; Read() emits silence so a late/early callback
    /// can't glitch on half-torn-down state. Volatile: set by the game thread, read by the audio thread.</summary>
    private volatile bool _deviceSwitching;

    /// <summary>The currently selected output device number (-1 = system default).</summary>
    public int CurrentDeviceNumber => _deviceNumber;

    /// <summary>The interleaved 32-bit-float stereo format this provider emits (set in the constructor).</summary>
    public WaveFormat WaveFormat { get; }

    #endregion

    #region Audio thread state

    // --- Ship reference (volatile for lock-free audio thread read) ---
    private volatile Ship? _ship;

    /// <summary>
    /// When false, the continuous resonance-drive synthesis is silenced and only queued sound effects
    /// play. Set true while the sim runs and false at the menus, so the engine doesn't drone under the
    /// main menu or sound dictionary. Volatile: written by the game thread, read by the audio thread.
    /// </summary>
    public volatile bool EngineEnabled;

    // --- Audio state: double-buffer pattern for thread safety ---
    // Game thread writes to _pendingSfx; audio thread drains into _activePlayback.
    // _activePlayback is exclusively owned by the audio thread after draining.
    private double _audioTime;
    private readonly List<GameSoundEffect> _pendingSfx = new();
    private readonly List<GameSoundEffect> _activePlayback = new();
    private readonly object _pendingLock = new();
    private volatile bool _clearAllFlag;
    private volatile bool _clearLoopingFlag;
    private volatile bool _clearFinishedFlag;

    // --- Pre-allocated buffers for Read() to avoid per-callback GC pressure ---
    private readonly float[] _snapRDrive = new float[NDimensions];
    private readonly float[] _snapFTarget = new float[NDimensions];
    private readonly float[] _snapCueTarget = new float[NDimensions]; // the realm's cue target (objective note or centre)
    private readonly List<(int DimA, int DimB, HarmonicType HType)> _harmonicPairsBuffer = new();

    // --- Continuous phases for the tuning-by-ear beat cue (audio thread only) ---
    // Accumulated per sample so the carrier and tremolo stay click-free even as the player tunes.
    private double _beatCarrierPhase;
    private double _beatAmPhase;

    #region Phase accumulators & gain smoothing (audio-thread-only, click-free synthesis)

    // Every continuous drive voice is advanced by a per-sample PHASE ACCUMULATOR instead of being
    // evaluated as sin(2*PI*f*t) against absolute time. With an accumulator a frequency change only
    // alters the per-sample increment (the phase ramp's slope), never the phase value — so tuning,
    // breathing, and resonance changes can no longer step the waveform and click. Double precision
    // keeps the phase from drifting over long sessions. ALL of this state is touched ONLY on the audio
    // thread; the game thread communicates only through the volatile target fields (DriveVolume, etc.).

    private const double TwoPiD = Math.PI * 2.0;
    private static readonly float[] PhiPow = { PHI, PHI * PHI, PHI * PHI * PHI };
    private static readonly double SchumannInc = TwoPiD * GameConstants.SchumannFreq / SampleRate;
    private static readonly double[] ModInc = { TwoPiD * 2.0 / SampleRate, TwoPiD * 3.0 / SampleRate }; // dim 3, dim 4 tremolo

    // Per-dimension carrier/vibrato phase accumulators.
    private readonly double[] _fundPhase = new double[NDimensions];
    private readonly double[,] _overtonePhase = new double[NDimensions, 3];
    private readonly double[] _subPhase = new double[NDimensions];
    private readonly double[] _vibPhase1 = new double[NDimensions];
    private readonly double[] _vibPhase2 = new double[NDimensions];
    private readonly double[] _modPhase = new double[2];
    private double _schumannPhase;
    private double _chargePhase;

    // Per-buffer increments / shaping (constant within a buffer; recomputed each Read).
    private readonly double[] _fundInc = new double[NDimensions];
    private readonly double[,] _overtoneInc = new double[NDimensions, 3];
    private readonly double[] _subInc = new double[NDimensions];
    private readonly double[] _vibInc1 = new double[NDimensions];
    private readonly double[] _vibInc2 = new double[NDimensions];
    private readonly float[] _vibDepth = new float[NDimensions];
    private readonly float[] _resSmoothed = new float[NDimensions];

    // Intermodulation: one stable slot per dimension pair (C(5,2)=10), keyed by PairIndex so a
    // persisting pair keeps its phase across buffers; gain ramps in/out so detect/lose never pops.
    private const int PairCount = 10;
    private static readonly int[] PairA = { 0, 0, 0, 0, 1, 1, 1, 2, 2, 3 };
    private static readonly int[] PairB = { 1, 2, 3, 4, 2, 3, 4, 3, 4, 4 };
    private readonly double[] _intermodSumPhase = new double[PairCount];
    private readonly double[] _intermodDiffPhase = new double[PairCount];
    private readonly double[] _intermodSumInc = new double[PairCount];
    private readonly double[] _intermodDiffInc = new double[PairCount];
    private readonly bool[] _intermodDiffActive = new bool[PairCount];
    private readonly bool[] _intermodDetected = new bool[PairCount];
    private readonly float[] _intermodGain = new float[PairCount];

    /// <summary>Stable slot index (0..9) for the unordered dimension pair (a,b), a&lt;b — independent of detection order.</summary>
    private static int PairIndex(int a, int b) => a * (9 - a) / 2 + (b - a - 1);

    // Smoothed output gains: a stepped gain multiplying a continuous tone clicks too, so glide them.
    private float _driveGain;
    private float _masterGain;

    // Smoothed tune-by-ear cue amplitude/pan, plus its current increments. The increments persist across
    // the fade-out so the cue's carrier keeps oscillating as it fades (rather than freezing into a click).
    private float _cueAmpSmoothed;
    private float _cuePanLSmoothed = 1f;
    private float _cuePanRSmoothed = 1f;
    private double _cueCarrierInc;
    private double _cueAmInc;
    private float _cueLockBlend;

    #endregion

    // --- Logging stats (throttled to once per second) ---
    private double _lastLogTimeSec;
    private int _readCallCount;
    private int _clippingCount;

    #endregion

    #region Volume settings

    // --- Volume settings ---
    /// <summary>Final output gain applied to the whole mix just before clipping. Volatile: the game thread
    /// writes it (volume keys); the audio thread reads it once per buffer into a smoothed gain.</summary>
    public volatile float MasterVolume = 0.2f;

    /// <summary>Level for short UI beeps.</summary>
    public float BeepVolume = 0.3f;

    /// <summary>Level for one-shot sound effects (chimes, whooshes, ambient loops).</summary>
    public float EffectVolume = 0.2f;

    /// <summary>Level for the continuously synthesised resonance-drive tone. Volatile: written by the game
    /// thread, read once per buffer by the audio thread into a smoothed gain.</summary>
    public volatile float DriveVolume = 0.05f;

    #endregion

    #region Precomputed waveforms

    // --- Precomputed waveforms ---
    // General UI sounds

    /// <summary>Short 440 Hz confirmation beep.</summary>
    public float[] BeepWaveform = Array.Empty<float>();

    /// <summary>Higher 880 Hz beep used for rift-related cues.</summary>
    public float[] RiftBeepWaveform = Array.Empty<float>();

    /// <summary>440 Hz ping followed by silence; looped as a periodic homing beacon when locked on a target.</summary>
    public float[] HomingBeacon = Array.Empty<float>();

    /// <summary>880 Hz ping followed by silence; the rift-flavoured homing beacon.</summary>
    public float[] RiftHomingBeacon = Array.Empty<float>();

    /// <summary>Brief click for menu navigation.</summary>
    public float[] ClickWaveform = Array.Empty<float>();

    /// <summary>Soft whoosh played when the view rotates.</summary>
    public float[] RotationWhooshWaveform = Array.Empty<float>();

    // Musical sounds

    /// <summary>Seven-second swelling golden chord (432 Hz base + harmonics).</summary>
    public float[] GoldenChordWaveform = Array.Empty<float>();

    /// <summary>One-second rift hum (220 Hz + PHI harmonics).</summary>
    public float[] RiftHumWaveform = Array.Empty<float>();

    // Harmonic chimes (9 types)
    // (one per detectable musical interval)

    /// <summary>Octave (2:1) chime.</summary>
    public float[] OctaveChime = Array.Empty<float>();

    /// <summary>Perfect-fifth (3:2) chime.</summary>
    public float[] FifthChime = Array.Empty<float>();

    /// <summary>Golden-ratio (PHI:1) chime.</summary>
    public float[] GoldenChime = Array.Empty<float>();

    /// <summary>Perfect-fourth (4:3) chime.</summary>
    public float[] FourthChime = Array.Empty<float>();

    /// <summary>Major-third (5:4) chime.</summary>
    public float[] MajorThirdChime = Array.Empty<float>();

    /// <summary>Minor-third (6:5) chime.</summary>
    public float[] MinorThirdChime = Array.Empty<float>();

    /// <summary>Major-sixth (5:3) chime.</summary>
    public float[] MajorSixthChime = Array.Empty<float>();

    /// <summary>Minor-sixth (8:5) chime.</summary>
    public float[] MinorSixthChime = Array.Empty<float>();

    /// <summary>Tritone (the "devil's interval") chime, with a low rumble for tension.</summary>
    public float[] TritoneChime = Array.Empty<float>();

    // Stellar ambient sounds

    /// <summary>Deep ~110 Hz pulsing throb for red-giant stars (raised from 40 Hz to be audible).</summary>
    public float[] RedGiantPulse = Array.Empty<float>();

    /// <summary>High 1350 Hz whine for white-dwarf stars.</summary>
    public float[] WhiteDwarfWhine = Array.Empty<float>();

    /// <summary>Low ~80 Hz rumble for brown-dwarf stars (raised from 25 Hz so it's actually audible).</summary>
    public float[] BrownDwarfRumble = Array.Empty<float>();

    /// <summary>Warm mid-range drone for ordinary main-sequence stars, so they're actually audible.</summary>
    public float[] MainSequenceHum = Array.Empty<float>();

    // Nebula ambient sounds

    /// <summary>Warm drone for emission nebulae.</summary>
    public float[] EmissionDrone = Array.Empty<float>();

    /// <summary>Tremolo shimmer for reflection nebulae.</summary>
    public float[] ReflectionShimmer = Array.Empty<float>();

    /// <summary>Layered harmonic tone for planetary nebulae.</summary>
    public float[] PlanetaryLayers = Array.Empty<float>();

    /// <summary>Swept, noisy chaos for supernova remnants.</summary>
    public float[] SupernovaChaos = Array.Empty<float>();

    // Exoplanet ambient sounds

    /// <summary>Modulated, noisy roar for hot-Jupiter worlds.</summary>
    public float[] HotJupiterRoar = Array.Empty<float>();

    /// <summary>Solid tone with octave for super-Earth worlds.</summary>
    public float[] SuperEarthTone = Array.Empty<float>();

    /// <summary>Gently undulating tone for ocean worlds.</summary>
    public float[] OceanWorldFlow = Array.Empty<float>();

    /// <summary>Low ~95 Hz ominous tone for sunless rogue planets (raised from 50 Hz to be audible).</summary>
    public float[] RogueOminous = Array.Empty<float>();

    /// <summary>Bell-like crystalline chime for ice giants.</summary>
    public float[] IceChime = Array.Empty<float>();

    #endregion

    #region Stereo panning table

    // --- Stereo panning table for 5 dimensions ---
    // dim0 -> left, dim1 -> center, dim2 -> right, dim3 -> 70/30, dim4 -> 30/70
    private static readonly (float Left, float Right)[] DimensionPan =
    {
        (1.0f, 0.0f),   // dim 0: hard left
        (0.5f, 0.5f),   // dim 1: center
        (0.0f, 1.0f),   // dim 2: hard right
        (0.7f, 0.3f),   // dim 3: 70% left
        (0.3f, 0.7f),   // dim 4: 30% left
    };

    #endregion

    #region Construction

    /// <summary>
    /// Create a new AudioSystem with the given volume settings.
    /// </summary>
    public AudioSystem(float masterVolume = 0.2f, float beepVolume = 0.3f,
                       float effectVolume = 0.2f, float driveVolume = 0.05f)
    {
        MasterVolume = masterVolume;
        BeepVolume = beepVolume;
        EffectVolume = effectVolume;
        DriveVolume = driveVolume;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

        DebugLogger.Log("Audio", "Generating waveforms...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        GenerateWaveforms();
        sw.Stop();
        DebugLogger.Log("Audio", $"Waveforms generated in {sw.ElapsedMilliseconds}ms");

        SubscribeToEvents();
    }

    #endregion

    #region Event subscriptions

    /// <summary>Wire up the GameEvents this system reacts to (play sound, clear all, stop ambient).</summary>
    private void SubscribeToEvents()
    {
        GameEvents.OnPlaySound += HandlePlaySound;
        GameEvents.OnClearAllSounds += HandleClearAllSounds;
        GameEvents.OnStopAmbientSounds += HandleStopAmbientSounds;
        DebugLogger.Log("Audio", "Subscribed to GameEvents");
    }

    /// <summary>Queue a sound effect described by a play-sound event for the audio thread to pick up.</summary>
    private void HandlePlaySound(object? sender, SoundEffectEventArgs e)
    {
        var sfx = new GameSoundEffect(e.Waveform, pan: e.Pan, pitch: e.Pitch, loop: e.Loop, volume: e.Volume);
        AddSoundEffect(sfx);
    }

    /// <summary>Stop every currently playing effect.</summary>
    private void HandleClearAllSounds(object? sender, EventArgs e)
    {
        ClearAllEffects();
    }

    /// <summary>Flag looping (ambient) effects to be removed on the next audio callback.</summary>
    private void HandleStopAmbientSounds(object? sender, EventArgs e)
    {
        _clearLoopingFlag = true;
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Start audio output. Creates and plays a WaveOutEvent using this ISampleProvider.
    /// </summary>
    public void Start()
    {
        if (_waveOut != null) return;

        DebugLogger.Log("Audio", "Starting audio output...");
        try
        {
            _waveOut = new WaveOutEvent
            {
                // Lower latency = snappier audio feedback (menu ticks, beeps) right after a keypress.
                // 60ms is a good balance for WaveOut; raise it again if any system crackles/underruns.
                DesiredLatency = 60,
                NumberOfBuffers = 3,
                DeviceNumber = _deviceNumber,   // -1 = system default; F3 lets the player pick another
            };
            _waveOut.Init(this);
            _waveOut.Play();
            DebugLogger.Log("Audio", "Audio output started successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "Failed to start audio output", ex);
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    /// <summary>
    /// Stop audio output and dispose the device.
    /// </summary>
    public void Stop()
    {
        if (_waveOut == null) return;
        DebugLogger.Log("Audio", "Stopping audio output...");
        _waveOut.Stop();
        _waveOut.Dispose();
        _waveOut = null;
        DebugLogger.Log("Audio", "Audio output stopped");
    }

    /// <summary>
    /// Switch the output to a different sound device (NAudio MME device number; -1 = system default) by
    /// stopping and recreating the WaveOut on that device. Safe to call while playing — a brief gap is
    /// expected as the device reopens. No-op if it is already the active device.
    /// </summary>
    public void SetOutputDevice(int deviceNumber)
    {
        if (_waveOut != null && deviceNumber == _deviceNumber) return;
        _deviceNumber = deviceNumber;
        bool wasRunning = _waveOut != null;
        _deviceSwitching = true;   // Read() returns silence during the swap so a late callback can't glitch
        try
        {
            Stop();
            if (wasRunning) Start();
        }
        finally
        {
            _deviceSwitching = false;
        }
        DebugLogger.Log("Audio", $"Output device set to {deviceNumber}.");
    }

    /// <summary>Enumerate the available output devices: system default first, then each NAudio device.</summary>
    public static List<(int Number, string Name)> GetOutputDevices()
    {
        var list = new List<(int, string)> { (-1, "System default") };
        try
        {
            for (int n = 0; n < WaveOut.DeviceCount; n++)
                list.Add((n, WaveOut.GetCapabilities(n).ProductName));
        }
        catch (Exception ex) { DebugLogger.LogError("Audio", "Enumerating output devices failed", ex); }
        return list;
    }

    /// <summary>Find the device number whose product name matches <paramref name="name"/> (-1 if none / default).</summary>
    public static int FindDeviceByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        try
        {
            for (int n = 0; n < WaveOut.DeviceCount; n++)
                if (string.Equals(WaveOut.GetCapabilities(n).ProductName, name, StringComparison.OrdinalIgnoreCase))
                    return n;
        }
        catch (Exception ex) { DebugLogger.LogError("Audio", "Matching output device by name failed", ex); }
        return -1;
    }

    /// <summary>
    /// Set the ship reference. Called from the game thread.
    /// </summary>
    public void SetShip(Ship ship)
    {
        _ship = ship; // volatile write, no lock needed
        DebugLogger.Log("Audio", "Ship reference set");
    }

    /// <summary>
    /// Thread-safe: add a sound effect to the active list.
    /// </summary>
    public void AddSoundEffect(GameSoundEffect effect)
    {
        lock (_pendingLock)
        {
            _pendingSfx.Add(effect);
        }
        DebugLogger.Log("Audio", $"SoundEffect added: {effect.Waveform.Length} samples, loop={effect.Loop}, vol={effect.Volume:F2}");
    }

    /// <summary>
    /// Thread-safe: remove all finished sound effects (useful for cleanup).
    /// </summary>
    public void ClearFinishedEffects()
    {
        _clearFinishedFlag = true;
    }

    /// <summary>
    /// Thread-safe: stop all active sound effects.
    /// </summary>
    public void ClearAllEffects()
    {
        _clearAllFlag = true;
    }

    #endregion

    #region IDisposable

    /// <summary>Unsubscribe from GameEvents and stop/dispose the audio device.</summary>
    public void Dispose()
    {
        GameEvents.OnPlaySound -= HandlePlaySound;
        GameEvents.OnClearAllSounds -= HandleClearAllSounds;
        GameEvents.OnStopAmbientSounds -= HandleStopAmbientSounds;
        DebugLogger.Log("Audio", "Unsubscribed from GameEvents");

        Stop();
        GC.SuppressFinalize(this);
    }

    #endregion
}
