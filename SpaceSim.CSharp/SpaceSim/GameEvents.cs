using System;
using System.Collections.Generic;
using SpaceSim.Models;

namespace SpaceSim;

// =========================================================================
//  EVENT ARGUMENT TYPES
// =========================================================================

public class SpeakEventArgs : EventArgs
{
    public string Message { get; init; } = "";
}

public class SoundEffectEventArgs : EventArgs
{
    public float[] Waveform { get; init; } = Array.Empty<float>();
    public float Pan { get; init; }
    public float Volume { get; init; } = 1f;
    public bool Loop { get; init; }
    public float Pitch { get; init; } = 1f;
}

public class ModeChangeEventArgs : EventArgs
{
    public string ModeName { get; init; } = "";
    public string Value { get; init; } = "";
}

public class StateChangeEventArgs : EventArgs
{
    public string StateName { get; init; } = "";
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}

public class ProximityEventArgs : EventArgs
{
    public CelestialBody? Body { get; init; }
    public float Distance { get; init; }
    public float Pan { get; init; }
    public bool Entering { get; init; }
}

public class RiftEventArgs : EventArgs
{
    public Rift Rift { get; init; } = null!;
    public string Action { get; init; } = ""; // "entered", "spawned", "faded"
}

public class CrystalEventArgs : EventArgs
{
    public string CrystalType { get; init; } = "";
    public string Chakra { get; init; } = "";
    public int TotalCollected { get; init; }
}

public class HarmonicEventArgs : EventArgs
{
    public string HarmonicName { get; init; } = "";
    public int[] Dims { get; init; } = Array.Empty<int>();
}

public class LandingEventArgs : EventArgs
{
    public bool IsLanding { get; init; } // true = landed, false = takeoff
    public CelestialBody? Planet { get; init; }
}

public class ConsciousnessEventArgs : EventArgs
{
    public string OldLevel { get; init; } = "";
    public string NewLevel { get; init; } = "";
    public float Value { get; init; }
}

public class TempleKeyEventArgs : EventArgs
{
    public string KeyName { get; init; } = "";
    public int KeyIndex { get; init; }
    public int TotalKeys { get; init; }
}

// =========================================================================
//  CENTRAL EVENT BUS
// =========================================================================

/// <summary>
/// Static event bus for decoupled communication between game systems.
/// Ship raises events, AudioSystem and SpaceSimGame subscribe.
/// </summary>
public static class GameEvents
{
    // --- Speech ---
    public static event EventHandler<SpeakEventArgs>? OnSpeak;

    // --- Audio ---
    public static event EventHandler<SoundEffectEventArgs>? OnPlaySound;
    public static event EventHandler? OnClearAllSounds;
    public static event EventHandler? OnStopAmbientSounds;

    // --- Mode / State Changes ---
    public static event EventHandler<ModeChangeEventArgs>? OnModeChanged;
    public static event EventHandler<StateChangeEventArgs>? OnStateChanged;

    // --- Gameplay Events ---
    public static event EventHandler<ProximityEventArgs>? OnProximityChanged;
    public static event EventHandler<RiftEventArgs>? OnRiftEvent;
    public static event EventHandler<CrystalEventArgs>? OnCrystalCollected;
    public static event EventHandler<HarmonicEventArgs>? OnHarmonicDetected;
    public static event EventHandler<LandingEventArgs>? OnLandingEvent;
    public static event EventHandler<ConsciousnessEventArgs>? OnConsciousnessChanged;
    public static event EventHandler<TempleKeyEventArgs>? OnTempleKeyCollected;
    public static event EventHandler? OnAscension;
    public static event EventHandler? OnUniverseRegenNeeded;
    public static event EventHandler? OnMerkabaActivated;
    public static event EventHandler? OnMerkabaDeactivated;

    // =====================================================================
    //  RAISE HELPERS (fire-and-forget, null-safe)
    // =====================================================================

    public static void RaiseSpeak(object? sender, string message)
        => OnSpeak?.Invoke(sender, new SpeakEventArgs { Message = message });

    public static void RaisePlaySound(object? sender, float[] waveform,
        float pan = 0f, float volume = 1f, bool loop = false, float pitch = 1f)
        => OnPlaySound?.Invoke(sender, new SoundEffectEventArgs
        {
            Waveform = waveform, Pan = pan, Volume = volume, Loop = loop, Pitch = pitch
        });

    public static void RaiseClearAllSounds(object? sender)
        => OnClearAllSounds?.Invoke(sender, EventArgs.Empty);

    public static void RaiseStopAmbientSounds(object? sender)
        => OnStopAmbientSounds?.Invoke(sender, EventArgs.Empty);

    public static void RaiseModeChanged(object? sender, string modeName, string value)
        => OnModeChanged?.Invoke(sender, new ModeChangeEventArgs { ModeName = modeName, Value = value });

    public static void RaiseStateChanged(object? sender, string stateName, object? oldValue = null, object? newValue = null)
        => OnStateChanged?.Invoke(sender, new StateChangeEventArgs { StateName = stateName, OldValue = oldValue, NewValue = newValue });

    public static void RaiseProximityChanged(object? sender, CelestialBody? body, float distance, float pan, bool entering)
        => OnProximityChanged?.Invoke(sender, new ProximityEventArgs { Body = body, Distance = distance, Pan = pan, Entering = entering });

    public static void RaiseRiftEvent(object? sender, Rift rift, string action)
        => OnRiftEvent?.Invoke(sender, new RiftEventArgs { Rift = rift, Action = action });

    public static void RaiseCrystalCollected(object? sender, string crystalType, string chakra, int total)
        => OnCrystalCollected?.Invoke(sender, new CrystalEventArgs { CrystalType = crystalType, Chakra = chakra, TotalCollected = total });

    public static void RaiseHarmonicDetected(object? sender, string harmonicName, int[] dims)
        => OnHarmonicDetected?.Invoke(sender, new HarmonicEventArgs { HarmonicName = harmonicName, Dims = dims });

    public static void RaiseLandingEvent(object? sender, bool isLanding, CelestialBody? planet = null)
        => OnLandingEvent?.Invoke(sender, new LandingEventArgs { IsLanding = isLanding, Planet = planet });

    public static void RaiseConsciousnessChanged(object? sender, string oldLevel, string newLevel, float value)
        => OnConsciousnessChanged?.Invoke(sender, new ConsciousnessEventArgs { OldLevel = oldLevel, NewLevel = newLevel, Value = value });

    public static void RaiseTempleKeyCollected(object? sender, string keyName, int keyIndex, int totalKeys)
        => OnTempleKeyCollected?.Invoke(sender, new TempleKeyEventArgs { KeyName = keyName, KeyIndex = keyIndex, TotalKeys = totalKeys });

    public static void RaiseAscension(object? sender)
        => OnAscension?.Invoke(sender, EventArgs.Empty);

    public static void RaiseUniverseRegenNeeded(object? sender)
        => OnUniverseRegenNeeded?.Invoke(sender, EventArgs.Empty);

    public static void RaiseMerkabaActivated(object? sender)
        => OnMerkabaActivated?.Invoke(sender, EventArgs.Empty);

    public static void RaiseMerkabaDeactivated(object? sender)
        => OnMerkabaDeactivated?.Invoke(sender, EventArgs.Empty);
}
