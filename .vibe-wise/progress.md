# Learning Progress

## Path-aware Spider (implemented, verification in progress)
- Learner decisions: EncounterSpawner hands over the path; Spider picks its path on first Update (option A, chosen from Claude's options at learner's request); EncounterSpawner holds an Inspector-dragged Paths group field; PositionBasedSpawner fires an after-spawn event that EncounterSpawner subscribes to in OnEnable; skip check via a cancellable pre-spawn event (option 3, switched from option 2 after learning multicast Func drops earlier return values); skip Spider with no wait, other enemies still spawn when no group; clear assigned path when pooled; random path from group, start near spawn point; hand-placed/Endless Spiders unchanged.
- Approved additions: separate encounterPath field, warning on skip, MCP scene wiring (learner chose to save their unsaved scene edits too), two PlayMode tests, SpawnRequest as class. Plan: docs/biformis_android-PathAwareSpider-plan.md.

## Unity lifecycle and spawn timing
- Introduced (Claude explained): Instantiate/SetActive run Awake+OnEnable synchronously before returning; Start is deferred and runs once per object lifetime; coroutine waits mean EncounterSpawner only resumes after a whole wave.
- Demonstrated: learner proposed an after-spawn event fired from PositionBasedSpawner to reach each object in its spawn frame.
- Needs reinforcement: initially expected assignment before OnEnable without a mechanism; needed options to resolve.

## C# delegates vs events
- Introduced (Claude explained): a delegate property holds one method and `=` replaces it; an event's `+=` adds subscribers; `event` is a modifier on a delegate field; a multicast Func returns only the last subscriber's value.
- Demonstrated: learner asked why Func can't be an event (connecting the two concepts) and revised the design to a cancellable request after the explanation.
