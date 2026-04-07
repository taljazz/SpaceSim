using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpaceSim;

public partial class AudioSystem
{
    // ========================================================================
    //  ISampleProvider.Read  -- the audio callback
    // ========================================================================

    /// <summary>
    /// Called by NAudio on the audio thread. Fills the interleaved stereo buffer [L,R,L,R,...].
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / Channels;

        // --- Snapshot ship state under lock so we don't race the game thread ---
        float[] rDrive = Vec5.Zero();
        float[] fTarget = Vec5.Zero();
        float resonanceWidth = GameConstants.ResonanceWidthBase;
        bool isCharging = false;
        float chargeProgress = 0f;
        List<(int, int, string)>? harmonicPairs = null;

        Ship? ship;
        lock (_lock)
        {
            ship = _ship;
        }

        if (ship != null)
        {
            // Copy volatile ship state
            for (int i = 0; i < NDimensions; i++)
            {
                rDrive[i] = ship.RDrive[i];
                fTarget[i] = ship.FTarget[i];
            }
            resonanceWidth = ship.ResonanceWidth;
            isCharging = ship.IsChargingRift;
            chargeProgress = ship.RiftChargeProgress;
            harmonicPairs = DetectHarmonicPairs(rDrive);
        }

        double audioTimeStart = _audioTime;

        for (int frame = 0; frame < frames; frame++)
        {
            float t = (float)((audioTimeStart + frame) / SampleRate);
            float left = 0f;
            float right = 0f;

            // --- 1. Schumann resonance carrier ---
            float schumann = GameConstants.SchumannVolume * MathF.Sin(TwoPi * GameConstants.SchumannFreq * t);
            left += schumann;
            right += schumann;

            // --- 2. Per-dimension drive synthesis ---
            if (ship != null)
            {
                for (int dim = 0; dim < NDimensions; dim++)
                {
                    float baseFreq = rDrive[dim] / 2f;
                    if (baseFreq < 1f) continue;

                    float delta = MathF.Abs(rDrive[dim] - fTarget[dim]);
                    float resLevel = 1f / (1f + (delta / resonanceWidth) * (delta / resonanceWidth));

                    float vibratoPhase = GetVibratoPhase(t, resLevel);

                    // Pure sine fundamental
                    float signal = DriveVolume * MathF.Sin(TwoPi * baseFreq * t + vibratoPhase);

                    // Golden ratio overtones (PHI^k, k=1,2,3)
                    for (int k = 1; k <= 3; k++)
                    {
                        float amp = DriveVolume * 0.25f / k;
                        float overtoneFreq = baseFreq * MathF.Pow(PHI, k);
                        signal += amp * MathF.Sin(TwoPi * overtoneFreq * t + vibratoPhase);
                    }

                    // Subharmonic at baseFreq / PHI
                    float subFreq = baseFreq / PHI;
                    signal += DriveVolume * 0.15f * MathF.Sin(TwoPi * subFreq * t + vibratoPhase * 0.5f);

                    // Higher dimension modulation for dims 3 and 4
                    if (dim >= 3)
                    {
                        float modRate = dim == 3 ? 2f : 3f;
                        float mod = 1f + 0.3f * MathF.Sin(TwoPi * modRate * t);
                        signal *= mod;
                    }

                    // Pan to stereo
                    var (panL, panR) = DimensionPan[dim];
                    left += signal * panL;
                    right += signal * panR;
                }

                // --- 3. Intermodulation tones for harmonic pairs ---
                if (harmonicPairs != null)
                {
                    foreach (var (dimA, dimB, _) in harmonicPairs)
                    {
                        float freqA = rDrive[dimA] / 2f;
                        float freqB = rDrive[dimB] / 2f;
                        if (freqA < 1f || freqB < 1f) continue;

                        float sumFreq = freqA + freqB;
                        float diffFreq = MathF.Abs(freqA - freqB);

                        float intermod = GameConstants.IntermodDepth
                            * (0.5f * MathF.Sin(TwoPi * sumFreq * t)
                             + 0.7f * MathF.Sin(TwoPi * diffFreq * t));

                        // Spread intermod across both channels
                        left += intermod * 0.5f;
                        right += intermod * 0.5f;
                    }
                }

                // --- 4. Rift charge rising tone ---
                if (isCharging)
                {
                    float chargeFreq = 200f + 600f * chargeProgress;
                    float chargeAmp = 0.08f * chargeProgress;
                    float chargeTone = chargeAmp * MathF.Sin(TwoPi * chargeFreq * t);
                    left += chargeTone;
                    right += chargeTone;
                }
            }

            // --- 5. Mix active sound effects ---
            lock (_lock)
            {
                for (int s = _activeSoundEffects.Count - 1; s >= 0; s--)
                {
                    var sfx = _activeSoundEffects[s];
                    if (sfx.Position >= sfx.Waveform.Length)
                    {
                        if (sfx.Loop)
                        {
                            sfx.Position = 0;
                        }
                        else
                        {
                            _activeSoundEffects.RemoveAt(s);
                            continue;
                        }
                    }

                    // sfx.Volume already contains the appropriate level (BeepVolume, EffectVolume, etc.)
                    // set by the caller — do NOT multiply by EffectVolume again.
                    float sample = sfx.Waveform[sfx.Position] * sfx.Volume;
                    sfx.Position++;

                    // Pan: -1 = full left, 0 = center, +1 = full right
                    float sL = sample * (1f - MathF.Max(0f, sfx.Pan));
                    float sR = sample * (1f + MathF.Min(0f, sfx.Pan));
                    left += sL;
                    right += sR;
                }
            }

            // --- 6. Apply master volume and clip ---
            left *= MasterVolume;
            right *= MasterVolume;

            // Track clipping
            if (left > 1f || left < -1f || right > 1f || right < -1f)
                _clippingCount++;

            left = Math.Clamp(left, -1f, 1f);
            right = Math.Clamp(right, -1f, 1f);

            // Write interleaved stereo
            int idx = offset + frame * Channels;
            buffer[idx] = left;
            buffer[idx + 1] = right;
        }

        _audioTime += frames;
        _readCallCount++;

        // Log stats once per second (throttled)
        double currentSec = _audioTime / SampleRate;
        if (currentSec - _lastLogTimeSec >= 1.0)
        {
            int sfxCount;
            lock (_lock) { sfxCount = _activeSoundEffects.Count; }
            DebugLogger.Log("Audio", $"Stats: {_readCallCount} callbacks, {_clippingCount} clips, {sfxCount} active SFX");
            _readCallCount = 0;
            _clippingCount = 0;
            _lastLogTimeSec = currentSec;
        }

        return count;
    }

    // ========================================================================
    //  Vibrato helper
    // ========================================================================

    /// <summary>
    /// Compute vibrato phase offset from two LFOs modulated by golden ratio.
    /// </summary>
    private static float GetVibratoPhase(float t, float resLevel)
    {
        float depth = 0.25f + (1.1f - 0.25f) * resLevel;
        float rate = 3.4f + (4.3f - 3.4f) * resLevel;
        float lfo1 = MathF.Sin(TwoPi * rate * t);
        float lfo2 = 0.3f * MathF.Sin(TwoPi * rate * PHI * t);
        return depth * (lfo1 + lfo2);
    }

    // ========================================================================
    //  Harmonic pair detection
    // ========================================================================

    /// <summary>
    /// Check all dimension pairs for known harmonic ratios.
    /// Returns a list of (dimA, dimB, harmonicName) tuples.
    /// </summary>
    public List<(int DimA, int DimB, string Name)> DetectHarmonicPairs(float[]? rDrive = null)
    {
        var result = new List<(int, int, string)>();
        if (rDrive == null)
        {
            lock (_lock)
            {
                if (_ship == null) return result;
                rDrive = Vec5.Clone(_ship.RDrive);
            }
        }

        for (int i = 0; i < NDimensions; i++)
        {
            for (int j = i + 1; j < NDimensions; j++)
            {
                float freqI = rDrive[i];
                float freqJ = rDrive[j];
                if (freqI < 1f || freqJ < 1f) continue;

                float ratio = MathF.Max(freqI, freqJ) / MathF.Min(freqI, freqJ);

                foreach (var (name, targetRatio) in GameConstants.HarmonicRatios)
                {
                    if (MathF.Abs(ratio - targetRatio) / targetRatio < GameConstants.HarmonicTolerance)
                    {
                        result.Add((i, j, name));
                        break; // only one harmonic per pair
                    }
                }
            }
        }

        return result;
    }
}
