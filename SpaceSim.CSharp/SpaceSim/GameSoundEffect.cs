namespace SpaceSim;

/// <summary>
/// A playing sound effect with position tracking, panning, and looping.
/// </summary>
public class GameSoundEffect
{
    public float[] Waveform;
    public int Position;
    public float Pan;
    public bool Loop;
    public float Volume;

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

    public bool IsFinished => !Loop && Position >= Waveform.Length;
}
