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

    /// <summary>The interleaved 32-bit-float stereo format this provider emits (set in the constructor).</summary>
    public WaveFormat WaveFormat { get; }

    #endregion

    #region Audio thread state

    // --- Ship reference (volatile for lock-free audio thread read) ---
    private volatile Ship? _ship;

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
    private readonly List<(int DimA, int DimB, HarmonicType HType)> _harmonicPairsBuffer = new();

    // --- Logging stats (throttled to once per second) ---
    private double _lastLogTimeSec;
    private int _readCallCount;
    private int _clippingCount;

    #endregion

    #region Volume settings

    // --- Volume settings ---
    /// <summary>Final output gain applied to the whole mix just before clipping.</summary>
    public float MasterVolume = 0.2f;

    /// <summary>Level for short UI beeps.</summary>
    public float BeepVolume = 0.3f;

    /// <summary>Level for one-shot sound effects (chimes, whooshes, ambient loops).</summary>
    public float EffectVolume = 0.2f;

    /// <summary>Level for the continuously synthesised resonance-drive tone.</summary>
    public float DriveVolume = 0.05f;

    #endregion

    #region Precomputed waveforms

    // --- Precomputed waveforms ---
    // General UI sounds

    /// <summary>Short 440 Hz confirmation beep.</summary>
    public float[] BeepWaveform = Array.Empty<float>();

    /// <summary>Higher 880 Hz beep used for rift-related cues.</summary>
    public float[] RiftBeepWaveform = Array.Empty<float>();

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

    /// <summary>Deep 40 Hz pulsing bass for red-giant stars.</summary>
    public float[] RedGiantPulse = Array.Empty<float>();

    /// <summary>High 1350 Hz whine for white-dwarf stars.</summary>
    public float[] WhiteDwarfWhine = Array.Empty<float>();

    /// <summary>Barely-audible 25 Hz rumble for brown-dwarf stars.</summary>
    public float[] BrownDwarfRumble = Array.Empty<float>();

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

    /// <summary>Low, ominous tone for sunless rogue planets.</summary>
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
                DesiredLatency = 100,
                NumberOfBuffers = 3,
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
