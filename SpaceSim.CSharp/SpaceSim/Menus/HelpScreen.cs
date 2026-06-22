using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// An in-game, screen-reader-navigable controls and goal reference. Opened with F1 from anywhere (and
/// from a main-menu item); the player arrows through one control per line and presses F1 or Escape to
/// return to whatever they were doing. Read-only — there are no actions to activate here.
/// </summary>
public sealed class HelpScreen : MenuScreen
{
    #region Content

    // One control (or the goal) per line, grouped by category. Each line is a full sentence so it
    // reads naturally on its own when arrowed to. Keys here are kept in lockstep with Ship.Input.cs
    // and SpaceSimGame — update both together.
    private static readonly string[] _lines =
    {
        "Goal. Tune your five realms into resonance to fly. Scan for destinations, anchor on planets to gather crystals, and spend them on attunements. Grow your resonance and consciousness toward ascension.",

        "Flight. W and S thrust forward and back.",
        "A and D strafe left and right.",
        "Page Up and Page Down move through the third realm.",
        "Left and Right arrows rotate your view.",
        "Home and End raise and lower the camera.",
        "Z cycles speed mode: approach, cruise, quantum.",

        "Tuning. Press 1 to 5 to select a realm.",
        "Up and Down arrows tune the selected realm.",
        "J toggles full tuning of all five realms.",
        "Q speaks the selected realm's target frequency.",

        "Navigation. M opens the scanner to find and lock a destination.",
        "E charges into a nearby Harmonic Chamber, or opens the chamber list.",
        "L anchors on a nearby planet.",
        "R speaks your full status.",

        "On a planet. W, A, S, and D move the crystal cursor.",
        "F scans for the nearest crystal.",
        "X collects a crystal.",
        "U opens the attunement menu to spend crystals.",
        "T takes off from the planet.",

        "Abilities. G cycles Tuaoi Crystal modes.",
        "P drops a portal anchor. Shift plus P travels to it.",
        "B toggles astral projection.",
        "I focuses intention navigation toward your locked target.",

        "Presets. Control plus 1 to 9 saves a frequency preset. Shift plus 1 to 9 recalls one.",

        "Audio. Plus and minus adjust master volume. Hold Alt, Shift, or Control for drive, beep, or effect volume.",
        "Brackets zoom out and in. Backslash resets zoom.",

        "System. V cycles speech verbosity. C toggles high contrast.",
        "Control plus S saves your game. Control plus L loads it. Control plus A toggles autosave.",
        "F10 switches 2D and 3D view. F11 toggles fullscreen.",
        "F1 opens and closes this help. Escape returns to the main menu.",
    };

    #endregion

    #region Construction & surface

    public HelpScreen(AudioSystem audio, Action<string> speak) : base(audio, speak) { }

    public override string Title => "Help";
    protected override IReadOnlyList<string> ItemLabels => _lines;

    protected override string EntryHint =>
        "Use up and down to read the controls. Press F1 or Escape to return.";

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Escape))
            return ScreenTransition.CloseHelp;

        if (Pressed(keys, prev, Keys.Up)) MoveSelection(-1);
        else if (Pressed(keys, prev, Keys.Down)) MoveSelection(+1);
        else if (Pressed(keys, prev, Keys.Enter)) AnnounceSelection();  // re-read the current line

        return ScreenTransition.None;
    }

    #endregion
}
