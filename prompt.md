Read `AGENTS.md`, `COMPATIBILITY_RULES.md`, `SERVER_SPEC.md`, `PORTING_PLAN.md`, `KNOWN_BLOCKERS.md`, and all docs under `docs/`.

We have reached the `ReadyForLevelWarp` boundary and implemented the source-confirmed subset of player property serialization used by the login boundary.

Now continue autonomously through the next safe implementation milestones.

Source of truth:

```txt
ai_resources/GServer-CPP-ORIGINAL/
external/gs2lib/
```

Do not modify anything inside `ai_resources/`.

Do not invent behavior.

Do not implement gameplay behavior unless it is strictly required as a source-confirmed login/world-entry boundary and can be isolated safely.

If something is unclear, document it as unknown and continue with the next safe task.

Work through these milestones in order, continuing as far as safely possible:

---

# Milestone 1: Complete `__sendLogin` property table

Trace `PlayerProps.cpp`, `Player.h`, `Account.cpp`, and related headers to identify the complete `__sendLogin` property boolean/list/table.

Implement only the source-confirmed table/order.

Focus on:

* Full list of property IDs included in `sendProps(__sendLogin)`
* Exact ordering
* Which props are always emitted
* Which props depend on account/player/version/state
* Which props are blocked by unknown/default/account loading behavior

Allowed:

* Add a tested `SendLoginPropertySet` or equivalent constant/table
* Add tests proving property order
* Expand serialization only for properties whose `getProp` behavior is confirmed
* Leave unsupported props explicitly blocked/documented

Do not invent default values.

---

# Milestone 2: Account/player data loading boundary

Trace account and player data loading used before/during login.

Focus on:

* `Account.cpp`
* `Account.h`
* Any file/database access used by login
* Default account/player field values
* Banned state
* admin/RC/NC rights
* staff-only behavior
* account name/player id
* nickname, level name, x/y, power, max power, and other fields needed by login props
* how missing/malformed account data behaves

Allowed:

* Add interfaces for account/player source data
* Add DTOs that mirror confirmed C++ fields
* Add test-only fake repositories
* Add parsers only if the format is fully confirmed
* Add docs and tests for confirmed defaults/fields

Do not create production account loading unless the exact file format and behavior are confirmed.

---

# Milestone 3: Expand player property serialization in confirmed groups

Expand `PlayerPropertySerializer` in small source-confirmed groups.

Priority properties:

* power/current power
* max power
* nickname/account name
* level name
* x/y position
* client/version-dependent props
* player type/rights/admin flags
* any login-required props confirmed by `sendProps(__sendLogin)`

For each property added:

* Cite source file/function
* Add exact byte/golden tests
* Document unknowns
* Preserve C++ formatting exactly

Do not implement props whose values depend on world/map/runtime state unless that behavior is fully confirmed.

---

# Milestone 4: CFileQueue and socket flush compatibility

Trace and implement only source-confirmed `CFileQueue`/send queue behavior needed for login packet output.

Focus on:

* packet queue behavior
* newline behavior
* bundle behavior
* compression flags
* encryption during flush
* raw data/file transfer behavior if used during login
* exact bytes written to socket when possible

Allowed:

* Add queue/flush abstractions
* Add tests for uncompressed confirmed packet sequences
* Add compression/encryption tests only if byte-exact behavior is confirmed
* Add golden fixtures

Do not implement approximate compression/socket behavior.

---

# Milestone 5: Trace `warp(...)` and first world-entry packet boundary

Trace the beginning of `warp(m_levelName, getX(), getY())`, but stop before real gameplay simulation.

Focus on:

* exact function called
* initial level/map load behavior
* first packets sent during warp
* whether file/resource transfer starts here
* whether level data is sent immediately
* what player fields are required
* where real level runtime/gameplay begins

Allowed:

* Document the warp/world-entry boundary deeply
* Add interfaces/stubs for level/resource providers
* Implement only packet builders or session states whose bytes are fully confirmed
* Add golden fixtures for first confirmed warp packets

Not allowed:

* Do not implement full level runtime.
* Do not implement gameplay simulation.
* Do not execute scripts.
* Do not invent level file formats or defaults.

---

# Milestone 6: Documentation and report

Create/update docs as needed:

```txt
docs/spec/PLAYER_PROPS_SPEC.md
docs/spec/ACCOUNT_LOADING_SPEC.md
docs/spec/CFILEQUEUE_FLUSH_SPEC.md
docs/spec/WARP_WORLD_ENTRY_SPEC.md
docs/spec/GOLDEN_FIXTURES.md
docs/spec/KNOWN_BLOCKERS.md
```

Run:

```bash
dotnet build GServharp.sln
dotnet test GServharp.sln
```

At the end, report:

* Which milestones were completed
* Which C++/gs2lib files were used
* Which C# files/tests were added or modified
* Which golden fixtures were added
* Which behavior is now source-confirmed
* Which behavior remains blocked
* Whether `ai_resources/` stayed untouched
* Build/test results
* Safest next step

Important: continue through all safe milestones as far as possible. Do not stop after one small task if the next task can be done safely.
