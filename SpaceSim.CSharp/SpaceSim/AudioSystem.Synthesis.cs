using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpaceSim;

public partial class AudioSystem
{
    #region ISampleProvider.Read -- the audio callback

    /// <summary>
    /// Called by NAudio on the audio thread. Fills the interleaved stereo buffer [L,R,L,R,...].
    ///
    /// <para>
    /// Every continuous voice is generated from a per-sample PHASE ACCUMULATOR (advanced by
    /// 2*PI*freq/SampleRate) rather than evaluated as sin(2*PI*freq*t) against absolute time. That is
    /// what keeps the drive click-free while the player tunes: a frequency change only bends the slope
    /// of the phase ramp, it never steps the phase value. Gains that multiply a continuous tone are
    /// smoothed per buffer for the same reason (a stepped amplitude clicks too).
    /// </para>
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / Channels;

        // During an output-device swap, emit silence so a late callback from the old device (or the first
        // from the new one) can't read half-torn-down state.
        if (_deviceSwitching)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        // --- Drain pending sound effects into active playback list ---
        // This is the only lock in the audio callback, held for microseconds (just a list swap).
        if (_clearAllFlag) { _activePlayback.Clear(); _clearAllFlag = false; }
        if (_clearLoopingFlag) { _activePlayback.RemoveAll(sfx => sfx.Loop); _clearLoopingFlag = false; }
        if (_clearFinishedFlag) { _activePlayback.RemoveAll(sfx => sfx.IsFinished); _clearFinishedFlag = false; }
        lock (_pendingLock)
        {
            if (_pendingSfx.Count > 0)
            {
                _activePlayback.AddRange(_pendingSfx);
                _pendingSfx.Clear();
            }
        }

        // --- Snapshot ship state (volatile read, no lock needed) ---
        Array.Clear(_snapRDrive);
        Array.Clear(_snapFTarget);
        float resonanceWidth = GameConstants.ResonanceWidthBase;
        bool isCharging = false;
        float chargeProgress = 0f;

        Ship? ship = _ship; // volatile read

        // The engine (drive synthesis + harmonics) only runs while the sim is active; at the menus
        // EngineEnabled is false and just the queued sound effects (menu ticks, sound demos) play.
        bool engineOn = ship != null && EngineEnabled;

        // --- Per-buffer gain smoothing (amplitude steps click too, so glide them) ---
        // _masterGain tracks MasterVolume always (it also scales menu sounds). _driveGain ramps to zero
        // while the engine is off, so resuming from a menu fades the drive back in click-free (the phase
        // accumulators are never reset, so they stay continuous across the silence).
        _masterGain += (MasterVolume - _masterGain) * GameConstants.GainSmoothingPerBuffer;
        float driveTarget = engineOn ? DriveVolume : 0f;
        _driveGain += (driveTarget - _driveGain) * GameConstants.GainSmoothingPerBuffer;

        // Tune-by-ear cue (selected realm): closeness + a countable pulse + a signed direction, present only
        // while actively tuning. Targets computed once per buffer here; smoothed and emitted below.
        float cueAmpTarget = 0f;
        float cuePanLTarget = _cuePanLSmoothed, cuePanRTarget = _cuePanRSmoothed;

        if (engineOn)
        {
            // engineOn guarantees the ship is non-null; copy its volatile state into the snapshots.
            Ship s = ship!;
            for (int i = 0; i < NDimensions; i++)
            {
                _snapRDrive[i] = s.RDrive[i];
                _snapFTarget[i] = s.FTarget[i];
                _snapCueTarget[i] = s.CueTargetFreqs[i];
            }
            resonanceWidth = s.ResonanceWidth;
            isCharging = s.IsChargingRift;
            chargeProgress = s.RiftChargeProgress;

            DetectHarmonicPairs(_harmonicPairsBuffer, _snapRDrive);

            // Precompute per-dimension increments (constant within a buffer). The inner sample loop then
            // only advances accumulators, so a frequency change between buffers is click-free.
            for (int dim = 0; dim < NDimensions; dim++)
            {
                float baseFreq = _snapRDrive[dim] * 0.5f;
                _fundInc[dim] = TwoPiD * baseFreq / SampleRate;
                _overtoneInc[dim, 0] = TwoPiD * (baseFreq * PhiPow[0]) / SampleRate;
                _overtoneInc[dim, 1] = TwoPiD * (baseFreq * PhiPow[1]) / SampleRate;
                _overtoneInc[dim, 2] = TwoPiD * (baseFreq * PhiPow[2]) / SampleRate;
                _subInc[dim] = TwoPiD * (baseFreq / PHI) / SampleRate;

                // Vibrato depth/rate follow a SMOOTHED resonance so a resonance change can't step the
                // vibrato (the symptom the player reported as crackle "when the resonance changes").
                float delta = MathF.Abs(_snapRDrive[dim] - _snapFTarget[dim]);
                float resLevel = ResonancePhysics.Resonance(delta, resonanceWidth);
                _resSmoothed[dim] += (resLevel - _resSmoothed[dim]) * GameConstants.GainSmoothingPerBuffer;
                var (vibDepth, vibRate) = VibratoShape.DepthRate(_resSmoothed[dim]);
                _vibDepth[dim] = vibDepth;
                _vibInc1[dim] = TwoPiD * vibRate / SampleRate;
                _vibInc2[dim] = TwoPiD * (vibRate * PHI) / SampleRate;
            }

            // Intermodulation: ramp each pair's gain toward 1 (detected) / 0 (not) and advance its
            // stable-slot accumulators, so pairs appearing/disappearing or moving never pop or click.
            Array.Clear(_intermodDetected);
            foreach (var (dimA, dimB, _) in _harmonicPairsBuffer)
                _intermodDetected[PairIndex(dimA, dimB)] = true;
            for (int p = 0; p < PairCount; p++)
            {
                float fa = _snapRDrive[PairA[p]] * 0.5f;
                float fb = _snapRDrive[PairB[p]] * 0.5f;
                _intermodSumInc[p] = TwoPiD * (fa + fb) / SampleRate;
                float diff = MathF.Abs(fa - fb);
                _intermodDiffInc[p] = TwoPiD * diff / SampleRate;
                // Below ~2 Hz the difference tone is near-DC; ramp its term out rather than emit a frozen offset.
                _intermodDiffActive[p] = diff >= 2f && fa >= 1f && fb >= 1f;
                float target = _intermodDetected[p] ? 1f : 0f;
                _intermodGain[p] += (target - _intermodGain[p]) * GameConstants.GainSmoothingPerBuffer;
            }

            // The cue is present while tuning a realm by ear (recently tuned, or full tuning mode) — but not on
            // a planet, where crystal collection has its own beeps. It tracks the realm's CUE TARGET
            // (_snapCueTarget): a nearby claimable temple/pyramid note when one is in range, otherwise the
            // still centre BaseFTarget — never the breathing/jittered FTarget, so a perfectly-held tuning reads
            // as a steady lock instead of breathing in and out of it or flipping the direction cue every breath.
            bool activelyTuning = !s.LandedMode &&
                (s.TuningMode || (s.SimulationTime - s.LastTuneTime) < GameConstants.CueActiveWindow);
            if (activelyTuning)
            {
                int selDim = Math.Clamp(s.SelectedDim, 0, NDimensions - 1);
                float center = _snapCueTarget[selDim];
                var cue = CueShape.Compute(_snapRDrive[selDim] - center);
                _cueCarrierInc = TwoPiD * (center * 0.5f) / SampleRate; // carrier FIXED at the target pitch (a landmark)
                _cueAmInc = TwoPiD * cue.PulseRate / SampleRate;
                _cueLockBlend = cue.LockBlend;
                cueAmpTarget = GameConstants.BeatCueVolume * (GameConstants.CueFloor + (1f - GameConstants.CueFloor) * cue.Closeness);
                float panPos = Math.Clamp(GameConstants.DirPan * cue.Direction, -1f, 1f); // flat = left, sharp = right
                cuePanLTarget = 1f - MathF.Max(0f, panPos);
                cuePanRTarget = 1f + MathF.Min(0f, panPos);
            }
        }

        // Rift-charge rising tone increment (per buffer).
        double chargeInc = 0;
        float chargeAmp = 0f;
        if (engineOn && isCharging)
        {
            float chargeFreq = 200f + 600f * chargeProgress;
            chargeInc = TwoPiD * chargeFreq / SampleRate;
            chargeAmp = 0.08f * chargeProgress;
        }

        // Smooth the cue's amplitude and pan once per buffer so closeness, direction, and realm changes
        // glide instead of stepping a continuous carrier — and so the cue fades out gently when tuning
        // stops (cueAmpTarget is 0 then, while the persisted increments keep the carrier oscillating).
        _cueAmpSmoothed += (cueAmpTarget - _cueAmpSmoothed) * GameConstants.GainSmoothingPerBuffer;
        _cuePanLSmoothed += (cuePanLTarget - _cuePanLSmoothed) * GameConstants.GainSmoothingPerBuffer;
        _cuePanRSmoothed += (cuePanRTarget - _cuePanRSmoothed) * GameConstants.GainSmoothingPerBuffer;

        for (int frame = 0; frame < frames; frame++)
        {
            float left = 0f;
            float right = 0f;

            // --- 1. Schumann resonance carrier (always on, fixed frequency, its own accumulator) ---
            _schumannPhase += SchumannInc;
            if (_schumannPhase >= TwoPiD) _schumannPhase -= TwoPiD;
            float schumann = GameConstants.SchumannVolume * MathF.Sin((float)_schumannPhase);
            left += schumann;
            right += schumann;

            // --- 2. Per-dimension drive synthesis (phase-continuous) ---
            if (engineOn)
            {
                for (int dim = 0; dim < NDimensions; dim++)
                {
                    // Vibrato: an additive phase OFFSET (continuous), shared by this dimension's voices.
                    _vibPhase1[dim] += _vibInc1[dim];
                    if (_vibPhase1[dim] >= TwoPiD) _vibPhase1[dim] -= TwoPiD;
                    _vibPhase2[dim] += _vibInc2[dim];
                    if (_vibPhase2[dim] >= TwoPiD) _vibPhase2[dim] -= TwoPiD;
                    float vibratoPhase = _vibDepth[dim] *
                        (MathF.Sin((float)_vibPhase1[dim]) + 0.3f * MathF.Sin((float)_vibPhase2[dim]));

                    // Pure sine fundamental.
                    _fundPhase[dim] += _fundInc[dim];
                    if (_fundPhase[dim] >= TwoPiD) _fundPhase[dim] -= TwoPiD;
                    float signal = _driveGain * MathF.Sin((float)_fundPhase[dim] + vibratoPhase);

                    // Golden-ratio overtones (PHI^k, k=1..3).
                    for (int k = 0; k < 3; k++)
                    {
                        _overtonePhase[dim, k] += _overtoneInc[dim, k];
                        if (_overtonePhase[dim, k] >= TwoPiD) _overtonePhase[dim, k] -= TwoPiD;
                        float amp = _driveGain * 0.25f / (k + 1);
                        signal += amp * MathF.Sin((float)_overtonePhase[dim, k] + vibratoPhase);
                    }

                    // Subharmonic at baseFreq / PHI (half-depth vibrato, as before).
                    _subPhase[dim] += _subInc[dim];
                    if (_subPhase[dim] >= TwoPiD) _subPhase[dim] -= TwoPiD;
                    signal += _driveGain * 0.15f * MathF.Sin((float)_subPhase[dim] + vibratoPhase * 0.5f);

                    // Higher-dimension tremolo for dims 3 and 4 (its own LFO accumulator).
                    if (dim >= 3)
                    {
                        int m = dim - 3;
                        _modPhase[m] += ModInc[m];
                        if (_modPhase[m] >= TwoPiD) _modPhase[m] -= TwoPiD;
                        signal *= 1f + 0.3f * MathF.Sin((float)_modPhase[m]);
                    }

                    // Pan to stereo.
                    var (panL, panR) = DimensionPan[dim];
                    left += signal * panL;
                    right += signal * panR;
                }

                // --- 3. Intermodulation tones for harmonic pairs (ramped, stable slots) ---
                for (int p = 0; p < PairCount; p++)
                {
                    if (_intermodGain[p] < 0.0001f) continue;

                    _intermodSumPhase[p] += _intermodSumInc[p];
                    if (_intermodSumPhase[p] >= TwoPiD) _intermodSumPhase[p] -= TwoPiD;
                    float val = 0.5f * MathF.Sin((float)_intermodSumPhase[p]);

                    if (_intermodDiffActive[p])
                    {
                        _intermodDiffPhase[p] += _intermodDiffInc[p];
                        if (_intermodDiffPhase[p] >= TwoPiD) _intermodDiffPhase[p] -= TwoPiD;
                        val += 0.7f * MathF.Sin((float)_intermodDiffPhase[p]);
                    }

                    float intermod = GameConstants.IntermodDepth * _intermodGain[p] * val;
                    left += intermod * 0.5f;
                    right += intermod * 0.5f;
                }

                // --- 4. Rift charge rising tone ---
                if (isCharging)
                {
                    _chargePhase += chargeInc;
                    if (_chargePhase >= TwoPiD) _chargePhase -= TwoPiD;
                    float chargeTone = chargeAmp * MathF.Sin((float)_chargePhase);
                    left += chargeTone;
                    right += chargeTone;
                }

                // --- 4b. Tune-by-ear cue: fixed-pitch carrier (landmark), pan = direction, pulse + loudness = closeness ---
                if (_cueAmpSmoothed > 0.0001f)
                {
                    _beatCarrierPhase += _cueCarrierInc;
                    if (_beatCarrierPhase >= TwoPiD) _beatCarrierPhase -= TwoPiD;
                    _beatAmPhase += _cueAmInc;
                    if (_beatAmPhase >= TwoPiD) _beatAmPhase -= TwoPiD;

                    float tremolo = 0.5f + 0.5f * MathF.Cos((float)_beatAmPhase);
                    float cueEnv = tremolo + (1f - tremolo) * _cueLockBlend;
                    float cueTone = _cueAmpSmoothed * cueEnv * MathF.Sin((float)_beatCarrierPhase);
                    left += cueTone * _cuePanLSmoothed;
                    right += cueTone * _cuePanRSmoothed;
                }
            }

            // --- 5. Mix active sound effects (no lock — audio thread owns _activePlayback) ---
            for (int s = _activePlayback.Count - 1; s >= 0; s--)
            {
                var sfx = _activePlayback[s];
                if (sfx.Position >= sfx.Waveform.Length)
                {
                    if (sfx.Loop)
                    {
                        sfx.Position = 0;
                    }
                    else
                    {
                        _activePlayback.RemoveAt(s);
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

            // --- 6. Apply master volume and clip ---
            left *= _masterGain;
            right *= _masterGain;

            // Track when the mix would have hard-clipped (soft-clip now engaged), for diagnostics.
            if (left > 1f || left < -1f || right > 1f || right < -1f)
                _clippingCount++;

            // Soft-clip instead of hard-clamping: stacked voices at high master volume saturate
            // gracefully (tanh) and stay warm, instead of crackling into hard-clip distortion.
            left = MathF.Tanh(left);
            right = MathF.Tanh(right);

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
            int sfxCount = _activePlayback.Count;
            DebugLogger.Log("Audio", $"Stats: {_readCallCount} callbacks, {_clippingCount} clips, {sfxCount} active SFX");
            _readCallCount = 0;
            _clippingCount = 0;
            _lastLogTimeSec = currentSec;
        }

        return count;
    }

    #endregion

    #region Harmonic pair detection

    /// <summary>
    /// Check all dimension pairs for known harmonic ratios.
    /// Fills the provided list with (dimA, dimB, harmonicName) tuples (clears it first).
    /// </summary>
    public void DetectHarmonicPairs(List<(int DimA, int DimB, HarmonicType HType)> result, float[] rDrive)
    {
        result.Clear();

        // Check every unique pair of dimensions for a recognised musical interval. The shared
        // HarmonicMath helper keeps this detection identical to the Ship's gameplay-side version.
        for (int i = 0; i < NDimensions; i++)
            for (int j = i + 1; j < NDimensions; j++)
                if (HarmonicMath.TryMatchRatio(rDrive[i], rDrive[j], out var hType))
                    result.Add((i, j, hType));
    }

    #endregion
}
