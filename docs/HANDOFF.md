# Handoff: changes since `58fe3658` ("port android game", 2026-09-03)

Range: `58fe3658..946f150d`, 5 commits (2026-09-11 to 2026-10-01), mostly by nhatbarry. Code alone is about +5.6k / −0.8k lines across ~98 files. The total diff is ~455k lines, but most of that is scenes, fonts and `.aseprite` files.

| Commit | Author | What it did |
|---|---|---|
| `99937971` latest | nhatbarry | Added `CLAUDE.md` |
| `992e2040` dialogue system update | CaptainPinkTurd | Ink dialogue rework, new `InputManager`, core utility refactors, removed old RPG dialogue system. **Also deleted `CameraFraming.cs`** (see Risks) |
| `7e3b7975` dev level game, more enemies, dialogue | nhatbarry | The bulk of the work: story mode, 6 story levels, Luneblade enemies, localization, Story System + tests |
| `9555faad` done after lv2 | nhatbarry | Cutscene staging (actors, portraits, speaker styles), Vietnamese font, level select, more tests |
| `946f150d` add cutscene | nhatbarry | First white-room cutscene art + ink, `MC_WakeUp_Floor` animation |

---

## 1. The big picture

At `58fe3658` the game was **Endless mode + the Android touch port**. Now it also has a **story mode** in Vietnamese and English: cutscenes alternate with hand-built levels, and the story ends in a scripted ending. Endless was left untouched. The mobile HUD and the port's design are as before (`ANDROID_PORT.md`), except for the camera regression below.

To play: open `Assets/Scenes/CaptainPinkTurd/Bootstrap Scenes/Core.unity` → Play → main menu → **Cốt truyện / Story**. You can also open any `Level Story …` scene and press Play: an editor hook (`Story System/Editor/PlayOpenedLevelThroughCore.cs`) boots through Core and jumps to that scene. A dev **Level select** button in the main menu starts at any step (turn it off with `StoryMenu.showLevelSelect` for release).

## 2. Story mode flow

```
StoryMenu (main menu) ─► StoryFlow (static) ─► loads StoryData.steps[i]
                                │                 ├─ Cutscene step → "Story Cutscene" scene → CutsceneDirector plays ink knot
                                │                 └─ Level step    → "Level Story N" scene → LevelManager(isStoryLevel)
                                └─ saves GameData.storyStep after EVERY move
```

- **Running order:** `Assets/Game Data/Story/Story Data.asset`
  `Intro` → Level 1 → Level 2 → `Level2_End` → Level 3 → `AfterLevel3` → Level 4 → `Level4_End` → Corridor → Level 5 → `Story Ending`.
  The other knots (`WhiteRoom_1/2/3`, `Hospital_1/2`, `Past`) are reached through ink diverts inside those cutscenes.
- **`StoryFlow`** (`Scene Architecture System/Story/`): `StartNew / StartAt / Continue / Advance / ReloadCurrent / ReturnToMenu`. It saves right away on every move because `DataPersistenceManager` reloads the save file on each scene load. Without that, a step change gets reverted by the very transition it triggers.
- **`LevelManager`** has a new *Story Mode* section. `isStoryLevel` makes the exit advance the story instead of picking a random level, and makes death restart the current step.
- **Save data:** `GameData` gained `storyStep` (−1 = none) and `storyCompleted`.

## 3. New systems and where they live

**Story System** (`Assets/_Scripts/CaptainPinkTurd/Story System/`, its own asmdef):

| Area | Files | Role |
|---|---|---|
| Menu | `StoryMenu` | Continue / New story, language toggle, runtime-built level select |
| Cutscenes | `CutsceneDirector`, `CutsceneStage`, `StageActor`, `StageActorAnimation`, `DialogueSpeakerStyle` | One shared cutscene scene. Ink tags drive the stage: `#speaker #bg #cast #fx #sfx #anim #move #wait`. Actors are imported straight from `.aseprite` files in `Assets/Animations/Story/` |
| Fights | `EncounterSpawner` | Each room is a trigger that spawns N waves of M enemies, then opens the room's gate or the level's door. No hand-placed enemies in story levels |
| Level FX | `LevelCountdown`, `Presentation/*` (`LevelTitleCard`, `StoryScreenFx`, `LightPulse`, `OverlayCanvas`, `SpriteFrameAnimator`) | Title cards, the Level 5 timer, fade, faint and collapse effects, alarm lights |
| Ending | `Presentation/EndingSequence` | The whole ending is built in code |
| Barks | `Barks/*` | Non-blocking floating lines read from ink with their own `Story` instance (player hurt lines, trigger zones) |
| Tests | `Tests/` (33 PlayMode tests) | Story flow, cutscene actors, camera-in-view, collision of every wall, path-finding through each level, ink knot/tag validity in both languages, a full main-menu-to-ending run |

**Gameplay additions** (`Game/`):
- `LunebladeEnemy : BiformisEmitterController` with three prefabs in `Prefabs/Enemies/Luneblade/`: **Reaper** (chaser, slashes), **Axion** (bruiser, fires its colour emitter on the smash frame), **Riven** (blinker, vanishes and lunges). They're animated from sprite frames, and they're also in Endless through `Enemy Loot Drop Profile`.
- `DimensionLock` with `GameManager.LockDimension/UnlockDimension`. Level 4 forces Blue (A).

**Dialogue** (`Ink Dialogue System/`, reworked in `992e2040` and later):
- `DialogueManager` has staging hooks (`OnStagePause`, `IsStaging`, `#wait`). `DialoguePanelUI` has portrait and speaker colours. `DialogueTapToContinue` lets a tap anywhere advance the dialogue on mobile.
- Triggers moved to `Dialogue Trigger/` (`DialogueTriggerBase`, `Auto`, `Manual`, `Interactable2D`).
- The **old RPG System dialogue** (`DialogueBox`, `DialogueData`, `DialogueInteractable` + its data assets) was **deleted**, and so was `InkExternalFunctions`.
- `Prefabs/Story/Story Dialogue System.prefab` is the story panel: 608×108, a 168×168 portrait frame, and per-speaker typewriter profiles in `Game Data/Story/Typewriter/`.

**Localization** (`Core/Localization/`): `Localization` + `LocalizedText` read `Assets/Resources/Localization/Strings.txt`, a tab-separated file with `key / vi / en` columns that must stay `.txt`. Each language has its own story ink file: `Biformis_Story.ink` (vi) and `Biformis_Story_EN.ink` (en). **The two files must keep identical knots, variables and tags.** New font `m3x6 VI SDF` (m3x6 plus Vietnamese letters); `VT323 SDF` is the TMP fallback.

**Core / misc** (from `992e2040`):
- `InputManager`: a shared `InputSystemActions` singleton. It's marked TODO for gradual adoption; most systems still create their own instance.
- `ComponentExtensions`, `Ray2DDetector`, `Physics2DSimulationManager`, `GrayscalePostProcessingController` (`GrayscaleEffect` was renamed to `GrayscaleShaderController`).
- `SceneController.IsBusy` is exposed.
- New ink test files: `PensDown_Dialogue`, `Teacher_Dialogue`, `Test 1 Questions`. `Intro.ink` and `End Game.ink` were removed.

**Content:**
- Scenes in `Assets/Scenes/CaptainPinkTurd/Story/`: `Level Story 1–5`, `Level Story Corridor`, `Story Cutscene`, `Story Ending`, all added to Build Settings. Level names must start with `Level` so the mobile HUD shows.
- Cutscene art in `Assets/Animations/Story/`: B in bed, the teen (A), the plague-doctor villain + portrait, B waking on the hospital floor.

## 4. Risks and things to check

1. **`CameraFraming.cs` was deleted in `992e2040`.** The Android port relied on it: it locks world width across aspect ratios, so a 20:9 phone doesn't see more of the level than 16:9. It installed itself at runtime and no scene references it, so nothing errors; the fix is just gone. `StoryCameraTests` still has a comment that assumes it exists. This was most likely lost while merging the dialogue update. **Decide whether to restore it** (`git show 58fe3658:"Assets/_Scripts/CaptainPinkTurd/Core/Rendering/CameraFraming.cs"`). `CLAUDE.md` and `ANDROID_PORT.md` still describe it as present.
2. `992e2040` also added iOS icon entries to `ProjectSettings.asset`, probably a side effect of the same merge. Harmless, but worth a glance.
3. `PopupManager` and `GameManager` still use the `old = Time.timeScale … restore old` pattern that `CLAUDE.md` forbids. Neither file changed in this range; it's an existing issue to check against `ANDROID_PORT.md` §5.
4. Ink: `DialogueManager.ExitDialogue()` resets the story state, so anything that must last across dialogues has to be an ink `VAR`.
5. Tilemaps edited from scripts must regenerate their composite colliders before saving (this once let the player walk through every wall). The `StoryLevelCollisionTests` cover it.
6. The cutscene art had outline and colour fixes made directly in the `.aseprite` files. Re-exporting from an older copy brings back the old look (no outline, red-haired teen).
7. Multi-touch still can't be tested in `-batchmode`; check it by hand on a device.

## 5. Where to read next

- `CLAUDE.md`: the most detailed and current description of story mode, ink tags and level rules.
- `ANDROID_PORT.md` (Vietnamese): the mobile input design and the rules for timescale and float comparisons.
- `Assets/Ink Dialogue/Biformis_Story_EN.ink`: the whole plot in about 160 lines.
- `Story System/Tests/StoryFlowTests.cs`: a readable spec of how the story is expected to behave.
