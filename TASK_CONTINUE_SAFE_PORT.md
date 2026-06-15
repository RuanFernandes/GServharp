Read `AGENTS.md`, `COMPATIBILITY_RULES.md`, `SERVER_SPEC.md`, `PORTING_PLAN.md`, `KNOWN_BLOCKERS.md`, and all docs under `docs/`.

Continue the C#/.NET 1:1 port using only source-confirmed behavior.

Source of truth:

```txt
ai_resources/GServer-CPP-ORIGINAL/
external/gs2lib/
```

Do not modify anything inside `ai_resources/`.

Do not invent behavior.

If something is unclear, document it as unknown and continue with the next safe task.

Current status:

* Login/session boundary exists.
* Account loading boundary exists.
* ReadyForLevelRuntime exists.
* sendLevel static packets exist.
* sendLevel dynamic packets exist.
* sendLevel tail/player visibility sync exists.
* PLO_OTHERPLPROPS exists.
* __getLogin property set exists.
* LevelEntryPlayerPropsSynchronized state exists.
* Tests are green.

Now continue through these next safe milestones:

# 1. Minimal level/player runtime ownership

Start replacing test-only snapshots with a small source-confirmed runtime model for level ownership and player membership.

Focus on:

* `Server::playerLoggedIn`
* `Server::playerLoggedOut`
* `Level` player list ownership
* player add/remove from level
* player id/account identity handling
* duplicate player handling
* level player lookup
* map/GMAP membership if source-confirmed
* ordering of players in level sync
* lifecycle boundaries when player warps between levels
* lifecycle boundaries when player disconnects

Allowed:

* Add minimal runtime classes only for ownership/listing.
* Add interfaces where behavior is not fully confirmed.
* Add tests for add/remove/same-level/GMAP filtering.
* Integrate with existing player visibility sync only where source-confirmed.

Not allowed:

* Do not implement movement runtime.
* Do not implement combat.
* Do not implement NPC AI.
* Do not execute scripts.
* Do not invent player ordering/filtering rules.

# 2. Level loading parser boundary

Trace and implement only source-confirmed level loading/parsing needed to create immutable level snapshots.

Focus on:

* `Level::loadLevel`
* `Level::loadNW`
* `Level::loadGraal`
* `Level::loadZelda`
* board data
* layers
* links
* signs
* chests
* horses
* baddies
* NPC payload extraction only as serialized packet data
* modTime handling
* missing/malformed file behavior
* extension/format detection

Allowed:

* Add pure parsers for source-confirmed formats.
* Add tests with small source-confirmed fixtures.
* Add DTOs/snapshots for parsed level data.
* Keep runtime behavior out of scope.

Not allowed:

* Do not implement NPC script execution.
* Do not implement baddy AI.
* Do not invent malformed file recovery.
* Do not approximate unknown level formats.

# 3. Integrate parsed level snapshots into sendLevel

If level parsing is confirmed enough, connect parsed level snapshots to existing `SendLevelBoundary`.

Focus on:

* board/layers raw payload
* links/signs payload
* chests/horses/baddies payload
* NPC serialized payload passthrough
* modTime
* level type/map type
* packet order

Allowed:

* Add integration tests from parsed fixture → sendLevel packet sequence.
* Add golden fixtures for exact bytes.
* Stop before runtime simulation.

# 4. Player visibility sync using minimal runtime

Use the minimal level/player runtime to feed existing player visibility sync.

Focus on:

* same-level filtering
* GMAP/group-map filtering
* singleplayer skip
* distance filtering
* packet order
* __getLogin props for other players

Allowed:

* Add deterministic tests with multiple players.
* Keep movement/live updates out of scope.

# 5. Docs and tests

Create/update docs:

```txt
docs/spec/LEVEL_RUNTIME_OWNERSHIP_SPEC.md
docs/spec/LEVEL_FILE_FORMAT_SPEC.md
docs/spec/LEVEL_LOADING_SPEC.md
docs/spec/PLAYER_VISIBILITY_SYNC_SPEC.md
docs/spec/SENDLEVEL_SPEC.md
docs/spec/GOLDEN_FIXTURES.md
docs/spec/KNOWN_BLOCKERS.md
```

Run:

```bash
dotnet build GServharp.sln
dotnet test GServharp.sln
```

At the end, report:

* What was completed
* Which C++/gs2lib files were used
* Which C# files/tests were added or modified
* Which docs were updated
* Which golden fixtures were added
* Which behavior is now source-confirmed
* Which behavior remains blocked
* Whether `ai_resources/` stayed untouched
* Build/test results
* Safest next step

Continue as far as safely possible. Do not stop after one small task if another safe task can be done.
