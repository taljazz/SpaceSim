using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    /// <summary>One learnable sound: a friendly name, a spoken description, and its waveform.</summary>
    private sealed record SoundEntry(string Name, string Description, float[] Waveform);

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
        // Stars
        new("Main sequence star", "A warm, steady hum. The most common star.", a.MainSequenceHum),
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
        // Navigation & feedback
        new("Harmonic chamber hum", "The hum of a nearby rift.", a.RiftHumWaveform),
        new("Proximity beep", "Plays when something is near.", a.BeepWaveform),
        new("Rift beep", "Plays when a harmonic chamber is near.", a.RiftBeepWaveform),
        new("Golden chord", "Plays when you reach golden harmony.", a.GoldenChordWaveform),
        new("Octave chime", "A detected octave, two to one.", a.OctaveChime),
        new("Perfect fifth chime", "A detected perfect fifth, three to two.", a.FifthChime),
        new("Golden ratio chime", "A detected golden-ratio harmony.", a.GoldenChime),
    };

    #endregion

    #region Menu surface

    public override string Title => "Learn Sounds";
    protected override IReadOnlyList<string> ItemLabels => _labels;

    protected override string EntryHint =>
        "Up and down to browse, Enter to play or stop a sound, Escape to go back.";

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
        if (_playingIndex == SelectedIndex) { StopDemo(); return; }
        StopDemo();
        var entry = _sounds[SelectedIndex];
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

    #region Rendering

    /// <summary>Footer: the selected sound's description plus the controls hint.</summary>
    protected override void DrawFooter(SpriteBatch sb, SpriteFont font, int screenW, int screenH)
    {
        string desc = _sounds[SelectedIndex].Description;
        string hint = "Enter: play / stop      Escape: back";

        Vector2 dSize = font.MeasureString(desc);
        sb.DrawString(font, desc, new Vector2((screenW - dSize.X) / 2f, screenH * 0.82f), Color.LightGray);
        Vector2 hSize = font.MeasureString(hint);
        sb.DrawString(font, hint, new Vector2((screenW - hSize.X) / 2f, screenH * 0.90f), Color.Gray);
    }

    #endregion
}
