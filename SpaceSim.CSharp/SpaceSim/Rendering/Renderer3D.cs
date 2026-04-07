using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SpaceSim.Models;

namespace SpaceSim.Rendering;

/// <summary>
/// 3D renderer implementation using PrimitiveRenderer with BasicEffect.
/// Draws celestial bodies as wireframe spheres, the ship as a cone,
/// and Atlantean structures as pyramids and lines in 3D space.
/// </summary>
public class Renderer3D : IGameRenderer
{
    private GraphicsDevice _device = null!;
    private Camera3D? _camera;

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

    // Rendering limits to avoid drawing too many distant objects
    private const float MaxRenderDistance = 500f;
    private const float MaxRenderDistanceSq = MaxRenderDistance * MaxRenderDistance;

    /// <summary>
    /// Sets the Camera3D reference used for view/projection matrices.
    /// </summary>
    public void SetCamera(Camera3D camera) => _camera = camera;

    public void Initialize(GraphicsDevice device, ContentManager content)
    {
        _device = device;
        DebugLogger.Log("Render", "Renderer3D initialized");
    }

    public void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH)
    {
        if (_camera == null)
        {
            DebugLogger.Log("Render", "WARNING: Renderer3D.DrawWorld called with null camera");
            return;
        }

        try
        {

        var view = _camera.ViewMatrix;
        var proj = _camera.ProjectionMatrix;
        var shipPos3D = GetPosition3D(ship.Position);

        // Enable depth buffer for 3D rendering
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
        _device.BlendState = BlendState.Opaque;

        // --- Draw stars ---
        if (Stars != null)
        {
            foreach (var star in Stars)
            {
                var pos = GetPosition3D(star.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetStellarColor(star.StellarType);
                float size = GetStellarSize(star.StellarType);
                PrimitiveRenderer.DrawSphere(_device, pos, size, color, view, proj, 12);
            }
        }

        // --- Draw planets ---
        if (Planets != null)
        {
            foreach (var planet in Planets)
            {
                var pos = GetPosition3D(planet.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetPlanetColor(planet.ExoplanetType);
                float size = 0.8f * planet.SizeMult;
                PrimitiveRenderer.DrawSphere(_device, pos, size, color, view, proj, 10);
            }
        }

        // --- Draw nebulae (semi-transparent, larger) ---
        _device.BlendState = BlendState.AlphaBlend;
        if (Nebulae != null)
        {
            foreach (var nebula in Nebulae)
            {
                var pos = GetPosition3D(nebula.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetNebulaColor(nebula.NebulaType);
                color = new Color(color.R, color.G, color.B, (byte)80);
                PrimitiveRenderer.DrawSphere(_device, pos, 5f, color, view, proj, 8);
            }
        }
        _device.BlendState = BlendState.Opaque;

        // --- Draw temples ---
        if (Temples != null)
        {
            foreach (var temple in Temples)
            {
                var pos = GetPosition3D(temple.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                bool isMaster = temple.TempleType == "master";
                bool hasKey = ship.TempleKeys.Contains(temple.KeyIndex);

                Color color;
                float size;
                if (isMaster)
                {
                    color = new Color(255, 215, 0); // Gold
                    size = 4f;
                }
                else if (hasKey)
                {
                    color = Color.LimeGreen;
                    size = 2f;
                }
                else
                {
                    color = new Color(218, 165, 32); // Goldenrod
                    size = 2f;
                }

                PrimitiveRenderer.DrawPyramid(_device, pos, size, color, view, proj);
            }
        }

        // --- Draw pyramids ---
        if (Pyramids != null)
        {
            foreach (var pyramid in Pyramids)
            {
                var pos = GetPosition3D(pyramid.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = new Color(218, 165, 32); // Goldenrod
                PrimitiveRenderer.DrawPyramid(_device, pos, 3f, color, view, proj);
            }
        }

        // --- Draw ley lines ---
        _device.BlendState = BlendState.AlphaBlend;
        if (LeyLines != null)
        {
            foreach (var ley in LeyLines)
            {
                var start = GetPosition3D(ley.Start);
                var end = GetPosition3D(ley.End);

                // Only draw if at least one end is near
                if (DistanceSquared(start, shipPos3D) > MaxRenderDistanceSq &&
                    DistanceSquared(end, shipPos3D) > MaxRenderDistanceSq)
                    continue;

                Color color = ley.AmentiPath
                    ? new Color(255, 215, 0, 60) // Gold, faint
                    : ley.Major
                        ? new Color(255, 200, 100, 80)
                        : new Color(200, 180, 100, 40);

                PrimitiveRenderer.DrawLine3D(_device, start, end, color, view, proj);
            }
        }
        _device.BlendState = BlendState.Opaque;

        // --- Draw rifts ---
        if (ship.Rifts.Count > 0)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f);
            foreach (var rift in ship.Rifts)
            {
                var pos = GetPosition3D(rift.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                int alpha = 150 + (int)(105 * pulse);
                Color riftColor = new Color(150, 50, 200 + (int)(55 * pulse), alpha);
                PrimitiveRenderer.DrawSphere(_device, pos, 1.5f + pulse * 0.5f, riftColor, view, proj, 8);
            }
        }

        // --- Draw ship ---
        DrawShip(ship, gameTime, view, proj);

        // --- Draw speed lines when moving fast ---
        DrawSpeedLines(ship, gameTime, view, proj);

        // Throttled stats logging (every 5 seconds)
        _frameCount++;
        double elapsed = gameTime.TotalGameTime.TotalSeconds;
        if (elapsed - _lastLogTime >= 5.0)
        {
            DebugLogger.Log("Render", $"3D stats: {_frameCount} frames/5s, " +
                $"stars={Stars?.Count ?? 0}, planets={Planets?.Count ?? 0}, " +
                $"nebulae={Nebulae?.Count ?? 0}, temples={Temples?.Count ?? 0}, " +
                $"rifts={ship.Rifts.Count}");
            _frameCount = 0;
            _lastLogTime = elapsed;
        }

        } // end try
        catch (Exception ex)
        {
            DebugLogger.LogError("Render", "Renderer3D.DrawWorld exception", ex);
        }
    }

    public void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH)
    {
        // HUD drawing is handled by HudRenderer in SpaceSimGame.Draw
    }

    // =========================================================================
    //  SHIP DRAWING
    // =========================================================================

    private void DrawShip(Ship ship, GameTime gameTime, Matrix view, Matrix proj)
    {
        var shipPos = GetPosition3D(ship.Position);
        Color shipColor = GetTuaoiColor(ship.TuaoiMode);

        // Draw ship as a cone pointing in the heading direction
        float heading = ship.Heading;

        // Create a rotation matrix to orient the cone
        Matrix world = Matrix.CreateRotationY(-heading) * Matrix.CreateTranslation(shipPos);

        // Draw the cone at origin (the world matrix handles positioning)
        PrimitiveRenderer.DrawCone(_device, shipPos, 0.5f, 1.5f, shipColor, view, proj, 8);

        // Draw Merkaba indicator if active
        if (ship.MerkabaActive)
        {
            float merkabaPulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f);
            Color merkabaColor = new Color(255, 255, 200, 100 + (int)(155 * merkabaPulse));
            PrimitiveRenderer.DrawSphere(_device, shipPos, 2f + merkabaPulse * 0.5f, merkabaColor, view, proj, 6);
        }

        // Draw rift charge indicator
        if (ship.IsChargingRift)
        {
            float progress = ship.RiftChargeProgress;
            Color chargeColor = new Color((byte)(255 * progress), 100, 255);
            PrimitiveRenderer.DrawSphere(_device, shipPos, 1f + progress * 2f, chargeColor, view, proj, 6);
        }
    }

    // =========================================================================
    //  SPEED LINES
    // =========================================================================

    private void DrawSpeedLines(Ship ship, GameTime gameTime, Matrix view, Matrix proj)
    {
        float speed = 0f;
        for (int i = 0; i < 3; i++)
            speed += ship.Velocity[i] * ship.Velocity[i];
        speed = MathF.Sqrt(speed);

        if (speed < 2f) return;

        var shipPos = GetPosition3D(ship.Position);
        int lineCount = Math.Min((int)(speed * 3), 30);
        float time = (float)gameTime.TotalGameTime.TotalSeconds;

        var rng = new Random((int)(time * 10));
        Color lineColor = new Color(200, 200, 255, 60);

        for (int i = 0; i < lineCount; i++)
        {
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);
            float dist = 5f + (float)(rng.NextDouble() * 20f);
            float lineLen = 1f + speed * 0.3f;

            Vector3 offset = new Vector3(
                MathF.Cos(angle) * dist,
                (float)(rng.NextDouble() - 0.5) * dist,
                MathF.Sin(angle) * dist);

            Vector3 start = shipPos + offset;
            // Lines stream toward the ship
            Vector3 dir = Vector3.Normalize(shipPos - start);
            Vector3 end = start + dir * lineLen;

            PrimitiveRenderer.DrawLine3D(_device, start, end, lineColor, view, proj);
        }
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    /// <summary>
    /// Extracts the first 3 components of a 5D position as a 3D Vector3.
    /// </summary>
    private static Vector3 GetPosition3D(float[] pos5D)
    {
        return new Vector3(pos5D[0], pos5D[1], pos5D[2]);
    }

    private static float DistanceSquared(Vector3 a, Vector3 b)
    {
        var d = a - b;
        return d.X * d.X + d.Y * d.Y + d.Z * d.Z;
    }

    private static Color GetStellarColor(string? stellarType)
    {
        if (stellarType == null) return Color.Yellow;
        if (GameConstants.StellarTypes.TryGetValue(stellarType, out var info))
            return info.Color;
        return Color.Yellow;
    }

    private static float GetStellarSize(string? stellarType)
    {
        return stellarType switch
        {
            "red_giant" => 3f,
            "white_dwarf" => 0.8f,
            "brown_dwarf" => 0.6f,
            _ => 1.5f, // main_sequence
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
