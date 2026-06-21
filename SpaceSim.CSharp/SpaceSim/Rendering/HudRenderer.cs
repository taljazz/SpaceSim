using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// Renders the 2D HUD overlay on top of the 3D or 2D scene.
/// Handles both the dynamic in-game HUD and menu modes (upgrades, starmap, rift selection).
/// </summary>
public static class HudRenderer
{
    #region Colors

    /// <summary>
    /// The color used for the currently selected menu item.
    /// </summary>
    private static readonly Color SelectedColor = Color.LimeGreen;

    /// <summary>
    /// The default text color for HUD and menu items.
    /// </summary>
    private static readonly Color DefaultColor = Color.White;

    #endregion

    #region Public entry point

    /// <summary>
    /// Draws the HUD overlay. Renders menu items when in a menu mode,
    /// or the dynamic HUD items during normal gameplay.
    /// </summary>
    /// <param name="spriteBatch">SpriteBatch for 2D text drawing.</param>
    /// <param name="font">The font used for HUD text.</param>
    /// <param name="ship">The player's ship for reading HUD state.</param>
    /// <param name="screenW">Current screen width.</param>
    /// <param name="screenH">Current screen height.</param>
    public static void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH)
    {
        int textSize = ship.HudTextSize;
        // Scale the font relative to the baseline size so the player's text-size
        // preference enlarges/shrinks the whole HUD proportionally.
        float scale = textSize / (float)GameConstants.HudTextSizeBase;
        int lineHeight = textSize + 5;
        int x = 10;
        int y = 10;

        // Check if we are in a menu mode
        if (ship.IsInMenuMode)
        {
            DrawMenuItems(spriteBatch, font, ship.MenuItems, ship.MenuSelectedIndex, x, y, lineHeight, scale);
            return;
        }

        // Normal gameplay HUD
        DrawHudItems(spriteBatch, font, ship.HudItems, x, y, lineHeight, scale);
    }

    #endregion

    #region Menu & HUD item drawing

    /// <summary>
    /// Draws a list of menu items with the selected item highlighted in green.
    /// </summary>
    private static void DrawMenuItems(
        SpriteBatch spriteBatch,
        SpriteFont font,
        IReadOnlyList<string> items,
        int selectedIndex,
        int x,
        int y,
        int lineHeight,
        float scale)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Color color = (i == selectedIndex) ? SelectedColor : DefaultColor;
            string prefix = (i == selectedIndex) ? "> " : "  ";

            spriteBatch.DrawString(
                font,
                prefix + items[i],
                new Vector2(x, y + i * lineHeight),
                color,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>
    /// Draws the dynamic HUD items during normal gameplay.
    /// </summary>
    private static void DrawHudItems(
        SpriteBatch spriteBatch,
        SpriteFont font,
        IReadOnlyList<string> items,
        int x,
        int y,
        int lineHeight,
        float scale)
    {
        for (int i = 0; i < items.Count; i++)
        {
            spriteBatch.DrawString(
                font,
                items[i],
                new Vector2(x, y + i * lineHeight),
                DefaultColor,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    #endregion
}
