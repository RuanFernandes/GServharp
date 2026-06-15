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
* SendLevelBoundary exists.
* Modern static sendLevel slice exists.
* Dynamic sendLevel boundary packets exist:

  * PLO_LEVELBOARD
  * PLO_LEVELCHEST
  * PLO_HORSEADD
  * PLO_BADDYPROPS
  * GMAP correction with PLO_LEVELNAME
  * PLO_GHOSTICON
  * PLO_ISLEADER
  * PLO_NEWWORLDTIME
  * PLO_SETACTIVELEVEL
  * NPC payload as already serialized packet data
* Session states exist:

  * DynamicLevelPayloadSent
  * LevelRuntimePacketsSent
* Tests are green.

Now continue through these next safe milestones:

# 1. Trace the remaining tail of sendLevel/level entry

Trace what happens immediately after `PLO_SETACTIVELEVEL` and NPC payload forwarding.

Focus on:

* remaining player props sent around level entry
* `sendProps(__getLogin)` or equivalent in this context
* forwarding of nearby player props
* GMAP/group-map filtering
* player visibility/filtering rules
* player list synchronization
* any packet order after NPC payloads
* any transition into true runtime/gameplay
* exact stop point before full simulation

Allowed:

* Add source-confirmed packet builders.
* Add DTOs/snapshots for nearby player/player-list data.
* Add tests/golden fixtures for exact bytes and ordering.
* Add new session state only when the source boundary is clear.

Not allowed:

* Do not implement actual player movement/runtime.
* Do not implement combat/inventory/NPC AI/scripts.
* Do not invent nearby player defaults.
* Do not approximate filtering rules.
* Do not send packet bytes unless confirmed.

# 2. Player props for level-entry context

Trace and implement only the player properties required by the level-entry tail.

Focus on:

* `sendProps(__getLogin)` or related prop tables
* property set/order
* getProp formatting for required fields
* current level
* x/y
* direction
* nickname/account
* animation/gani
* body/head/sword/shield/colors
* AP, status, guild/nick fields if sent here
* version-specific behavior

Allowed:

* Add confirmed prop sets/tables.
* Expand `PlayerPropertySerializer` only for confirmed fields.
* Add golden byte fixtures.
* Keep unknown props blocked.

# 3. Nearby player forwarding/list sync

Trace nearby player forwarding during level entry.

Focus on:

* which players are sent to the logging-in player
* which props are sent for other players
* which packet IDs are used
* GMAP vs single-level filtering
* hidden/invisible/staff conditions
* version/client branches
* order of players/properties

Allowed:

* Add snapshot models and packet builders.
* Add tests with deterministic small fixtures.
* Stop before live runtime updates.

# 4. Docs and tests

Create/update docs:

```txt
docs/spec/SENDLEVEL_TAIL_SPEC.md
docs/spec/PLAYER_PROPS_SPEC.md
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
