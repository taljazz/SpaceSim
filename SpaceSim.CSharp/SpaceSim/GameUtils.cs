using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SpaceSim;

/// <summary>
/// Static utility functions shared across the game, mirroring utils.py from the Python version.
/// </summary>
public static class GameUtils
{
    /// <summary>
    /// Speaks a message via the screen reader if the cooldown has elapsed.
    /// </summary>
    /// <param name="msg">The message to speak.</param>
    /// <param name="simTime">Current simulation time in seconds.</param>
    /// <param name="lastSpoken">Dictionary tracking when each message was last spoken.</param>
    /// <param name="tolk">The speech service instance.</param>
    public static void SpeakWithCooldown(
        string msg,
        float simTime,
        Dictionary<string, float> lastSpoken,
        TolkSpeechService tolk)
    {
        if (lastSpoken.TryGetValue(msg, out float lastTime)
            && (simTime - lastTime) < GameConstants.SpeechCooldown)
        {
            return;
        }

        lastSpoken[msg] = simTime;
        tolk.Speak(msg);
    }

    /// <summary>
    /// Projects a 5D position to 2D screen coordinates using rotation around spatial
    /// and higher dimensions. Mirrors project_to_2d from the Python version.
    /// </summary>
    /// <param name="pos">5D position array (length 5).</param>
    /// <param name="rotation">View rotation angle in radians.</param>
    /// <param name="screenW">Screen width in pixels.</param>
    /// <param name="screenH">Screen height in pixels.</param>
    /// <param name="zoom">Zoom level (1.0 = normal).</param>
    /// <param name="centerPos">Optional center position to compute relative coordinates from.</param>
    /// <returns>Tuple of (screenX, screenY) in pixel coordinates.</returns>
    public static (int X, int Y) ProjectTo2D(
        float[] pos,
        float rotation,
        int screenW,
        int screenH,
        float zoom = 1f,
        float[]? centerPos = null)
    {
        float[] rel;
        if (centerPos != null)
        {
            rel = Vec5.Subtract(pos, centerPos);
        }
        else
        {
            rel = Vec5.Clone(pos);
        }

        float cosR = MathF.Cos(rotation);
        float sinR = MathF.Sin(rotation);

        // Project using spatial dims (0,1) and higher dims (3,4)
        float x = rel[0] * cosR + rel[3] * sinR;
        float y = rel[1] * cosR + rel[4] * sinR;

        // Apply zoom
        x *= zoom;
        y *= zoom;

        // Convert to screen coordinates
        int screenX = (int)(screenW / 2f + x * (screenW / 200f));
        int screenY = (int)(screenH / 2f + y * (screenH / 200f));

        return (screenX, screenY);
    }

    /// <summary>
    /// Converts HSV color values to a MonoGame Color.
    /// </summary>
    /// <param name="h">Hue in degrees (0-360).</param>
    /// <param name="s">Saturation (0-100).</param>
    /// <param name="v">Value/brightness (0-100).</param>
    /// <param name="a">Alpha (0-255).</param>
    /// <returns>A MonoGame Color.</returns>
    public static Color FromHSVA(float h, float s, float v, float a = 255f)
    {
        float sNorm = s / 100f;
        float vNorm = v / 100f;

        float c = vNorm * sNorm;
        float hPrime = (h % 360f) / 60f;
        float x = c * (1f - MathF.Abs(hPrime % 2f - 1f));
        float m = vNorm - c;

        float r, g, b;

        if (hPrime < 1f)
        {
            r = c; g = x; b = 0f;
        }
        else if (hPrime < 2f)
        {
            r = x; g = c; b = 0f;
        }
        else if (hPrime < 3f)
        {
            r = 0f; g = c; b = x;
        }
        else if (hPrime < 4f)
        {
            r = 0f; g = x; b = c;
        }
        else if (hPrime < 5f)
        {
            r = x; g = 0f; b = c;
        }
        else
        {
            r = c; g = 0f; b = x;
        }

        return new Color(
            (byte)Math.Clamp((r + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((g + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((b + m) * 255f, 0f, 255f),
            (byte)Math.Clamp(a, 0f, 255f));
    }
}
