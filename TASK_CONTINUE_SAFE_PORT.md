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

* Dev-only local TCP shell exists behind explicit `--dev-only-local`.
* Shell integrates `GraalFileQueue.FlushSocket`.
* Shell maintains connection state and reads multiple length-prefixed frames.
* Dev-only pipeline can reach login -> fake dev auth -> account/level boundary -> `.nw` sendLevel.
* Manual diagnostic client test is now possible, but not gameplay.
* Post-login frames currently stop with explicit unsupported logs.
* Known blockers:

  * bzip2 socket flush for large payloads,
  * real auth/list-server,
  * post-login movement/player props/runtime dispatch.
* Tests are green.

Goal of this run:

Implement the first source-confirmed post-login incoming packet boundary: movement/player props parsing, safe state mutation, and nearby player forwarding where confirmed.

---

# Milestone 1: Trace post-login packet dispatch

Deeply trace:

* `Player::parsePacket`
* packet handlers under `server/src/player/packets/`
* incoming movement/player props packets
* unsupported packet behavior
* behavior after login/level entry
* rawdata mode interactions if any

Focus on:

* exact PLI packet IDs involved in movement/player props
* packet body format
* handler dispatch order
* unknown/unsupported packet behavior
* disconnect or ignore behavior for bad packets
* session state requirements

Allowed:

* Add docs and packet catalog updates.
* Add dispatcher skeleton only for source-confirmed packets.
* Unsupported packets should behave according to C++ if known; otherwise log/block explicitly.

---

# Milestone 2: Incoming player props parser

Trace and implement source-confirmed parser for incoming player property updates.

Focus on:

* `Player::setProps`
* `Player::setProp`
* property ID encoding
* property value boundaries
* property sequence parsing
* x/y/level/direction/gani props if present
* exact string/number parsing behavior
* empty/malformed property behavior
* client-version branches

Allowed:

* Add DTOs for parsed property updates.
* Add parsers with golden fixtures.
* Add tests for exact byte inputs.
* Only include confirmed props.

Not allowed:

* Do not invent prop defaults.
* Do not guess unknown props.
* Do not implement gameplay side effects.

---

# Milestone 3: Safe RuntimePlayer mutation

Apply only source-confirmed safe props to runtime player state.

Focus on:

* x/y
* level name if handled here
* direction
* gani/animation
* nickname/body/head/etc only if confirmed here
* clipping/validation if source-confirmed
* no side effects beyond confirmed state update

Allowed:

* Add methods on RuntimePlayer or a movement/props service.
* Add tests for state mutation.
* Keep unsupported props ignored/blocked exactly as C++ behavior if known.

Not allowed:

* Do not implement combat.
* Do not implement item use.
* Do not implement anti-cheat unless traced.
* Do not implement link traversal unless traced.

---

# Milestone 4: Forward player props to nearby players

Trace and implement source-confirmed forwarding behavior.

Focus on:

* which packet ID is used
* PLO_OTHERPLPROPS or related packets
* property table/order for forwarded updates
* same-level filtering
* GMAP filtering
* singleplayer behavior
* hidden/staff/invisible filters if confirmed
* packet order
* whether sender also receives anything

Allowed:

* Add forwarding builder/service.
* Add tests with two or three RuntimePlayers.
* Integrate with dev-only shell only if safe and source-confirmed.

Not allowed:

* Do not invent visibility rules.
* Do not implement live gameplay beyond prop sync.

---

# Milestone 5: Integrate minimal movement/player props into dev-only TCP shell

If parser + mutation + forwarding are source-confirmed enough, integrate them into the dev-only shell.

Focus on:

* after level entry, accept movement/player prop frames
* mutate runtime player state
* flush forwarding packets to affected players if supported
* log unsupported packets
* keep fake auth clearly dev-only
* preserve packet framing/encryption/flush behavior

Allowed:

* Add integration tests with fake transports.
* Add docs for manual client test expectations.

Not allowed:

* Do not implement unknown packets.
* Do not fake gameplay.
* Do not hide unsupported behavior.

---

# Milestone 6: Docs/tests/report

Create/update docs:

```txt
docs/spec/MOVEMENT_PLAYER_PROPS_SPEC.md
docs/spec/PLAYER_VISIBILITY_SYNC_SPEC.md
docs/spec/TCP_SESSION_PIPELINE_SPEC.md
docs/spec/RUN_LOCAL_DEV_SERVER.md
docs/spec/GOLDEN_FIXTURES.md
docs/spec/KNOWN_BLOCKERS.md
KNOWN_BLOCKERS.md
```

Run:

```bash
dotnet build GServharp.sln
dotnet test GServharp.sln
```

At the end, report:

* What was completed
* Which incoming movement/player prop packets are supported
* Whether dev-only shell now handles post-login movement/props
* Which C++/gs2lib files were used
* Which C# files/tests were added or modified
* Which docs were updated
* Whether `ai_resources/` stayed untouched
* Build/test results
* Whether manual client test should now be attempted
* Exact run command and expected limitations
* Safest next step

Continue as far as safely possible. Do not stop after one small task if another safe task can be done safely.
