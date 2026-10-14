# Biformis app icon

The launcher portrait joins the viewer-left half of the banner's mint green character with the viewer-right half of its red character. The seam aligns the face and upper torso. The original banner lettering is excluded so the portrait stays readable at launcher sizes.

Assets:

- `Assets/Branding/Biformis.png`: square full-color icon used for every Android legacy and round size.
- `Assets/Branding/Biformis-Adaptive-Background.png`: opaque navy background, RGB 22, 33, 49.
- `Assets/Branding/Biformis-Adaptive-Foreground.png`: transparent portrait with padding for adaptive masks.

The Android adaptive slots use background first, foreground second. Textures import without mipmaps or lossy compression and use Point filtering. The global product name and build-profile display labels are Biformis. The existing Android application identifier is preserved.

## Verification

- Unity 6000.3.10f1 built `Builds/Biformis.apk` successfully with zero errors.
- Android SDK `aapt dump badging` confirms both the default and Vietnamese application labels are `Biformis`.
- The packaged adaptive icon references the correct background and foreground resources; generated Android PNGs match the source assets.
- Android SDK `apksigner verify` passes. All 18 Android icon slots resolve to valid texture GUIDs.
- A central circular mask preserves the full head and face; only shoulders and chest are trimmed.

## Image generation

Created with the built-in `image_gen` tool. The two generated PNGs are 1254 x 1254; the solid background is 1024 x 1024. Generated originals remain in Codex's generated-images directory; the final assets above are stored in this project.

### Composite prompt

```text
Use case: compositing
Asset type: square Android game app icon for Biformis, 1024 x 1024 pixels.
Primary request: From the attached BIFORMIS banner, take the LEFT HALF (as seen by the viewer) of the mint green / teal character at the right edge of the banner, and the RIGHT HALF (as seen by the viewer) of the red character at the left edge. Join these two halves flush along one vertical center seam into a single centered character bust. The final icon must have GREEN ON THE VIEWER'S LEFT and RED ON THE VIEWER'S RIGHT.
Input images: Image 1 is the original pixel art banner. Use the actual character halves as the source, preserve their recognizable silhouette, hair, stern eyes, face mask, scarf and upper torso and their original colors. Align forehead, eyes, chin and shoulders at the seam to form one coherent face and body.
Scene/backdrop: flat solid very dark navy background matching the banner, extending all the way to the square canvas edges.
Style/medium: authentic sharp retro pixel art matching the source, visible square pixel grid and hard edges. No smoothing or painterly treatment.
Composition/framing: close portrait from top of hair to upper chest; fit the whole recognizable head within the central safe area with around 18 percent clear space on all sides for Android launcher masking. Straight-on head, centered seam. Preserve source design; only join and align the two halves. Strong small-size readability.
Text: none; remove the banner lettering and background decorations entirely.
Constraints: exactly one composite character, mint green LEFT, coral red RIGHT, symmetrical center alignment, no words, no lettering, no border, no pre-rounded corners, no gradients, no extra symbols, no watermark.
```

### Adaptive foreground prompt

```text
Use case: background-extraction
Asset type: Android adaptive app icon FOREGROUND layer, square canvas.
Primary request: Preserve this EXACT single split green-left red-right pixel-art character design, face, eyes, hair, mask, scarf and chest. Remove the dark navy background completely, replacing it with true transparent alpha. Scale down and center the whole portrait on a square transparent canvas so all visible content is within the central 60 percent of the canvas width and height (20 percent fully transparent padding on each side). Include a clean contained upper chest so no body reaches the canvas edge. Do not redraw the design. Preserve crisp square pixel-art edges and the original red and mint palette. Keep the exact vertical center seam. No shadow, no glow, no text, no border, no new elements. Return genuinely transparent background.
```
