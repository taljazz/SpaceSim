using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpaceSim;

public partial class AudioSystem
{
    #region Waveform generation

    /// <summary>
    /// Pre-compute all waveform buffers used for UI sounds, chimes, and ambient loops.
    /// </summary>
    private void GenerateWaveforms()
    {
        // --- General UI sounds ---
        BeepWaveform = GenerateTone(440f, 0.1f, 0.2f);
        RiftBeepWaveform = GenerateTone(880f, 0.1f, 0.2f);

        // Homing beacons: a brief ping followed by silence so looping them gives a periodic
        // "ping ... ping" homing cue while autopiloting to a lock, not a continuous muddy drone.
        float[] ping440 = GenerateTone(440f, 0.1f, 0.18f);
        HomingBeacon = new float[(int)(SampleRate * 0.7f)];
        Array.Copy(ping440, HomingBeacon, ping440.Length);
        float[] ping880 = GenerateTone(880f, 0.1f, 0.18f);
        RiftHomingBeacon = new float[(int)(SampleRate * 0.7f)];
        Array.Copy(ping880, RiftHomingBeacon, ping880.Length);

        ClickWaveform = GenerateTone(100f * PHI, 0.05f, 0.15f);
        RotationWhooshWaveform = GenerateTone(200f * PHI, 0.2f, 0.1f);

        // --- Golden Chord: 7 seconds, base 432 Hz + harmonics + double swell ---
        GoldenChordWaveform = GenerateGoldenChord();

        // --- Rift Hum: 1 second, 220 Hz + PHI harmonics ---
        RiftHumWaveform = GenerateRiftHum();

        // --- 9 Harmonic Chimes ---
        OctaveChime = GenerateChime(523.25f, 1046.5f);
        FifthChime = GenerateChime(523.25f, 783.99f);
        GoldenChime = GenerateGoldenRatioChime();
        FourthChime = GenerateChime(523.25f, 698.46f);
        MajorThirdChime = GenerateChime(523.25f, 659.25f);
        MinorThirdChime = GenerateChime(523.25f, 622.25f);
        MajorSixthChime = GenerateChime(523.25f, 880f);
        MinorSixthChime = GenerateChime(523.25f, 830.6f);
        TritoneChime = GenerateTritoneChime();

        // --- Stellar ambient sounds ---
        RedGiantPulse = GenerateRedGiantPulse();
        WhiteDwarfWhine = GenerateTone(1350f, 1f, 0.08f);
        BrownDwarfRumble = GenerateBrownDwarfRumble();
        MainSequenceHum = GenerateMainSequenceHum();

        // --- Nebula ambient sounds ---
        EmissionDrone = GenerateEmissionDrone();
        ReflectionShimmer = GenerateReflectionShimmer();
        PlanetaryLayers = GeneratePlanetaryLayers();
        SupernovaChaos = GenerateSupernovaChaos();

        // --- Exoplanet ambient sounds ---
        HotJupiterRoar = GenerateHotJupiterRoar();
        SuperEarthTone = GenerateSuperEarthTone();
        OceanWorldFlow = GenerateOceanWorldFlow();
        RogueOminous = GenerateTone(95f, 1f, 0.06f);  // raised from 50 Hz so it's audible, still low/ominous
        IceChime = GenerateIceChime();
    }

    #endregion

    #region Generator helpers

    /// <summary>Build a constant-amplitude pure sine tone of the given frequency, duration, and level.</summary>
    // --- Simple tone helper ---
    private static float[] GenerateTone(float freq, float duration, float amplitude)
    {
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            buf[i] = amplitude * MathF.Sin(TwoPi * freq * t);
        }
        return buf;
    }

    /// <summary>Two summed sine partials shaped by an exponential decay envelope — a generic bell/chime.</summary>
    // --- Chime with exponential decay (two frequencies) ---
    private static float[] GenerateChime(float freq1, float freq2,
                                         float duration = 0.4f, float decay = 0.15f, float amp = 0.15f)
    {
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = MathF.Exp(-t / decay);
            buf[i] = amp * envelope * (MathF.Sin(TwoPi * freq1 * t) + MathF.Sin(TwoPi * freq2 * t));
        }
        return buf;
    }

    /// <summary>Decaying chime whose three partials are stacked at golden-ratio multiples of 432 Hz.</summary>
    // --- Golden ratio chime: 432 Hz + 432*PHI + 432*PHI^2 ---
    private static float[] GenerateGoldenRatioChime()
    {
        const float baseFreq = 432f;
        const float duration = 0.4f;
        const float decay = 0.15f;
        const float amp = 0.15f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = MathF.Exp(-t / decay);
            float signal = MathF.Sin(TwoPi * baseFreq * t)
                         + MathF.Sin(TwoPi * baseFreq * PHI * t)
                         + MathF.Sin(TwoPi * baseFreq * PHI * PHI * t);
            buf[i] = amp * envelope * signal;
        }
        return buf;
    }

    /// <summary>Dissonant tritone chime (C + F#) plus a quiet low octave for added tension.</summary>
    // --- Tritone chime with low rumble ---
    private static float[] GenerateTritoneChime()
    {
        const float duration = 0.4f;
        const float decay = 0.15f;
        const float amp = 0.15f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = MathF.Exp(-t / decay);
            float signal = MathF.Sin(TwoPi * 523.25f * t)
                         + MathF.Sin(TwoPi * 739.99f * t)
                         + 0.5f * MathF.Sin(TwoPi * 261.63f * t); // low rumble
            buf[i] = amp * envelope * signal;
        }
        return buf;
    }

    /// <summary>Lush seven-second chord on 432 Hz with extra partials, shaped by a two-hump swell envelope.</summary>
    // --- Golden Chord: 7s, base 432 + 540 + 685 + PHI overtones, double swell ---
    private static float[] GenerateGoldenChord()
    {
        const float duration = 7f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            // Double swell envelope: two humps over the duration
            float env = MathF.Sin(MathF.PI * t / duration) * MathF.Sin(MathF.PI * 2f * t / duration);
            env = MathF.Abs(env);
            float signal = MathF.Sin(TwoPi * 432f * t)
                         + 0.7f * MathF.Sin(TwoPi * 540f * t)
                         + 0.5f * MathF.Sin(TwoPi * 685f * t)
                         + 0.3f * MathF.Sin(TwoPi * 432f * PHI * t)
                         + 0.2f * MathF.Sin(TwoPi * 432f * PHI * PHI * t);
            buf[i] = 0.12f * env * signal;
        }
        return buf;
    }

    /// <summary>Steady one-second hum on 220 Hz with two golden-ratio overtones.</summary>
    // --- Rift Hum: 1s, 220 Hz + PHI harmonics ---
    private static float[] GenerateRiftHum()
    {
        const float duration = 1f;
        const float baseFreq = 220f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float signal = MathF.Sin(TwoPi * baseFreq * t)
                         + 0.5f * MathF.Sin(TwoPi * baseFreq * PHI * t)
                         + 0.3f * MathF.Sin(TwoPi * baseFreq * PHI * PHI * t);
            buf[i] = 0.1f * signal;
        }
        return buf;
    }

    /// <summary>Two-second ~110 Hz bass throb under a smooth sin^2 swell — the heartbeat of a red giant.</summary>
    // --- Red Giant Pulse: 2s, 110 Hz deep bass with sin^2 envelope (raised from 40 Hz to be audible) ---
    private static float[] GenerateRedGiantPulse()
    {
        const float duration = 2f;
        const float freq = 110f;   // raised from 40 Hz so the deep throb is actually audible/lockable
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = MathF.Sin(MathF.PI * t / duration);
            envelope *= envelope; // sin^2
            float signal = MathF.Sin(TwoPi * freq * t)
                         + 0.3f * MathF.Sin(TwoPi * freq * 2f * t); // soft octave adds presence
            buf[i] = 0.1f * envelope * signal;
        }
        return buf;
    }

    /// <summary>
    /// Low ~80 Hz rumble for brown-dwarf "failed stars": a soft octave on top for presence and a slow
    /// undulation. Deep but now actually audible — the old 25 Hz fundamental was below most ears/speakers.
    /// </summary>
    private static float[] GenerateBrownDwarfRumble()
    {
        const float duration = 1.5f;
        const float baseFreq = 80f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float undulation = 1f + 0.2f * MathF.Sin(TwoPi * 1.2f * t);   // slow rumble swell
            float signal = MathF.Sin(TwoPi * baseFreq * t)
                         + 0.4f * MathF.Sin(TwoPi * baseFreq * 2f * t);   // soft octave for presence
            buf[i] = 0.08f * undulation * signal;
        }
        return buf;
    }

    /// <summary>
    /// Warm one-second main-sequence star drone: a 220 Hz fundamental with a soft perfect fifth and
    /// a golden-ratio shimmer, gently undulating — an audible "sunlike" glow. Most stars are
    /// main-sequence, so this is the star sound the player hears most often.
    /// </summary>
    private static float[] GenerateMainSequenceHum()
    {
        const float duration = 1f;
        const float baseFreq = 220f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float shimmer = 1f + 0.15f * MathF.Sin(TwoPi * 2f * t);          // gentle slow undulation
            float signal = MathF.Sin(TwoPi * baseFreq * t)
                         + 0.4f * MathF.Sin(TwoPi * baseFreq * 1.5f * t)     // soft perfect fifth
                         + 0.2f * MathF.Sin(TwoPi * baseFreq * PHI * t);     // golden shimmer
            buf[i] = 0.07f * shimmer * signal;
        }
        return buf;
    }

    /// <summary>Warm one-second drone on 250 Hz with a softer fifth-ish 375 Hz partial.</summary>
    // --- Emission Drone: 1s, 250 Hz + 375 Hz harmonic ---
    private static float[] GenerateEmissionDrone()
    {
        const float duration = 1f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            buf[i] = 0.08f * (MathF.Sin(TwoPi * 250f * t)
                            + 0.5f * MathF.Sin(TwoPi * 375f * t));
        }
        return buf;
    }

    /// <summary>Bright 700 Hz tone gated by a 4 Hz tremolo for a cool shimmering effect.</summary>
    // --- Reflection Shimmer: 1s, 700 Hz with 4 Hz tremolo ---
    private static float[] GenerateReflectionShimmer()
    {
        const float duration = 1f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float tremolo = 0.5f + 0.5f * MathF.Sin(TwoPi * 4f * t);
            buf[i] = 0.08f * tremolo * MathF.Sin(TwoPi * 700f * t);
        }
        return buf;
    }

    /// <summary>Multi-layered 500 Hz tone with major-third and fifth partials for a planetary-nebula shell.</summary>
    // --- Planetary Layers: 1s, 500 Hz + 1.25x + 1.5x harmonics ---
    private static float[] GeneratePlanetaryLayers()
    {
        const float duration = 1f;
        const float baseFreq = 500f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            buf[i] = 0.07f * (MathF.Sin(TwoPi * baseFreq * t)
                            + 0.6f * MathF.Sin(TwoPi * baseFreq * 1.25f * t)
                            + 0.4f * MathF.Sin(TwoPi * baseFreq * 1.5f * t));
        }
        return buf;
    }

    /// <summary>Frequency sweep from 200 to 900 Hz blended with deterministic noise — a chaotic blast wave.</summary>
    // --- Supernova Chaos: 1s, swept 200-900 Hz with noise ---
    private static float[] GenerateSupernovaChaos()
    {
        const float duration = 1f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        var rng = new Random(42); // deterministic seed
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float sweepFreq = 200f + 700f * t / duration;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
            buf[i] = 0.1f * (MathF.Sin(TwoPi * sweepFreq * t) + noise);
        }
        return buf;
    }

    /// <summary>Vibrato-modulated 300-500 Hz tone with noise, evoking a roaring gas-giant furnace.</summary>
    // --- Hot Jupiter Roar: 1s, modulated 300-500 Hz with noise ---
    private static float[] GenerateHotJupiterRoar()
    {
        const float duration = 1f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        var rng = new Random(77);
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float modFreq = 300f + 200f * MathF.Sin(TwoPi * 3f * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.2f;
            buf[i] = 0.1f * (MathF.Sin(TwoPi * modFreq * t) + noise);
        }
        return buf;
    }

    /// <summary>Solid, resonant 350 Hz tone with its octave for a massive rocky world.</summary>
    // --- Super Earth Tone: 1s, 350 Hz + octave ---
    private static float[] GenerateSuperEarthTone()
    {
        const float duration = 1f;
        const float baseFreq = 350f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            buf[i] = 0.08f * (MathF.Sin(TwoPi * baseFreq * t)
                            + 0.5f * MathF.Sin(TwoPi * baseFreq * 2f * t));
        }
        return buf;
    }

    /// <summary>Flowing 275 Hz tone with a slow 1.5 Hz amplitude undulation, like gentle water.</summary>
    // --- Ocean World Flow: 1s, 275 Hz with gentle undulation ---
    private static float[] GenerateOceanWorldFlow()
    {
        const float duration = 1f;
        const float baseFreq = 275f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float undulation = 1f + 0.3f * MathF.Sin(TwoPi * 1.5f * t);
            buf[i] = 0.08f * undulation * MathF.Sin(TwoPi * baseFreq * t);
        }
        return buf;
    }

    /// <summary>Crystalline 800 Hz tone with two upper harmonics and a bell-like exponential decay.</summary>
    // --- Ice Chime: 1s, 800 Hz with harmonics, bell-like decay ---
    private static float[] GenerateIceChime()
    {
        const float duration = 1f;
        const float baseFreq = 800f;
        int samples = (int)(duration * SampleRate);
        var buf = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = MathF.Exp(-t / 0.3f);
            buf[i] = 0.1f * envelope * (MathF.Sin(TwoPi * baseFreq * t)
                                       + 0.5f * MathF.Sin(TwoPi * baseFreq * 2f * t)
                                       + 0.3f * MathF.Sin(TwoPi * baseFreq * 3f * t));
        }
        return buf;
    }

    #endregion
}
