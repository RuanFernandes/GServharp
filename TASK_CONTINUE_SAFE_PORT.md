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
* Auth/server-list boundary exists.
* Account loading boundary exists.
* Player::sendLogin pre-world boundary exists.
* Runtime ownership exists.
* sendLevel static/dynamic/tail boundaries exist.
* Player visibility sync exists.
* Pure `.nw` parser exists.
* `.nw` board/layer/link/sign/chest packets exist.
* LevelItem catalog exists.
* Filesystem-backed `.nw` loading boundary exists.
* LoadedNwLevel can be converted into ModernLevelPayload and passed into SendLevelBoundary.
* Tests are green.
* Manual client connection is still blocked by:

  1. TCP/session pipeline real until ClientSessionSkeleton.
  2. Dev-only auth/server-list provider clearly separated from production.
  3. Login/world-entry handoff to loaded `.nw` without inventing NPC/file-transfer/movement behavior.

Goal of this run:

Create the first dev-only local server shell that can exercise the confirmed login -> account -> level loading -> sendLevel boundary as far as safely possible.

This server shell must be clearly marked as development-only and must not pretend to be production-compatible.

---

# Milestone 1: TCP/session pipeline shell

Implement or improve a minimal TCP listener pipeline that reaches existing source-confirmed session boundaries.

Focus on:

* accepting TCP connections
* reading client frames using existing packet framing rules
* passing packets into ClientSessionSkeleton/session pipeline
* writing queued outbound bytes using existing GraalFileQueue/packet builders where confirmed
* connection lifecycle
* disconnect behavior where confirmed
* logging useful debug output

Allowed:

* Add minimal TCP server shell.
* Add interfaces for transport/read/write.
* Add tests using in-memory streams or fake transports.
* Keep behavior source-confirmed where protocol-visible.

Not allowed:

* Do not invent protocol behavior.
* Do not bypass packet framing.
* Do not implement unconfirmed socket quirks.
* Do not modify reference sources.

---

# Milestone 2: Dev-only auth/server-list provider

Create a clearly separated dev-only auth provider that allows local manual testing.

Focus on:

* using the existing pre-world auth boundary
* providing a test account result
* clearly marking this as not production behavior
* ensuring production auth remains blocked unless source-confirmed
* keeping server-list/list-server behavior documented separately

Allowed:

* Add namespace/class names that clearly include `DevOnly` or `LocalTest`.
* Add config flag requiring explicit opt-in.
* Add docs warning that this is not authentic production behavior.
* Add tests proving dev-only provider is not default production behavior.

Not allowed:

* Do not pretend fake auth is real.
* Do not remove production blockers.
* Do not mix dev-only provider into compatibility core.

---

# Milestone 3: Dev-only level handoff

Wire the dev-only local path:

```txt
login accepted by dev-only auth
-> account loaded or dev account snapshot
-> ReadyForLevelWarp
-> filesystem-loaded .nw level
-> SendLevelBoundary
-> stop before unimplemented runtime behavior
```

Focus on:

* using AccountLoadService where possible
* using NwLevelFileLoader
* using RuntimeServer/RuntimeLevel ownership where safe
* using SendLevelBoundary for packet generation
* stopping before movement/NPC/script/file-transfer behavior
* explicit logs/state when stopped at a known boundary

Allowed:

* Add a dev-only bootstrap level file path.
* Add sample minimal `.nw` fixture under a clearly dev/test folder if acceptable.
* Add docs on how to run and what is expected.
* Add integration tests where possible.

Not allowed:

* Do not invent NPC behavior.
* Do not execute scripts.
* Do not implement fake movement as production.
* Do not hide that file/resource transfer and movement are incomplete.

---

# Milestone 4: Movement/player props boundary research

If the dev server shell is safe enough, continue research/implementation of incoming movement/player props.

Focus on:

* `Player::parsePacket`
* `PLI_PLAYERPROPS` or equivalent incoming player property packets
* `Player::setProps`
* `Player::setProp`
* position updates
* level changes
* direction
* gani/animation
* forwarding to other players
* validation/clipping if source-confirmed

Allowed:

* Add parsers and DTOs only for confirmed incoming property packets.
* Add unit tests/golden fixtures.
* Add docs.

Not allowed:

* Do not implement live movement in dev server unless source-confirmed enough.
* Do not invent validation.
* Do not implement combat/items/NPC/scripts.

---

# Milestone 5: Docs and tests

Create/update docs:

```txt
docs/spec/RUN_LOCAL_DEV_SERVER.md
docs/spec/LOCAL_DEV_AUTH_WARNING.md
docs/spec/TCP_SESSION_PIPELINE_SPEC.md
docs/spec/MOVEMENT_PLAYER_PROPS_SPEC.md
docs/spec/LEVEL_FILESYSTEM_LOADING_SPEC.md
docs/spec/SENDLEVEL_SPEC.md
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
* Which milestones were completed
* Which milestones were blocked and why
* Which C++/gs2lib files were used
* Which C# files/tests were added or modified
* Which docs were updated
* Whether `ai_resources/` stayed untouched
* Build/test results
* Whether a manual client connection test is now possible
* If yes, give exact run instructions and expected limitations
* If no, list the exact 3 smallest blockers
* Safest next step

Continue as far as safely possible. Do not stop after one small task if another safe task can be done safely.
