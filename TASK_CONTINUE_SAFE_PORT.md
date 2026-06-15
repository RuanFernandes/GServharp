Read `AGENTS.md`, `COMPATIBILITY_RULES.md`, `SERVER_SPEC.md`, `PORTING_PLAN.md`, `KNOWN_BLOCKERS.md`, and all docs under `docs/`.

Continue the C#/.NET 1:1 port using only source-confirmed behavior.

Source of truth:

```txt
ai_resources/GServer-CPP-ORIGINAL/
external/gs2lib/
```

Do not modify anything inside `ai_resources/`.

Do not invent behavior.

Do not implement gameplay unless it is required for a source-confirmed boundary and can be isolated safely behind interfaces/tests.

If something is unclear, document it as unknown and continue with the next safe task.

Work autonomously through the next safest milestones, in this priority order:

# 1. Account loading and player defaults

Trace and implement only source-confirmed account/player loading behavior needed before world entry.

Focus on:

* Account.cpp / Account.h
* FileSystem or database/file access used by account loading
* defaultaccount behavior
* missing account behavior
* malformed account behavior
* member default values
* account fields used by login props
* player ID, account name, nickname
* power/maxpower
* level name
* x/y position
* rights/admin/RC/NC flags
* banned status
* staff-only/admin-IP behavior
* save/load side effects

Allowed:

* Implement production account parsing only if exact file format and behavior are fully confirmed.
* Otherwise create interfaces/DTOs and test-only fakes.
* Add tests for confirmed defaults and parsing behavior.
* Document blockers clearly.

# 2. Complete player property serialization

Continue expanding `PlayerPropertySerializer` using only confirmed `PlayerProps.cpp` behavior.

Focus on:

* remaining `__sendLogin` properties
* getProp formatting
* property-specific encoding
* version-specific behavior
* empty/default behavior
* account/player fields required by each prop

Allowed:

* Add confirmed properties in small groups.
* Add golden byte fixtures.
* Add tests for property order and exact bytes.
* Keep blocked properties explicitly documented.

# 3. CFileQueue/socket flush byte compatibility

Implement source-confirmed send queue behavior needed to turn queued packets into socket bytes.

Focus on:

* CFileQueue.cpp / CFileQueue.h
* CSocket.cpp / CSocket.h
* packet queue order
* bundle behavior
* newline behavior
* compression thresholds
* compression flags
* encryption during flush
* partial socket writes
* websocket behavior if present

Allowed:

* Implement uncompressed confirmed flush behavior first.
* Add compression/encryption only with byte-exact confirmed tests.
* Add golden fixtures for exact byte output.

# 4. Warp/world-entry boundary

Trace `warp(...)`, `setLevel(...)`, and first world-entry packets.

Stop before full gameplay simulation.

Focus on:

* first packets sent during warp
* level file/resource transfer
* player position serialization
* level name behavior
* map/single-level differences
* when NPC/script/runtime behavior begins
* which packet builders are needed before runtime

Allowed:

* Implement packet builders if byte structure is confirmed.
* Add interfaces/stubs for level/resource providers.
* Add golden fixtures.
* Do not implement full level runtime yet.

# 5. Scripting system research

If scripting becomes required by world-entry behavior, analyze it before implementing.

Focus on:

* script manager
* script loading
* exposed APIs
* player/NPC/level hooks
* login/warp-related hooks
* side effects during login/world entry

Do not implement a full scripting runtime unless a compatibility strategy is documented and approved.

# 6. Tests and docs

For every confirmed behavior:

* Add or update docs under `docs/spec/`
* Add golden fixtures where possible
* Add unit tests
* Keep blockers updated
* Keep unknowns explicit

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
