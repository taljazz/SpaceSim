using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceSim.Rendering;

/// <summary>
/// Static helper for drawing 3D primitives using MonoGame's BasicEffect.
/// Used for rendering celestial bodies, the ship, and other 3D objects procedurally
/// without requiring external 3D model files.
/// </summary>
public static class PrimitiveRenderer
{
    private static BasicEffect? _effect;
    private static bool _initialized;

    // Pre-allocated vertex buffers — reused each call to avoid per-frame GC pressure.
    // Only accessed from the render thread, so no concurrency issue.
    private const int MaxSegments = 16;
    private static readonly VertexPositionColor[] _sphereVerts = new VertexPositionColor[(MaxSegments + 1) * 3];
    private static readonly VertexPositionColor[] _lineVerts = new VertexPositionColor[2];
    private static readonly VertexPositionColor[] _pyramidVerts = new VertexPositionColor[18];
    private static readonly VertexPositionColor[] _coneVerts = new VertexPositionColor[MaxSegments * 3 + (MaxSegments - 2) * 3];

    /// <summary>
    /// Initializes the shared BasicEffect. Must be called once after the GraphicsDevice is ready.
    /// </summary>
    public static void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
        _initialized = true;
    }

    /// <summary>
    /// Draws a wireframe sphere at the given center position.
    /// Uses latitude/longitude line strips to approximate the sphere.
    /// </summary>
    public static void DrawSphere(
        GraphicsDevice device,
        Vector3 center,
        float radius,
        Color color,
        Matrix view,
        Matrix projection,
        int segments = 16)
    {
        if (!_initialized || _effect == null) return;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        // Fill pre-allocated vertex buffer for 3 rings
        // Ring in XZ plane (equator)
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float x = center.X + radius * MathF.Cos(angle);
            float z = center.Z + radius * MathF.Sin(angle);
            _sphereVerts[i] = new VertexPositionColor(new Vector3(x, center.Y, z), color);
        }

        // Ring in XY plane (meridian)
        int offset1 = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float x = center.X + radius * MathF.Cos(angle);
            float y = center.Y + radius * MathF.Sin(angle);
            _sphereVerts[offset1 + i] = new VertexPositionColor(new Vector3(x, y, center.Z), color);
        }

        // Ring in YZ plane
        int offset2 = (segments + 1) * 2;
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float y = center.Y + radius * MathF.Cos(angle);
            float z = center.Z + radius * MathF.Sin(angle);
            _sphereVerts[offset2 + i] = new VertexPositionColor(new Vector3(center.X, y, z), color);
        }

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineStrip, _sphereVerts, 0, segments);
            device.DrawUserPrimitives(PrimitiveType.LineStrip, _sphereVerts, offset1, segments);
            device.DrawUserPrimitives(PrimitiveType.LineStrip, _sphereVerts, offset2, segments);
        }
    }

    /// <summary>
    /// Draws a 3D line between two points.
    /// </summary>
    public static void DrawLine3D(
        GraphicsDevice device,
        Vector3 start,
        Vector3 end,
        Color color,
        Matrix view,
        Matrix projection)
    {
        if (!_initialized || _effect == null) return;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        _lineVerts[0] = new VertexPositionColor(start, color);
        _lineVerts[1] = new VertexPositionColor(end, color);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineList, _lineVerts, 0, 1);
        }
    }

    /// <summary>
    /// Draws a 4-sided pyramid (square base) at the given center position.
    /// The apex is above the center by the given size.
    /// </summary>
    public static void DrawPyramid(
        GraphicsDevice device,
        Vector3 center,
        float size,
        Color color,
        Matrix view,
        Matrix projection)
    {
        if (!_initialized || _effect == null) return;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        float halfBase = size * 0.5f;
        float height = size;

        Vector3 apex = center + new Vector3(0, height, 0);
        Vector3 bl = center + new Vector3(-halfBase, 0, -halfBase);
        Vector3 br = center + new Vector3(halfBase, 0, -halfBase);
        Vector3 fr = center + new Vector3(halfBase, 0, halfBase);
        Vector3 fl = center + new Vector3(-halfBase, 0, halfBase);

        // Slightly darker color for base
        Color baseColor = new Color(
            (byte)(color.R * 0.7f),
            (byte)(color.G * 0.7f),
            (byte)(color.B * 0.7f),
            color.A);

        // 4 triangular faces + 2 base triangles = 18 vertices
        _pyramidVerts[0]  = new(apex, color); _pyramidVerts[1]  = new(fl, color);  _pyramidVerts[2]  = new(fr, color);
        _pyramidVerts[3]  = new(apex, color); _pyramidVerts[4]  = new(fr, color);  _pyramidVerts[5]  = new(br, color);
        _pyramidVerts[6]  = new(apex, color); _pyramidVerts[7]  = new(br, color);  _pyramidVerts[8]  = new(bl, color);
        _pyramidVerts[9]  = new(apex, color); _pyramidVerts[10] = new(bl, color);  _pyramidVerts[11] = new(fl, color);
        _pyramidVerts[12] = new(bl, baseColor); _pyramidVerts[13] = new(br, baseColor); _pyramidVerts[14] = new(fr, baseColor);
        _pyramidVerts[15] = new(bl, baseColor); _pyramidVerts[16] = new(fr, baseColor); _pyramidVerts[17] = new(fl, baseColor);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _pyramidVerts, 0, 6);
        }
    }

    /// <summary>
    /// Draws a cone at the given center position (base at center, apex above).
    /// Useful for rendering the ship model.
    /// </summary>
    public static void DrawCone(
        GraphicsDevice device,
        Vector3 center,
        float radius,
        float height,
        Color color,
        Matrix view,
        Matrix projection,
        int segments = 12)
    {
        if (!_initialized || _effect == null) return;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        Vector3 apex = center + new Vector3(0, height, 0);

        // Darker color for the base
        Color baseColor = new Color(
            (byte)(color.R * 0.6f),
            (byte)(color.G * 0.6f),
            (byte)(color.B * 0.6f),
            color.A);

        // Side faces (triangle fan from apex)
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathHelper.TwoPi * i / segments;
            float a1 = MathHelper.TwoPi * ((i + 1) % segments) / segments;

            Vector3 p0 = center + new Vector3(radius * MathF.Cos(a0), 0, radius * MathF.Sin(a0));
            Vector3 p1 = center + new Vector3(radius * MathF.Cos(a1), 0, radius * MathF.Sin(a1));

            int idx = i * 3;
            _coneVerts[idx] = new VertexPositionColor(apex, color);
            _coneVerts[idx + 1] = new VertexPositionColor(p0, color);
            _coneVerts[idx + 2] = new VertexPositionColor(p1, color);
        }

        // Base (triangle fan from center)
        int baseOffset = segments * 3;
        for (int i = 0; i < segments - 2; i++)
        {
            float a0 = 0f;
            float a1 = MathHelper.TwoPi * (i + 1) / segments;
            float a2 = MathHelper.TwoPi * (i + 2) / segments;

            Vector3 p0 = center + new Vector3(radius * MathF.Cos(a0), 0, radius * MathF.Sin(a0));
            Vector3 p1 = center + new Vector3(radius * MathF.Cos(a1), 0, radius * MathF.Sin(a1));
            Vector3 p2 = center + new Vector3(radius * MathF.Cos(a2), 0, radius * MathF.Sin(a2));

            int idx = baseOffset + i * 3;
            _coneVerts[idx] = new VertexPositionColor(p0, baseColor);
            _coneVerts[idx + 1] = new VertexPositionColor(p1, baseColor);
            _coneVerts[idx + 2] = new VertexPositionColor(p2, baseColor);
        }

        int totalTriangles = segments + (segments - 2);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _coneVerts, 0, totalTriangles);
        }
    }
}
