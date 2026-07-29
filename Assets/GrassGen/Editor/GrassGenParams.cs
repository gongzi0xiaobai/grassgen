// GrassGenParams.cs
// Part of GrassGen for Unity – a Unity 2020.3.9 editor port of the browser-based
// Grass texture generator (https://github.com/jmdejong/grassgen) by ~troido.
// Original work licensed under GPL v3. This adaptation is also licensed under GPL v3.

using UnityEngine;

namespace GrassGen
{
    /// <summary>
    /// Parameters controlling grass texture generation.
    /// Each field corresponds directly to an HTML input in the original browser tool.
    /// </summary>
    [System.Serializable]
    public class GrassGenParams
    {
        /// <summary>
        /// Integer random seed. Identical seed + identical parameters always produce
        /// the same output (deterministic). Default: 12 (matches original tool default).
        /// </summary>
        public int seed = 12;

        /// <summary>Texture width in pixels. Must be ≥ 1.</summary>
        public int width = 512;

        /// <summary>Texture height in pixels. Must be ≥ 1.</summary>
        public int height = 256;

        /// <summary>Number of grass blades to draw. 0 produces a blank texture.</summary>
        public int bladeCount = 200;

        /// <summary>
        /// Number of quadratic Bezier segments per blade outline half.
        /// Higher values produce smoother curves. Must be ≥ 1.
        /// </summary>
        public int segments = 6;

        /// <summary>
        /// Maximum horizontal spread of a blade's tip and control point relative
        /// to its base position, measured in pixels. Clamped to ≥ 0.
        /// </summary>
        public float spread = 80f;

        /// <summary>
        /// Fraction of the texture width kept clear on each horizontal edge
        /// (blade bases are sampled from [clearEdge, 1−clearEdge] × width).
        /// Range [0, 0.5].
        /// </summary>
        public float clearEdge = 0.1f;

        /// <summary>Minimum blade base half-width in pixels. Clamped to ≥ 0.</summary>
        public float bladeWidthMin = 2f;

        /// <summary>Maximum blade base half-width in pixels. Must be ≥ bladeWidthMin.</summary>
        public float bladeWidthMax = 8f;

        /// <summary>
        /// Base grass colour. Approximately #11dd11 in sRGB by default.
        /// Each blade receives a per-blade HSL perturbation around this colour.
        /// </summary>
        public Color baseColor = new Color(17f / 255f, 221f / 255f, 17f / 255f, 1f);

        /// <summary>
        /// Maximum hue shift per blade as a fraction of the full colour wheel [0, 1].
        /// The actual shift is sampled uniformly from [−hueSpread, +hueSpread].
        /// </summary>
        public float hueSpread = 0.1f;

        /// <summary>
        /// Maximum saturation shift per blade [0, 1].
        /// Sampled from [−saturationSpread, +saturationSpread], then clamped to [0, 1].
        /// </summary>
        public float saturationSpread = 0.3f;

        /// <summary>
        /// Maximum lightness shift per blade [0, 1].
        /// Sampled from [−lightnessSpread, +lightnessSpread], then clamped to [0, 1].
        /// </summary>
        public float lightnessSpread = 0.06f;

        /// <summary>Alpha (opacity) for every blade pixel [0, 1].</summary>
        public float alpha = 1f;

        /// <summary>
        /// Reset all fields to the defaults that match the original HTML tool's
        /// initial values.
        /// </summary>
        public void Reset()
        {
            seed = 12;
            width = 512;
            height = 256;
            bladeCount = 200;
            segments = 6;
            spread = 80f;
            clearEdge = 0.1f;
            bladeWidthMin = 2f;
            bladeWidthMax = 8f;
            baseColor = new Color(17f / 255f, 221f / 255f, 17f / 255f, 1f);
            hueSpread = 0.1f;
            saturationSpread = 0.3f;
            lightnessSpread = 0.06f;
            alpha = 1f;
        }
    }
}
