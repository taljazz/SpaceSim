using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// Interface that both 2D and 3D renderers implement, allowing the game to swap
/// rendering backends without changing game logic.
/// </summary>
public interface IGameRenderer
{
    /// <summary>
    /// One-time initialization called after the graphics device is ready.
    /// </summary>
    void Initialize(GraphicsDevice device, ContentManager content);

    /// <summary>
    /// Draws the game world (celestial bodies, ship, effects, etc.).
    /// The Ship class and world data are accessed through the provided parameters.
    /// </summary>
    /// <param name="spriteBatch">SpriteBatch for 2D drawing (or overlay in 3D mode).</param>
    /// <param name="ship">The player's ship containing position, state, and world references.</param>
    /// <param name="gameTime">Current game timing information.</param>
    /// <param name="screenW">Current screen width.</param>
    /// <param name="screenH">Current screen height.</param>
    void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH);

    /// <summary>
    /// Draws the HUD overlay on top of the world rendering.
    /// </summary>
    /// <param name="spriteBatch">SpriteBatch for 2D text and UI drawing.</param>
    /// <param name="font">The font used for HUD text.</param>
    /// <param name="ship">The player's ship for reading HUD state.</param>
    /// <param name="screenW">Current screen width.</param>
    /// <param name="screenH">Current screen height.</param>
    void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH);
}
