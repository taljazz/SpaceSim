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

    private static readonly string[] _items = { "Start Sim", "Learn Sounds", "Quit Sim" };

    #endregion

    #region Construction

    public MainMenuScreen(AudioSystem audio, Action<string> speak) : base(audio, speak) { }

    public override string Title => "Space Sim";
    protected override IReadOnlyList<string> ItemLabels => _items;

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Up)) MoveSelection(-1);
        else if (Pressed(keys, prev, Keys.Down)) MoveSelection(+1);
        else if (Pressed(keys, prev, Keys.Enter) || Pressed(keys, prev, Keys.Space))
        {
            return SelectedIndex switch
            {
                0 => ScreenTransition.StartSim,
                1 => ScreenTransition.OpenLearnSounds,
                2 => ScreenTransition.Quit,
                _ => ScreenTransition.None,
            };
        }
        return ScreenTransition.None;
    }

    #endregion
}
