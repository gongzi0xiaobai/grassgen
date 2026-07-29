// GrassGenerator.cs
// Part of GrassGen for Unity – a Unity 2020.3.9 editor port of the browser-based
// Grass texture generator (https://github.com/jmdejong/grassgen) by ~troido.
// Original work licensed under GPL v3. This adaptation is also licensed under GPL v3.
//
// Algorithm correspondence with original main.js / hsv.js:
//   Rng struct           ← Rng() closure (murmurhash3 finalizer variant)
//   SampleRange()        ← sample_range() / Range.sample()
//   RgbToHsl()           ← rgbToHsl() from hsv.js
//   HslToColor()         ← hslToRgb() from hsv.js
//   ModifyHslColor()     ← HslColor.modify()
//   QuadBezierPos/W()    ← quadBezier() + lerp()
//   FillPolygon()        ← canvas 2D scanline fill (replaces ctx.fill())
//   Generate()           ← redraw()

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrassGen
{
    /// <summary>
    /// Stateless grass texture generator.  Call <see cref="Generate"/> to obtain a
    /// <see cref="Texture2D"/>; the caller is responsible for destroying it when no
    /// longer needed (call <c>Object.DestroyImmediate</c> in editor code).
    /// </summary>
    public static class GrassGenerator
    {
        // ------------------------------------------------------------------ //
        //  Deterministic RNG – exact port of the JS Rng() closure            //
        //  Source: https://stackoverflow.com/a/47593316                       //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Murmurhash3-style finalizer RNG, matching the original JS <c>Rng(seed)</c>
        /// closure exactly.  Each call to <see cref="Next"/> advances internal state
        /// and returns a value in [0, 1).
        /// </summary>
        private struct Rng
        {
            private uint _state;

            /// <summary>Seed the generator. Matches JS <c>Rng(seed)</c>.</summary>
            public Rng(int seed) { _state = (uint)seed; }

            /// <summary>
            /// Return next pseudo-random float in [0, 1).
            /// Matches one invocation of the closure returned by JS <c>Rng(seed)</c>.
            /// </summary>
            public float Next()
            {
                // a += 0x6D2B79F5  (uint wrap)
                uint t = unchecked(_state += 0x6D2B79F5u);
                // t = Math.imul(t ^ t>>>15, t | 1)
                t = unchecked((t ^ (t >> 15)) * (t | 1u));
                // t ^= t + Math.imul(t ^ t>>>7, t | 61)
                t ^= unchecked(t + (t ^ (t >> 7)) * (t | 61u));
                // ((t ^ t>>>14) >>> 0) / 4294967296
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }

        // ------------------------------------------------------------------ //
        //  Math helpers                                                        //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Linearly map <paramref name="t"/> ∈ [0, 1] onto [min, max].
        /// If min &gt; max they are swapped first (matching JS <c>Range</c> constructor).
        /// </summary>
        private static float SampleRange(float min, float max, float t)
        {
            if (min > max) { float tmp = min; min = max; max = tmp; }
            return t * (max - min) + min;
        }

        private static Vector2 Lerp2(Vector2 a, Vector2 b, float t)
            => new Vector2(a.x * (1f - t) + b.x * t, a.y * (1f - t) + b.y * t);

        private static float LerpF(float a, float b, float t)
            => a * (1f - t) + b * t;

        /// <summary>Quadratic Bezier position.</summary>
        private static Vector2 QuadBezierPos(Vector2 p0, Vector2 p1, Vector2 p2, float t)
            => Lerp2(Lerp2(p0, p1, t), Lerp2(p1, p2, t), t);

        /// <summary>Quadratic Bezier width (scalar).</summary>
        private static float QuadBezierW(float w0, float w1, float w2, float t)
            => LerpF(LerpF(w0, w1, t), LerpF(w1, w2, t), t);

        // ------------------------------------------------------------------ //
        //  HSL colour helpers – ported from hsv.js                            //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Convert RGB (each in [0, 1]) to HSL (each in [0, 1]).
        /// Matches <c>rgbToHsl(r*255, g*255, b*255)</c> from hsv.js.
        /// </summary>
        public static void RgbToHsl(float r, float g, float b,
                                     out float h, out float s, out float l)
        {
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            l = (max + min) * 0.5f;

            if (max == min)
            {
                h = s = 0f; // achromatic
            }
            else
            {
                float d = max - min;
                s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

                if (max == r)
                    h = (g - b) / d + (g < b ? 6f : 0f);
                else if (max == g)
                    h = (b - r) / d + 2f;
                else
                    h = (r - g) / d + 4f;

                h /= 6f;
            }
        }

        private static float Hue2Rgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f)    return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        /// <summary>
        /// Convert HSL (each in [0, 1]) to a Unity <see cref="Color"/>.
        /// Matches <c>hslToRgb</c> from hsv.js.
        /// </summary>
        public static Color HslToColor(float h, float s, float l, float alpha)
        {
            float r, g, b;
            if (s == 0f)
            {
                r = g = b = l;
            }
            else
            {
                float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
                float p = 2f * l - q;
                r = Hue2Rgb(p, q, h + 1f / 3f);
                g = Hue2Rgb(p, q, h);
                b = Hue2Rgb(p, q, h - 1f / 3f);
            }
            return new Color(r, g, b, alpha);
        }

        /// <summary>
        /// Apply HSL offsets to a base colour, with hue wrap and saturation/lightness
        /// clamp.  Matches JS <c>HslColor.modify(hdiff, sdiff, ldiff)</c>.
        /// </summary>
        private static Color ModifyHslColor(
            float baseH, float baseS, float baseL, float baseAlpha,
            float hdiff, float sdiff, float ldiff)
        {
            float hue = baseH + hdiff;
            hue -= Mathf.Floor(hue);                          // wrap to [0, 1)
            float sat = Mathf.Clamp(baseS + sdiff, 0f, 1f);
            float lit = Mathf.Clamp(baseL + ldiff, 0f, 1f);
            return HslToColor(hue, sat, lit, baseAlpha);
        }

        // ------------------------------------------------------------------ //
        //  Public entry point                                                  //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Generate a grass texture from <paramref name="p"/>.
        /// The returned <see cref="Texture2D"/> is RGBA32, has a fully transparent
        /// background, and must be destroyed by the caller when no longer needed.
        /// </summary>
        /// <param name="p">Generation parameters. Must not be null.</param>
        /// <returns>A new, uncompressed RGBA32 <see cref="Texture2D"/>.</returns>
        public static Texture2D Generate(GrassGenParams p)
        {
            if (p == null) throw new ArgumentNullException("p");

            int w = Mathf.Max(1, p.width);
            int h = Mathf.Max(1, p.height);

            // false = no mip chain; false = sRGB (not linear)
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);

            // Pixel buffer – default Color() is (0,0,0,0): transparent black.
            var pixels = new Color[w * h];

            var rng = new Rng(p.seed);

            // Decompose base colour to HSL once.
            float baseH, baseS, baseL;
            RgbToHsl(p.baseColor.r, p.baseColor.g, p.baseColor.b,
                     out baseH, out baseS, out baseL);
            float baseAlpha = Mathf.Clamp01(p.alpha);

            float bladeWMin  = Mathf.Min(p.bladeWidthMin, p.bladeWidthMax);
            float bladeWMax  = Mathf.Max(p.bladeWidthMin, p.bladeWidthMax);
            float clearEdge  = Mathf.Clamp(p.clearEdge, 0f, 0.5f);
            float spread     = Mathf.Max(0f, p.spread);
            int   nseg       = Mathf.Max(1, p.segments);

            for (int i = 0; i < p.bladeCount; i++)
            {
                // ---- Base point (bottom of blade) ----
                // Call order matches JS redraw() exactly (10 RNG calls per blade).
                float baseX = SampleRange(clearEdge, 1f - clearEdge, rng.Next()) * w;   // call 1
                float baseW = SampleRange(bladeWMin, bladeWMax, rng.Next());             // call 2

                // ---- End point (tip) ----
                float xLo = Mathf.Max(baseX - spread, 0f);
                float xHi = Mathf.Min(baseX + spread, w);
                float endX = SampleRange(xLo, xHi, rng.Next());                         // call 3
                float endY = SampleRange(h * 0.5f, h, rng.Next());                      // call 4

                // ---- Control point (middle) ----
                float ctrlX = SampleRange(xLo, xHi, rng.Next());                        // call 5
                float ctrlY = SampleRange(endY * 0.5f, endY, rng.Next());               // call 6
                float ctrlW = SampleRange(baseW * 0.5f, baseW, rng.Next());             // call 7

                // ---- Per-blade colour perturbation ----
                float hdiff = SampleRange(-p.hueSpread,        p.hueSpread,        rng.Next()); // call 8
                float sdiff = SampleRange(-p.saturationSpread, p.saturationSpread, rng.Next()); // call 9
                float ldiff = SampleRange(-p.lightnessSpread,  p.lightnessSpread,  rng.Next()); // call 10
                Color bladeColor = ModifyHslColor(baseH, baseS, baseL, baseAlpha,
                                                  hdiff, sdiff, ldiff);

                // ---- Build polygon vertices ----
                // Matches the JS loop:
                //   ctx.moveTo(base.x - base.w,  height - base.y);   ← v[0]
                //   for p in 1..2*nseg:  ctx.lineTo(pos.x + pos.w*b, height - pos.y);
                //
                // Coordinate note: JS uses canvas (y=0 at top), Unity Texture2D uses
                // y=0 at bottom.  The JS draw call uses (height − pos.y), which maps
                // JS-y=0 → pixel-row=height (bottom).  Unity SetPixel y=0 IS the
                // bottom row, so we can use pos.y directly – no additional flip needed.
                int      nVerts = 1 + 2 * nseg;
                Vector2[] verts = new Vector2[nVerts];
                verts[0] = new Vector2(baseX - baseW, 0f);   // left base corner

                Vector2 bPos = new Vector2(baseX, 0f);
                Vector2 cPos = new Vector2(ctrlX, ctrlY);
                Vector2 ePos = new Vector2(endX,  endY);

                for (int pIdx = 1; pIdx <= 2 * nseg; pIdx++)
                {
                    float t = Mathf.Min(pIdx, 2 * nseg - pIdx) / (float)nseg;
                    float b = (pIdx > nseg) ? 1f : -1f;

                    Vector2 pos  = QuadBezierPos(bPos, cPos, ePos, t);
                    float   posW = QuadBezierW(baseW, ctrlW, 0f, t);

                    verts[pIdx] = new Vector2(pos.x + posW * b, pos.y);
                }

                FillPolygon(verts, bladeColor, pixels, w, h);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ //
        //  Scanline polygon rasteriser                                         //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Fill a convex-or-simple polygon into <paramref name="pixels"/> using
        /// even-odd scanline rasterisation.
        /// Coordinate space: x ∈ [0, width), y ∈ [0, height) with y = 0 at the
        /// bottom (matching Unity's Texture2D layout).
        /// </summary>
        private static void FillPolygon(
            Vector2[] verts, Color color, Color[] pixels, int width, int height)
        {
            if (verts == null || verts.Length < 3) return;

            // Bounding box of the polygon.
            float yMin = float.MaxValue;
            float yMax = float.MinValue;
            foreach (var v in verts)
            {
                if (v.y < yMin) yMin = v.y;
                if (v.y > yMax) yMax = v.y;
            }

            int yScanMin = Mathf.Max(0, Mathf.FloorToInt(yMin));
            int yScanMax = Mathf.Min(height - 1, Mathf.CeilToInt(yMax));
            int n = verts.Length;

            var xs = new List<float>(4);   // re-used per scanline

            for (int scanY = yScanMin; scanY <= yScanMax; scanY++)
            {
                float fy = scanY + 0.5f;   // sample at pixel centre
                xs.Clear();

                for (int vi = 0; vi < n; vi++)
                {
                    Vector2 a = verts[vi];
                    Vector2 b = verts[(vi + 1) % n];

                    // Edge crosses the scan line?
                    if ((a.y <= fy && b.y > fy) || (b.y <= fy && a.y > fy))
                    {
                        float tEdge = (fy - a.y) / (b.y - a.y);
                        xs.Add(a.x + tEdge * (b.x - a.x));
                    }
                }

                xs.Sort();

                for (int ii = 0; ii + 1 < xs.Count; ii += 2)
                {
                    int xStart = Mathf.Max(0,         Mathf.FloorToInt(xs[ii]));
                    int xEnd   = Mathf.Min(width - 1, Mathf.CeilToInt(xs[ii + 1]));
                    for (int px = xStart; px <= xEnd; px++)
                        pixels[scanY * width + px] = color;
                }
            }
        }
    }
}
