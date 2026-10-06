# Project Map

## Purpose
Biformis: Unity 6 (6000.3.10f1) 2D top-down action game, Android port of a PC game, with a story mode beside Endless. Full background lives in CLAUDE.md and ANDROID_PORT.md.

## Requirements
- Current task: Spider enemies spawned by an encounter in Level Story 3 should follow only the paths belonging to that encounter's area.

## Components
- `Spawn System/EncounterSpawner.cs`: room trigger; on player enter, runs N waves through its sibling `PositionBasedSpawner`, waits until all spawned are inactive, then fires `onCleared` (opens gate/door).
- `Spawn System/PositionBasedSpawner.cs`: list of (spawn point Transform, enemy prefab) pairs; spawns each via `ObjectPoolManager.SpawnObject(prefab, position, rotation)`.
- `Core/Utilities/ObjectPoolManager.cs`: pooled spawn; new objects are `Instantiate`d at the position (Awake + OnEnable run inside that call), reused ones are moved then `SetActive(true)` (OnEnable runs then). Spawned enemies are parented under the pool holder, not the encounter.
- `Game/Enemy/Spider.cs` (`Spider : BiformisEmitterController`): in `OnEnable` picks `assignedPath` or else a random `PathCreator` from the whole scene, picks a random start distance, teleports there and moves along it in `Update`.
- `PathCreator` (vendored `_Scripts/PathCreator/`): bezier path component.

## Main Flow
Player enters Encounter trigger -> EncounterSpawner.Run -> PositionBasedSpawner.SpawnAllPair -> ObjectPoolManager.SpawnObject -> (Instantiate / SetActive) -> Spider.OnEnable -> InitializePath (scene-wide random path) -> Update moves along path.

## Level Story 3 setup (verified from scene file, includes uncommitted work)
-------------SETUP------------- / Encounters /
- Encounter 1: EncounterSpawner (1 wave, onCleared -> SetActive(false) on a gate), spawns Axion + 2 Turrets. No Paths group.
- Encounter 2: 1 wave, gate on clear, spawn list empty. Child `Encounter 2 Paths` with Path (closed), Path (1), Path (2) (open).
- Encounter Door: 2 waves, onCleared -> Door.OpenDoor, spawn list empty. Child `Encounter Door Paths` with Path (closed), Path (1), Path (2) (open).
- No Spider is in any spawn list yet.

## Data and Trust Boundaries
Save data via DataPersistenceManager (not relevant to this task).

## Build and Deployment
Builds through Unity Editor. PlayMode tests via Unity batchmode (see CLAUDE.md); Story tests filter `CaptainPinkTurd.Story.Tests*`.

## Unknowns
- How a spawned Spider learns which encounter spawned it (not designed yet).
