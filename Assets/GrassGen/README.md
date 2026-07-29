# GrassGen for Unity

A **Unity 2020.3.9** editor tool that generates grass texture atlases using
quadratic Bézier curves.  This is a direct C# port of the browser-based
[Grass texture generator](https://github.com/jmdejong/grassgen) by ~troido,
preserving the algorithm and default parameters exactly.

> **Licence:** GPL v3 – see [`LICENCE.txt`](../../LICENCE.txt) in the repository
> root.  The original JavaScript (`main.js`, `hsv.js`, `index.html`) is unchanged
> and remains under the same licence.

---

## Directory structure

```
Assets/GrassGen/
├── Editor/
│   ├── GrassGen.Editor.asmdef      ← editor-only assembly (auto-referenced)
│   ├── GrassGenParams.cs           ← serialisable parameter class
│   ├── GrassGenerator.cs           ← core generation algorithm
│   └── GrassGenWindow.cs           ← IMGUI EditorWindow
├── Tests/
│   └── Editor/
│       ├── GrassGen.Editor.Tests.asmdef
│       └── GrassGeneratorTests.cs  ← NUnit edit-mode tests
└── README.md                       ← this file
```

All code is in an **Editor** folder (and an `[Editor]`-platform assembly
definition), so it is never compiled into a player build.

---

## Installation

1. Copy the entire `Assets/GrassGen` directory into your Unity 2020.3.9
   project's `Assets` folder.
2. Unity will automatically compile the scripts.  No additional packages are
   required.

---

## Opening the window

**Tools → Grass Generator**

---

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| **Seed** | 12 | Integer RNG seed.  Identical seed + identical params → identical output. |
| **Width** | 512 | Output texture width (pixels). |
| **Height** | 256 | Output texture height (pixels). |
| **Blade Count** | 200 | Number of grass blades drawn. |
| **Segments** | 6 | Bézier curve segments per blade half – higher = smoother. |
| **Spread (px)** | 80 | Maximum horizontal offset of a blade's tip/control point from its base. |
| **Clear Edge** | 0.1 | Fraction of texture width kept clear on each side (blade bases sampled from `[clearEdge, 1−clearEdge] × width`). |
| **Blade Width Min/Max** | 2 / 8 | Base half-width range (pixels). |
| **Base Colour** | #11dd11 | Mean grass colour.  Each blade is perturbed independently in HSL space. |
| **Hue Spread** | 0.1 | Per-blade hue variation (fraction of the colour wheel, 0–1). |
| **Saturation Spread** | 0.3 | Per-blade saturation variation (0–1). |
| **Lightness Spread** | 0.06 | Per-blade lightness variation (0–1). |
| **Alpha** | 1.0 | Opacity of every blade pixel. |

---

## Workflow

1. Adjust parameters in the panel.
2. Click **Generate** to create the texture (shown in the preview area).
3. Click **Reset** at any time to restore original defaults.
4. Click **Save PNG…** to write the texture as a PNG file inside the project's
   `Assets` folder.  Paths outside `Assets` are rejected with an error dialog.
5. After saving, the file is automatically imported as a `Texture2D` asset with
   the following importer settings:
   - **Alpha Is Transparency** = ✔  (important for grass overlay sprites)
   - Mipmaps disabled
   - Wrap mode: Repeat
   - Filter mode: Bilinear

---

## Algorithm correspondence with the original JavaScript

| C# | JavaScript (`main.js` / `hsv.js`) |
|----|-----------------------------------|
| `Rng` struct | `Rng(seed)` closure (murmurhash3 finalizer) |
| `SampleRange(min, max, t)` | `sample_range(min, max, t)` / `Range.sample(t)` |
| `RgbToHsl()` | `rgbToHsl()` from `hsv.js` |
| `HslToColor()` | `hslToRgb()` from `hsv.js` |
| `ModifyHslColor()` | `HslColor.modify(hdiff, sdiff, ldiff)` |
| `QuadBezierPos/W()` | `quadBezier()` + `lerp()` |
| `FillPolygon()` | HTML Canvas `ctx.fill()` (scanline rasteriser) |
| `Generate()` | `redraw()` |

### Coordinate system note

The HTML Canvas has `y = 0` at the **top**.  The original code draws blade
vertices at `(x, height − y)`, effectively flipping the canvas so grass grows
upward from the bottom.

Unity's `Texture2D.SetPixel` has `y = 0` at the **bottom**, which exactly
cancels the flip: blade bases are placed at `y = 0` (bottom of the texture)
and tips grow upward, matching the original visual output.

---

## Running the tests

1. Open **Window → General → Test Runner**.
2. Select the **Edit Mode** tab.
3. Expand `GrassGen.Editor.Tests` and click **Run All** (or run individual
   tests).

The test suite covers:
- **Determinism** – same seed always produces the same pixels.
- **Dimensions** – generated texture has exactly the requested width/height.
- **Alpha** – transparent background when `bladeCount = 0` or `alpha = 0`.
- **Non-trivial output** – at least one non-transparent pixel when blades exist.
- **Extreme parameters** – 1×1 textures, zero spread, min/max seeds, etc.
- **Null guard** – `Generate(null)` throws `ArgumentNullException`.
- **HSL round-trip** – `RgbToHsl → HslToColor` preserves colour within 1/10 000.
- **Path validation** – save-path check logic (no file I/O).
- **`GrassGenParams.Reset`** – verifies all fields are restored to defaults.

### Manual verification steps

If Test Runner is unavailable:

1. Open the window via **Tools → Grass Generator**.
2. Click **Generate** with default settings – a green grass texture should
   appear in the preview area with a transparent (checkerboard) background.
3. Change **Seed** and click **Generate** again – the result should differ.
4. Restore the original seed and click **Generate** – the result should be
   identical to step 2.
5. Set **Alpha** to 0 and **Generate** – the preview should be fully
   transparent.
6. Click **Save PNG…**, choose a path inside `Assets/`, confirm the file
   appears in the Project window and has *Alpha Is Transparency* enabled.
7. Try saving to a path outside `Assets/` – an error dialog should appear.

---

## Unity 2020.3.9 compatibility notes

- Uses **IMGUI** (`EditorWindow`, `EditorGUILayout`, `GUI`) exclusively – no
  UI Toolkit.
- Uses `TextureFormat.RGBA32` (not `GraphicsFormat`).
- Uses `AssetDatabase`, `TextureImporter`, `EditorUtility.SaveFilePanel` –
  all available since Unity 5.
- The assembly definition uses the JSON format introduced in Unity 2017.3.
- No packages beyond those built into Unity are required.

---

## Credits

- Original browser tool: **~troido** –
  <https://github.com/jmdejong/grassgen>
- Unity C# port: adapted from the original under the terms of GPL v3.
