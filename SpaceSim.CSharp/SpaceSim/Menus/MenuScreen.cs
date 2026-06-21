using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// Base class for the audio-first, keyboard-driven full-screen menus that sit <em>above</em> the
/// simulation — the main menu and the Learn Sounds dictionary. Subclasses supply a title and a list
/// of item labels; this base handles up/down navigation (with a click tick and a screen-reader
/// announcement) and draws a simple centered list. Built for blind players: every move is spoken.
///
/// <para>This is distinct from the in-sim <c>MenuMode</c> hierarchy (HUD/upgrade/starmap/rift), which
/// runs inside the live game; these screens replace the whole game view.</para>
/// </summary>
public abstract class MenuScreen
{
    #region Fields

    /// <summary>Audio engine — for the navigation tick and (in subclasses) sound playback.</summary>
    protected readonly AudioSystem Audio;

    /// <summary>Speaks a line through the screen reader, interrupting the previous announcement.</summary>
    protected readonly Action<string> Speak;

    /// <summary>Index of the highlighted item.</summary>
    protected int SelectedIndex;

    #endregion

    #region Construction

    protected MenuScreen(AudioSystem audio, Action<string> speak)
    {
        Audio = audio;
        Speak = speak;
    }

    #endregion

    #region Abstract surface

    /// <summary>Heading shown at the top and spoken on entry.</summary>
    public abstract string Title { get; }

    /// <summary>The selectable rows, top to bottom.</summary>
    protected abstract IReadOnlyList<string> ItemLabels { get; }

    /// <summary>Handle one frame of input; return the screen change to apply (or <see cref="ScreenTransition.None"/>).</summary>
    public abstract ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev);

    #endregion

    #region Lifecycle

    /// <summary>Called when this screen becomes active: reset to the top and announce it.</summary>
    public virtual void OnEnter()
    {
        SelectedIndex = 0;
        Speak($"{Title}. {EntryHint} {CurrentLabel}.");
    }

    /// <summary>Called when leaving this screen (subclasses stop any playing demo here).</summary>
    public virtual void OnExit() { }

    /// <summary>Short spoken hint appended after the title on entry (e.g. how to navigate).</summary>
    protected virtual string EntryHint => "Use up and down arrows to move, Enter to select.";

    #endregion

    #region Navigation helpers

    /// <summary>The currently highlighted label.</summary>
    protected string CurrentLabel => ItemLabels[SelectedIndex];

    /// <summary>Move the selection by <paramref name="delta"/> (wrapping), tick, and announce the new item.</summary>
    protected void MoveSelection(int delta)
    {
        int n = ItemLabels.Count;
        if (n == 0) return;
        SelectedIndex = (SelectedIndex + delta + n) % n;
        PlayTick();
        AnnounceSelection();
    }

    /// <summary>Announce the highlighted item. Overridden (e.g. by Learn Sounds) to add detail.</summary>
    protected virtual void AnnounceSelection() => Speak(CurrentLabel);

    /// <summary>Short navigation click for menu feedback.</summary>
    protected void PlayTick()
    {
        Audio.AddSoundEffect(new GameSoundEffect(Audio.ClickWaveform, pan: 0f, loop: false, volume: Audio.BeepVolume));
    }

    /// <summary>Edge-trigger: true only on the frame <paramref name="key"/> goes from up to down.</summary>
    protected static bool Pressed(KeyboardState keys, KeyboardState prev, Keys key)
        => keys.IsKeyDown(key) && prev.IsKeyUp(key);

    #endregion

    #region Rendering

    /// <summary>Draw the title and the centered item list, with the selected row highlighted.</summary>
    public virtual void Draw(SpriteBatch sb, SpriteFont font, int screenW, int screenH)
    {
        const float titleScale = 1.7f;
        Vector2 titleSize = font.MeasureString(Title) * titleScale;
        sb.DrawString(font, Title, new Vector2((screenW - titleSize.X) / 2f, screenH * 0.16f),
                      Color.Gold, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        var labels = ItemLabels;
        const float itemScale = 1.2f;
        float lineH = font.LineSpacing * itemScale + 10f;
        float startY = screenH * 0.36f;
        for (int i = 0; i < labels.Count; i++)
        {
            bool sel = i == SelectedIndex;
            string text = (sel ? "> " : "   ") + labels[i];
            Color color = sel ? Color.LimeGreen : Color.White;
            Vector2 size = font.MeasureString(text) * itemScale;
            sb.DrawString(font, text, new Vector2((screenW - size.X) / 2f, startY + i * lineH),
                          color, 0f, Vector2.Zero, itemScale, SpriteEffects.None, 0f);
        }

        DrawFooter(sb, font, screenW, screenH);
    }

    /// <summary>Optional bottom-of-screen text (controls hint, item description). Default: nothing.</summary>
    protected virtual void DrawFooter(SpriteBatch sb, SpriteFont font, int screenW, int screenH) { }

    #endregion
}
