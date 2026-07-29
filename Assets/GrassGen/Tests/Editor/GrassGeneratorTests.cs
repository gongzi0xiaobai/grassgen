// GrassGeneratorTests.cs
// Part of GrassGen for Unity – editor test suite.
// Licensed under GPL v3.
//
// Run these tests via Unity's Test Runner window (Window → General → Test Runner)
// in Edit Mode.  The assembly definition GrassGen.Editor.Tests.asmdef wires them
// up automatically.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GrassGen;

namespace GrassGen.Tests.Editor
{
    /// <summary>
    /// Edit-mode NUnit tests for <see cref="GrassGenerator"/> and
    /// <see cref="GrassGenParams"/>.  All tests are deterministic and require no
    /// special project setup.
    /// </summary>
    [TestFixture]
    public class GrassGeneratorTests
    {
        // ------------------------------------------------------------------ //
        //  Determinism                                                         //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("Same seed and params must always produce identical pixel data.")]
        public void Generate_SameSeed_ProducesSamePixels()
        {
            var p = new GrassGenParams { seed = 42, width = 32, height = 16, bladeCount = 10 };

            Texture2D tex1 = null;
            Texture2D tex2 = null;
            try
            {
                tex1 = GrassGenerator.Generate(p);
                tex2 = GrassGenerator.Generate(p);

                Color[] px1 = tex1.GetPixels();
                Color[] px2 = tex2.GetPixels();

                Assert.AreEqual(px1.Length, px2.Length, "Pixel array length must match.");
                for (int i = 0; i < px1.Length; i++)
                    Assert.AreEqual(px1[i], px2[i],
                        string.Format("Pixel {0} differs between two calls with the same seed.", i));
            }
            finally
            {
                SafeDestroy(tex1);
                SafeDestroy(tex2);
            }
        }

        [Test]
        [Description("Different seeds must NOT produce the same result (statistical sanity check).")]
        public void Generate_DifferentSeeds_ProduceDifferentPixels()
        {
            var p1 = new GrassGenParams { seed = 1, width = 64, height = 32, bladeCount = 50 };
            var p2 = new GrassGenParams { seed = 2, width = 64, height = 32, bladeCount = 50 };

            Texture2D tex1 = null;
            Texture2D tex2 = null;
            try
            {
                tex1 = GrassGenerator.Generate(p1);
                tex2 = GrassGenerator.Generate(p2);

                Color[] px1 = tex1.GetPixels();
                Color[] px2 = tex2.GetPixels();

                int differences = 0;
                for (int i = 0; i < px1.Length; i++)
                    if (px1[i] != px2[i]) differences++;

                Assert.Greater(differences, 0,
                    "Textures generated with different seeds should differ in at least one pixel.");
            }
            finally
            {
                SafeDestroy(tex1);
                SafeDestroy(tex2);
            }
        }

        // ------------------------------------------------------------------ //
        //  Texture dimensions                                                  //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("Generated texture must have exactly the requested dimensions.")]
        public void Generate_ReturnsCorrectDimensions()
        {
            var p = new GrassGenParams { width = 128, height = 64, bladeCount = 5 };
            var tex = GrassGenerator.Generate(p);
            try
            {
                Assert.AreEqual(128, tex.width,  "Texture width must match the requested value.");
                Assert.AreEqual(64,  tex.height, "Texture height must match the requested value.");
                Assert.AreEqual(TextureFormat.RGBA32, tex.format, "Format must be RGBA32.");
            }
            finally { SafeDestroy(tex); }
        }

        [Test]
        [Description("1×1 texture with many blades must not throw.")]
        public void Generate_MinimalDimensions_DoesNotThrow()
        {
            var p = new GrassGenParams { width = 1, height = 1, bladeCount = 100, segments = 1 };
            Assert.DoesNotThrow(() =>
            {
                var tex = GrassGenerator.Generate(p);
                SafeDestroy(tex);
            });
        }

        // ------------------------------------------------------------------ //
        //  Alpha / transparent background                                      //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("A texture generated with 0 blades must be fully transparent.")]
        public void Generate_ZeroBlades_FullyTransparent()
        {
            var p = new GrassGenParams { width = 32, height = 16, bladeCount = 0 };
            var tex = GrassGenerator.Generate(p);
            try
            {
                Color[] pixels = tex.GetPixels();
                foreach (var px in pixels)
                    Assert.AreEqual(0f, px.a, 1e-5f,
                        "Every pixel must be transparent when no blades are generated.");
            }
            finally { SafeDestroy(tex); }
        }

        [Test]
        [Description("A texture with blades must contain at least one non-transparent pixel.")]
        public void Generate_WithBlades_HasNonTransparentPixels()
        {
            var p = new GrassGenParams { width = 128, height = 64, bladeCount = 50, alpha = 1f };
            var tex = GrassGenerator.Generate(p);
            try
            {
                Color[] pixels = tex.GetPixels();
                bool found = false;
                foreach (var px in pixels)
                    if (px.a > 0f) { found = true; break; }

                Assert.IsTrue(found, "At least one pixel must be non-transparent.");
            }
            finally { SafeDestroy(tex); }
        }

        [Test]
        [Description("Alpha = 0 must produce a fully transparent texture even with many blades.")]
        public void Generate_AlphaZero_FullyTransparent()
        {
            var p = new GrassGenParams
            {
                width = 64, height = 32, bladeCount = 100,
                alpha = 0f
            };
            var tex = GrassGenerator.Generate(p);
            try
            {
                Color[] pixels = tex.GetPixels();
                foreach (var px in pixels)
                    Assert.AreEqual(0f, px.a, 1e-5f,
                        "All pixels must be transparent when alpha = 0.");
            }
            finally { SafeDestroy(tex); }
        }

        // ------------------------------------------------------------------ //
        //  Extreme / edge-case parameters                                      //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("Extreme parameters must not throw any exception.")]
        public void Generate_ExtremeParams_DoesNotThrow()
        {
            var extremeCases = new[]
            {
                new GrassGenParams { width = 1024, height = 1024, bladeCount = 1, segments = 1 },
                new GrassGenParams { width = 2,    height = 2,    bladeCount = 500, segments = 20 },
                new GrassGenParams { width = 256,  height = 256,  bladeCount = 200, spread = 0f },
                new GrassGenParams { width = 256,  height = 256,  bladeCount = 200, clearEdge = 0f },
                new GrassGenParams { width = 256,  height = 256,  bladeCount = 200, clearEdge = 0.5f },
                new GrassGenParams { width = 256,  height = 256,  bladeCount = 200,
                                     bladeWidthMin = 0f, bladeWidthMax = 0f },
                new GrassGenParams { width = 256,  height = 256,  bladeCount = 200,
                                     hueSpread = 1f, saturationSpread = 1f, lightnessSpread = 1f },
                new GrassGenParams { seed = int.MinValue, width = 64, height = 32, bladeCount = 10 },
                new GrassGenParams { seed = int.MaxValue, width = 64, height = 32, bladeCount = 10 },
            };

            foreach (var p in extremeCases)
            {
                Assert.DoesNotThrow(() =>
                {
                    var tex = GrassGenerator.Generate(p);
                    SafeDestroy(tex);
                }, "Generate() must not throw for params: seed={0} w={1} h={2} n={3}",
                   p.seed, p.width, p.height, p.bladeCount);
            }
        }

        [Test]
        [Description("Null params must throw ArgumentNullException.")]
        public void Generate_NullParams_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => GrassGenerator.Generate(null));
        }

        // ------------------------------------------------------------------ //
        //  HSL round-trip                                                      //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("RgbToHsl → HslToColor round-trip must preserve the original colour.")]
        public void HslRoundTrip_PreservesColour()
        {
            var testColors = new[]
            {
                new Color(1f, 0f, 0f),         // red
                new Color(0f, 1f, 0f),         // green
                new Color(0f, 0f, 1f),         // blue
                new Color(0f, 0f, 0f),         // black
                new Color(1f, 1f, 1f),         // white
                new Color(0.5f, 0.5f, 0.5f),   // grey
                new Color(17f/255f, 221f/255f, 17f/255f), // default base colour
            };

            foreach (var c in testColors)
            {
                float h, s, l;
                GrassGenerator.RgbToHsl(c.r, c.g, c.b, out h, out s, out l);
                Color roundTripped = GrassGenerator.HslToColor(h, s, l, 1f);

                Assert.AreEqual(c.r, roundTripped.r, 1e-4f,
                    string.Format("Red channel mismatch for input {0}", c));
                Assert.AreEqual(c.g, roundTripped.g, 1e-4f,
                    string.Format("Green channel mismatch for input {0}", c));
                Assert.AreEqual(c.b, roundTripped.b, 1e-4f,
                    string.Format("Blue channel mismatch for input {0}", c));
            }
        }

        // ------------------------------------------------------------------ //
        //  Save path validation (logic-only, no file I/O)                     //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("A path inside Assets should be considered valid.")]
        public void PathValidation_InsideAssets_IsValid()
        {
            string assets = NormalizePath("/Project/Assets");
            string path   = NormalizePath("/Project/Assets/Textures/grass.png");
            Assert.IsTrue(path.StartsWith(assets,
                System.StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        [Description("A path outside Assets (e.g. the Desktop) must NOT be considered valid.")]
        public void PathValidation_OutsideAssets_IsInvalid()
        {
            string assets = NormalizePath("/Project/Assets");
            string path   = NormalizePath("/Users/user/Desktop/grass.png");
            Assert.IsFalse(path.StartsWith(assets,
                System.StringComparison.OrdinalIgnoreCase));
        }

        // ------------------------------------------------------------------ //
        //  GrassGenParams.Reset                                                //
        // ------------------------------------------------------------------ //

        [Test]
        [Description("Reset() must restore all fields to their documented defaults.")]
        public void GrassGenParams_Reset_RestoresDefaults()
        {
            var p = new GrassGenParams
            {
                seed = 9999, width = 1, height = 1, bladeCount = 0,
                segments = 99, spread = 0f, clearEdge = 0.5f,
                bladeWidthMin = 100f, bladeWidthMax = 200f,
                alpha = 0f,
                hueSpread = 0f, saturationSpread = 0f, lightnessSpread = 0f
            };
            p.Reset();

            Assert.AreEqual(12,    p.seed);
            Assert.AreEqual(512,   p.width);
            Assert.AreEqual(256,   p.height);
            Assert.AreEqual(200,   p.bladeCount);
            Assert.AreEqual(6,     p.segments);
            Assert.AreEqual(80f,   p.spread,        1e-5f);
            Assert.AreEqual(0.1f,  p.clearEdge,     1e-5f);
            Assert.AreEqual(2f,    p.bladeWidthMin, 1e-5f);
            Assert.AreEqual(8f,    p.bladeWidthMax, 1e-5f);
            Assert.AreEqual(1f,    p.alpha,         1e-5f);
            Assert.AreEqual(0.1f,  p.hueSpread,     1e-5f);
            Assert.AreEqual(0.3f,  p.saturationSpread, 1e-5f);
            Assert.AreEqual(0.06f, p.lightnessSpread,  1e-5f);
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static void SafeDestroy(Object obj)
        {
            if (obj != null) Object.DestroyImmediate(obj);
        }

        private static string NormalizePath(string path)
        {
            return System.IO.Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }
    }
}
