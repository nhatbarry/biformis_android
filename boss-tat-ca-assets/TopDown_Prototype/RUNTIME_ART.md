# Boss top-down body and knife

Superseded for runtime by the user-supplied `boss-phase2-topdown-dao-x2.zip`. This file describes the earlier generated prototype only.

Generated with the built-in image_gen tool on 2026-10-08 using the user-approved four-direction draft as reference. `Boss Body and Knife.png` is the selected source. Source pixels are preserved. Unity imports five full rectangles at 132 PPU with Point filtering, no mipmaps and uncompressed textures. The material clips low-alpha generation fringes at 0.94; it clips texture alpha before renderer tint so fading dash afterimages still work.

`BossDirectionalArt` replaces the visible red-haired combat body with south/north/west/east views and poses a separate knife using the existing skill frame cues. Movement bob, anticipation, slash swing, vertical raise and dash lean are procedural. The original Aseprite timelines and ice transformation remain authored sources; this is not a newly hand-drawn directional animation pack.

## Generation prompt

```text
Edit the provided four-direction boss concept into a runtime-ready separated body-and-weapon asset sheet. Keep the exact red hair, pale mint skin, charcoal coat, beige buttons, chunky pixel style and overhead perspective.
Transparent canvas. Exactly FIVE isolated items. Four complete boss bodies in a 2x2 arrangement in the LEFT TWO THIRDS of the canvas, and ONE standalone knife in the RIGHT THIRD, vertically pointing straight UP with hilt at bottom. Top left boss faces south/front, top right faces north/back with NO FACE, bottom left faces west/left, bottom right faces east/right. Equal character scale and consistent feet baseline.
Change only separation of weapon: remove ALL knives from the four bodies, keep visible relaxed pale mint hands by sides. The standalone knife matches the reference knife's ivory blade and dark-grey grip, no hand attached; simple long straight broad knife blade, no fancy guard. This allows the game to animate the weapon separately.
No extra poses, NO embedded weapon in any boss. Generous transparent empty gaps between all five shapes. No shadow, backdrop, labels, cell dividers, text, checkerboard, black outside borders, gradients or anti-aliasing. Real alpha transparency. Preserve strong simple hard pixel clusters.
```

## Cleanup prompt

```text
Remove every glow, halo, aura, soft shadow and background haze from this sprite asset sheet. Keep all five exact shapes, positions, poses, character design and knife unchanged. Outside the hard-edged opaque pixel-art silhouettes, ALL pixels must be fully transparent alpha 0. Only opaque hard square pixel blocks inside the sprites; no partially transparent red clouds, pale halos, soft edges or antialiasing. Remove black contour pixels around the standalone ivory knife, retain its dark handle. This is a game sprite atlas, not a presentation illustration. Do not add anything. Transparent background.
```

