using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// A sound dictionary: an accessible list of the game's celestial ambients and navigation cues so the
/// player can learn what each one means. Arrow to an entry to hear its name and description; press
/// Enter (or Space) to play and loop the sound, again to stop; Escape returns to the main menu.
/// </summary>
public sealed class LearnSoundsScreen : MenuScreen
{
    #region Sound entry

    /// <summary>
    /// One learnable cue: a friendly name and a spoken description. <see cref="Waveform"/> is the sound
    /// to audition; when it's null the entry is explanation-only (a dynamic cue with no fixed sample),
    /// and Enter just re-reads the description.
    /// </summary>
    private sealed record SoundEntry(string Name, string Description, float[]? Waveform = null);

    #endregion

    #region Fields

    private readonly List<SoundEntry> _sounds;
    private readonly string[] _labels;

    // The currently-looping demo (NAudio SFX) and which entry it belongs to (-1 = nothing playing).
    private GameSoundEffect? _demo;
    private int _playingIndex = -1;

    #endregion

    #region Construction

    public LearnSoundsScreen(AudioSystem audio, Action<string> speak) : base(audio, speak)
    {
        _sounds = BuildCatalog(audio);
        _labels = new string[_sounds.Count];
        for (int i = 0; i < _sounds.Count; i++)
            _labels[i] = _sounds[i].Name;
    }

    /// <summary>The catalog of demoable sounds, grouped stars -> nebulae -> planets -> navigation cues.</summary>
    private static List<SoundEntry> BuildCatalog(AudioSystem a) => new()
    {
        // Stars (proximity ambients — they come from the object's direction and grow louder up close)
        new("Main sequence star", "A warm, steady hum, and the most common star. Star, planet, and nebula sounds come from the object's direction and grow louder as you near it.", a.MainSequenceHum),
        new("Red giant star", "A deep, slow bass pulse.", a.RedGiantPulse),
        new("White dwarf star", "A high, thin whine.", a.WhiteDwarfWhine),
        new("Brown dwarf star", "A low, faint rumble.", a.BrownDwarfRumble),
        // Nebulae
        new("Emission nebula", "A warm drone.", a.EmissionDrone),
        new("Reflection nebula", "A cool, shimmering tremolo.", a.ReflectionShimmer),
        new("Planetary nebula", "A layered, harmonic tone.", a.PlanetaryLayers),
        new("Supernova remnant", "A chaotic, noisy sweep.", a.SupernovaChaos),
        // Planets
        new("Hot Jupiter", "A roaring furnace.", a.HotJupiterRoar),
        new("Super Earth", "A solid, resonant tone.", a.SuperEarthTone),
        new("Ocean world", "A gently flowing, watery tone.", a.OceanWorldFlow),
        new("Rogue planet", "A faint, ominous low tone.", a.RogueOminous),
        new("Ice giant", "A bright, crystalline chime.", a.IceChime),
        // Navigation & feedback (what each one means, not just how it sounds)
        new("Harmonic Chamber hum", "The looping hum of a nearby rift. It comes from the chamber's direction and rises in pitch as you approach.", a.RiftHumWaveform),
        new("Proximity beep", "Marks a nearby object. The faster it repeats, the closer you are.", a.BeepWaveform),
        new("Harmonic Chamber beep", "The homing beep of a locked rift, from the chamber's direction.", a.RiftBeepWaveform),
        new("Golden chord", "Plays when all realms reach golden harmony.", a.GoldenChordWaveform),
        new("Octave chime", "Plays when two realms align an octave apart, a two-to-one ratio, granting a bonus.", a.OctaveChime),
        new("Perfect fifth chime", "Plays when two realms align a perfect fifth apart, three to two.", a.FifthChime),
        new("Golden ratio chime", "Plays when two realms align in the golden ratio.", a.GoldenChime),
        // Dynamic cues — no fixed sample to play; these explain a sound you hear while flying or tuning
        new("Tuning beat", "While you tune the selected realm, a tone pulses at how far off you are. It slows as you approach the target and steadies into a clear tone when you lock on. Tune until the pulsing stops."),
        new("Resonance click", "A click that ticks while your tuning is strong; the better your resonance, the more often it ticks."),
        new("Doppler shift", "Sounds out in the world rise in pitch as you fly toward them and fall as you fly away, hinting at your motion."),
    };

    #endregion

    #region Menu surface

    public override string Title => "Learn Sounds";
    protected override IReadOnlyList<string> ItemLabels => _labels;

    protected override string EntryHint =>
        "Up and down to browse, Enter to play a sound or repeat a description, Escape to go back.";

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Escape))
            return ScreenTransition.BackToMainMenu;

        if (Pressed(keys, prev, Keys.Up)) { StopDemo(); MoveSelection(-1); }
        else if (Pressed(keys, prev, Keys.Down)) { StopDemo(); MoveSelection(+1); }
        else if (Pressed(keys, prev, Keys.Enter) || Pressed(keys, prev, Keys.Space)) ToggleDemo();

        return ScreenTransition.None;
    }

    #endregion

    #region Lifecycle

    /// <summary>Stop any demo when leaving the screen.</summary>
    public override void OnExit() => StopDemo();

    /// <summary>Speak the highlighted sound's name and description on navigation.</summary>
    protected override void AnnounceSelection()
    {
        var s = _sounds[SelectedIndex];
        Speak($"{s.Name}. {s.Description}");
    }

    #endregion

    #region Demo playback

    /// <summary>Start the highlighted sound (looping) if it isn't playing; stop it if it is.</summary>
    private void ToggleDemo()
    {
        var entry = _sounds[SelectedIndex];
        if (entry.Waveform == null)
        {
            // Explanation-only cue (no fixed sample) — just re-read its description.
            AnnounceSelection();
            return;
        }
        if (_playingIndex == SelectedIndex) { StopDemo(); return; }
        StopDemo();
        _demo = new GameSoundEffect(entry.Waveform, pan: 0f, loop: true, volume: GameConstants.LearnSoundGain);
        Audio.AddSoundEffect(_demo);
        _playingIndex = SelectedIndex;
    }

    /// <summary>Stop the current looping demo, if any (it plays out its current pass silently, then drops).</summary>
    private void StopDemo()
    {
        if (_demo == null) return;
        _demo.Loop = false;
        _demo.Volume = 0f;
        _demo = null;
        _playingIndex = -1;
    }

    #endregion
}
