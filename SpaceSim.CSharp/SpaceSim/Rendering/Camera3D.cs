using System;
using Microsoft.Xna.Framework;

namespace SpaceSim.Rendering;

/// <summary>
/// Third-person 3D camera that orbits behind and above the ship.
/// The camera follows the ship position (using the first 3 components of the 5D position
/// as 3D world space) and can be rotated horizontally and vertically around it.
/// </summary>
public class Camera3D
{
    /// <summary>
    /// Horizontal orbit angle around the ship in radians.
    /// Controlled by Left/Right arrow keys (replaces Python's view_rotation).
    /// </summary>
    public float YawAngle { get; set; }

    /// <summary>
    /// Vertical pitch angle in degrees. Clamped between MinPitch and MaxPitch.
    /// Higher values look more top-down; lower values look from behind/level.
    /// </summary>
    public float PitchAngle { get; set; } = 45f;

    /// <summary>
    /// Distance from the camera to the ship. Controlled by zoom keys and mouse wheel.
    /// </summary>
    public float ZoomDistance { get; set; } = 30f;

    /// <summary>
    /// Minimum pitch angle in degrees (nearly behind the ship).
    /// </summary>
    public const float MinPitch = 10f;

    /// <summary>
    /// Maximum pitch angle in degrees (top-down view).
    /// </summary>
    public const float MaxPitch = 90f;

    /// <summary>
    /// Minimum zoom distance.
    /// </summary>
    public const float MinZoom = 5f;

    /// <summary>
    /// Maximum zoom distance.
    /// </summary>
    public const float MaxZoom = 150f;

    /// <summary>
    /// The world-space position the camera is looking at (the ship).
    /// </summary>
    public Vector3 Target { get; private set; }

    /// <summary>
    /// The world-space position of the camera.
    /// </summary>
    public Vector3 Position { get; private set; }

    /// <summary>
    /// The current view matrix for rendering.
    /// </summary>
    public Matrix ViewMatrix { get; private set; }

    /// <summary>
    /// The current projection matrix for rendering.
    /// </summary>
    public Matrix ProjectionMatrix { get; private set; }

    private float _aspectRatio = 4f / 3f;

    /// <summary>
    /// Updates the camera based on the ship's 5D position.
    /// Uses the first 3 components as 3D world-space coordinates.
    /// </summary>
    /// <param name="shipPosition">The ship's 5D position array.</param>
    /// <param name="dt">Delta time in seconds.</param>
    public void Update(float[] shipPosition, float dt)
    {
        // Extract 3D position from 5D (x, y, z are the spatial dimensions)
        Target = new Vector3(shipPosition[0], shipPosition[1], shipPosition[2]);

        // Compute camera offset from ship using spherical coordinates
        float pitchRad = MathHelper.ToRadians(PitchAngle);

        // Camera offset: behind the ship at yaw angle, elevated by pitch
        float horizontalDist = ZoomDistance * MathF.Cos(pitchRad);
        float verticalDist = ZoomDistance * MathF.Sin(pitchRad);

        float offsetX = horizontalDist * MathF.Sin(YawAngle);
        float offsetZ = horizontalDist * MathF.Cos(YawAngle);
        float offsetY = verticalDist;

        Position = Target + new Vector3(offsetX, offsetY, offsetZ);

        // Build view and projection matrices
        ViewMatrix = Matrix.CreateLookAt(Position, Target, Vector3.Up);
    }

    /// <summary>
    /// Updates the projection matrix. Call when the screen size changes.
    /// </summary>
    /// <param name="screenW">Screen width.</param>
    /// <param name="screenH">Screen height.</param>
    public void UpdateProjection(int screenW, int screenH)
    {
        _aspectRatio = screenH > 0 ? (float)screenW / screenH : 4f / 3f;
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(60f),
            _aspectRatio,
            0.1f,
            2000f);
    }

    /// <summary>
    /// Adjusts the yaw angle by the given delta (in radians).
    /// Wraps to [0, 2*PI].
    /// </summary>
    public void RotateYaw(float deltaRadians)
    {
        YawAngle = (YawAngle + deltaRadians) % MathHelper.TwoPi;
        if (YawAngle < 0f)
            YawAngle += MathHelper.TwoPi;
    }

    /// <summary>
    /// Adjusts the pitch angle by the given delta (in degrees).
    /// Clamped between MinPitch and MaxPitch.
    /// </summary>
    public void AdjustPitch(float deltaDegrees)
    {
        PitchAngle = MathHelper.Clamp(PitchAngle + deltaDegrees, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Adjusts the zoom distance by the given delta.
    /// Clamped between MinZoom and MaxZoom.
    /// </summary>
    public void AdjustZoom(float delta)
    {
        ZoomDistance = MathHelper.Clamp(ZoomDistance + delta, MinZoom, MaxZoom);
    }
}
