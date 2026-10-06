# Paths-aware Spider — plan

Branch: `Gameplay_Polish` (no worktree). Tests: Story PlayMode suite through Unity MCP.

## Goal
A Spider spawned by an encounter (`EncounterSpawner`) walks a random path from that encounter's own Paths group and starts at the point of it nearest its spawn point. Hand-placed and Endless Spiders behave as before (Inspector `assignedPath`, else a random path in the scene).

## Decisions
- The encounter hands the path over; the group is an Inspector field on `EncounterSpawner`.
- The Spider picks its path on its first `Update` (Instantiate / SetActive run `OnEnable` before the spawner gets the object back).
- `PositionBasedSpawner` fires a cancellable pre-spawn event (`OnSpawning`, `SpawnRequest { Prefab, Cancel }`) and an after-spawn event (`OnObjectSpawned`). `EncounterSpawner` subscribes in `OnEnable`, unsubscribes in `OnDisable`.
- No Paths group: the Spider is skipped with no wait and a warning; the other enemies still spawn. Handlers only ever set `Cancel = true`.
- The encounter-given path is cleared when the Spider is disabled (returned to the pool). It lives in its own field, so the Inspector `assignedPath` is kept.

## Steps
1. `Game/Enemy/Spider.cs`: `encounterPath` field, `AssignPath(PathCreator)`, `CurrentPath`; `OnEnable` only hides/resets; path picked on first `Update` (encounter path, then `assignedPath`, then random); start distance nearest the spawn point for an encounter path; `OnDisable` null-safe unsubscribe and clears `encounterPath`.
2. `Spawn System/PositionBasedSpawner.cs`: `SpawnRequest`, `OnSpawning`, `OnObjectSpawned`; skip cancelled pairs without waiting.
3. `Spawn System/EncounterSpawner.cs`: `pathsGroup` field, collects its `PathCreator`s in `Awake`; veto Spiders when it has none; assign a random path to each spawned Spider. `SpawnSystem.asmdef` gains a reference to `PathCreator`.
4. `Story System/Tests/EncounterSpiderPathTests.cs` (+ `PathCreator` reference in the tests asmdef): a Spider spawned by an encounter walks one of its group's paths, near its spawn point; an encounter without a group skips the Spider and still spawns the other enemy.
5. Level Story 3: drag `Encounter 2 Paths` and `Encounter Door Paths` into their encounters' `pathsGroup`. Spawn lists stay as they are (level design).

Steps 1–3 depend on each other; 4 depends on 1–3; 5 depends on 3. No split into waves.

## Verification
- Story PlayMode suite (`CaptainPinkTurd.Story.Tests*`) through Unity MCP, more than 0 tests executed.
- The user plays Level Story 3 with Spiders in Encounter 2 / Encounter Door spawn lists.
