// GrassGenWindow.cs
// Part of GrassGen for Unity – a Unity 2020.3.9 editor port of the browser-based
// Grass texture generator (https://github.com/jmdejong/grassgen) by ~troido.
// Original work licensed under GPL v3. This adaptation is also licensed under GPL v3.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GrassGen.Editor
{
    /// <summary>
    /// IMGUI EditorWindow for the Grass texture generator.
    /// Open via <b>Tools → Grass Generator</b>.
    /// </summary>
    public class GrassGenWindow : EditorWindow
    {
        // ------------------------------------------------------------------ //
        //  State                                                               //
        // ------------------------------------------------------------------ //

        [SerializeField] private GrassGenParams _params = new GrassGenParams();
        private Texture2D _preview;
        private Vector2   _scrollPos;
        private string    _lastSaveDir;

        // ------------------------------------------------------------------ //
        //  Menu item                                                           //
        // ------------------------------------------------------------------ //

        /// <summary>Opens (or focuses) the Grass Generator window.</summary>
        [MenuItem("Tools/Grass Generator")]
        public static void OpenWindow()
        {
            var win = GetWindow<GrassGenWindow>(false, "Grass Generator", true);
            win.minSize = new Vector2(380f, 520f);
            win.Show();
        }

        // ------------------------------------------------------------------ //
        //  Unity messages                                                      //
        // ------------------------------------------------------------------ //

        private void OnEnable()
        {
            _lastSaveDir = Application.dataPath;
        }

        private void OnDestroy()
        {
            DestroyPreview();
        }

        // ------------------------------------------------------------------ //
        //  GUI                                                                 //
        // ------------------------------------------------------------------ //

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // ---- Basic -------------------------------------------------------
            EditorGUILayout.LabelField("Basic", EditorStyles.boldLabel);

            _params.seed       = EditorGUILayout.IntField(
                new GUIContent("Seed", "Integer seed for the RNG. Same seed + same params = same texture."),
                _params.seed);
            _params.width      = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Width", "Output texture width in pixels."), _params.width));
            _params.height     = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Height", "Output texture height in pixels."), _params.height));
            _params.bladeCount = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Blade Count", "Number of grass blades drawn onto the texture."), _params.bladeCount));

            EditorGUILayout.Space(6f);

            // ---- Blade Shape -------------------------------------------------
            EditorGUILayout.LabelField("Blade Shape", EditorStyles.boldLabel);

            _params.segments = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Segments", "Bezier curve segments per blade half. Higher = smoother curve."),
                _params.segments));
            _params.spread = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Spread (px)", "Maximum horizontal offset of blade tip from its base, in pixels."),
                _params.spread));
            _params.clearEdge = EditorGUILayout.Slider(
                new GUIContent("Clear Edge", "Fraction of texture width kept clear of blade bases on each side."),
                _params.clearEdge, 0f, 0.5f);

            EditorGUILayout.BeginHorizontal();
            _params.bladeWidthMin = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Blade Width Min", "Minimum blade base half-width in pixels."),
                _params.bladeWidthMin));
            _params.bladeWidthMax = Mathf.Max(_params.bladeWidthMin, EditorGUILayout.FloatField(
                new GUIContent("Max", "Maximum blade base half-width in pixels."),
                _params.bladeWidthMax));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);

            // ---- Colour ------------------------------------------------------
            EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);

            _params.baseColor = EditorGUILayout.ColorField(
                new GUIContent("Base Colour", "Mean grass colour. Each blade is randomly perturbed in HSL space around this value."),
                _params.baseColor);
            _params.hueSpread = EditorGUILayout.Slider(
                new GUIContent("Hue Spread", "Per-blade hue variation (fraction of full colour wheel, 0–1)."),
                _params.hueSpread, 0f, 1f);
            _params.saturationSpread = EditorGUILayout.Slider(
                new GUIContent("Saturation Spread", "Per-blade saturation variation (0–1)."),
                _params.saturationSpread, 0f, 1f);
            _params.lightnessSpread = EditorGUILayout.Slider(
                new GUIContent("Lightness Spread", "Per-blade lightness variation (0–1)."),
                _params.lightnessSpread, 0f, 1f);
            _params.alpha = EditorGUILayout.Slider(
                new GUIContent("Alpha", "Opacity of every blade pixel (0 = transparent, 1 = opaque)."),
                _params.alpha, 0f, 1f);

            EditorGUILayout.Space(8f);

            // ---- Action buttons ----------------------------------------------
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset"))
            {
                _params.Reset();
                DestroyPreview();
                GUI.FocusControl(null);
            }
            if (GUILayout.Button("Generate"))
            {
                DoGenerate();
            }
            EditorGUILayout.EndHorizontal();

            // ---- Preview + Save button ---------------------------------------
            if (_preview != null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(
                    string.Format("Preview  ({0} × {1})", _preview.width, _preview.height),
                    EditorStyles.boldLabel);

                float maxW = EditorGUIUtility.currentViewWidth - 24f;
                float aspect = (float)_preview.width / _preview.height;
                float previewW = Mathf.Min(maxW, 512f);
                float previewH = previewW / aspect;
                // Limit height so the window doesn't get overwhelmingly tall.
                if (previewH > 300f)
                {
                    previewH = 300f;
                    previewW = previewH * aspect;
                }

                Rect previewRect = GUILayoutUtility.GetRect(
                    previewW, previewH,
                    GUILayout.Width(previewW), GUILayout.Height(previewH));

                // DrawPreviewTexture shows a checkerboard behind transparent pixels.
                EditorGUI.DrawPreviewTexture(previewRect, _preview, null, ScaleMode.ScaleToFit);

                EditorGUILayout.Space(4f);
                if (GUILayout.Button("Save PNG…"))
                {
                    DoSave();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ //
        //  Generation                                                          //
        // ------------------------------------------------------------------ //

        private void DoGenerate()
        {
            DestroyPreview();
            try
            {
                _preview = GrassGenerator.Generate(_params);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GrassGen] Generation failed: " + ex);
                EditorUtility.DisplayDialog("Grass Generator – Error",
                    "Failed to generate texture:\n" + ex.Message, "OK");
            }
            Repaint();
        }

        // ------------------------------------------------------------------ //
        //  Save                                                                //
        // ------------------------------------------------------------------ //

        private void DoSave()
        {
            if (_preview == null) return;

            string defaultName = "grass_" + _params.seed + ".png";
            string savePath = EditorUtility.SaveFilePanel(
                "Save Grass Texture as PNG",
                _lastSaveDir,
                defaultName,
                "png");

            if (string.IsNullOrEmpty(savePath)) return;

            // Validate: the file must be inside the Assets folder.
            string normalizedAssets = NormalizePath(Application.dataPath);
            string normalizedSave   = NormalizePath(savePath);

            if (!normalizedSave.StartsWith(normalizedAssets, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Grass Generator – Invalid Path",
                    "The save path must be inside the project's Assets folder:\n\n"
                    + Application.dataPath
                    + "\n\nChosen path:\n" + savePath,
                    "OK");
                return;
            }

            _lastSaveDir = Path.GetDirectoryName(savePath);

            // Encode and write.
            byte[] pngData = _preview.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                EditorUtility.DisplayDialog("Grass Generator – Error",
                    "Failed to encode the texture as PNG.", "OK");
                return;
            }

            try
            {
                File.WriteAllBytes(savePath, pngData);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Grass Generator – Write Error",
                    "Could not write file:\n" + ex.Message, "OK");
                return;
            }

            // Import the asset and configure its TextureImporter.
            // Convert absolute path → "Assets/…" relative path for AssetDatabase.
            string relPath = "Assets" + savePath.Substring(Application.dataPath.Length)
                                                 .Replace('\\', '/');
            AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType        = TextureImporterType.Default;
                importer.alphaIsTransparency = true;   // enable "Alpha Is Transparency"
                importer.mipmapEnabled      = false;
                importer.isReadable         = false;
                importer.wrapMode           = TextureWrapMode.Repeat;
                importer.filterMode         = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh();
            Debug.Log("[GrassGen] Saved and imported: " + relPath);

            // Ping the new asset in the Project window.
            EditorUtility.FocusProjectWindow();
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(relPath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        /// <summary>Destroy the current preview texture to avoid memory leaks.</summary>
        private void DestroyPreview()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview);
                _preview = null;
            }
        }

        private static string NormalizePath(string path)
        {
            // Resolve symlinks, trailing slashes, etc., then ensure separator is '/'.
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }
    }
}
