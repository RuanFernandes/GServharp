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

* AccountFileParser exists for source-confirmed GRACC001 parsing.
* GraalFileQueue exists for confirmed passthrough behavior.
* WarpPackets exists for confirmed warp packet builders.
* ReadyForLevelWarp boundary exists.
* Tests are green.

Now continue through these next safe milestones:

# 1. Production account file resolution

Trace and implement the production account file resolution boundary only where source-confirmed.

Focus on:

* `FileSystem::findi`
* account path lookup
* account filename normalization
* fallback to `defaultaccount.txt`
* startlevel/startx/starty overrides
* guest account behavior if source-confirmed
* missing account behavior
* malformed account behavior
* side effect of saving/adding account when loaded from default
* exact behavior when account exists but fields are missing
* exact behavior when account is banned/staff/admin/RC/NC

Allowed:

* Add filesystem abstraction/interfaces.
* Add a source-confirmed account resolver service.
* Add test-only in-memory filesystem.
* Add tests for exact lookup/fallback behavior.
* Add docs and golden fixtures where relevant.
* Implement production parsing only if exact behavior is confirmed.

Not allowed:

* Do not invent filesystem paths or defaults.
* Do not invent guest RNG behavior.
* Do not implement real persistence writes unless exact C++ behavior is traced.
* Do not fake production auth behavior.

# 2. Complete CFileQueue behavior in source-confirmed layers

Expand GraalFileQueue only where exact C++/gs2lib behavior is confirmed.

Focus on:

* queue thresholds
* compression trigger behavior
* compression flags
* encryption during flush
* socket write partial behavior
* websocket behavior if present
* raw-data/file-transfer queue behavior
* board/file routing
* exact byte output for confirmed cases

Allowed:

* Add tests for uncompressed and confirmed compressed cases.
* Add golden fixtures.
* Keep unsupported compression/encryption/websocket cases documented as blocked if not byte-confirmed.

Do not approximate compression or socket flushing.

# 3. Warp/setLevel boundary and level resource packets

Trace `warp(...)`, `setLevel(...)`, `sendLevel`, and related file/resource packet builders.

Stop before full gameplay simulation.

Focus on:

* first packet sequence after warp
* level name handling
* coordinate handling
* map vs single-level behavior
* level file lookup
* level data transfer packets
* file/resource transfer packets
* PLO_LEVELNAME, PLO_PLAYERWARP, PLO_PLAYERWARP2, PLO_WARPFAILED usage
* where NPC/script/runtime behavior starts

Allowed:

* Implement confirmed level/resource packet builders.
* Add interfaces/stubs for level/resource provider.
* Add tests for exact packet bytes.
* Add docs for the boundary.

Not allowed:

* Do not implement full level runtime.
* Do not implement NPC logic.
* Do not execute scripts.
* Do not invent level file format behavior.

# 4. Continue player property expansion only where safe

Expand player properties only when backed by Account.cpp / PlayerProps.cpp / confirmed defaults.

Focus on:

* properties required before/around warp
* nickname/account/level/position properties
* movement-relevant props
* version-specific props
* blocked props list

Add tests for exact bytes.

# 5. Docs, tests, report

Update docs:

```txt
docs/spec/ACCOUNT_LOADING_SPEC.md
docs/spec/CFILEQUEUE_FLUSH_SPEC.md
docs/spec/WARP_WORLD_ENTRY_SPEC.md
docs/spec/PLAYER_PROPS_SPEC.md
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
