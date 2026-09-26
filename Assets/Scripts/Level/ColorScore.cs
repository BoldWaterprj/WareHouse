using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>
    /// Colour matching rules.
    ///
    /// Instead of hard "spectrum" buckets, two colours are compared with a plain
    /// 3D (RGB) distance:
    ///   * distance >= MaxDistance  -> no match, 0 points
    ///   * otherwise                -> a multiplier in 0..1 used for the score
    ///
    /// Tweak <see cref="MaxDistance"/> to make matching stricter or more forgiving
    /// (larger = more forgiving). Max possible RGB distance is sqrt(3) ~= 1.732.
    /// </summary>
    public static class ColorScore
    {
        public const float MaxDistance = 0.6f;

        public static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>Overall multiplier in [0..1]. 0 means "not the same colour".</summary>
        public static float Closeness(Color box, Color shelf)
        {
            float d = Distance(box, shelf);
            if (d >= MaxDistance)
                return 0f;

            float t = d / MaxDistance;     // 0 at perfect match, 1 at the threshold
            return Mathf.Clamp01(1f - t * t); // smooth falloff, still high when close
        }

        /// <summary>Per-channel precision in [0..1] (used for the session report).</summary>
        public static void Precision(Color box, Color shelf, out float r, out float g, out float b)
        {
            r = Mathf.Clamp01(1f - Mathf.Abs(box.r - shelf.r));
            g = Mathf.Clamp01(1f - Mathf.Abs(box.g - shelf.g));
            b = Mathf.Clamp01(1f - Mathf.Abs(box.b - shelf.b));
        }
    }
}
