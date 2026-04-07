using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SpaceSim.Models;

namespace SpaceSim.Rendering;

/// <summary>
/// 2D renderer implementation using PrimitiveRenderer2D and SpriteBatch.
/// Projects 5D positions to 2D screen coordinates using the ship's view rotation,
/// similar to the original Python pygame rendering.
/// </summary>
public class Renderer2D : IGameRenderer
{
    private GraphicsDevice _device = null!;

    // Logging throttle
    private int _frameCount;
    private double _lastLogTime;

    // World data references (set by SpaceSimGame)
    public List<CelestialBody>? Stars;
    public List<CelestialBody>? Planets;
    public List<CelestialBody>? Nebulae;
    public List<Temple>? Temples;
    public List<LeyLine>? LeyLines;
    public List<Pyramid>? Pyramids;

    /// <summary>
    /// Zoom level for the 2D projection. Set by SpaceSimGame.
    /// </summary>
    public float ZoomLevel = 1f;

    public void Initialize(GraphicsDevice device, ContentManager content)
    {
        _device = device;
        DebugLogger.Log("Render", "Renderer2D initialized");
    }

    public void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH)
    {
        try
        {
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
                    ? new Color(255, 215, 0, 40)
                    : ley.Major
                        ? new Color(255, 200, 100, 60)
                        : new Color(200, 180, 100, 30);

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

                Color color = GetNebulaColor(nebula.NebulaType);
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

                Color color = GetStellarColor(star.StellarType);
                float radius = GetStellarRadius(star.StellarType) * zoom;
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

                Color color = GetPlanetColor(planet.ExoplanetType);
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

                bool isMaster = temple.TempleType == "master";
                bool hasKey = ship.TempleKeys.Contains(temple.KeyIndex);

                Color color;
                float size;
                if (isMaster)
                {
                    color = new Color(255, 215, 0); // Gold
                    size = 12f * zoom;
                }
                else if (hasKey)
                {
                    color = Color.LimeGreen;
                    size = 8f * zoom;
                }
                else
                {
                    color = new Color(218, 165, 32); // Goldenrod
                    size = 8f * zoom;
                }

                // Draw triangle (temple shape)
                var cx = new Vector2(x, y);
                var points = new Vector2[]
                {
                    cx + new Vector2(0, -size),
                    cx + new Vector2(-size * 0.866f, size * 0.5f),
                    cx + new Vector2(size * 0.866f, size * 0.5f),
                };
                PrimitiveRenderer2D.DrawPolygon(spriteBatch, points, color, 2);
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
                Color color = new Color(218, 165, 32); // Goldenrod

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
        _frameCount++;
        double elapsed = gameTime.TotalGameTime.TotalSeconds;
        if (elapsed - _lastLogTime >= 5.0)
        {
            DebugLogger.Log("Render", $"2D stats: {_frameCount} frames/5s, " +
                $"stars={Stars?.Count ?? 0}, planets={Planets?.Count ?? 0}, " +
                $"nebulae={Nebulae?.Count ?? 0}, temples={Temples?.Count ?? 0}");
            _frameCount = 0;
            _lastLogTime = elapsed;
        }

        } // end try
        catch (Exception ex)
        {
            DebugLogger.LogError("Render", "Renderer2D.DrawWorld exception", ex);
        }
    }

    public void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH)
    {
        // HUD drawing is handled by HudRenderer in SpaceSimGame.Draw
    }

    // =========================================================================
    //  SHIP DRAWING
    // =========================================================================

    private void DrawShip2D(SpriteBatch spriteBatch, Ship ship, GameTime gameTime,
                            int screenW, int screenH)
    {
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

        var shipPoints = new Vector2[] { nose, left, right };
        PrimitiveRenderer2D.DrawPolygon(spriteBatch, shipPoints, shipColor, 2);

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

    // =========================================================================
    //  SPEED LINES
    // =========================================================================

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
        Color lineColor = new Color(180, 180, 255, 40);

        var rng = new Random((int)(time * 10));
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

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private static bool IsNearScreen(int x, int y, int screenW, int screenH, int margin)
    {
        return x > -margin && x < screenW + margin &&
               y > -margin && y < screenH + margin;
    }

    private static Color GetStellarColor(string? stellarType)
    {
        if (stellarType == null) return Color.Yellow;
        if (GameConstants.StellarTypes.TryGetValue(stellarType, out var info))
            return info.Color;
        return Color.Yellow;
    }

    private static float GetStellarRadius(string? stellarType)
    {
        return stellarType switch
        {
            "red_giant" => 5f,
            "white_dwarf" => 2f,
            "brown_dwarf" => 1.5f,
            _ => 3f, // main_sequence
        };
    }

    private static Color GetPlanetColor(string? exoplanetType)
    {
        return exoplanetType switch
        {
            "hot_jupiter" => new Color(255, 120, 50),
            "super_earth" => new Color(100, 180, 100),
            "ocean_world" => new Color(50, 100, 200),
            "rogue_planet" => new Color(80, 80, 100),
            "ice_giant" => new Color(150, 200, 255),
            _ => Color.Gray,
        };
    }

    private static Color GetNebulaColor(string? nebulaType)
    {
        if (nebulaType == null) return new Color(100, 50, 150);
        if (GameConstants.NebulaTypes.TryGetValue(nebulaType, out var info))
            return info.Color;
        return new Color(100, 50, 150);
    }

    private static Color GetTuaoiColor(string tuaoiMode)
    {
        if (GameConstants.TuaoiModes.TryGetValue(tuaoiMode, out var info))
            return info.Color;
        return Color.Cyan;
    }
}
