using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// 3D renderer implementation using PrimitiveRenderer with BasicEffect.
/// Draws celestial bodies as wireframe spheres, the ship as a cone,
/// and Atlantean structures as pyramids and lines in 3D space.
/// </summary>
public class Renderer3D : BaseGameRenderer
{
    #region Fields & initialization

    private Camera3D? _camera;

    // Rendering limits to avoid drawing too many distant objects
    private const float MaxRenderDistance = 500f;
    private const float MaxRenderDistanceSq = MaxRenderDistance * MaxRenderDistance;

    // Static color constants to avoid per-frame reconstruction
    private static readonly Color SpeedLineColor = new(200, 200, 255, 60);
    private static readonly Color TempleGoldColor = new(255, 215, 0);
    private static readonly Color PyramidGoldenrodColor = new(218, 165, 32);
    private static readonly Color LeyLineAmentiColor = new(255, 215, 0, 60);
    private static readonly Color LeyLineCollectedColor = new(255, 200, 100, 80);
    private static readonly Color LeyLineDefaultColor = new(200, 180, 100, 40);

    /// <summary>
    /// Sets the Camera3D reference used for view/projection matrices.
    /// </summary>
    public void SetCamera(Camera3D camera) => _camera = camera;

    /// <inheritdoc/>
    protected override void OnInitialize(ContentManager content)
    {
        // No 3D-specific initialization needed beyond what base handles
    }

    #endregion

    #region World drawing

    /// <summary>
    /// Draws the full 3D scene: celestial bodies, Atlantean structures, rifts, the
    /// ship, and speed lines, using the camera's view/projection matrices. Distant
    /// objects beyond <see cref="MaxRenderDistance"/> are culled to keep the frame cheap.
    /// </summary>
    public override void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH)
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
        Device.DepthStencilState = DepthStencilState.Default;
        Device.RasterizerState = RasterizerState.CullCounterClockwise;
        Device.BlendState = BlendState.Opaque;

        // --- Draw stars ---
        if (Stars != null)
        {
            foreach (var star in Stars)
            {
                var pos = GetPosition3D(star.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetStellarColor(star.StellarClass);
                float size = GetStellarSize(star.StellarClass);
                PrimitiveRenderer.DrawSphere(Device, pos, size, color, view, proj, 12);
            }
        }

        // --- Draw planets ---
        if (Planets != null)
        {
            foreach (var planet in Planets)
            {
                var pos = GetPosition3D(planet.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetPlanetColor(planet.ExoplanetClass);
                float size = 0.8f * planet.SizeMult;
                PrimitiveRenderer.DrawSphere(Device, pos, size, color, view, proj, 10);
            }
        }

        // --- Draw nebulae (semi-transparent, larger) ---
        Device.BlendState = BlendState.AlphaBlend;
        if (Nebulae != null)
        {
            foreach (var nebula in Nebulae)
            {
                var pos = GetPosition3D(nebula.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                Color color = GetNebulaColor(nebula.NebulaClass);
                color = new Color(color.R, color.G, color.B, (byte)80);
                PrimitiveRenderer.DrawSphere(Device, pos, 5f, color, view, proj, 8);
            }
        }
        Device.BlendState = BlendState.Opaque;

        // --- Draw temples ---
        if (Temples != null)
        {
            foreach (var temple in Temples)
            {
                var pos = GetPosition3D(temple.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                bool isMaster = temple.Kind == TempleType.Master;
                bool hasKey = ship.TempleKeys.Contains(temple.KeyIndex);

                Color color;
                float size;
                if (isMaster)
                {
                    color = TempleGoldColor;
                    size = 4f;
                }
                else if (hasKey)
                {
                    color = Color.LimeGreen;
                    size = 2f;
                }
                else
                {
                    color = PyramidGoldenrodColor;
                    size = 2f;
                }

                PrimitiveRenderer.DrawPyramid(Device, pos, size, color, view, proj);
            }
        }

        // --- Draw pyramids ---
        if (Pyramids != null)
        {
            foreach (var pyramid in Pyramids)
            {
                var pos = GetPosition3D(pyramid.Position);
                if (DistanceSquared(pos, shipPos3D) > MaxRenderDistanceSq) continue;

                PrimitiveRenderer.DrawPyramid(Device, pos, 3f, PyramidGoldenrodColor, view, proj);
            }
        }

        // --- Draw ley lines ---
        Device.BlendState = BlendState.AlphaBlend;
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
                    ? LeyLineAmentiColor
                    : ley.Major
                        ? LeyLineCollectedColor
                        : LeyLineDefaultColor;

                PrimitiveRenderer.DrawLine3D(Device, start, end, color, view, proj);
            }
        }
        Device.BlendState = BlendState.Opaque;

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
                PrimitiveRenderer.DrawSphere(Device, pos, 1.5f + pulse * 0.5f, riftColor, view, proj, 8);
            }
        }

        // --- Draw ship ---
        DrawShip(ship, gameTime, view, proj);

        // --- Draw speed lines when moving fast ---
        DrawSpeedLines(ship, gameTime, view, proj);

        // Throttled stats logging (every 5 seconds)
        FrameCount++;
        double elapsed = gameTime.TotalGameTime.TotalSeconds;
        if (elapsed - LastLogTime >= 5.0)
        {
            DebugLogger.Log("Render", $"3D stats: {FrameCount} frames/5s, " +
                $"stars={Stars?.Count ?? 0}, planets={Planets?.Count ?? 0}, " +
                $"nebulae={Nebulae?.Count ?? 0}, temples={Temples?.Count ?? 0}, " +
                $"rifts={ship.Rifts.Count}");
            FrameCount = 0;
            LastLogTime = elapsed;
        }

        } // end try
        catch (Exception ex)
        {
            DebugLogger.LogError("Render", "Renderer3D.DrawWorld exception", ex);
        }
    }

    /// <inheritdoc/>
    public override void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH)
    {
        // HUD drawing is handled by HudRenderer in SpaceSimGame.Draw
    }

    #endregion

    #region Ship drawing

    private void DrawShip(Ship ship, GameTime gameTime, Matrix view, Matrix proj)
    {
        var shipPos = GetPosition3D(ship.Position);
        Color shipColor = GetTuaoiColor(ship.TuaoiMode);

        // Draw ship as a cone pointing in the heading direction
        float heading = ship.Heading;

        // Create a rotation matrix to orient the cone
        Matrix world = Matrix.CreateRotationY(-heading) * Matrix.CreateTranslation(shipPos);

        // Draw the cone at origin (the world matrix handles positioning)
        PrimitiveRenderer.DrawCone(Device, shipPos, 0.5f, 1.5f, shipColor, view, proj, 8);

        // Draw Merkaba indicator if active
        if (ship.MerkabaActive)
        {
            float merkabaPulse = 0.5f + 0.5f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 5f);
            Color merkabaColor = new Color(255, 255, 200, 100 + (int)(155 * merkabaPulse));
            PrimitiveRenderer.DrawSphere(Device, shipPos, 2f + merkabaPulse * 0.5f, merkabaColor, view, proj, 6);
        }

        // Draw rift charge indicator
        if (ship.IsChargingRift)
        {
            float progress = ship.RiftChargeProgress;
            Color chargeColor = new Color((byte)(255 * progress), 100, 255);
            PrimitiveRenderer.DrawSphere(Device, shipPos, 1f + progress * 2f, chargeColor, view, proj, 6);
        }
    }

    #endregion

    #region Speed lines

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

        var rng = Random.Shared;
        Color lineColor = SpeedLineColor;

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

            PrimitiveRenderer.DrawLine3D(Device, start, end, lineColor, view, proj);
        }
    }

    #endregion

    #region Helpers (3D-specific)

    /// <summary>
    /// Extracts the first 3 components of a 5D position as a 3D Vector3.
    /// </summary>
    private static Vector3 GetPosition3D(float[] pos5D)
    {
        return new Vector3(pos5D[0], pos5D[1], pos5D[2]);
    }

    /// <summary>
    /// Squared distance between two points — avoids the sqrt for cheap proximity culling.
    /// </summary>
    private static float DistanceSquared(Vector3 a, Vector3 b)
    {
        var d = a - b;
        return d.X * d.X + d.Y * d.Y + d.Z * d.Z;
    }

    private static float GetStellarSize(StellarType? stellarClass)
    {
        return stellarClass switch
        {
            StellarType.RedGiant => 3f,
            StellarType.WhiteDwarf => 0.8f,
            StellarType.BrownDwarf => 0.6f,
            _ => 1.5f, // MainSequence
        };
    }

    #endregion
}
