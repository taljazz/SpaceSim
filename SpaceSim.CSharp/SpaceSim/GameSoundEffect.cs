namespace SpaceSim;

/// <summary>
/// A playing sound effect with position tracking, panning, and looping.
/// </summary>
public class GameSoundEffect
{
    /// <summary>The PCM samples to play (already pitch-resampled if requested).</summary>
    public float[] Waveform;

    /// <summary>Current playback cursor into <see cref="Waveform"/>, advanced by the audio mixer.</summary>
    public int Position;

    /// <summary>Stereo pan, -1 (left) to +1 (right).</summary>
    public float Pan;

    /// <summary>Whether playback restarts from the beginning when it reaches the end.</summary>
    public bool Loop;

    /// <summary>Playback gain multiplier.</summary>
    public float Volume;

    /// <summary>
    /// Builds a playable effect from a waveform, optionally resampling it for a pitch shift up front
    /// so the mixer can just stream samples.
    /// </summary>
    public GameSoundEffect(float[] waveform, float pan = 0f, float pitch = 1f, bool loop = false, float volume = 1f)
    {
        if (pitch != 1f && pitch > 0f)
        {
            // Resample waveform for pitch adjustment
            int newLength = (int)(waveform.Length / pitch);
            var pitched = new float[newLength];
            for (int i = 0; i < newLength; i++)
            {
                float srcIdx = i * pitch;
                int idx = (int)srcIdx;
                if (idx >= waveform.Length - 1)
                {
                    pitched[i] = waveform[^1];
                }
                else
                {
                    float frac = srcIdx - idx;
                    pitched[i] = waveform[idx] * (1f - frac) + waveform[idx + 1] * frac;
                }
            }
            Waveform = pitched;
        }
        else
        {
            Waveform = waveform;
        }

        Position = 0;
        Pan = pan;
        Loop = loop;
        Volume = volume;
    }

    /// <summary>True once a non-looping effect has played all its samples (the mixer can drop it).</summary>
    public bool IsFinished => !Loop && Position >= Waveform.Length;
}
