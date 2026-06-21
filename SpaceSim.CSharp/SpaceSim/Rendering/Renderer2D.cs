using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// 2D renderer implementation using PrimitiveRenderer2D and SpriteBatch.
/// Projects 5D positions to 2D screen coordinates using the ship's view rotation,
/// similar to the original Python pygame rendering.
/// </summary>
public class Renderer2D : BaseGameRenderer
{
    #region Fields & initialization

    // Static color constants to avoid per-frame reconstruction
    private static readonly Color SpeedLineColor = new(180, 180, 255, 40);
    private static readonly Color TempleGoldColor = new(255, 215, 0);
    private static readonly Color PyramidGoldenrodColor = new(218, 165, 32);
    private static readonly Color LeyLineAmentiColor = new(255, 215, 0, 40);
    private static readonly Color LeyLineCollectedColor = new(255, 200, 100, 60);
    private static readonly Color LeyLineDefaultColor = new(200, 180, 100, 30);

    /// <summary>
    /// Zoom level for the 2D projection. Set by SpaceSimGame.
    /// </summary>
    public float ZoomLevel = 1f;

    // Reused 3-vertex buffer for triangle outlines (temples, ship) so we don't allocate a Vector2[]
    // every object every frame. DrawPolygon consumes it synchronously, so a single shared buffer is safe.
    private readonly Vector2[] _triBuffer = new Vector2[3];

    /// <inheritdoc/>
    protected override void OnInitialize(ContentManager content)
    {
        // No 2D-specific initialization needed
    }

    #endregion

    #region World drawing

    /// <summary>
    /// Draws the full 2D scene by projecting every 5D world position onto the screen
    /// through the ship's view rotation and zoom, then drawing ley lines, nebulae,
    /// stars, planets, structures, rifts, and the centered ship. Objects far off
    /// screen are skipped via <see cref="IsNearScreen"/>.
    /// </summary>
    public override void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH)
    {
        try
        {
        // Projection inputs: the view rotates around the ship, zoom scales the spread,
        // and the ship's position is the world-space point kept at screen center.
        float rotation = ship.ViewRotation;
        float zoom = ZoomLevel;
        float[] center = ship.Position;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // --- Draw ley lines (behind everything) ---
        if (LeyLines != null)
        {
            foreach (var ley in LeyLines)
            {
                var (sx, sy) = GameUtils.ProjectTo2D(ley.Start, rotation, screenW, screenH, zoom, center);
                var (ex, ey) = GameUtils.ProjectTo2D(ley.End, rotation, screenW, screenH, zoom, center);

                // Skip if both endpoints are far off screen
                if (!IsNearScreen(sx, sy, screenW, screenH, 200) &&
                    !IsNearScreen(ex, ey, screenW, screenH, 200))
                    continue;

                Color color = ley.AmentiPath
                    ? LeyLineAmentiColor
                    : ley.Major
                        ? LeyLineCollectedColor
                        : LeyLineDefaultColor;

                PrimitiveRenderer2D.DrawLine(spriteBatch,
                    new Vector2(sx, sy), new Vector2(ex, ey), color);
            }
        }

        // --- Draw nebulae (large, semi-transparent) ---
        if (Nebulae != null)
        {
            foreach (var nebula in Nebulae)
            {
                var (x, y) = GameUtils.ProjectTo2D(nebula.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 100)) continue;

                Color color = GetNebulaColor(nebula.NebulaClass);
                color = new Color(color.R, color.G, color.B, (byte)60);
                float radius = 15f * zoom;
                PrimitiveRenderer2D.FillCircle(spriteBatch, new Vector2(x, y), radius, color);
                PrimitiveRenderer2D.DrawCircle(spriteBatch, new Vector2(x, y), radius,
                    new Color(color.R, color.G, color.B, (byte)100), 16);
            }
        }

        // --- Draw stars ---
        if (Stars != null)
        {
            foreach (var star in Stars)
            {
                var (x, y) = GameUtils.ProjectTo2D(star.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 50)) continue;

                Color color = GetStellarColor(star.StellarClass);
                float radius = GetStellarRadius(star.StellarClass) * zoom;
                PrimitiveRenderer2D.FillCircle(spriteBatch, new Vector2(x, y), radius, color);
            }
        }

        // --- Draw planets ---
        if (Planets != null)
        {
            foreach (var planet in Planets)
            {
                var (x, y) = GameUtils.ProjectTo2D(planet.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 50)) continue;

                Color color = GetPlanetColor(planet.ExoplanetClass);
                float radius = (2f + planet.SizeMult) * zoom;
                PrimitiveRenderer2D.FillCircle(spriteBatch, new Vector2(x, y), radius, color);
            }
        }

        // --- Draw temples ---
        if (Temples != null)
        {
            foreach (var temple in Temples)
            {
                var (x, y) = GameUtils.ProjectTo2D(temple.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 50)) continue;

                bool isMaster = temple.Kind == TempleType.Master;
                bool hasKey = ship.TempleKeys.Contains(temple.KeyIndex);

                Color color;
                float size;
                if (isMaster)
                {
                    color = TempleGoldColor;
                    size = 12f * zoom;
                }
                else if (hasKey)
                {
                    color = Color.LimeGreen;
                    size = 8f * zoom;
                }
                else
                {
                    color = PyramidGoldenrodColor;
                    size = 8f * zoom;
                }

                // Draw triangle (temple shape) — reuse the shared buffer instead of allocating.
                var cx = new Vector2(x, y);
                _triBuffer[0] = cx + new Vector2(0, -size);
                _triBuffer[1] = cx + new Vector2(-size * 0.866f, size * 0.5f);
                _triBuffer[2] = cx + new Vector2(size * 0.866f, size * 0.5f);
                PrimitiveRenderer2D.DrawPolygon(spriteBatch, _triBuffer, color, 2);
            }
        }

        // --- Draw pyramids ---
        if (Pyramids != null)
        {
            foreach (var pyramid in Pyramids)
            {
                var (x, y) = GameUtils.ProjectTo2D(pyramid.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 50)) continue;

                float size = 6f * zoom;
                Color color = PyramidGoldenrodColor;

                // Draw as a square
                if (PrimitiveRenderer2D.Pixel != null)
                {
                    spriteBatch.Draw(PrimitiveRenderer2D.Pixel,
                        new Rectangle((int)(x - size), (int)(y - size),
                                      (int)(size * 2), (int)(size * 2)),
                        color);
                }
            }
        }

        // --- Draw rifts ---
        if (ship.Rifts.Count > 0)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f);
            foreach (var rift in ship.Rifts)
            {
                var (x, y) = GameUtils.ProjectTo2D(rift.Position, rotation, screenW, screenH, zoom, center);
                if (!IsNearScreen(x, y, screenW, screenH, 50)) continue;

                int alpha = 150 + (int)(105 * pulse);
                Color riftColor = new Color(150, 50, 200 + (int)(55 * pulse), alpha);
                float radius = (4f + pulse * 2f) * zoom;
                PrimitiveRenderer2D.DrawCircle(spriteBatch, new Vector2(x, y), radius, riftColor, 16);
                PrimitiveRenderer2D.FillCircle(spriteBatch, new Vector2(x, y), radius * 0.5f,
                    new Color(riftColor.R, riftColor.G, riftColor.B, (byte)(alpha / 2)));
            }
        }

        // --- Draw ship at screen center ---
        DrawShip2D(spriteBatch, ship, gameTime, screenW, screenH);

        // --- Draw speed lines ---
        DrawSpeedLines2D(spriteBatch, ship, gameTime, screenW, screenH);

        spriteBatch.End();

        // Throttled stats logging (every 5 seconds)
        FrameCount++;
        double elapsed = gameTime.TotalGameTime.TotalSeconds;
        if (elapsed - LastLogTime >= 5.0)
        {
            DebugLogger.Log("Render", $"2D stats: {FrameCount} frames/5s, " +
                $"stars={Stars?.Count ?? 0}, planets={Planets?.Count ?? 0}, " +
                $"nebulae={Nebulae?.Count ?? 0}, temples={Temples?.Count ?? 0}");
            FrameCount = 0;
            LastLogTime = elapsed;
        }

        } // end try
        catch (Exception ex)
        {
            DebugLogger.LogError("Render", "Renderer2D.DrawWorld exception", ex);
        }
    }

    /// <inheritdoc/>
    public override void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH)
    {
        // HUD drawing is handled by HudRenderer in SpaceSimGame.Draw
    }

    #endregion

    #region Ship drawing

    private void DrawShip2D(SpriteBatch spriteBatch, Ship ship, GameTime gameTime,
                            int screenW, int screenH)
    {
        // The ship is always pinned to screen center; the world scrolls around it.
        float cx = screenW / 2f;
        float cy = screenH / 2f;
        float heading = ship.Heading;
        Color shipColor = GetTuaoiColor(ship.TuaoiMode);

        float size = 10f;

        // Triangular ship pointing in heading direction
        float cosH = MathF.Cos(heading);
        float sinH = MathF.Sin(heading);

        var nose = new Vector2(cx + cosH * size, cy + sinH * size);
        var left = new Vector2(cx + MathF.Cos(heading + 2.5f) * size * 0.7f,
                               cy + MathF.Sin(heading + 2.5f) * size * 0.7f);
        var right = new Vector2(cx + MathF.Cos(heading - 2.5f) * size * 0.7f,
                                cy + MathF.Sin(heading - 2.5f) * size * 0.7f);

        _triBuffer[0] = nose;
        _triBuffer[1] = left;
        _triBuffer[2] = right;
        PrimitiveRenderer2D.DrawPolygon(spriteBatch, _triBuffer, shipColor, 2);

        // Ship body fill (approximate with lines between vertices)
        PrimitiveRenderer2D.DrawLine(spriteBatch, nose, left, shipColor);
        PrimitiveRenderer2D.DrawLine(spriteBatch, nose, right, shipColor);
        PrimitiveRenderer2D.DrawLine(spriteBatch, left, right, shipColor);

        // Merkaba indicator
        if (ship.MerkabaActive)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f);
            Color merkabaColor = new Color(255, 255, 200, 80 + (int)(100 * pulse));
            PrimitiveRenderer2D.DrawCircle(spriteBatch, new Vector2(cx, cy),
                15f + pulse * 5f, merkabaColor, 24);
        }

        // Rift charge ring
        if (ship.IsChargingRift)
        {
            float progress = ship.RiftChargeProgress;
            Color chargeColor = new Color((int)(255 * progress), 100, 255, 180);
            PrimitiveRenderer2D.DrawCircle(spriteBatch, new Vector2(cx, cy),
                12f + progress * 8f, chargeColor, 20);
        }
    }

    #endregion

    #region Speed lines

    private void DrawSpeedLines2D(SpriteBatch spriteBatch, Ship ship, GameTime gameTime,
                                  int screenW, int screenH)
    {
        float speed = 0f;
        for (int i = 0; i < GameConstants.NDimensions; i++)
            speed += ship.Velocity[i] * ship.Velocity[i];
        speed = MathF.Sqrt(speed);

        if (speed < 2f) return;

        float cx = screenW / 2f;
        float cy = screenH / 2f;
        int lineCount = Math.Min((int)(speed * 2), 20);
        float time = (float)gameTime.TotalGameTime.TotalSeconds;
        Color lineColor = SpeedLineColor;

        var rng = Random.Shared;
        for (int i = 0; i < lineCount; i++)
        {
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);
            float dist = 50f + (float)(rng.NextDouble() * Math.Max(screenW, screenH) * 0.3);
            float lineLen = 5f + speed * 2f;

            var start = new Vector2(
                cx + MathF.Cos(angle) * dist,
                cy + MathF.Sin(angle) * dist);

            // Lines point toward center
            Vector2 dir = Vector2.Normalize(new Vector2(cx, cy) - start);
            var end = start + dir * lineLen;

            PrimitiveRenderer2D.DrawLine(spriteBatch, start, end, lineColor);
        }
    }

    #endregion

    #region Helpers (2D-specific)

    /// <summary>
    /// True if a projected point lies within <paramref name="margin"/> pixels of the
    /// viewport — a cheap cull so off-screen world objects are skipped.
    /// </summary>
    private static bool IsNearScreen(int x, int y, int screenW, int screenH, int margin)
    {
        return x > -margin && x < screenW + margin &&
               y > -margin && y < screenH + margin;
    }

    private static float GetStellarRadius(StellarType? stellarClass)
    {
        return stellarClass switch
        {
            StellarType.RedGiant => 5f,
            StellarType.WhiteDwarf => 2f,
            StellarType.BrownDwarf => 1.5f,
            _ => 3f, // MainSequence
        };
    }

    #endregion
}
