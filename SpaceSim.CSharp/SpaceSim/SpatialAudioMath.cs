using System;

namespace SpaceSim;

/// <summary>
/// Pure, testable helpers for the OpenAL spatial-audio layer (Round 3). Kept free of any OpenAL
/// types so the math can be unit-tested without an audio device.
///
/// <para>
/// The listener sits at the origin facing forward, and every world sound is positioned in
/// <em>listener space</em> — i.e. relative to the ship and rotated by the current view. This mirrors
/// exactly how the 2D projection already places things on screen, so what you hear lines up with
/// what (a sighted player) would see.
/// </para>
/// </summary>
public static class SpatialAudioMath
{
    #region 5D -> 3D listener-space mapping

    /// <summary>
    /// Convert a world object's 5D position into the 3D position OpenAL wants, expressed relative to
    /// the listener (ship) and rotated by <paramref name="viewRotation"/>.
    ///
    /// <para>
    /// The horizontal plane reuses the existing projection mix (spatial dim 1 with higher dim 4 for
    /// left/right; spatial dim 2 with higher dim 5 for front/back), and the third spatial dimension
    /// becomes elevation. Output uses OpenAL's axes: +X right, +Y up, −Z forward.
    /// </para>
    /// </summary>
    /// <param name="shipPos">The ship's 5D position (the listener).</param>
    /// <param name="targetPos">The world object's 5D position.</param>
    /// <param name="viewRotation">Current view rotation in radians.</param>
    /// <returns>The source position in OpenAL listener space.</returns>
    public static (float X, float Y, float Z) ToListenerSpace(float[] shipPos, float[] targetPos, float viewRotation)
    {
        float cosR = MathF.Cos(viewRotation);
        float sinR = MathF.Sin(viewRotation);

        float dx = targetPos[0] - shipPos[0];
        float dy = targetPos[1] - shipPos[1];
        float dz = targetPos[2] - shipPos[2];
        float dw = targetPos[3] - shipPos[3];
        float dv = targetPos[4] - shipPos[4];

        // Same mix the 2D projection uses, so audio direction matches on-screen direction.
        float right = dx * cosR + dw * sinR;   // left/right (azimuth)
        float forward = dy * cosR + dv * sinR; // toward/away
        float up = dz;                         // elevation from the third spatial realm

        // OpenAL convention: +X right, +Y up, and forward is −Z.
        return (right, up, -forward);
    }

    #endregion

    #region Doppler

    /// <summary>
    /// Doppler pitch multiplier for a source, from the listener's radial speed toward it: &gt; 1 when
    /// approaching, &lt; 1 when receding, exactly 1 when stationary or moving across the source.
    /// Clamped to [<paramref name="minPitch"/>, <paramref name="maxPitch"/>].
    /// </summary>
    /// <param name="shipPos">Listener (ship) 5D position.</param>
    /// <param name="shipVel">Listener (ship) 5D velocity.</param>
    /// <param name="sourcePos">Source 5D position.</param>
    /// <param name="scale">Pitch shift per unit of radial speed.</param>
    /// <param name="minPitch">Lower clamp.</param>
    /// <param name="maxPitch">Upper clamp.</param>
    public static float DopplerPitch(float[] shipPos, float[] shipVel, float[] sourcePos,
                                     float scale, float minPitch, float maxPitch)
    {
        float radial = 0f, distSq = 0f;
        for (int i = 0; i < Vec5.Dimensions; i++)
        {
            float d = sourcePos[i] - shipPos[i];   // ship -> source
            radial += shipVel[i] * d;
            distSq += d * d;
        }
        if (distSq < 1e-8f) return 1f;
        radial /= MathF.Sqrt(distSq);              // velocity component along the line to the source
        return Math.Clamp(1f + radial * scale, minPitch, maxPitch);
    }

    #endregion

    #region PCM conversion

    /// <summary>
    /// Convert a normalized float waveform (−1..1) into 16-bit PCM samples for
    /// <c>AL.BufferData(..., ALFormat.Mono16, ...)</c>. Out-of-range samples are clamped.
    /// </summary>
    /// <param name="samples">Source samples, nominally in [−1, 1].</param>
    /// <returns>16-bit signed PCM.</returns>
    public static short[] FloatToPcm16(float[] samples)
    {
        var pcm = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float s = Math.Clamp(samples[i], -1f, 1f);
            pcm[i] = (short)(s * short.MaxValue);
        }
        return pcm;
    }

    #endregion
}
