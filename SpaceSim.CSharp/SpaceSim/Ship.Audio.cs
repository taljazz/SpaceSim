using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;

namespace SpaceSim;

public partial class Ship
{
    #region Proximity ambient audio helpers

    /// <summary>
    /// Update the looping ambient soundscape from the nearest star, planet, and nebula — found in a
    /// single spatial scan in Ship.Update and passed in here. Each gets its own positioned 3D voice
    /// (so a star is heard even when one of its planets is closer), and each slot stops the moment
    /// its type leaves range (no more stale, stuck drones). With HRTF the simultaneous sources are
    /// individually locatable.
    /// </summary>
    private void UpdateProximityAmbients(
        CelestialBody? star, float dStar,
        CelestialBody? planet, float dPlanet,
        CelestialBody? nebula, float dNebula)
    {
        if (!AmbientSoundsEnabled)
        {
            StopAllAmbientSounds();
            return;
        }

        // Consciousness opens the soundscape: the higher the pilot's awareness, the farther these ambient
        // voices carry, so more of the universe becomes audible as they rise. Each slot still applies its
        // own (now consciousness-scaled) radius, or stops when nothing of that type is in range.
        float hear = ConsciousnessHearingMult();
        float starR = GameConstants.StarHarmonyRadius * hear;
        float planetR = GameConstants.InteractionDistance * hear;
        float nebulaR = GameConstants.NebulaDissonanceRadius * hear;

        if (star != null && dStar < starR) HandleStarAmbient(star, dStar, starR);
        else StopWorldLoop(ref _starSound);

        if (planet != null && dPlanet < planetR) HandlePlanetAmbient(planet, dPlanet, planetR);
        else StopWorldLoop(ref _planetSound);

        if (nebula != null && dNebula < nebulaR) HandleNebulaAmbient(nebula, dNebula, nebulaR);
        else StopWorldLoop(ref _nebulaSound);
    }

    /// <summary>Looping ambient for the nearest star — type-specific timbre, including a warm hum for the common main-sequence stars.</summary>
    private void HandleStarAmbient(CelestialBody body, float dist, float radius)
    {
        var sType = body.StellarClass ?? StellarType.MainSequence;
        float volume = ComputeAmbientGain(dist, radius, 0.9f);
        float[] waveform = sType switch
        {
            StellarType.RedGiant => _audio.RedGiantPulse,
            StellarType.WhiteDwarf => _audio.WhiteDwarfWhine,
            StellarType.BrownDwarf => _audio.BrownDwarfRumble,
            _ => _audio.MainSequenceHum   // main-sequence stars now have an audible warm drone
        };
        UpdateWorldLoop(ref _starSound, waveform, body.Position, volume);
    }

    /// <summary>Looping ambient for the nearest nebula — type-specific drone / shimmer / chaos.</summary>
    private void HandleNebulaAmbient(CelestialBody body, float dist, float radius)
    {
        var nType = body.NebulaClass ?? NebulaType.Emission;
        float volume = ComputeAmbientGain(dist, radius, 1.0f);
        float[] waveform = nType switch
        {
            NebulaType.Emission => _audio.EmissionDrone,
            NebulaType.Reflection => _audio.ReflectionShimmer,
            NebulaType.Planetary => _audio.PlanetaryLayers,
            NebulaType.SupernovaRemnant => _audio.SupernovaChaos,
            _ => _audio.EmissionDrone
        };
        UpdateWorldLoop(ref _nebulaSound, waveform, body.Position, volume);
    }

    /// <summary>Looping ambient for the nearest planet — type-specific timbre.</summary>
    private void HandlePlanetAmbient(CelestialBody body, float dist, float radius)
    {
        var eType = body.ExoplanetClass ?? ExoplanetType.SuperEarth;
        float volume = ComputeAmbientGain(dist, radius, 0.9f);
        float[] waveform = eType switch
        {
            ExoplanetType.HotJupiter => _audio.HotJupiterRoar,
            ExoplanetType.SuperEarth => _audio.SuperEarthTone,
            ExoplanetType.OceanWorld => _audio.OceanWorldFlow,
            ExoplanetType.RoguePlanet => _audio.RogueOminous,
            ExoplanetType.IceGiant => _audio.IceChime,
            _ => _audio.SuperEarthTone
        };
        UpdateWorldLoop(ref _planetSound, waveform, body.Position, volume);
    }

    /// <summary>
    /// Reconcile a looping world-sound slot with the desired waveform at a world position. If the
    /// slot already plays that waveform, refresh its position/volume; if it plays a different one,
    /// stop the old loop and start the new one. A null waveform leaves the slot untouched.
    /// </summary>
    private void UpdateWorldLoop(ref WorldSound? slot, float[]? waveform, float[] worldPos, float volume)
    {
        if (waveform == null) return;

        // Waveform changed (e.g. flew from one star type to another) -> restart the slot.
        if (slot != null && !ReferenceEquals(slot.Waveform, waveform))
            StopWorldLoop(ref slot);

        if (slot == null)
        {
            slot = StartWorldLoop(waveform, worldPos, volume);
            return;
        }

        // Same waveform still playing -> just move it and adjust its level.
        if (slot.Voice != null)
            slot.Voice.Update(SpatialAudioMath.ToListenerSpace(Position, worldPos, ViewRotation), volume, DopplerPitch(worldPos));
        else if (slot.Sfx != null)
        {
            slot.Sfx.Pan = ComputePan(worldPos);
            slot.Sfx.Volume = volume;
        }
    }

    /// <summary>
    /// Start a looping positioned sound, preferring OpenAL + HRTF and falling back to a panned
    /// NAudio effect when spatial audio is unavailable.
    /// </summary>
    private WorldSound StartWorldLoop(float[] waveform, float[] worldPos, float volume)
    {
        var ws = new WorldSound(waveform);

        if (_openAl.IsAvailable)
        {
            var pos = SpatialAudioMath.ToListenerSpace(Position, worldPos, ViewRotation);
            ws.Voice = _openAl.PlayLoop(waveform, pos, volume);
            if (ws.Voice != null) return ws;
            // PlayLoop unexpectedly failed (e.g. source exhaustion) -> fall back to NAudio.
        }

        ws.Sfx = new GameSoundEffect(waveform, pan: ComputePan(worldPos), loop: true, volume: volume);
        _audio.AddSoundEffect(ws.Sfx);
        return ws;
    }

    /// <summary>Stop and clear a looping world-sound slot (whichever backend it used).</summary>
    private void StopWorldLoop(ref WorldSound? slot)
    {
        if (slot == null) return;
        slot.Voice?.Stop();
        if (slot.Sfx != null)
        {
            slot.Sfx.Loop = false;
            slot.Sfx.Volume = 0;
            // Mark it finished so the mixer drops it from _activePlayback on the next callback, instead of
            // stepping a silent (volume 0) loop until it happens to reach its end (OpenAL-less fallback path).
            slot.Sfx.Position = slot.Sfx.Waveform.Length;
        }
        slot = null;
    }

    /// <summary>Stereo pan (-1..+1) toward a world position — the legacy directional cue, used for the NAudio fallback.</summary>
    private float ComputePan(float[] worldPos)
    {
        var proj = ProjectRelative(worldPos);
        float angle = MathF.Atan2(proj.Y, proj.X);
        return MathF.Sin(angle);
    }

    /// <summary>
    /// Fire a one-shot positioned sound (a beep) toward a world position — OpenAL + HRTF when available,
    /// falling back to a stereo-panned NAudio effect. Used for the rift locator and proximity beeps.
    /// </summary>
    private void PlayWorldOneShot(float[] waveform, float[] worldPos, float volume)
    {
        if (_openAl.IsAvailable)
            _openAl.PlayOneShot(waveform, SpatialAudioMath.ToListenerSpace(Position, worldPos, ViewRotation), volume, DopplerPitch(worldPos));
        else
            GameEvents.RaisePlaySound(this, waveform, pan: ComputePan(worldPos), volume: volume);
    }

    /// <summary>
    /// Doppler pitch for a world source: shifts up when the ship moves toward it, down when moving
    /// away (scaled by the radial component of the ship's velocity, clamped to a sane range). 1.0 when
    /// stationary or moving across the source. OpenAL-only — the NAudio fallback can't pitch-shift.
    /// </summary>
    private float DopplerPitch(float[] worldPos)
        => SpatialAudioMath.DopplerPitch(Position, Velocity, worldPos,
               GameConstants.DopplerScale, GameConstants.DopplerMinPitch, GameConstants.DopplerMaxPitch);

    /// <summary>
    /// Loudness for a proximity ambient: a strong, distance-faded level so the universe is actually
    /// audible up close. The normalized ambient waveforms peak very low (~0.1), so we boost them
    /// (gains above 1 amplify the quiet buffers) and clamp per-voice so overlapping ambients don't
    /// clip. EffectVolume trims the level (0.55..1.0) but can't crush it to silence; MasterVolume is
    /// applied downstream (OpenAL listener gain / NAudio master).
    /// </summary>
    private float ComputeAmbientGain(float dist, float radius, float typeWeight)
    {
        float proximity = MathHelper.Clamp(1f - dist / radius, 0f, 1f);
        float effectTrim = 0.55f + 0.45f * _audio.EffectVolume;
        float gain = proximity * typeWeight * GameConstants.AmbientGain * effectTrim;
        return MathF.Min(gain, GameConstants.AmbientMaxVoiceGain);
    }

    /// <summary>Stop every looping proximity ambient (star, nebula, planet) — e.g. when none are in range.</summary>
    private void StopAllAmbientSounds()
    {
        StopWorldLoop(ref _starSound);
        StopWorldLoop(ref _nebulaSound);
        StopWorldLoop(ref _planetSound);
    }

    /// <summary>Stop the looping hum of every active rift (the voices, not the rift objects themselves).</summary>
    private void StopAllRiftSounds()
    {
        foreach (var rift in Rifts)
            StopWorldLoop(ref rift.Sound);
    }

    /// <summary>
    /// Stop every positional world sound: proximity ambients, rift hums, and the target-lock beacon.
    /// A clear-all only drains the NAudio mix, so leaving the sim or ascending calls this to make sure
    /// no OpenAL world voice keeps playing.
    /// </summary>
    internal void SilenceAllWorldSounds()
    {
        StopAllAmbientSounds();
        StopWorldLoop(ref LockSound);
        StopAllRiftSounds();
    }

    #endregion
}
