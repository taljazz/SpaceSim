using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// The first screen the player sees: <b>Start Sim</b>, <b>Learn Sounds</b>, <b>Quit Sim</b>.
/// Up/Down to move, Enter (or Space) to choose — every move announced for blind players.
/// </summary>
public sealed class MainMenuScreen : MenuScreen
{
    #region Items

    private static readonly string[] _items = { "Start Sim", "Learn Sounds", "Help", "Quit Sim" };

    // When true, the next Escape confirms quitting and any other key cancels — guards against an
    // accidental exit from a stray Escape.
    private bool _confirmingQuit;

    #endregion

    #region Construction

    public MainMenuScreen(AudioSystem audio, Action<string> speak) : base(audio, speak) { }

    public override string Title => "Space Sim";
    protected override IReadOnlyList<string> ItemLabels => _items;

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        // While awaiting a quit confirmation, Escape/any-key are handled there instead of the menu.
        if (_confirmingQuit)
            return HandleQuitConfirmation(keys, prev);

        // Escape at the main menu asks to exit, but only after confirming.
        if (Pressed(keys, prev, Keys.Escape))
        {
            _confirmingQuit = true;
            Speak("Exit Space Sim? Press Enter to exit, or Escape to cancel.");
            return ScreenTransition.None;
        }

        if (Pressed(keys, prev, Keys.Up)) MoveSelection(-1);
        else if (Pressed(keys, prev, Keys.Down)) MoveSelection(+1);
        else if (Pressed(keys, prev, Keys.Enter) || Pressed(keys, prev, Keys.Space))
        {
            return SelectedIndex switch
            {
                0 => ScreenTransition.StartSim,
                1 => ScreenTransition.OpenLearnSounds,
                2 => ScreenTransition.OpenHelp,
                3 => ScreenTransition.Quit,
                _ => ScreenTransition.None,
            };
        }
        return ScreenTransition.None;
    }

    /// <summary>
    /// While the quit prompt is up: Enter exits the game; Escape (or any other key) cancels and
    /// returns to the menu. Stops a stray Escape from quitting by accident.
    /// </summary>
    private ScreenTransition HandleQuitConfirmation(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Enter))
        {
            _confirmingQuit = false;
            return ScreenTransition.Quit;
        }
        if (AnyKeyPressed(keys, prev))   // Escape (or any other key) cancels and returns to the menu
        {
            _confirmingQuit = false;
            Speak($"Cancelled. {CurrentLabel}.");
        }
        return ScreenTransition.None;
    }

    /// <summary>True if any key transitioned from up to down this frame (used to cancel the quit prompt).</summary>
    private static bool AnyKeyPressed(KeyboardState keys, KeyboardState prev)
    {
        foreach (var key in keys.GetPressedKeys())
            if (prev.IsKeyUp(key))
                return true;
        return false;
    }

    #endregion

    #region Lifecycle

    /// <summary>Clear any pending quit confirmation whenever the menu is (re)entered, then announce it.</summary>
    public override void OnEnter()
    {
        _confirmingQuit = false;
        base.OnEnter();
    }

    #endregion
}
