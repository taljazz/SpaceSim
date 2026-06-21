namespace SpaceSim;

/// <summary>
/// A positional world sound — something out in the universe (a star's drone, a rift's hum) that
/// should appear to come from a direction. It plays through OpenAL + HRTF when available, and falls
/// back to a stereo-panned NAudio effect otherwise. Exactly one backing field is set at a time.
///
/// <para>
/// Created and driven entirely from the game thread via the Ship's world-sound helpers
/// (StartWorldLoop / UpdateWorldLoop / StopWorldLoop). Never touch this from the audio callback.
/// </para>
/// </summary>
public sealed class WorldSound
{
    #region Backing voices (exactly one is set)

    /// <summary>The OpenAL voice (preferred path); null when using the NAudio fallback.</summary>
    internal SpatialVoice? Voice;

    /// <summary>The NAudio fallback effect; null when using OpenAL.</summary>
    internal GameSoundEffect? Sfx;

    #endregion

    /// <summary>The waveform being played — used to detect when a slot should switch sounds.</summary>
    public float[] Waveform { get; }

    internal WorldSound(float[] waveform) => Waveform = waveform;
}
