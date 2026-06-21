using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// Static helper for 2D drawing using SpriteBatch and a 1x1 pixel texture.
/// Serves as the fallback 2D rendering mode and for HUD overlays.
/// </summary>
public static class PrimitiveRenderer2D
{
    #region Shared state & initialization

    private static Texture2D? _pixel;
    private static bool _initialized;

    // Pre-computed unit circle points for common segment counts (avoids per-call trig)
    private static readonly Vector2[] _unitCircle32 = PrecomputeUnitCircle(32);
    private static readonly Vector2[] _unitCircle20 = PrecomputeUnitCircle(20);

    /// <summary>
    /// Builds a reusable unit-circle lookup table (cos/sin per segment) so circle
    /// drawing can skip trig at runtime. The extra closing point mirrors index 0
    /// so callers can connect the final segment without a wraparound check.
    /// </summary>
    private static Vector2[] PrecomputeUnitCircle(int segments)
    {
        var points = new Vector2[segments + 1];
        float step = MathHelper.TwoPi / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = step * i;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return points;
    }

    /// <summary>
    /// Creates the shared 1x1 white pixel texture used for all 2D primitive drawing.
    /// Must be called once after the GraphicsDevice is ready.
    /// </summary>
    public static void Initialize(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _initialized = true;
    }

    /// <summary>
    /// Returns the shared 1x1 pixel texture, or null if not initialized.
    /// </summary>
    public static Texture2D? Pixel => _pixel;

    #endregion

    #region Primitive drawing

    /// <summary>
    /// Draws a line between two points using the pixel texture stretched and rotated.
    /// </summary>
    public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, int thickness = 1)
    {
        if (!_initialized || _pixel == null) return;

        Vector2 delta = end - start;
        float length = delta.Length();
        if (length < 0.001f) return;

        // Stretch the 1x1 pixel to the segment length and rotate it to the line's angle.
        float angle = MathF.Atan2(delta.Y, delta.X);

        sb.Draw(
            _pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Draws a circle outline using line segments.
    /// </summary>
    public static void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color, int segments = 32)
    {
        if (!_initialized || _pixel == null) return;
        if (segments < 3) segments = 3;

        // Use pre-computed unit circle when available, fall back to trig
        Vector2[]? unitCircle = segments == 32 ? _unitCircle32 : segments == 20 ? _unitCircle20 : null;

        if (unitCircle != null)
        {
            Vector2 prev = center + unitCircle[0] * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 current = center + unitCircle[i] * radius;
                DrawLine(sb, prev, current, color, 1);
                prev = current;
            }
        }
        else
        {
            float angleStep = MathHelper.TwoPi / segments;
            Vector2 prev = center + new Vector2(radius, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i;
                Vector2 current = center + new Vector2(
                    radius * MathF.Cos(angle),
                    radius * MathF.Sin(angle));
                DrawLine(sb, prev, current, color, 1);
                prev = current;
            }
        }
    }

    /// <summary>
    /// Fills a circle by drawing the pixel texture scaled to the diameter.
    /// This is an approximation using a square; for a true filled circle
    /// at small sizes this is visually acceptable.
    /// </summary>
    public static void FillCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        if (!_initialized || _pixel == null) return;

        // For small radii, a scaled square is sufficient and performant.
        // For larger radii, we draw concentric rings for better approximation.
        if (radius <= 3f)
        {
            float diameter = radius * 2f;
            sb.Draw(
                _pixel,
                new Rectangle(
                    (int)(center.X - radius),
                    (int)(center.Y - radius),
                    (int)diameter,
                    (int)diameter),
                color);
            return;
        }

        // Draw filled circle using horizontal line strips
        int r = (int)MathF.Ceiling(radius);
        for (int y = -r; y <= r; y++)
        {
            float halfWidth = MathF.Sqrt(radius * radius - y * y);
            int x0 = (int)(center.X - halfWidth);
            int x1 = (int)(center.X + halfWidth);
            int lineY = (int)center.Y + y;

            sb.Draw(
                _pixel,
                new Rectangle(x0, lineY, x1 - x0, 1),
                color);
        }
    }

    /// <summary>
    /// Draws a polygon outline by connecting the given points with lines.
    /// The polygon is automatically closed (last point connects to first).
    /// </summary>
    public static void DrawPolygon(SpriteBatch sb, Vector2[] points, Color color, int thickness = 1)
    {
        if (!_initialized || _pixel == null) return;
        if (points.Length < 2) return;

        for (int i = 0; i < points.Length; i++)
        {
            // Wrap the last index back to 0 so the outline is closed.
            int next = (i + 1) % points.Length;
            DrawLine(sb, points[i], points[next], color, thickness);
        }
    }

    #endregion
}
