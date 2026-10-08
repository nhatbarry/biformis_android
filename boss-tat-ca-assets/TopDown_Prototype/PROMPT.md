# Boss top-down draft v1

Created 2026-10-08 with the built-in image_gen tool. Selected output: `boss-redhair-top-down-v1.png`.

This is a four-direction visual concept, not a game-ready animation sheet. Layout: south/front at top left, north/back at top right, west at bottom left, east at bottom right. The verified 1374x1145 RGBA PNG has real alpha transparency. It is saved outside Assets so the approved combat animation pack is still available while the user reviews the new direction. Frames need consistent pixel grids, anchors, silhouette cleanup and complete attack animations before runtime replacement.

References: `.utmp/phase-two-reference/01_VungDaoNgang-0.png` for character identity; `Logs/art-diagnostics/corridor-moving-Blue.png` for game camera and style.

## Exact selected generation prompt

```text
Use case: stylized-concept. Asset type: four-direction concept sprite sheet for the existing Unity top-down game.
Use image 1 ONLY for the boss identity: red shaggy hair, pale mint face/hands, charcoal coat with two beige buttons and one pale knife. Use image 2 for the overhead game camera.
Generate exactly FOUR full-body sprites on real transparent background in a clean 2x2 arrangement, generous margins, same scale. NO walk frames. All four use neutral idle, knife held low.
TOP LEFT: faces SOUTH/down, viewed from above; pale mint face is visible only on the BOTTOM rim of the head.
TOP RIGHT: faces NORTH/up, viewed from behind and above; visible crown and back of red hair, back of coat, NO eyes or face.
BOTTOM LEFT: faces WEST/left; face on LEFT rim of head; shoulders and upper surfaces visible from above.
BOTTOM RIGHT: faces EAST/right; face on RIGHT rim of head; shoulders and upper surfaces visible from above.
Camera angle 65 degrees ABOVE the floor, strongly foreshortened. Crown dominates the silhouette; short body and feet tuck beneath the broad shoulders. All sprites grounded on a horizontal floor plane rather than upright side-scrolling proportions.
Simple authentic crisp pixel art at roughly 40x40 logical pixels per character, enlarged in uniform square blocks. Use bright red and muted red for hair, pale mint face, mid charcoal and slightly darker charcoal clothing, warm beige coat buttons/knife. No pure black pixels: outside silhouette must meet transparency directly with NO black or dark outline or jagged border. No tiny dithering, realistic textures, gradients or antialiasing.
Retain the existing simple visual identity, do not add clothing, mask or hood. Consistent knife hand anatomically. No cast shadow, floor, labels, text, grid lines, checkerboard pattern or background. Only four isolated sprites with alpha transparency. This is a draft art direction preview.
```

