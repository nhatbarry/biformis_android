# Red Final cutscene

`Story Ending` now opens with the `Cutscene_Red_Final` montage, immediately after
the brothers enter the door of light in the `Ending` ink knot. A white fade leads
from Red's thanks into the hospital waking scene; the final heart-monitor/beep
sequence has been removed. Story steps and build scenes stay the same.

`RedFinalCutscene` reads the committed `Resources/Story Ending/Red Final/timing_positions.json`.
It preserves the supplied 26.12-second timeline, world foot positions, closeup
canvases, per-frame durations and the independent Green/Bunny loops. Red turns
left and right once, then loops only frames 6–11. The 320×180 composition fits
inside the display, with a full-screen black backing for other aspect ratios.

Every spoken line, including the hospital epilogue, is stored in both story Ink
files. The ending instantiates the shared `Story Dialogue System` prefab and
calls `DialogueManager.EnterDialogue` with eleven independent knots:

- `RedFinal_Blue`, `RedFinal_Mom`, `RedFinal_Doctor`, `RedFinal_Zoro` and
  `RedFinal_Teen`: `chúc mừng` during each
  `clap_talk` shot, with `Congratulations` in English.
- `RedFinal_Usagi`: `Unaa~`. In the shared Zoro/Usagi closeup, Zoro speaks for
  the first 1.3 seconds, then Usagi for the next 1.3 seconds.
- `RedFinal_Reaper`: `grrr`.
- `RedFinal_Pink`: `prrfffff`, displayed as **Captain Pink Turd**.
- `RedFinal_Thanks`: `cảm ơn mọi người` / `Thank you, everyone`, from 24.12 seconds,
  when the front-facing talking frames begin.
- `HospitalEnding_A`: `Anh...?` / `Brother...?`.
- `HospitalEnding_B`: `Ừ. Anh về rồi.` / `Yeah. I'm home.`.

Speaker display names come from `Resources/Localization/Strings.txt`: Green is
**Zoro**, Bunny is **Usagi**, and Pink is **Captain Pink Turd**. All dialogue uses
the same panel, font, spacing, colours and continue controls as the rest of the
story; no ending-specific caption boxes remain. The nine `RedFinal_*` knots
explicitly use `#layout:top` to place the final applause montage's dialogue at
the top edge, including Red's thanks. All other story dialogue, including the
drowning memory and hospital epilogue, stays at the bottom. The montage retains
its automatic timeline, and stage animation continues while the shared dialogue
manager types a line.

At the end of the montage, the last Red frame stays visible beneath a 1.6-second
fade to white. The hospital room and A at the bedside become active only once
the screen is fully white; the white overlay then fades out over 1.4 seconds.
There is no intervening black screen or final beep text. The hospital's waking
animation, shared dialogue controls, ending music and THE END remain.

Only the 20 required PNGs and timing JSON are copied into Resources; source
PNG/Aseprite files remain in `Cutscene_Red_Final`. Textures use Point filtering,
full canvases, no mipmaps or compression, including Android RGBA32 overrides.
The menu **Biformis > Ending > Import Red Final Cutscene Assets** restores these
import settings. No setup command is required to play the committed integration.

Play from Core and select the `Ending` step to include the door, or `Story Ending`
to preview the montage directly. `RedFinalCutsceneTests`, the door-of-light touch
test and the complete story flow test cover the new scene and its surrounding flow.
