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
    // --- Constants ---
    private const int SampleRate = GameConstants.SampleRate;
    private const int Channels = 2;
    private const float PHI = GameConstants.PHI;
    private const int NDimensions = GameConstants.NDimensions;
    private const float TwoPi = MathF.PI * 2f;

    // --- NAudio plumbing ---
    private WaveOutEvent? _waveOut;
    public WaveFormat WaveFormat { get; }

    // --- Ship reference ---
    private Ship? _ship;
    private readonly object _lock = new();

    // --- Audio state ---
    private double _audioTime;
    private readonly List<GameSoundEffect> _activeSoundEffects = new();

    // --- Logging stats (throttled to once per second) ---
    private double _lastLogTimeSec;
    private int _readCallCount;
    private int _clippingCount;

    // --- Volume settings ---
    public float MasterVolume = 0.2f;
    public float BeepVolume = 0.3f;
    public float EffectVolume = 0.2f;
    public float DriveVolume = 0.05f;

    // --- Precomputed waveforms ---
    // General UI sounds
    public float[] BeepWaveform = Array.Empty<float>();
    public float[] RiftBeepWaveform = Array.Empty<float>();
    public float[] ClickWaveform = Array.Empty<float>();
    public float[] RotationWhooshWaveform = Array.Empty<float>();

    // Musical sounds
    public float[] GoldenChordWaveform = Array.Empty<float>();
    public float[] RiftHumWaveform = Array.Empty<float>();

    // Harmonic chimes (9 types)
    public float[] OctaveChime = Array.Empty<float>();
    public float[] FifthChime = Array.Empty<float>();
    public float[] GoldenChime = Array.Empty<float>();
    public float[] FourthChime = Array.Empty<float>();
    public float[] MajorThirdChime = Array.Empty<float>();
    public float[] MinorThirdChime = Array.Empty<float>();
    public float[] MajorSixthChime = Array.Empty<float>();
    public float[] MinorSixthChime = Array.Empty<float>();
    public float[] TritoneChime = Array.Empty<float>();

    // Stellar ambient sounds
    public float[] RedGiantPulse = Array.Empty<float>();
    public float[] WhiteDwarfWhine = Array.Empty<float>();
    public float[] BrownDwarfRumble = Array.Empty<float>();

    // Nebula ambient sounds
    public float[] EmissionDrone = Array.Empty<float>();
    public float[] ReflectionShimmer = Array.Empty<float>();
    public float[] PlanetaryLayers = Array.Empty<float>();
    public float[] SupernovaChaos = Array.Empty<float>();

    // Exoplanet ambient sounds
    public float[] HotJupiterRoar = Array.Empty<float>();
    public float[] SuperEarthTone = Array.Empty<float>();
    public float[] OceanWorldFlow = Array.Empty<float>();
    public float[] RogueOminous = Array.Empty<float>();
    public float[] IceChime = Array.Empty<float>();

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

    // ========================================================================
    //  Event subscriptions
    // ========================================================================

    private void SubscribeToEvents()
    {
        GameEvents.OnPlaySound += HandlePlaySound;
        GameEvents.OnClearAllSounds += HandleClearAllSounds;
        GameEvents.OnStopAmbientSounds += HandleStopAmbientSounds;
        DebugLogger.Log("Audio", "Subscribed to GameEvents");
    }

    private void HandlePlaySound(object? sender, SoundEffectEventArgs e)
    {
        var sfx = new GameSoundEffect(e.Waveform, pan: e.Pan, pitch: e.Pitch, loop: e.Loop, volume: e.Volume);
        AddSoundEffect(sfx);
    }

    private void HandleClearAllSounds(object? sender, EventArgs e)
    {
        ClearAllEffects();
    }

    private void HandleStopAmbientSounds(object? sender, EventArgs e)
    {
        // Remove all looping effects
        lock (_lock)
        {
            _activeSoundEffects.RemoveAll(sfx => sfx.Loop);
        }
    }

    // ========================================================================
    //  Lifecycle
    // ========================================================================

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
        lock (_lock)
        {
            _ship = ship;
        }
        DebugLogger.Log("Audio", "Ship reference set");
    }

    /// <summary>
    /// Thread-safe: add a sound effect to the active list.
    /// </summary>
    public void AddSoundEffect(GameSoundEffect effect)
    {
        lock (_lock)
        {
            _activeSoundEffects.Add(effect);
        }
        DebugLogger.Log("Audio", $"SoundEffect added: {effect.Waveform.Length} samples, loop={effect.Loop}, vol={effect.Volume:F2}");
    }

    /// <summary>
    /// Thread-safe: remove all finished sound effects (useful for cleanup).
    /// </summary>
    public void ClearFinishedEffects()
    {
        lock (_lock)
        {
            _activeSoundEffects.RemoveAll(sfx => sfx.IsFinished);
        }
    }

    /// <summary>
    /// Thread-safe: stop all active sound effects.
    /// </summary>
    public void ClearAllEffects()
    {
        lock (_lock)
        {
            _activeSoundEffects.Clear();
        }
    }

    // ========================================================================
    //  IDisposable
    // ========================================================================

    public void Dispose()
    {
        GameEvents.OnPlaySound -= HandlePlaySound;
        GameEvents.OnClearAllSounds -= HandleClearAllSounds;
        GameEvents.OnStopAmbientSounds -= HandleStopAmbientSounds;
        DebugLogger.Log("Audio", "Unsubscribed from GameEvents");

        Stop();
        GC.SuppressFinalize(this);
    }
}
