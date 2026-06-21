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

        // Each slot applies its own tighter radius, or stops when nothing of that type is in range.
        if (star != null && dStar < GameConstants.StarHarmonyRadius) HandleStarAmbient(star, dStar);
        else StopWorldLoop(ref _starSound);

        if (planet != null && dPlanet < GameConstants.InteractionDistance) HandlePlanetAmbient(planet, dPlanet);
        else StopWorldLoop(ref _planetSound);

        if (nebula != null && dNebula < GameConstants.NebulaDissonanceRadius) HandleNebulaAmbient(nebula, dNebula);
        else StopWorldLoop(ref _nebulaSound);
    }

    /// <summary>Looping ambient for the nearest star — type-specific timbre, including a warm hum for the common main-sequence stars.</summary>
    private void HandleStarAmbient(CelestialBody body, float dist)
    {
        var sType = body.StellarClass ?? StellarType.MainSequence;
        float volume = _audio.EffectVolume * (1f - dist / GameConstants.StarHarmonyRadius) * 0.3f;
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
    private void HandleNebulaAmbient(CelestialBody body, float dist)
    {
        var nType = body.NebulaClass ?? NebulaType.Emission;
        float volume = _audio.EffectVolume * (1f - dist / GameConstants.NebulaDissonanceRadius) * 0.4f;
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
    private void HandlePlanetAmbient(CelestialBody body, float dist)
    {
        var eType = body.ExoplanetClass ?? ExoplanetType.SuperEarth;
        float volume = _audio.EffectVolume * (1f - dist / GameConstants.InteractionDistance) * 0.3f;
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
            slot.Voice.Update(SpatialAudioMath.ToListenerSpace(Position, worldPos, ViewRotation), volume);
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

    /// <summary>Stop every looping proximity ambient (star, nebula, planet) — e.g. when none are in range.</summary>
    private void StopAllAmbientSounds()
    {
        StopWorldLoop(ref _starSound);
        StopWorldLoop(ref _nebulaSound);
        StopWorldLoop(ref _planetSound);
    }

    #endregion
}
