using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;

namespace SpaceSim;

/// <summary>
/// OpenAL Soft spatial-audio engine for positioned <em>world</em> sounds, with HRTF for true 3D
/// placement (azimuth, elevation, and front/back over headphones).
///
/// <para>
/// This runs <b>alongside</b> the NAudio engine (the "hybrid" design): NAudio keeps the
/// listener-attached drive synth and the flat UI sounds; OpenAL handles point-sources out in the
/// world. The listener sits at the origin and every source is positioned in listener space
/// (see <see cref="SpatialAudioMath.ToListenerSpace"/>), so a sound's direction matches where the
/// object actually is relative to the ship's heading.
/// </para>
///
/// <para>
/// Every step is guarded: if OpenAL or HRTF can't initialise on a given machine,
/// <see cref="IsAvailable"/> stays <c>false</c> and callers fall back to the existing NAudio
/// panning — audio never fully breaks. The actual spatialisation can only be judged by ear, so this
/// class logs its HRTF status for confirmation.
/// </para>
/// </summary>
public sealed class OpenAlAudio : IDisposable
{
    #region HRTF constants (ALC_SOFT_HRTF extension)

    // OpenTK doesn't expose strongly-typed names for the ALC_SOFT_HRTF extension, so we use the
    // documented raw values. https://openal-soft.org/openal-extensions/SOFT_HRTF.txt
    private const int ALC_HRTF_SOFT = 0x1992;        // context attribute: request HRTF
    private const int ALC_HRTF_STATUS_SOFT = 0x1993; // query: did HRTF actually engage?
    private const int ALC_TRUE = 1;
    private const int ALC_HRTF_ENABLED_SOFT = 1;     // value returned by the status query when on

    #endregion

    #region State

    private ALDevice _device;
    private ALContext _context;
    private bool _disposed;

    /// <summary>True when OpenAL initialised and world sounds can be spatialised. If false, callers fall back to NAudio panning.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>True when HRTF actually engaged (gives the best 3D image over headphones).</summary>
    public bool HrtfEnabled { get; private set; }

    // One OpenAL buffer per distinct source waveform (keyed by the array reference — waveforms are
    // shared, long-lived arrays on AudioSystem, so reference identity is the right key).
    private readonly Dictionary<float[], int> _bufferCache = new();

    // Fire-and-forget sources we need to reclaim once they finish playing.
    private readonly List<int> _oneShotSources = new();

    // Live looping voices, tracked so teardown can always stop them (a still-playing source would
    // otherwise block context/device cleanup). Pruned each frame as voices are stopped.
    private readonly List<SpatialVoice> _voices = new();

    #endregion

    #region Initialization

    /// <summary>Open the system-default OpenAL device with HRTF (see <see cref="Initialize"/>).</summary>
    public OpenAlAudio() => Initialize(null);

    /// <summary>
    /// Open an OpenAL device (null = system default), create an HRTF-enabled context, and make it current.
    /// Any failure leaves <see cref="IsAvailable"/> false (and is logged) rather than throwing.
    /// </summary>
    private void Initialize(string? deviceName)
    {
        try
        {
            _device = ALC.OpenDevice(deviceName);
            if (_device == ALDevice.Null)
            {
                DebugLogger.Log("Audio", "OpenAL: no audio device available — spatial audio disabled (NAudio fallback).");
                return;
            }

            // Request HRTF only if the device advertises the extension; otherwise create a plain context.
            bool hrtfSupported = ALC.IsExtensionPresent(_device, "ALC_SOFT_HRTF");
            int[] attrs = hrtfSupported ? new[] { ALC_HRTF_SOFT, ALC_TRUE, 0 } : new[] { 0 };
            _context = ALC.CreateContext(_device, attrs);

            if (_context == ALContext.Null || !ALC.MakeContextCurrent(_context))
            {
                DebugLogger.Log("Audio", "OpenAL: could not create/activate context — spatial audio disabled (NAudio fallback).");
                Cleanup();
                return;
            }

            // Listener fixed at the origin; we position each source in listener space ourselves.
            AL.Listener(ALListener3f.Position, 0f, 0f, 0f);
            AL.Listener(ALListenerf.Gain, 1f);
            // We apply our own distance-based volume (to match the existing curves), so disable
            // OpenAL's built-in distance attenuation and let it handle direction/HRTF only.
            AL.DistanceModel(ALDistanceModel.None);

            HrtfEnabled = QueryHrtfEnabled(hrtfSupported);
            IsAvailable = true;
            DebugLogger.Log("Audio", $"OpenAL initialized on '{deviceName ?? "default"}'. HRTF supported={hrtfSupported}, enabled={HrtfEnabled}.");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "OpenAL init failed — spatial audio disabled (NAudio fallback).", ex);
            IsAvailable = false;
            Cleanup();
        }
    }

    /// <summary>
    /// Re-open the spatial engine on the device whose name best matches <paramref name="matchName"/> (the
    /// NAudio device chosen with F3), falling back to the system default if there's no match. Best-effort:
    /// OpenAL and NAudio name devices differently, so a clean match isn't guaranteed — if it fails, spatial
    /// audio simply stays on the default. The caller must silence live world sounds first (their old OpenAL
    /// sources die with the context; the ship recreates them next frame).
    /// </summary>
    public void TryReopen(string? matchName)
    {
        if (_disposed) return;
        try
        {
            Cleanup();
            IsAvailable = false;
            HrtfEnabled = false;

            string? chosen = null;
            if (!string.IsNullOrEmpty(matchName))
            {
                foreach (string dev in EnumerateDevices())
                {
                    if (dev.IndexOf(matchName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        matchName.IndexOf(dev, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chosen = dev;
                        break;
                    }
                }
            }

            Initialize(chosen);
            DebugLogger.Log("Audio", $"OpenAL reopen (match '{matchName}' -> '{chosen ?? "default"}'). Available={IsAvailable}.");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "OpenAL reopen failed — spatial audio off.", ex);
            IsAvailable = false;
        }
    }

    /// <summary>List the OpenAL output device names, or an empty list if enumeration is unsupported.</summary>
    private static List<string> EnumerateDevices()
    {
        var result = new List<string>();
        try
        {
            foreach (string name in ALC.GetStringList(GetEnumerationStringList.DeviceSpecifier))
                result.Add(name);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "OpenAL device enumeration unavailable.", ex);
        }
        return result;
    }

    /// <summary>Ask OpenAL Soft whether HRTF actually engaged (the request can be silently denied).</summary>
    private bool QueryHrtfEnabled(bool hrtfSupported)
    {
        if (!hrtfSupported) return false;
        try
        {
            var status = new int[1];
            ALC.GetInteger(_device, (AlcGetInteger)ALC_HRTF_STATUS_SOFT, 1, status);
            return status[0] == ALC_HRTF_ENABLED_SOFT;
        }
        catch
        {
            return false; // status query not supported by this driver/binding — assume off
        }
    }

    #endregion

    #region Buffers

    /// <summary>Get (creating once) the OpenAL buffer for a normalized float waveform.</summary>
    private int GetBuffer(float[] waveform)
    {
        if (_bufferCache.TryGetValue(waveform, out int existing))
            return existing;

        int buffer = AL.GenBuffer();
        short[] pcm = SpatialAudioMath.FloatToPcm16(waveform);
        AL.BufferData(buffer, ALFormat.Mono16, pcm, GameConstants.SampleRate);
        _bufferCache[waveform] = buffer;
        return buffer;
    }

    #endregion

    #region Playback

    /// <summary>
    /// Generate an OpenAL source, returning 0 if generation failed (e.g. the implementation ran out of
    /// voices). Clears any stale error first, then checks for one — a clean way to detect exhaustion so
    /// callers can skip the sound or fall back to NAudio instead of using an invalid handle.
    /// </summary>
    private static int GenSourceChecked()
    {
        AL.GetError();
        int source = AL.GenSource();
        return AL.GetError() == ALError.NoError ? source : 0;
    }

    /// <summary>
    /// Start a looping positioned sound and return a <see cref="SpatialVoice"/> handle for updating
    /// its position/gain each frame or stopping it. Returns null if spatial audio is unavailable.
    /// </summary>
    public SpatialVoice? PlayLoop(float[] waveform, (float X, float Y, float Z) pos, float gain)
    {
        if (!IsAvailable) return null;

        int source = GenSourceChecked();
        if (source == 0) return null; // out of sources — caller falls back to NAudio panning
        AL.Source(source, ALSourcei.Buffer, GetBuffer(waveform));
        AL.Source(source, ALSourceb.SourceRelative, true); // position is relative to the listener
        AL.Source(source, ALSourceb.Looping, true);
        AL.Source(source, ALSource3f.Position, pos.X, pos.Y, pos.Z);
        AL.Source(source, ALSourcef.Gain, gain);
        AL.SourcePlay(source);

        var voice = new SpatialVoice(source);
        _voices.Add(voice);
        return voice;
    }

    /// <summary>Fire a one-shot positioned sound; its source is reclaimed automatically by <see cref="Update"/>.</summary>
    public void PlayOneShot(float[] waveform, (float X, float Y, float Z) pos, float gain, float pitch = 1f)
    {
        if (!IsAvailable) return;

        int source = GenSourceChecked();
        if (source == 0) return; // out of sources — skip this one-shot rather than use an invalid handle
        AL.Source(source, ALSourcei.Buffer, GetBuffer(waveform));
        AL.Source(source, ALSourceb.SourceRelative, true);
        AL.Source(source, ALSourceb.Looping, false);
        AL.Source(source, ALSource3f.Position, pos.X, pos.Y, pos.Z);
        AL.Source(source, ALSourcef.Gain, gain);
        AL.Source(source, ALSourcef.Pitch, pitch);
        AL.SourcePlay(source);
        _oneShotSources.Add(source);
    }

    /// <summary>Reclaim finished one-shot sources. Call once per frame.</summary>
    public void Update()
    {
        if (!IsAvailable) return;

        for (int i = _oneShotSources.Count - 1; i >= 0; i--)
        {
            AL.GetSource(_oneShotSources[i], ALGetSourcei.SourceState, out int state);
            if ((ALSourceState)state == ALSourceState.Stopped)
            {
                AL.DeleteSource(_oneShotSources[i]);
                _oneShotSources.RemoveAt(i);
            }
        }

        // Drop references to voices the caller has already stopped.
        _voices.RemoveAll(v => v.IsStopped);
    }

    /// <summary>
    /// Set the overall output level. We mirror NAudio's master volume here so OpenAL sounds sit at
    /// the same loudness as the rest of the mix and respond to the player's volume keys.
    /// </summary>
    public void SetMasterGain(float gain)
    {
        if (!IsAvailable) return;
        AL.Listener(ALListenerf.Gain, gain);
    }

    #endregion

    #region Disposal

    /// <summary>Best-effort teardown of sources, buffers, context, and device.</summary>
    private void Cleanup()
    {
        try
        {
            // Stop any still-playing looping voices first so the context owns no live sources.
            foreach (var voice in _voices)
                voice.Stop();
            _voices.Clear();

            foreach (int src in _oneShotSources)
            {
                AL.SourceStop(src);
                AL.DeleteSource(src);
            }
            _oneShotSources.Clear();

            foreach (int buf in _bufferCache.Values)
                AL.DeleteBuffer(buf);
            _bufferCache.Clear();

            if (_context != ALContext.Null)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
                _context = ALContext.Null;
            }
            if (_device != ALDevice.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;
            }
        }
        catch { /* teardown is best-effort; never throw from Dispose */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsAvailable = false;
        Cleanup();
        DebugLogger.Log("Audio", "OpenAL disposed.");
    }

    #endregion
}

/// <summary>
/// A handle to one looping, positioned OpenAL sound. Update its position/gain each frame to keep it
/// tracking a world object, then <see cref="Stop"/> it when the object is gone.
/// </summary>
public sealed class SpatialVoice
{
    private readonly int _source;
    private bool _stopped;

    internal SpatialVoice(int source) => _source = source;

    /// <summary>True once this voice has been stopped (so the engine can drop its reference).</summary>
    public bool IsStopped => _stopped;

    /// <summary>Move the voice to a new listener-space position, set its gain, and apply a Doppler pitch.</summary>
    public void Update((float X, float Y, float Z) pos, float gain, float pitch = 1f)
    {
        if (_stopped) return;
        AL.Source(_source, ALSource3f.Position, pos.X, pos.Y, pos.Z);
        AL.Source(_source, ALSourcef.Gain, gain);
        AL.Source(_source, ALSourcef.Pitch, pitch);
    }

    /// <summary>Stop and release this voice's OpenAL source.</summary>
    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        AL.SourceStop(_source);
        AL.DeleteSource(_source);
    }
}
