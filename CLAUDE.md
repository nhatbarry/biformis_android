# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

"Biformis" — a Unity 2D top-down action game (working title `GameNameJamProject`, package `com.DefaultCompany.GameNameJamProject`). This branch (`GameName_Jam`) is the Android port of a PC-first game. Unity **6000.3.10f1**, URP 2D, New Input System only (`activeInputHandler: 1`).

Open `Assets/Scenes/Main` (per `Assets/Read Me.txt`) or the level scenes under `Assets/Scenes/CaptainPinkTurd/` to start exploring in the editor.

## Build / test commands

There is no CLI build step for the game itself — builds happen through the Unity Editor (`File > Build Profiles`). For Android specifically: IL2CPP, ARM64 only, minSdk 25, landscape only, debug keystore (no signing setup needed). First IL2CPP build takes 10–25 minutes.

Read device logs:
```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" logcat -s Unity
```

Run PlayMode tests headlessly (**close the Unity Editor first** — it holds a project lock):
```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe" \
  -batchmode -projectPath "D:/biformis_android" \
  -runTests -testPlatform PlayMode \
  -testFilter "CaptainPinkTurd.MobileControls.Tests*" \
  -testResults "D:/biformis_android/Logs/tests.xml" \
  -logFile "D:/biformis_android/Logs/tests.log"
```
The only test suite in the repo lives in `Assets/_Scripts/CaptainPinkTurd/Mobile Controls System/Tests/` (9 PlayMode tests covering the mobile input rig and timescale bugs below). It's gated by `defineConstraints: UNITY_INCLUDE_TESTS`, so it never ships in the APK. Known limitation: injecting virtual multi-touch events in `-batchmode` doesn't work — multi-touch (e.g. hold sprint + drag joystick) must be verified by hand on a device.

## Architecture

### Assembly layout

Code is split into ~20 asmdefs under `Assets/_Scripts/CaptainPinkTurd/<System Name>/`, one per gameplay system (Core, Input System, Animation System, Effect System, Spawn System, RPG System, UI, Units System, Score System, Scene Architecture System, Mobile Controls System, etc.), plus vendored third-party code under `Assets/_Scripts/PathCreator/`, `Assets/_Scripts/Unity-Bullet-Hell/`, and `Assets/_Scripts/xNode-master/`. Third-party assets are unmodified except where noted in `ANDROID_PORT.md`.

Per `Assets/Read Me.txt`, the codebase intentionally uses **heavy coupling between systems** (system A directly references system B) as a deliberate simplicity choice for a small team/game-jam scope — this is not an oversight, don't "fix" it by adding abstraction layers.

Each system that needs input reads it via its **own** `new InputSystemActions()` instance rather than a shared `PlayerInput` component — this is a foundational architectural fact that shaped how mobile input had to be implemented (see below).

### Mobile controls port (read `ANDROID_PORT.md` before touching input/UI/timescale code)

`ANDROID_PORT.md` (in Vietnamese) is a comprehensive, load-bearing design record for this branch: it documents the port's design decisions, every PC bug this port fixed, mistakes made and reverted, and traps for anyone touching PC code afterward. Read it fully before changing anything touching input, HUD/UI, `Time.timeScale`, camera framing, or sorting layers — it explains *why*, not just *what*.

Key points to internalize:

- **Core principle:** touch emulates a virtual Gamepad via Unity's `OnScreenControl`, injecting into control paths gameplay code already binds to (e.g. `<Gamepad>/leftStick`, `<Gamepad>/leftStickPress`, `<Gamepad>/rightShoulder`, `<Gamepad>/buttonEast`). No gameplay branch exists for mobile vs. PC — touch takes the exact same code path as keyboard input. Since input is per-system (no shared `PlayerInput`), device-level emulation was the only option that didn't require touching every system's action-reading code.
- Control-path-to-action mapping constants live in `Assets/_Scripts/CaptainPinkTurd/Core/Input Paths/MobileControlPaths.cs`. Adding any new keybind on PC requires also wiring a mobile button: bind an unused Gamepad control, add a constant there, add a `BuildButton(...)` call in `MobileControlsHUD.Build()`. `ANDROID_PORT.md` §7 lists exactly which Gamepad controls are free vs. already claimed — check it before reusing one.
- The mobile HUD (`Assets/_Scripts/CaptainPinkTurd/Mobile Controls System/`) is built **entirely at runtime in code** — no prefabs, no imported sprites, no scene edits — specifically to keep scene/build-settings diffs at zero.
- `MobileControlsHUD.IsGameplaySceneLoaded()` gates HUD visibility on scene names starting with `"Level"` — renaming that convention silently breaks HUD visibility.
- Several latent PC bugs were found and fixed while porting (detailed in `ANDROID_PORT.md` §5): a `Time.timeScale` restore bug in `HitStop`/`ShakeUtils` that could freeze the game permanently after a popup set timescale to 0, a `Renderer2D.asset` sorting-layer-texture bug that only manifested on Vulkan/GLES, an animation direction bug from comparing floats exactly against joystick input, and a `Timer` leak from `AnimationControllerBase.PlayAnimation()` not disposing its previous timer. Do not reintroduce any of these patterns (see the two rules below).
- **`Time.timeScale` rule:** never write `float old = Time.timeScale; Time.timeScale = 0; ...; Time.timeScale = old;` — if timescale is already 0 when this runs (e.g. during hit-stop), it freezes the game forever. Always restore via `Time.timeScale > 0f ? Time.timeScale : 1f`, and if restoring from an async callback (DOTween), use `OnKill` not `OnComplete` so it still fires when the tween is cancelled.
- **Float-equality rule:** never compare analog input (joystick) to cardinal vectors with `!=`/`==`; keyboard composites happen to produce exact `(1,0)`-style vectors so this bug hides on PC and only appears with analog input. Use nearest-direction-by-dot-product instead.
- To check whether a prefab is actually placed in a scene, grep the **prefab's GUID**, not the script's GUID — a prefab instance in a scene only stores an override reference to the prefab GUID, not the script GUID (a past investigative mistake, documented in §6).
- Full-screen UI overlays must use anchor stretch `(0,0)-(1,1)` with zero offsets, never a fixed size — a fixed `640x360` size only happens to cover 16:9 and leaves gaps on other aspect ratios.
- `CameraFraming.cs` (`Assets/_Scripts/CaptainPinkTurd/Core/Rendering/`) fixes world-space horizontal width across aspect ratios via `orthographicSize = min(authored, authored * (16/9) / aspect)`, applied directly to `Camera.orthographicSize` in `LateUpdate` with `[DefaultExecutionOrder(10000)]` — it deliberately does not depend on Cinemachine's `LensSettings.Orthographic`, which reports stale values on scene load.
- If sorting layers are added/removed/reordered, `m_CameraSortingLayersTextureBound` in `Renderer2D.asset` must be updated to match (it currently assumes `CameraSortingLayer` is index 9, so the bound covers layers 0–8).
