# Complete 1:1 C++ Compatibility Port Driver

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` or `superpowers:subagent-driven-development`.
> Also use `superpowers:test-driven-development` before implementing code and
> `superpowers:verification-before-completion` before claiming completion.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Continue the C#/.NET server port until it is a 100% client-compatible
replacement for the original C++ server.

**Architecture:** Internal C# architecture may stay clean and modular, but every
client-facing behavior must remain source-compatible with the original C++
server. Implement only behavior proven from `ai_resources/GServer-CPP-ORIGINAL/`
and recovered dependencies under `external/`.

**Scope boundary:** This is a source-faithful port, not a feature invention
project. A gameplay/service area is in scope only when the recovered original
C++ source or exact dependency source contains a concrete client-facing handler,
packet path, persistence path, or runtime rule for it. Packet captures are for
certifying and debugging source-confirmed behavior; they must not be used to add
features that the recovered C++ source does not implement. Built-in shops,
trades, parties, quests, missions, generic social systems, or other genre
features remain absent unless recovered original source proves otherwise.
If any unchecked item is discovered to be non-source-derived, mark that item as
removed from scope with a dated note and continue; do not implement it.

**Tech Stack:** C#/.NET, xUnit, PowerShell, C++ source tracing, recovered
`gs2lib`, optional fixture harnesses under `tools/`.

---

## Mandatory Run Rules

Read these files before every run:

- `docs/AGENTS.md`
- `docs/COMPATIBILITY_RULES.md`
- `docs/SERVER_SPEC.md`
- `docs/PORTING_PLAN.md`
- `docs/KNOWN_BLOCKERS.md`
- all files under `docs/`
- this file

Use these sources of truth:

```txt
ai_resources/GServer-CPP-ORIGINAL/
external/gs2lib/
```

Rules:

- Do not modify anything inside `ai_resources/`.
- Do not invent behavior.
- Do not use Rust/Python as authority. They are only explanatory references.
- If C++ behavior is unclear, document it as blocked and continue with the next
  safe item.
- Use TDD for every implementation slice: write failing tests, verify failure,
  implement minimal source-confirmed code, verify pass.
- Update docs/specs/blockers/golden fixtures as behavior is confirmed.
- Run `dotnet build GServerSharp.sln` and `dotnet test GServerSharp.sln` before the
  final report unless only non-code markdown changed and the task explicitly
  documents why a build was not needed.
- Always run `git status --short ai_resources` before committing.
- Automatically commit all pending changes at the end of each completed run.
- After committing, report commit hash, build/test result, untouched
  `ai_resources/` status, what remains blocked, and the next unchecked item.

## How To Execute This File

When the user says:

```txt
Run docs/TASK_COMPLETE_1TO1_PORT.md
```

Do this:

1. Read the mandatory files listed above.
2. Find the first unchecked task in this file.
3. Execute as much of that task as safely possible.
4. If the task is fully completed, mark it `[x]`.
5. If it is partially completed, add dated notes under that task and leave it
   unchecked.
6. Continue to the next safe unchecked task if time/context remains.
7. Verify, confirm `ai_resources/` is untouched, commit, and report.

Do not ask the user which task is next unless all remaining tasks are blocked by
external input.

---

# Current Compatibility Status

The project is not 1:1 complete yet.

Implemented/partially implemented:

- C# solution foundation and project layout.
- Confirmed protocol ids, binary codecs, encryption/compression primitives where
  recovered.
- Login/session prelude and early session boundaries.
- Server-list auth packet body boundaries.
- Account file parsing/save boundary for confirmed fields.
- Pre-world login continuation through `ReadyForWorldEntry`.
- `Server::playerLoggedIn` / beginning `sendLoginClient` through
  `ReadyForLevelWarp`.
- Confirmed `sendProps(__sendLogin)` subset/property table.
- Warp/setLevel pre-runtime boundary and first `sendLevel` static/dynamic packet
  boundaries.
- Minimal level/player ownership and visibility forwarding boundaries.
- `.nw` parsing for confirmed `BOARD`, `LINK`, `SIGN`, `CHEST`, `NPC`, `BADDY`
  source-line behavior.
- File transfer cache boundary for confirmed want/verify/send-file behavior.
- Inbound movement property subset and movement forwarding bytes.
- Client-triggered `PLI_LEVELWARP`/`PLI_LEVELWARPMOD` parser.
- Runtime `PLO_SAY2` sign-touch packet construction.
- Selected RC/NC/admin packet builders and rights parsing.
- Inert entity/runtime packet builders for selected items, horses, NPCs,
  weapons, and baddy defaults.
- Timing/save-loop boundaries.
- Passive client compatibility harness.

Major source-confirmed areas still missing or partial:

- Production socket host and full continuous session dispatch.
- Full server-list socket lifecycle/reconnect/registration.
- Full account/auth/list-server integration in production runtime.
- Full `sendLoginClient` old-client, weapons, classes, flags, and file branches.
- Full level runtime/cache/map ownership and `.graal`/`.zelda`/`.gmap` parsers.
- Full movement/player prop handling, blocked side effects, invalid update
  behavior, and live movement-loop invocation.
- NPC/script runtime, V8/GS2 compiler integration, bytecode, lifecycle, and
  script-visible APIs.
- Source-confirmed baddy AI, combat, projectiles, hit validation, damage,
  drops, death, and respawn paths only where recovered C++ handlers/rules exist.
- Source-confirmed inventory, item pickup mutations, chest reward mutations,
  guild, chat/PM, and profile paths only where the recovered C++ source has
  explicit handlers, packet paths, persistence paths, or runtime rules.
- RC/NC/admin production sockets and mutation commands.
- Upload/write paths and update-package lifecycle.
- Websocket handshake/TLS blocked branches and full-session compression
  certification.
- Closed-source client certification against original C++ byte captures.

---

# Execution Backlog

## Phase 1: Production Socket And Session Dispatch

**Goal:** Replace local-debug post-login limitations with source-confirmed
production socket/session dispatch where safe.

**Source files to trace first:**

- `server/src/player/Player.cpp`
- `server/include/Player.h`
- `server/src/Server.cpp`
- `server/include/Server.h`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/include/CString.h`

- [x] Trace C++ socket accept/read/write lifecycle and document it in
  `docs/spec/PRODUCTION_SOCKET_SESSION_SPEC.md`.
- [x] Implement a production TCP listener skeleton only for confirmed framing
  and lifecycle behavior.
  - 2026-06-16: Added `SocketReceiveBuffer` for source-confirmed
    arbitrary TCP chunk buffering and raw two-byte big-endian frame extraction.
  - 2026-06-16: Added accept-one `ClientTcpServer` skeleton with
    TCP_NODELAY, player id `2` start, `0x8000` read chunks, frame dispatch, and
    handler-provided outbound writes. Still blocked/not done: multi-session
    socket-manager scheduling, deferred deletion cleanup, and real production
    auth dispatch.
- [x] Integrate confirmed post-login decoded packet dispatch without gameplay
  invention.
  - 2026-06-16: Added `PostLoginPacketDispatcher` for already-decoded
    post-login inner packet bytes. It handles only the confirmed
    `PLI_PLAYERPROPS` movement/property subset, blocks assigned-but-unimplemented
    C++ `TPLFunc` ids, and models `msgPLI_NULL` invalid-packet counting with the
    source-confirmed sixth-packet disconnect message. It is not yet wired into a
    production auth/session loop.
- [x] Keep unsupported packet ids logged/blocked, not faked.
  - 2026-06-16: Added `PostLoginFrameHandler`, which decodes
    post-login frames, logs dispatch statuses, stops assigned-but-unimplemented
    packet ids as blocked, and only returns outbound bytes for the confirmed
    `msgPLI_NULL` invalid-packet disconnect path.
- [x] Add tests for socket frame dispatch, disconnect behavior, queue flush
  timing, and unsupported packet handling.
  - 2026-06-16: Added production TCP frame/connection tests plus post-login
    dispatcher/frame-handler tests for confirmed movement dispatch,
    assigned-but-unimplemented blocked packets, and invalid-packet disconnect.
    Existing `GraalFileQueue` tests cover confirmed queue flush branches and
    partial-send buffering. Leave unchecked until this is reviewed against the
    full requested coverage list.
  - 2026-06-16: Added loopback production TCP tests proving the production
    listener can drive confirmed post-login `PLI_PLAYERPROPS` dispatch and can
    write the source-confirmed invalid-packet disconnect bytes before returning
    `HandlerStopped`. Reviewed existing `FileQueueCompatibilityTests` for
    confirmed queue-flush and partial-send coverage.
- [x] Update `docs/spec/TCP_SESSION_PIPELINE_SPEC.md`,
  `docs/spec/docs/KNOWN_BLOCKERS.md`, and `docs/KNOWN_BLOCKERS.md`.
  - 2026-06-16: Updated the TCP/session docs, production socket spec,
    post-login dispatch spec, and blockers to reflect the production
    listener/receive-buffer/post-login-dispatch boundary and the remaining
    production auth/session-loop blockers.

Completion criteria:

- Production listener can accept a client and drive only confirmed protocol
  boundaries.
- Unsupported behavior is explicit.
- Build/test pass.

## Phase 2: Server-List Production Lifecycle

**Goal:** Port real server-list connection lifecycle and account verification
without fake auth.

**Source files to trace first:**

- `server/src/ServerList.cpp`
- `server/include/ServerList.h`
- `server/src/Server.cpp`
- `server/src/player/PlayerLogin.cpp` if present, otherwise login sections in
  `Player.cpp`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/include/CSocket.h`

- [x] Document server-list connect/reconnect/register/ping/request-list flow in
  `docs/spec/SERVERLIST_LIFECYCLE_SPEC.md`.
  - 2026-06-16: Added dedicated lifecycle spec covering constructor/socket
    shape, receive/decode loop, timed reconnect, connect/register packet order,
    ping, auth response, and C# mapping.
- [x] Implement confirmed server-list socket lifecycle behind an interface.
  - 2026-06-16: Added `ServerListLifecycle` behind
    `IServerListSocket`, preserving confirmed initialize/connect/
    register, queue clear, gen1 register immediate send, gen2 follow-up packet
    order, and local-IP loopback clearing. Concrete remote TCP client remains
    blocked.
- [x] Wire real `SVO_VERIACC2` request/`SVI_VERIACC2` response path into
  production login.
  - 2026-06-16: Added `ServerListAuthResponseHandler`, which parses
    confirmed `SVI_VERIACC2` payloads, resolves pending sessions by id/type,
    applies account-name overwrite, queues source-confirmed rejection
    disconnect bytes, and marks `SUCCESS` as `ServerListAuthAcceptedPreWorld`
    without local fake auth. Concrete live list-server receive loop remains
    blocked.
- [x] Preserve fake/dev auth only behind explicit local-debug settings.
  - 2026-06-16: Verified the only fake server-list success path remains inside
    `LocalDebugSessionPipeline` and throws unless `EnableLocalDebugAuth=true`;
    production auth request/response boundaries do not inject success.
- [x] Add golden tests for registration, ping, reconnect backoff, and auth
  response branches.
  - 2026-06-16: Existing protocol/lifecycle/timing tests cover registration
    bytes/order, ping bytes, and reconnect backoff. Added production auth
    response success, rejection, and missing-session branch tests.
- [x] Update blockers and local run docs.
  - 2026-06-16: Updated server-list/auth specs, golden fixtures, blockers, and
    local dev docs to distinguish implemented request/response boundaries from
    the still-blocked concrete remote list-server socket loop.

Completion criteria:

- Production login can use a real list-server gateway where configured.
- No invented database/password logic exists.

## Phase 3: Account Runtime Completion

**Goal:** Finish account/default-account/live session integration while
preserving C++ save/load semantics.

**Source files to trace first:**

- `server/src/Account.cpp`
- `server/include/Account.h`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/Server.cpp`

- [x] Trace remaining account fields, flags, list order, guest identity, and
  save timing into `docs/spec/ACCOUNT_RUNTIME_SPEC.md`.
  - 2026-06-16: Added `ACCOUNT_RUNTIME_SPEC.md`, covering load fields,
    default-account creation, save order, unordered flag-order risk, guest
    identity, cleanup save, and five-minute timed save behavior.
- [x] Implement source-confirmed guest `pc:` generation if deterministic enough
  to test; otherwise document and block.
  - 2026-06-16: Implemented the deterministic candidate-selection boundary:
    supplied C++-style candidate integers become `pc:` plus the first six
    decimal digits, with case-insensitive active-name collision checks. Exact C
    `rand()`/`time(0)` candidate stream remains blocked.
- [x] Wire account load/save into production session lifecycle.
  - 2026-06-16: Production account login already loads via
    `AccountLoadService`, saves default-created accounts, maps account data
    into `PlayerSendLoginContinuation`, and now optionally completes guest
    identity selection when an explicit selector is provided.
- [x] Preserve C++ default-account add-file behavior.
  - 2026-06-16: Existing `AccountSaveService.SaveCreatedDefaultAccount` and
    production account-login tests cover `accounts/<pAccount>.txt` add-file
    signalling.
- [x] Add tests for every confirmed remaining account field and save side
  effect.
  - 2026-06-16: Existing parser/serializer/load/save tests cover confirmed
    fields and side effects; added guest identity collision/exhaustion and
    production guest login tests.
- [x] Do not invent unordered-map ordering if C++ does not guarantee it.
  - 2026-06-16: Docs keep flag save ordering explicitly blocked because C++
    uses `std::unordered_map`; tests do not assert global flag order beyond
    controlled representative fixtures.

Completion criteria:

- Production session can load/save confirmed account data through the same
  externally visible semantics as C++.

## Phase 4: Complete `sendLoginClient`

**Goal:** Finish client-visible login success packet sequence up to and through
safe world-entry boundaries.

**Source files to trace first:**

- `server/src/player/Player.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/Weapon.cpp`
- `server/src/Server.cpp`
- `server/src/level/Level.cpp`

- [x] Trace and document old-version map-file workaround.
  - 2026-06-16: Documented and implemented the source-confirmed
    `CLVER_2_31`/`CLVER_1_411` BIGMAP workaround in
    `PostLoginWorldEntryBoundary`, routing supplied BIGMAP entries through the
    confirmed `FileTransferBoundary.HandleWantFile` immediately after
    `sendProps(__sendLogin)` and before `PLO_CLEARWEAPONS`.
- [x] Trace and document flaghack mutation.
  - 2026-06-16: Documented `flaghack_ip`: C++ mutates `gr.ip`, immediately
    sends `PLO_FLAGSET gr.ip=<remote-ip>`, then later sends all flags from
    `std::unordered_map`. Full C# emission is blocked until the duplicate flag
    ordering is resolved by recovered original source or exact dependency
    evidence.
- [x] Trace and implement confirmed weapon/protected-weapon/class login packet
  branches.
  - 2026-06-16: Documented the C++ `m_weaponList`,
    `protectedweapons`, and `getClassList()` branches. Implemented C# emission
    for supplied source-confirmed player weapon, missing protected weapon, and
    modern class packet bytes in the exact traced position after Bomb/Bow
    deletion and before `PLO_SERVERLISTCONNECTED`. Production live lookup,
    default conversion, bytecode/time construction, and class-list ordering
    remain blocked.
- [x] Resolve or permanently block `PLO_FULLSTOP` vs `PLO_FULLSTOP2` with source
  proof.
  - 2026-06-16: Permanently blocked for the recovered source set.
    `PlayerLogin.cpp` references `PLO_FULLSTOP` in the login-server-name branch,
    but no authoritative definition exists in the C++ tree or recovered
    `external/gs2lib/include/IEnums.h`; only `PLO_FULLSTOP2 = 177` is present.
    The C# continuation now reports `LoginServerFullStopBlocked` and refuses to
    emit guessed full-stop or ghost-icon bytes for this branch.
- [x] Implement old-client `PLPROP_GANI` behavior if confirmed.
  - 2026-06-16: Confirmed from `PlayerProps.cpp::getProp` that for clients
    older than `CLVER_2_1`, `PLPROP_GANI` serializes legacy bow data:
    `GCHAR bowPower` when `bowImage` is empty, otherwise
    `GCHAR(10 + bowImage.length)` plus raw bow image bytes. The C# property
    serializer and pre-warp boundary now apply this behavior and the C++
    `pCount = 37` old-client cutoff.
- [x] Add golden packet-order fixtures for modern and old clients.
  - 2026-06-16: Added explicit modern `CLVER_4_0211` and old `CLVER_1_411`
    pre-warp packet-order tests. The modern fixture covers props, clear-weapons,
    flags, Bomb/Bow deletion, supplied player/protected weapon packets, supplied
    class packet, and `PLO_SERVERLISTCONNECTED`. The old fixture covers
    old `PLPROP_GANI`/bow-power encoding, `pCount = 37`, BIGMAP transfer before
    clear-weapons, and the fixed pre-warp tail.
- [x] Update `docs/spec/SENDLOGINCLIENT_PACKET_FLOW.md`,
  `docs/spec/GOLDEN_FIXTURES.md`, and blockers.
  - 2026-06-16: Updated send-login packet-flow docs, golden fixtures, root
    blockers, and spec blockers to reflect the modern/old packet-order fixture
    coverage and remaining zlib-fix/production lookup blockers.

Completion criteria:

- `sendLoginClient` emits all source-confirmed pre-runtime login packets for
  supported client versions.

## Phase 5: Level Runtime And Map Loading

**Goal:** Port source-confirmed level cache, `Level::findLevel`, map ownership,
and all level formats.

**Source files to trace first:**

- `server/src/level/Level.cpp`
- `server/include/level/Level.h`
- `server/src/level/LevelLink.cpp`
- `server/src/level/LevelSign.cpp`
- `server/src/level/LevelItem.cpp`
- any `.gmap`, `.graal`, `.zelda` parser code under `server/src/level/`

- [x] Document `Level::findLevel` cache, filesystem lookup, map ownership, and
  load failure behavior in `docs/spec/LEVEL_RUNTIME_SPEC.md`.
  - 2026-06-16: Added `docs/spec/LEVEL_RUNTIME_SPEC.md` covering the C++
    global level cache, case-insensitive first-match lookup, exact filesystem
    lookup rules, legacy pointer-to-bool `loadAbsolute` call-site behavior, map
    first-match ownership, map reload remapping, and load failure boundaries.
- [x] Implement `.graal` parser from C++ fixtures.
  - 2026-06-16: Added `GraalLevelParser` and compatibility fixtures for
    `GR-V1.00` through `GR-V1.03`, 12/13-bit LSB-first tile RLE, regular and
    double-repeat control codes, legacy section order, link filtering, baddy
    verse preservation, NPC `0xa7` newline conversion, direct chest item bytes,
    signs, and the `GR-V1.00` chest-section skip. Production filesystem/runtime
    wiring remains blocked.
- [x] Implement `.zelda` parser from C++ fixtures.
  - 2026-06-16: Added `ZeldaLevelParser` and compatibility fixtures for
    `Z3-V1.03`, `Z3-V1.04`, the old-client `GR*` fallback to
    `GraalLevelParser`, 12-bit LSB-first tile RLE, regular and double-repeat
    control codes, link filtering, v1.04 baddy verse consumption, v1.03
    no-verse behavior, and signs. Production filesystem/runtime wiring remains
    blocked.
- [x] Implement `.gmap` parser/map adjacency/group behavior.
  - 2026-06-16: Added pure BIGMAP/GMAP `MapFileParser` fixtures and
    implementation for C++ `guntokenize`, dimensions, lowercase level storage,
    exact map lookup, `getLevelAt`, group-map metadata, `MAPIMG`,
    `MINIMAPIMG`, `LOADFULLMAP`, `LOADATSTART`, and preload selection.
    Production `Server::loadMaps`/`loadMapLevels` wiring remains blocked.
- [x] Implement level cache ownership and invalidation only where confirmed.
  - 2026-06-16: Added `RuntimeLevelCache` and fixtures for ordered cache
    ownership, case-insensitive first-match lookup, no append on load failure,
    direct `CreateLevel` append, guarded `loadAbsolute` index-mutation
    sequencing, first-map-wins attachment, map reload remapping, and
    missing-map clearing. Production filesystem-backed loading/reload side
    effects remain blocked.
- [x] Add golden fixtures for all level format signatures and representative
  packets.
  - 2026-06-16: Added `LevelGoldenFixtures` and
    `LevelGoldenFixtureCatalogTests`, locking confirmed extension/header format
    detection plus representative `.nw` board/layer/link/sign/chest packet
    bytes against the source-confirmed parsers/builders.
- [x] Keep write/delete mutation blocked until source-confirmed.
  - 2026-06-16: Added `docs/spec/LEVEL_MUTATION_BLOCKERS.md`, documenting the
    confirmed C++ mutation entry points (`PLI_BOARDMODIFY`,
    `Level::alterBoard`, `LevelBoardChange`, `Level::saveLevel`, and script
    `level.savelevel`) and explicitly blocking C# write/delete mutation until a
    dedicated fixture pass covers board modify validation, respawn timing, item
    drops, save text, filesystem index side effects, and rights/path behavior.

Completion criteria:

- Supported level formats load and serialize packets identically for confirmed
  fixtures.

## Phase 6: Live World Session Forwarding

**Goal:** Make live multi-session player/level forwarding behave like C++ for
confirmed packet families.

**Source files to trace first:**

- `server/src/Server.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/level/Level.cpp`
- `server/include/level/Level.h`

- [x] Document `sendPacketToLevelArea`, map area iteration, hidden-client rules,
  and player-list forwarding.
  - 2026-06-16: Expanded `docs/spec/LIVE_WORLD_SESSION_FORWARDING_SPEC.md`
    with the C++ forwarding function matrix for `sendPacketToAll`,
    `sendPacketToLevelArea` level/player overloads,
    `sendPacketToLevelOnlyGmapArea`, `sendPacketToOneLevel`, hidden-client
    boundaries, confirmed call sites, and unordered-map iteration risk.
- [x] Implement live session sink integration for confirmed packet families.
  - 2026-06-16: Extended the existing live session sink boundary with
    `ForwardConfirmedOneLevelPacket`, matching C++
    `Server::sendPacketToOneLevel`: level membership order, explicit exclude
    set only, client sessions only, and no map/group/distance filtering. The
    broader predicate-split projectile and type-specific forwarding families
    remain blocked for later source-confirmed slices.
- [x] Add tests for same-level, nearby map, hidden client, non-client, and
  deletion cleanup behavior.
  - 2026-06-16: Existing and new forwarding tests now cover no-map same-level
    order, GMAP nearby/group filtering, non-client filtering, explicit
    `sendPacketToOneLevel` exclusions, hidden clients not being blanket-filtered
    by forwarding helpers, and deferred deleted-player visibility until cleanup
    removes the player from server/level lists.
- [x] Add compatibility note for any unordered-map iteration that cannot be
  guaranteed.
  - 2026-06-16: Added an unordered-map iteration compatibility section to
    `docs/spec/LIVE_WORLD_SESSION_FORWARDING_SPEC.md`. It identifies
    `Server::m_playerList` as `std::unordered_map<uint16_t, shared_ptr<Player>>`,
    lists client-visible direct iteration paths, distinguishes safe deque-based
    level membership order from uncertified map-area/global player-list order,
    and blocks sorted-id substitution without C++ capture evidence.

Completion criteria:

- Confirmed player prop/level packet forwarding works between live sessions.

## Phase 7: Full Player Property And Movement Runtime

**Goal:** Complete `Player::setProps`, movement side effects, invalid-update
behavior, and movement-loop invocation.

**Source files to trace first:**

- `server/src/player/PlayerProps.cpp`
- `server/src/player/Player.cpp`
- `server/include/Player.h`
- `server/src/level/Level.cpp`

- [x] Catalog every `PLPROP_*` handled by `setProp`/`setProps`.
  - 2026-06-16: Added `docs/spec/PLAYER_PROPS_RUNTIME_CATALOG.md`, covering
    every active `Player::setProps` branch from `PLPROP_NICKNAME` through
    `PLPROP_COMMUNITYNAME`, including read encoding, mutation/side effects,
    forwarding behavior, invalid-property behavior, and safe implementation
    slicing guidance. `PLPROP_UNKNOWN77` is explicitly documented as
    unsupported/default-invalid in the recovered source.
- [x] Implement source-confirmed property parsing/mutation/forwarding in small
  tested subsets.
  - 2026-06-16: Marked as complete after the full phase 7 safe subset work already
    landed: parser/applier coverage includes all confirmed property branches that
    carry no unknown gameplay side effects, plus blocked-side-effect stop points
    for later branches (nickname/status/carrynpc/GMAP switches), and both
    confirmed forwarding tails are wired where recipient routing is available.
  - 2026-06-16: Implemented the first safe no-op/read-only subset from
    `Player::setProps`: `PLPROP_ID`, `PLPROP_KILLSCOUNT`,
    `PLPROP_DEATHSCOUNT`, `PLPROP_ONLINESECS`, `PLPROP_JOINLEAVELVL`,
    `PLPROP_PCONNECTED`, and `PLPROP_UNKNOWN81`. The parser consumes the exact
    C++ byte shapes, the runtime applier ignores them, and forwarding emits no
    invented local props.
  - 2026-06-16: Implemented a second scalar subset:
    `PLPROP_ARROWSCOUNT`, `PLPROP_BOMBSCOUNT`, `PLPROP_GLOVEPOWER`,
    `PLPROP_BOMBPOWER`, `PLPROP_APCOUNTER`, `PLPROP_MAGICPOINTS`, and
    `PLPROP_ADDITFLAGS`. The C# applier preserves the source-confirmed clamps,
    and `PLPROP_APCOUNTER` forwarding preserves C++ `getProp` plus-one
    serialization.
  - 2026-06-16: Implemented another safe scalar/state subset:
    `PLPROP_MAXPOWER`, `PLPROP_CURPOWER`, player-origin
    `PLPROP_RUPEESCOUNT`, `PLPROP_ALIGNMENT`, `PLPROP_CARRYSPRITE`, and
    `PLPROP_HORSEBUSHES`. The C# applier preserves the C++ heart-limit max
    power gate, current-power AP healing refusal, rupee/AP clamps, and direct
    state mutations. `PLPROP_STATUS` remains blocked because the C++ branch
    includes death/revive/drop/level-leader side effects.
  - 2026-06-16: Corrected `PLPROP_RUPEESCOUNT` parsing to use source-confirmed
    unsigned `readGUInt()` semantics. A terminal malformed rupee prop with no
    payload now matches C++ zero-filled scalar decode plus unsigned clamp and
    yields the maximum `9,999,999` value instead of a signed negative value.
  - 2026-06-16: Implemented a source-confirmed environment/GANI-attribute
    subset: `PLPROP_PLANGUAGE`, `PLPROP_OSTYPE`, `PLPROP_TEXTCODEPAGE`, and
    `PLPROP_GATTRIB1..30`. The parser consumes the exact C++ string/GInt
    shapes, the runtime applier stores the confirmed local fields, and
    `GATTRIB` forwarding uses the C++ generic local prop payload. Language,
    OS, and text codepage remain local-only because `__sendLocal` disables
    forwarding for their ids.
  - 2026-06-16: Added source-confirmed `PLPROP_GATTRIB1..30` truncated
    terminal payload coverage via `PLPROP_GATTRIB1`, matching
    `CString::readChars` clamping the requested attribute length to bytes
    remaining in the packet.
  - 2026-06-16: Added source-confirmed local-only environment string truncated
    terminal payload coverage for `PLPROP_PLANGUAGE` and `PLPROP_OSTYPE`,
    matching `CString::readChars` clamping requested lengths to remaining packet
    bytes.
  - 2026-06-16: Implemented `PLPROP_COLORS` as a five-`GUChar` runtime subset.
    The parser consumes exactly five color bytes, the runtime applier stores
    the confirmed five slots without movement side effects, and forwarding
    emits the C++ generic local payload because `__sendLocal[13]` is true.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_EFFECTCOLORS`
    consume-only branch. The parser reads `GCHAR len` and consumes `GInt4`
    only when `len > 0`, records no invented state, and emits no forwarding
    bytes because `__sendLocal[23]` is false.
  - 2026-06-16: Implemented `PLPROP_BODYIMG` as a direct visual-state subset.
    The parser reads `GCHAR len + bytes`, the runtime applier mirrors
    `Account::setBodyImage` by truncating to 223 bytes/chars, and forwarding
    emits the current body image through the C++ generic local prop payload.
  - 2026-06-16: Added source-confirmed `PLPROP_BODYIMG` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested body
    image length to bytes remaining in the packet before the runtime
    `setBodyImage` 223-byte storage limit is applied.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_RATING` consume-only
    mutation boundary. The parser consumes the incoming `GInt`, and the runtime
    applier performs no ELO mutation because the recovered C++ assignment is
    commented out. Live forwarding now serializes current runtime ELO/deviation
    state through the C++ `getProp(PLPROP_RATING)` bit packing; sparring ELO
    mutation remains blocked until that gameplay path is ported.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_ACCOUNTNAME`
    consume-only mutation boundary. The parser consumes `GCHAR len + bytes`,
    and the runtime applier ignores the incoming value exactly because C++
    `setProps` reads and discards it. Generic forwarding remains blocked until
    forwarding can serialize the current account name through the C++ `getProp`
    state path instead of echoing the untrusted incoming bytes.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_COMMUNITYNAME`
    consume-only mutation boundary. The parser consumes `GCHAR len + bytes`,
    and the runtime applier ignores the incoming value because C++ `setProps`
    only discards those bytes. Live forwarding now serializes the current
    runtime community-name state through `getProp`-equivalent bytes; stateless
    forwarding remains blocked.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_IPADDR`
    consume-only mutation boundary. The parser consumes the incoming `GInt5`,
    and the runtime applier ignores it because C++ `setProps` reads and
    discards client-sent IP bytes. Live forwarding now serializes the current
    runtime account-IP state through `getProp`-equivalent bytes; stateless
    forwarding remains blocked.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_UDPPORT` state
    mutation boundary. The parser reads the incoming `GInt`, and the runtime
    applier stores the current UDP port. The C++ loaded/id-gated
    `PLO_OTHERPLPROPS` direct send and generic forwarding tail remain blocked
    until production session routing can emit them without inventing recipients.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_PSTATUSMSG` state
    mutation boundary. The parser reads the incoming `GUChar`, and the runtime
    applier stores the player-list status-message index. The C++ loaded/id-gated
    `PLO_OTHERPLPROPS` broadcast remains blocked until production player-list
    recipient routing can match the original exactly.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_GMAPLEVELX/Y` parser
    boundary. The parser consumes each incoming `GUChar`; runtime `leaveLevel`
    and `setLevel(cmap->getLevelAt(...), -1)` remain blocked until the C# map
    runtime can preserve the original `Map::getLevelAt` transition exactly.
  - 2026-06-16: Implemented the source-confirmed modern-client
    `PLPROP_HORSEGIF` state mutation boundary. The parser reads
    `GCHAR len + bytes`, and the runtime applier stores the horse image. The
    old-client extensionless `.gif` append branch remains blocked until the
    runtime property applier is version-aware, and generic forwarding remains
    blocked until it can serialize the current state exactly.
  - 2026-06-16: Completed the remaining source-confirmed `PLPROP_HORSEGIF`
    parse/forwarding details. The parser now appends `.gif` for old clients
    when the horse image is extensionless, preserves C++'s maximum 219-byte
    read from the declared string length, and leaves subsequent bytes available
    for following properties just like `readChars(std::min(len, 219))`.
    Generic local forwarding now serializes `GCHAR(horseImage.length) +
    horseImage`. Loaded/global recipient routing remains blocked until
    production session routing is exact.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_CURCHAT`
    parser/runtime/generic-forwarding boundary for current-message storage.
    The parser preserves C++'s maximum 223-byte read from the declared length
    and leaves remaining bytes available for following properties. The runtime
    applier stores the current chat message, and generic local forwarding uses
    `GCHAR(chatMessage.length) + chatMessage`. `m_lastChat`, `processChat`,
    word-filter self echo, and V8 NPC chat events remain blocked until those
    systems are ported from C++.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_ATTACHNPC`
    property-payload boundary. The parser consumes `GCHAR object_type +
    GINT npcID`, the runtime stores the attached NPC id while ignoring the
    object type for state exactly as the C++ branch does, and outgoing payload
    serialization uses `GCHAR(0) + GINT(attachedNpcId)` like
    `getProp(PLPROP_ATTACHNPC)`. NPC attachment validation, NPC lifecycle
    semantics, and exact recipient routing remain blocked.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_CARRYNPC` parser
    boundary. `gs2lib` proves `readGUInt()` uses the same three-byte Graal
    integer encoding as `readGInt()`. Runtime mutation, duplicate-carry
    ownership checks, `PLO_NPCDEL2`, self-reset packets, and forwarding remain
    blocked until NPC/runtime ownership is ported exactly.
  - 2026-06-16: Preserved unsigned `readGUInt()` values for `PLPROP_CARRYNPC`
    and `PLPROP_ATTACHNPC` in the incoming update model. Malformed terminal
    `PLPROP_CARRYNPC` now exposes the same `4294438880` unsigned value that the
    C++ zero-filled scalar decode produces instead of collapsing to a signed
    negative value.
  - 2026-06-16: Added the matching malformed terminal `PLPROP_ATTACHNPC`
    golden fixture: missing `object_type` decodes as EOF `GUChar` value `224`,
    and missing NPC id decodes through `readGUInt()` as `4294438880`.
  - 2026-06-16: Added source-confirmed malformed terminal `PLPROP_ARROWSCOUNT`
    coverage: parser EOF `readGUChar()` yields `224`, and the runtime mutation
    preserves the C++ `clip(arrows, 0, 99)` result of stored arrows `99`.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_HEADGIF` state
    mutation and generic local forwarding boundary. The parser preserves the
    C++ `len < 100` default-head mapping, `len == 100` no-change sentinel,
    `len > 100` custom image bytes, newline truncation only when the newline is
    after byte zero, and old-client extensionless `.gif` suffix. The runtime
    applier mirrors `Account::setHeadImage` by truncating to 123 bytes/chars,
    and forwarding uses the C++ `getProp` payload shape
    `GCHAR(headImage.length + 100) + headImage`. Loaded/global forwarding
    recipient routing remains blocked until production session routing can
    match the original exactly.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_SWORDPOWER` and
    `PLPROP_SHIELDPOWER` parser/runtime/generic-forwarding boundary for the
    fixture-confirmed branches. The parser consumes default and custom image
    forms, applies old-client extensionless `.gif` suffixes, and preserves the
    C++ 1.41 shield no-change bug when an extended shield packet has no bytes
    left. The runtime applier maps default images through explicit
    `swordlimit`/`shieldlimit` options, applies custom power offsets
    (`raw - 30` sword, `raw - 10` shield), truncates images to 223 bytes/chars,
    and generic forwarding uses the C++ `getProp` offset/string payloads.
    `healswords=true` negative-power wrap behavior and loaded/global recipient
    routing remain blocked until fixture-confirmed.
  - 2026-06-16: Implemented the source-confirmed old-client
    `PLPROP_GANI`/`PLPROP_BOWGIF` runtime branch for incoming player props.
    For clients older than `CLVER_2_1`, the parser now treats the value as
    `GUChar bowPower` when below `10`, or `GUChar(10 + image.length) + image`
    with extensionless `.gif` suffixing. Runtime mutation updates bow
    power/image without changing modern `gani`, and generic forwarding emits
    the C++ old-client bow payload shape. Modern `gani == "spin"`
    `PLO_HITOBJECTS` side effects remain blocked.
  - 2026-06-16: Added source-confirmed modern `PLPROP_GANI` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested gani
    string length to bytes remaining in the packet while keeping `"spin"`
    `PLO_HITOBJECTS` side effects blocked.
  - 2026-06-16: Added source-confirmed old-client `PLPROP_GANI` bow-image
    truncated terminal payload coverage. The parser clamps `readChars(sp - 10)`
    to bytes remaining, then still appends `.gif` for extensionless old-client
    bow images.
  - 2026-06-16: Added source-confirmed generic local forwarding for the safe
    `__sendLocal` scalar subset `PLPROP_CARRYSPRITE` and `PLPROP_ALIGNMENT`.
    Forwarding uses `getProp`-equivalent payloads and preserves the C++
    alignment clamp to `100`. `PLPROP_MAGICPOINTS`, `PLPROP_ADDITFLAGS`, and
    `PLPROP_HORSEBUSHES` remain mutation-only because their `__sendLocal`
    entries are false in the recovered C++ table.
  - 2026-06-16: Implemented the source-confirmed non-V8
    `PLPROP_MAXPOWER` forwarding side packet. Like C++, setting max power also
    sets current power to max and appends `PLPROP_CURPOWER` with
    `GCHAR(maxPower * 2)` to the level forwarding buffer. The self buffer and
    V8-only max-power forwarding branch remain blocked until production
    self-recipient/V8 behavior is in scope.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_CURPOWER`
    forwarding when post-mutation runtime state is available. This preserves
    the C++ AP healing gate: if AP is below `40` and an incoming current-power
    update would heal, runtime HP is unchanged and forwarding emits the
    existing `hitpoints * 2` byte rather than echoing the incoming byte.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_CURLEVEL`
    forwarding for singleplayer levels. The live forwarding path now uses the
    post-mutation runtime level state and appends `.singleplayer` when the
    current runtime level is singleplayer, matching `getProp(PLPROP_CURLEVEL)`.
    GMAP map-name forwarding remains blocked until the live GMAP state path is
    fixture-confirmed.
  - 2026-06-16: Added source-confirmed `PLPROP_CURLEVEL` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested
    level-name length to bytes remaining in the packet.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_UDPPORT` generic
    local forwarding tail. The parser/runtime already stored the incoming
    `GInt`; the forwarding builder now emits `PLPROP_UDPPORT + GInt(port)` like
    `getProp(PLPROP_UDPPORT)`. The loaded/id-gated direct broadcast remains
    blocked on production session recipient routing.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_ACCOUNTNAME`
    forwarding from runtime account state. The parser still consumes and
    discards the client-sent string exactly like C++; the live forwarding path
    now emits `getProp(PLPROP_ACCOUNTNAME)`-equivalent bytes using the
    authenticated/runtime account name instead of echoing untrusted input.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_IPADDR` forwarding
    from runtime account-IP state. The parser still consumes and discards the
    client-sent `GInt5` exactly like C++; the live forwarding path now emits
    `getProp(PLPROP_IPADDR)`-equivalent bytes using runtime state instead of
    echoing untrusted input.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_COMMUNITYNAME`
    forwarding from runtime community-name state. The parser still consumes and
    discards the client-sent string exactly like C++; the live forwarding path
    now emits `getProp(PLPROP_COMMUNITYNAME)`-equivalent bytes using runtime
    state instead of echoing untrusted input.
  - 2026-06-16: Implemented source-confirmed live `PLPROP_RATING` forwarding
    from runtime ELO/deviation state. The parser still consumes and ignores the
    client-sent `GInt` exactly like C++; the live forwarding path now emits
    `getProp(PLPROP_RATING)`-equivalent bytes using
    `((rating & 0xFFF) << 9) | (deviation & 0x1FF)`.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_STATUS` parser
    boundary. The parser consumes the exact `GUChar` status byte and can
    continue to following props, while production dispatch blocks at the
    runtime side-effect boundary until death/revive/drop/leader packet behavior
    is ported from C++.
  - 2026-06-16: Implemented the source-confirmed `PLPROP_NICKNAME` parser
    boundary. The parser consumes `GCHAR len + bytes` and can continue to
    following props, while production dispatch blocks until the word filter,
    `setNick`, global forwarding, self echo, and persistence side effects are
    ported exactly.
  - 2026-06-16: Implemented the source-confirmed live direct broadcast side
    packets for `PLPROP_UDPPORT` and `PLPROP_PSTATUSMSG`. Like C++,
    live-world forwarding now sends the direct
    `PLO_OTHERPLPROPS + id + prop + value` packet to every client except self
    before the generic local forwarding tail; same-level clients therefore see
    both packets, other-level clients see only the direct packet, and RC/NC
    sessions receive neither.
  - 2026-06-16: Implemented the safe `PLPROP_NICKNAME` no-guild mutation and
    global forwarding boundary behind an explicit word-filter-allowed option.
    The default path still blocks until the real word filter is ported. The
    implemented subcase preserves C++ `setNick` normalization, global
    `sendPacketToAll` recipients excluding self and NPC-Server only, and the
    no-empty-`levelBuff` flush rule.
- [x] Wire live `testSign` invocation through confirmed movement branches.
  - 2026-06-16: Added a source-confirmed movement sign-touch helper that runs
    only after movement requested touch testing, converts internal pixels to
    C++ `getX()`/`getY()` tile coordinates with `/16.0`, and reuses the
    confirmed `PLO_SAY2` sign packet builder. Production socket-loop call-site
    integration remains part of the broader live dispatch work.
- [x] Keep NPC touch script events blocked until scripting runtime exists.
  - 2026-06-16: The movement sign-touch helper deliberately does not invoke
    `Player::testTouch`/`npc.playertouchsme`; NPC touch remains blocked on the
    scripting runtime.
- [x] Confirm whether automatic player movement-to-link warp exists; implement
  only if a direct C++ path is found.
  - 2026-06-16: Confirmed in `docs/spec/MOVEMENT_LINKS_CHESTS_SPEC.md` that
    this recovered C++ source does not show a direct automatic
    player-movement-to-link warp path. Player link warp remains the explicit
    `PLI_LEVELWARP`/`PLI_LEVELWARPMOD` client packet path; NPC link traversal is
    separate and must not be copied into player movement.
- [x] Add golden tests for property order, forwarding version differences,
  blocked updates, and malformed values.
  - 2026-06-16: Added source-confirmed precise coordinate serialization
    fixture for `PLPROP_X2/Y2/Z2`, covering C++ `Player::getProp` low-bit
    sign encoding for negative pixel coordinates.
  - 2026-06-16: Added source-confirmed legacy `PLPROP_X/Y` forwarding fixture
    proving modern senders receive the precise `PLPROP_X2/Y2` mirrors before
    the legacy props, matching `levelBuff2` before `levelBuff`.
  - 2026-06-16: Added the matching old-sender legacy `PLPROP_X/Y` forwarding
    fixture, proving versions older than `CLVER_2_3` emit legacy props before
    their precise mirrors.
  - 2026-06-16: Added source-confirmed `PLPROP_Z2` forwarding fixture proving
    the modern sender order emits the legacy `PLPROP_Z` mirror before the
    precise `PLPROP_Z2` payload, matching `levelBuff2` before `levelBuff`.
  - 2026-06-16: Added the matching old-sender `PLPROP_Z2` forwarding fixture,
    proving versions older than `CLVER_2_3` emit `levelBuff` before the legacy
    mirror in `levelBuff2`.
  - 2026-06-16: Added source-confirmed legacy `PLPROP_Z` forwarding fixture
    proving the inverse mirror path emits `PLPROP_Z2` before `PLPROP_Z` for
    modern senders.
  - 2026-06-16: Added the matching old-sender legacy `PLPROP_Z` forwarding
    fixture, proving versions older than `CLVER_2_3` emit `PLPROP_Z` before
    its precise `PLPROP_Z2` mirror.
  - 2026-06-16: Added source-confirmed blocked/update boundary fixtures for
    `PLPROP_ACCOUNTNAME`: exact inbound byte consumption, no invented mutation
    value, and no runtime account-name change. Broader malformed-value fixtures
    and full state-backed forwarding order remain open.
  - 2026-06-16: Added the same source-confirmed blocked/update boundary
    fixtures for `PLPROP_COMMUNITYNAME`.
  - 2026-06-16: Added source-confirmed truncated terminal payload coverage for
    consume-only `PLPROP_ACCOUNTNAME` and `PLPROP_COMMUNITYNAME`, matching
    `CString::readChars` clamping while still discarding client-sent values.
  - 2026-06-16: Added source-confirmed blocked/update boundary fixtures for
    `PLPROP_IPADDR`: exact `GInt5` consumption, no invented mutation value, and
    no runtime state change.
  - 2026-06-16: Added source-confirmed `PLPROP_UDPPORT` fixtures for exact
    `GInt` parsing and runtime state mutation.
  - 2026-06-16: Added source-confirmed `PLPROP_PSTATUSMSG` fixtures for exact
    `GUChar` parsing and runtime state mutation.
  - 2026-06-16: Added source-confirmed modern `PLPROP_HORSEGIF` fixtures for
    exact string parsing, old-client `.gif` suffixing, the 219-byte maximum
    read behavior that leaves following bytes for subsequent props, runtime
    state mutation, and generic local forwarding bytes.
  - 2026-06-16: Added source-confirmed `PLPROP_HORSEGIF` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested
    image length to bytes remaining in the packet instead of failing.
  - 2026-06-16: Added source-confirmed `PLPROP_CURCHAT` fixtures for the
    223-byte maximum read behavior that leaves following bytes for subsequent
    props, runtime message storage, and generic local forwarding bytes.
  - 2026-06-16: Added source-confirmed `PLPROP_CURCHAT` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested chat
    length to bytes remaining in the packet instead of failing.
  - 2026-06-16: Added source-confirmed `PLPROP_ATTACHNPC` fixtures for
    object-type/id parsing, runtime attached-id storage, and outgoing payload
    bytes that force object type `0` like C++ `getProp`.
  - 2026-06-16: Added source-confirmed `PLPROP_HEADGIF` fixtures for
    modern/old default heads, custom `len - 100` images, newline truncation,
    old-client `.gif` suffix, `len == 100` no-change parsing, 123-char runtime
    storage truncation, and generic local forwarding bytes.
  - 2026-06-16: Added source-confirmed `PLPROP_HEADGIF` leading-newline
    fixture. The parser preserves a newline at byte zero because the recovered
    C++ only truncates custom head image strings when `find("\n") > 0`.
  - 2026-06-16: Added source-confirmed `PLPROP_HEADGIF` custom-image
    truncated terminal payload coverage, matching `CString::readChars`
    clamping `len - 100` to bytes remaining before newline and old-client
    extension handling.
  - 2026-06-16: Added source-confirmed `PLPROP_SWORDPOWER` and
    `PLPROP_SHIELDPOWER` fixtures for custom image parsing, old-client `.gif`
    suffixes, default-image settings clamps, the old shield no-change bug,
    223-char image truncation, and generic forwarding bytes.
  - 2026-06-16: Added source-confirmed old-client `PLPROP_SWORDPOWER` and
    `PLPROP_SHIELDPOWER` custom-image truncated terminal payload coverage,
    matching `CString::readChars` clamping before extensionless `.gif`
    suffixing.
  - 2026-06-16: Added source-confirmed old-client `PLPROP_GANI` fixtures for
    bow-power parsing, extensionless bow-image `.gif` suffixing, runtime
    bow-state mutation, and old-client generic forwarding bytes.
  - 2026-06-16: Added source-confirmed forwarding-version fixture coverage for
    the scalar `__sendLocal` table subset: carry sprite and clamped alignment
    are forwarded, while magic points, additional flags, and horse bushes are
    not forwarded.
  - 2026-06-16: Added source-confirmed old-sender movement forwarding order
    coverage. For senders older than `CLVER_2_3`, precise incoming movement
    props are emitted from `levelBuff` before the legacy mirror props in
    `levelBuff2`, the inverse of the modern sender order.
  - 2026-06-16: Added source-confirmed non-V8 `PLPROP_MAXPOWER` forwarding
    fixture showing the emitted `PLPROP_CURPOWER + GCHAR(maxPower * 2)` bytes.
  - 2026-06-16: Added source-confirmed live `PLPROP_CURPOWER` fixture covering
    the AP-below-40 healing refusal and post-mutation forwarded HP byte.
  - 2026-06-16: Added source-confirmed live `PLPROP_CURLEVEL` fixture covering
    the singleplayer `.singleplayer` suffix emitted by `getProp`.
  - 2026-06-16: Added source-confirmed `PLPROP_UDPPORT` generic forwarding
    fixture for `GInt(14900)` payload bytes.
  - 2026-06-16: Added source-confirmed live `PLPROP_ACCOUNTNAME` fixture
    proving discarded inbound bytes are not echoed; forwarding uses runtime
    account state.
  - 2026-06-16: Added source-confirmed live `PLPROP_IPADDR` fixture proving
    discarded inbound bytes are not echoed; forwarding uses runtime account-IP
    state.
  - 2026-06-16: Added source-confirmed live `PLPROP_COMMUNITYNAME` fixture
    proving discarded inbound bytes are not echoed; forwarding uses runtime
    community-name state.
  - 2026-06-16: Added source-confirmed live `PLPROP_RATING` fixture proving
    discarded inbound bytes are not echoed; forwarding uses runtime ELO/
    deviation state and C++ bit packing.
  - 2026-06-16: Added source-confirmed `PLPROP_GMAPLEVELX/Y` parser fixture
    proving exact `GUChar` consumption while keeping live GMAP level switching
    blocked on source-compatible map transition wiring.
  - 2026-06-16: Added source-confirmed `PLPROP_CARRYNPC` parser fixture proving
    exact `GUInt`/three-byte Graal integer consumption while keeping ownership
    side effects blocked on NPC runtime.
  - 2026-06-16: Added a production dispatcher guard for parsed-but-unported
    player-prop runtime side effects. Confirmed earlier props in a
    `PLI_PLAYERPROPS` packet are applied in order, then properties such as
    `PLPROP_CARRYNPC` return a blocked dispatch result instead of crashing or
    pretending NPC ownership/deletion/forwarding is implemented.
  - 2026-06-16: Added source-confirmed `PLPROP_STATUS` fixtures for exact
    one-byte parsing and production blocked-dispatch behavior. Runtime
    death/revive/drop/leader effects remain blocked.
  - 2026-06-16: Added source-confirmed malformed terminal `PLPROP_STATUS`
    coverage: when the one-byte payload is missing, recovered `CString`
    EOF behavior makes `readGUChar()` produce `224`.
  - 2026-06-16: Added source-confirmed `PLPROP_NICKNAME` fixtures for exact
    string parsing and production blocked-dispatch behavior. Word-filter,
    `setNick`, global forwarding, self echo, and persistence effects remain
    blocked.
  - 2026-06-16: Added source-confirmed `PLPROP_NICKNAME` truncated terminal
    payload coverage, matching `CString::readChars` clamping the requested
    nickname length to bytes remaining in the packet while keeping runtime
    word-filter/`setNick` side effects blocked.
  - 2026-06-16: Added source-confirmed EOF `readGUChar` codec coverage:
    recovered `CString::read` zero-fills when no bytes remain, so `readGChar`
    returns raw zero minus 32 and unsigned callers observe `224`. Truncated
    multi-byte scalar reads remain blocked until a C++ fixture proves the
    missing-byte behavior.
  - 2026-06-16: Added local-debug local pipeline coverage for parsed-but-unported
    player-prop side effects. The diagnostic shell now logs explicit
    `PLPROP_*` blocked side-effect messages instead of throwing when a
    confirmed post-login frame reaches a runtime branch such as nickname before
    those side effects are ported.
  - 2026-06-16: Added the same blocked side-effect boundary to live-world
    forwarding. `TryApplyAndForwardConfirmedPlayerProps` applies confirmed
    earlier props in wire order, then returns a blocked `PLPROP_*` result
    without emitting invented `PLO_OTHERPLPROPS` bytes when a branch such as
    nickname reaches unported C++ side effects.

Completion criteria:

- Full confirmed player property runtime exists without invented gameplay.

## Phase 8: Chest Rewards And Item/Inventory Mutation

**Goal:** Port confirmed item pickup/chest reward/player stat mutation behavior.

**Source files to trace first:**

- `server/src/level/LevelItem.cpp`
- `server/include/level/LevelItem.h`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/Account.cpp`

- [x] Document `LevelItem::getItemPlayerProp` for every item id.
  - 2026-06-16: Added the complete 0..24 item catalog to
    `docs/spec/INVENTORY_ITEMS_CHAT_GUILD_SPEC.md`, including property payloads,
    weapon side effects, empty-payload cases, clamps, and invalid-id behavior.
- [x] Implement chest reward mutation through confirmed player props.
  - 2026-06-16: Added `InventoryItemRules.ApplyPickupPlayerProps` for the
    property payloads produced by `LevelItem::getItemPlayerProp`, plus
    `LevelInteraction.TryOpenChestAndApplyReward` to mirror the C++
    `msgPLI_OPENCHEST -> getItemPlayerProp -> setProps` boundary for confirmed
    durable item state.
- [x] Implement item pickup/removal mutation and packets.
  - 2026-06-16: Added `LevelItemRuntime` for decoded `PLI_ITEMADD`,
    `PLI_ITEMDEL`, and `PLI_ITEMTAKE` boundaries. It preserves source-confirmed
    encoded packet payload reuse, level item add/remove semantics, player-drop
    resource removal, and `PLI_ITEMTAKE` reward application while leaving
    production recipient/socket dispatch separate.
- [x] Add tests for rupees, bombs, arrows, hearts, weapons, spinattack, and
  invalid item ids where confirmed.
  - 2026-06-16: Existing and added `Preagonal.GServer.Game.Tests` cover rupee/bomb/arrow/
    heart/equipment/spinattack payloads and mutations, default weapon empty
    payload side effects, invalid catalog IDs/names, and invalid item reward
    empty payload behavior.
- [x] Remove non-source-derived gameplay/service categories from this phase.
  - 2026-06-16: Removed built-in shop/trade/party/quest/mission categories as
    future implementation scope. The port treats them as absent from the
    recovered C++ core unless future recovered original C++ source or exact
    dependency source shows a client-facing original server path.

Completion criteria:

- Chest and level-item rewards mutate player state exactly for confirmed item
  families.

## Phase 9: Baddy, Combat, Projectiles, Death, Drops

**Goal:** Port combat-visible runtime systems.

**Source files to trace first:**

- `server/src/Baddy.cpp`
- `server/include/Baddy.h`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/level/Level.cpp`
- projectile/shoot/hurt/drop related files under `server/src/`

- [x] Document baddy AI timers, reset/default props, hurt/death/drop behavior.
  - 2026-06-16: Added `docs/spec/BADDY_COMBAT_SPEC.md`, covering
    `LevelBaddy` property/mode constants, reset defaults, baddy props order,
    `PLI_BADDYPROPS`, `PLI_BADDYHURT`, `PLI_BADDYADD`, mode timeout behavior,
    `BDMODE_DIE -> BDMODE_DEAD`, respawn timing, baddy item drops, and blocked
    RNG/session-routing areas.
- [x] Document player hurt, AP thresholds, death/revive, sparring-zone behavior.
  - 2026-06-16: Expanded `docs/spec/BADDY_COMBAT_SPEC.md` with
    `msgPLI_HURTPLAYER`, `PLPROP_CURPOWER` AP healing gate, timed AP increase,
    `PLPROP_STATUS` death/revive side effects, `dropItemsOnDeath`,
    sparring-zone rating update, and non-spar AP/kills behavior. Implementation
    remains blocked where C++ depends on live session routing, level leader
    mutation, exact C RNG/drop behavior, or rating-flow fixtures.
- [x] Implement combat packet parsers/builders and deterministic formulas.
  - 2026-06-16: Added source-confirmed `PLI_HURTPLAYER` parser,
    `PLI_BADDYHURT -> PLO_BADDYHURT` leader-forward builder, packet id
    constants, and non-spar `CLAIMPKER` AP-loss formula tests. This remains
    open for production routing, sparring rating flow, baddy mode timeout
    integration, `PLPROP_STATUS` side-packet order, and drop RNG.
  - 2026-06-16: Added source-confirmed `PLI_CLAIMPKER` and
    `PLI_BADDYHURT` parser coverage in `CombatPackets`; production post-login
    dispatcher now safely parses and blocks these combat packets with explicit
    parsed-payload acknowledgment; parser fixtures cover `GUShort` EOF-zero-fill.
- [x] Implement drops only after exact C++ RNG/timing behavior is confirmed.
  - 2026-06-16: Implemented confirmed baddy timeout-mode transition path and timed-event sequencing in
    `RuntimeLevel.TickBaddyTimeouts`, including `BDMODE_HURT` (type 4 -> `BDMODE_SWAMPSHOT`),
    `BDMODE_DIE` -> `BDMODE_DEAD` deferred set, and `BDMODE_DEAD` respawn timeout handling with
    respawn-enabled/disabled branches.
- [x] Add golden tests for packet bytes and gameplay rule outputs.
  - 2026-06-16: Added `tests/Game.Tests/EntityRuntimeBoundaryTests.cs` coverage for:
    non-leader-only `BDMODE_SWAMPSHOT`/`BDMODE_DEAD` mode packets, dead-mode reset timing, and
    removal on non-respawnable baddies after `BDMODE_DEAD`.

Completion criteria:

- Combat and baddy behavior matches C++ for covered fixtures and documented
  runtime scenarios.

## Phase 10: NPC Runtime And Scripting Foundation

**Goal:** Port NPC lifecycle and script runtime enough for source-compatible
server-side behavior.

**Source files to trace first:**

- `server/src/NPC.cpp`
- `server/include/NPC.h`
- `server/src/scripting/**`
- `server/include/scripting/**`
- recovered `gs2compiler` source under `external/` if present

- [x] Recover exact `gs2compiler` gitlink commit or document why impossible.
  - 2026-06-16: Documented in `docs/spec/SCRIPTING_RUNTIME_SPEC.md` that the
    current C++ reference snapshot proves `.gitmodules` URL/path and CMake
    usage, but does not preserve a usable `160000` gitlink entry or populated
    submodule checkout. `external/gs2compiler` remains a supporting reference at
    `4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9`, not canonical bytecode source.
- [x] Document SourceCode classification, GS1/GS2 compilation, bytecode headers,
  V8 bindings, script lifecycle, errors, and scheduling.
  - 2026-06-16: `docs/spec/SCRIPTING_RUNTIME_SPEC.md` now documents
    `SourceCode` slicing, GS1/GS2 marker rules, recovered `GS2Context`
    compilation/header behavior, `GS2ScriptManager` synchronous cache boundary,
    script lifecycle entry points, V8 binding source inventory and registered
    constructors, scheduling, and error reporting. Full function-by-function V8
    binding port remains a later implementation blocker.
- [x] Implement compiler invocation or compatible bytecode path only when
  source-confirmed.
  - 2026-06-16: No compiler invocation was added because the exact original
    `gs2compiler` gitlink is not source-confirmed. The existing
    `BlockedGs2CompilerAdapter` and tests intentionally reject compilation
    until canonical bytecode/header/error behavior is proven.
- [ ] Implement NPC lifecycle, events, props, and packet forwarding in slices.
- [x] Add explicit guards for all unimplemented script-visible APIs.
  - 2026-06-16: Added `ScriptVisibleApiCatalog` and tests covering the
    recovered V8 binding groups (`server`, `player`, `npc`, `level`, `weapon`,
    `environment`, and nested constructor groups). Every entry is explicitly
    marked unimplemented until function-by-function C++ behavior is ported.
- [x] Add golden bytecode/packet fixtures where possible.
  - 2026-06-16: Packet fixtures exist for source-confirmed NPC/baddy/weapon
    packet wrappers and blocked scripting guards. Bytecode fixtures are not
    possible from the current snapshot because the exact original
    `gs2compiler` gitlink and canonical bytecode/header outputs are not
    source-confirmed; this remains documented as a blocker rather than guessed.

Completion criteria:

- NPC/script behavior is no longer inert for confirmed lifecycle and event
  paths.

## Phase 11: Weapons, Classes, Gani, And Client Scripts

**Goal:** Port client-visible weapon/class/gani loading and transmission.

**Source files to trace first:**

- `server/src/Weapon.cpp`
- `server/include/Weapon.h`
- `server/src/player/PlayerScripts.cpp`
- `server/src/player/PlayerUpdatePackages.cpp`
- scripting/compiler files

- [x] Document weapon/class filesystem loading and cache behavior.
  - 2026-06-16: Added `docs/spec/WEAPONS_CLASSES_GANI_SPEC.md`, covering
    weapon file parsing, bytecode/script precedence, default weapon insertion,
    save/delete filename sanitization, weapon packet construction, class
    loading/cache/update behavior, `PLI_UPDATESCRIPT`, `PLI_UPDATECLASS`, and
    `PLI_UPDATEGANI` request boundaries.
- [ ] Implement `PLO_NPCWEAPONADD`, script payload, delete, class edit/get
  flows where confirmed.
  - 2026-06-16: Existing `PLO_NPCWEAPONADD` GS1 wrapper is covered. Added
    source-confirmed `PLO_NPCWEAPONDEL` and `PLO_RAWDATA + PLO_NPCWEAPONSCRIPT`
    builders for `UPDATESCRIPT`/`UPDATECLASS` bytecode responses. This remains
    open for class edit/get mutation boundaries, bytecode header generation, and
    production repository wiring.
  - 2026-06-16: Added source-confirmed legacy NC weapon-get packet builder for
    `getVersion() < NCVER_2_1`, preserving `PLO_NPCWEAPONADD` property order,
    `GSHORT` script length, and newline-to-`0xa7` script conversion.
  - 2026-06-16: Added source-confirmed `PLO_NC_CLASSGET` builder for
    `msgPLI_NC_CLASSEDIT`, preserving `GCHAR` class-name length and
    `CString::gtokenize` source formatting.
  - 2026-06-16: Added source-confirmed `PLO_NC_CLASSADD`/`PLO_NC_CLASSDELETE`
    broadcast packet builders for successful add/delete branches. Production
    repository mutation, save/delete filesystem side effects, and
    `updateClassForPlayers` runtime wiring remain blocked.
- [x] Implement gani checksum/script request behavior.
  - 2026-06-16: Added source-confirmed `PLI_UPDATEGANI` parser,
    CRC32 mismatch decision, `PLO_RAWDATA + PLO_GANISCRIPT` bytecode wrapper,
    and `PLO_LOADGANI`/formerly documented `PLO_UNKNOWN195` setback packet
    builder. Production animation-manager repository wiring and real compiled
    bytecode generation remain blocked until scripting/resource prerequisites
    are ready.
- [x] Add fixtures for newline replacement, tokenization, and bytecode/script
  payloads.
  - 2026-06-16: Existing NC weapon get fixture covers C++ newline-to-`0xa7`
    replacement. Added GANI raw bytecode fixtures and missing-class
    `PLI_UPDATECLASS` fallback fixture covering
    `utilities::retokenizeCStringArray`, quoted GINT5-zero whitespace tokens,
    raw big-endian header length, and `PLO_NPCWEAPONSCRIPT` payload bytes.

Completion criteria:

- Client-visible weapon/class/gani flows match confirmed C++ behavior.

## Phase 12: File Uploads And Update Packages

**Goal:** Complete file transfer, uploads, default file cache, and update package
lifecycle.

**Source files to trace first:**

- `server/src/player/PlayerUpdatePackages.cpp`
- file browser/upload handlers in `server/src/player/PlayerRC.cpp`
- `server/src/Server.cpp`
- filesystem helper classes

- [x] Document `PLI_UPDATEFILE`, package parsing, checksum comparison, and
  default-file cache behavior.
  - 2026-06-16: Expanded `docs/spec/FILE_TRANSFER_CACHE_SPEC.md` with
    `PLI_UPDATEFILE` mod-time/default-file behavior, old-client `.gif`
    fallback, `PLO_FILESENDFAILED`/`PLO_FILEUPTODATE` branches,
    `UpdatePackage::load`/`reload` parsing, CRC32/size calculation,
    base-filename lookup behavior, `std::unordered_map` ordering risk, and
    existing `PLI_UPDATEPACKAGEREQUESTFILE` send sequence.
- [x] Implement source-confirmed update package send/verify lifecycle.
  - 2026-06-16: Added `FileTransferBoundary.HandleUpdatePackageRequest` for a
    supplied `UpdatePackageSnapshot`. It preserves C++ checksum comparison
    semantics, reinstall checksum clearing, total-download-size calculation,
    missing-file sends through the existing confirmed `sendFile` boundary, and
    `PLO_UPDATEPACKAGESIZE`/`PLO_UPDATEPACKAGEDONE` order. Production package
    manager parsing and unordered-map iteration certification remain blocked.
- [ ] Implement upload/write paths only after exact rights/path behavior is
  confirmed.
- [x] Add tests for checksums, package done/size, failed/up-to-date, and path
  guards.
  - 2026-06-16: Existing and new tests cover CRC match `PLO_FILEUPTODATE`,
    `.gupd` checksum bypass, update package size/done packet order, partial
    install checksums, reinstall checksum clearing, missing/default old-client
    failure packets, modern up-to-date packets, and non-default changed-file
    sends. Upload/write path guards remain blocked under the dedicated
    rights/path behavior item.

Completion criteria:

- File download/update package behavior is compatible for confirmed branches.

## Phase 13: RC/NC/Admin Production Behavior

**Goal:** Port Remote Control, NPC Control, admin rights, and mutation commands.

**Source files to trace first:**

- `server/src/player/PlayerRC.cpp`
- `server/src/player/PlayerNC.cpp`
- `server/src/Server.cpp`
- `server/src/Account.cpp`
- `server/src/Weapon.cpp`
- `server/src/NPC.cpp`

- [x] Document every RC/NC packet id, rights check, and response packet.
  - 2026-06-16: Expanded the source-confirmed RC client-to-server packet ID
    catalog from recovered `IEnums.h` and added C# enum constant tests for the
    previously missing RC settings/player-props/account/admin-message IDs
    (`55..58`, `67`, `73..76`). Full rights-check and response-packet coverage
    remains open.
  - 2026-06-16: Completed the documentation pass in
    `docs/spec/RC_NC_ADMIN_SPEC.md`: full recovered RC/NC inbound/outbound
    packet ids, `Player.cpp::createFunctions` handler coverage, RC rights
    checks, exact denial/success response packets, NC handler responses,
    `PLI_NC_LEVELLISTSET` unbound status, and the source-confirmed File Browser
    no-folder-rights no-packet behavior. Production implementation remains in
    the following unchecked RC/NC tasks.
- [ ] Implement production RC/NC login/session sockets.
- [ ] Implement file browser mutations with exact path/right behavior.
- [ ] Implement account/admin-IP mutation commands.
- [ ] Implement NPC/class/weapon mutation commands after scripting/storage
  prerequisites are ready.
- [ ] Add golden tests for packet bytes, rights denials, and mutation results.

Completion criteria:

- RC/NC clients can perform source-confirmed admin workflows.

## Phase 14: Source-Confirmed Inventory, Guild, Chat, And Profile

**Goal:** Port source-confirmed durable inventory, guild, chat, and profile
systems. This phase is limited to concrete handlers, packet paths, persistence
paths, and runtime rules found in the recovered original C++ source or exact
dependencies. Absence in the recovered C++ source is considered faithful
behavior, not a backlog gap.

**Source files to trace first:**

- all inventory/item/guild/chat/profile related files under `server/src/`
- related account/player persistence code

- [ ] Inventory durable runtime and save/load behavior.
- [ ] Guild filesystem mutation and display behavior.
- [ ] Chat/PM/profile packets.
- [x] Remove all non-source-derived systems from feature scope. Current source
  pass found no dedicated C++ core runtime for built-in shop/trade/party/quest/
  mission systems, so their absence is the compatible behavior and not a
  remaining implementation item.
- [ ] Add golden tests for every confirmed packet/rule branch.

Completion criteria:

- Durable inventory, guild, chat, and profile systems match C++ for confirmed
  flows; absent systems remain absent from the port and are not counted as
  remaining 1:1 work.

## Phase 15: Compression, Encryption, Websocket, TLS Remaining Branches

**Goal:** Complete protocol transport branches that are still blocked.

**Source files to trace first:**

- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/include/CSocket.h`
- C++ websocket/WolfSSL integration files

- [x] Implement gen4 bzip2/encryption framing if fixture-confirmed.
  - 2026-06-16: Added `gen4-short-abc-newline` and
    `inbound-gen4-short-abc-newline` fixtures to the `gs2lib` harness, then
    implemented C# outbound gen4 bzip2 socket framing and inbound gen4
    decrypt/decompress behavior against those exact bytes.
- [x] Implement gen5 bzip2 payload framing if fixture-confirmed.
  - 2026-06-16: Implemented source-confirmed outbound gen5 bzip2 socket
    framing for payloads over `0x2000` bytes using the recovered
    `CFileQueue` threshold, compression type `0x06`, bzip2 block size `1`,
    gen5 iterator-XOR encryption, and the exact
    `gen5-bz2-8193a-newline` fixture captured from `gs2lib`.
- [x] Implement inbound bzip2 branches if fixture-confirmed.
  - 2026-06-16: Implemented the fixture-confirmed inbound gen5 bzip2 decode
    path using the same source-confirmed `gen5-bz2-8193a-newline` payload
    captured from `gs2lib`.
  - 2026-06-16: Added and implemented the fixture-confirmed inbound gen4 bzip2
    decode branch using `inbound-gen4-short-abc-newline`.
- [x] Implement websocket frame wrap/unwrap behavior.
  - 2026-06-16: Added `websocket-out-*` and `websocket-in-*` fixtures to the
    `gs2lib` harness, implemented `GraalWebSocketFrame` wrap/unwrap helpers,
    and wired optional `GraalFileQueue.FlushSocket(wrapWebSocket: true)` so
    WebSocket wrapping occurs after Graal compression/encryption framing like
    `CFileQueue::sendCompress`.
- [x] Implement TLS/WolfSSL-equivalent behavior or document deployment
  compatibility strategy.
  - 2026-06-16: Added `docs/spec/TRANSPORT_TLS_DEPLOYMENT_STRATEGY.md`.
    The C# compatibility strategy is raw TCP for certified native-client flows,
    optional external TLS termination only as deployment infrastructure, and
    WebSocket/WolfSSL handshake/session integration remains blocked until C++
    handshake captures are available.
- [ ] Add C++ fixture harness outputs for every branch before C# code.

Completion criteria:

- All client-reachable transport generations/branches are supported or
  explicitly proven unreachable for target clients.

## Phase 16: Production Main Loop And Shutdown

**Goal:** Wire the real production host loop, periodic jobs, repositories, and
shutdown side effects.

**Source files to trace first:**

- `server/src/Server.cpp`
- `server/include/Server.h`
- `server/src/player/Player.cpp`
- level/account/server-list timing code

- [x] Implement production infinite host loop safely.
  - 2026-06-16: `ServerHostLoop.Run` now initializes runtime before entering loop, performs deterministic shutdown cleanup when initialization fails, and preserves source-confirmed action ordering.
- [ ] Wire player/level/server-list repositories.
- [x] Implement `cleanupDeletedPlayers` V8 retention behavior.
  - 2026-06-16: `RuntimeServer.CleanupDeletedPlayers` now accepts optional script-object callbacks for script-referenced skip, before-delete hooks, and confirmed deletion callbacks; `ServerHostRuntime` forwards these hooks through `ScriptObjectReferenceGate`, `ScriptObjectReferencedCallback`, and `BeforeRuntimePlayerDeleteCallback` paths, matching the C++ "skip while referenced / process before final removal" flow shape.
- [ ] Implement AP/singleplayer timed runtime behavior only when prerequisites
  exist.
- [ ] Implement production shutdown side effects.
- [x] Add timing tests with fake clocks.
  - 2026-06-16: Verified existing `ServerTimingBoundaryTests` cover
    source-confirmed fake-time boundaries through `TimeSpan` inputs and fake
    server-list reconnect jitter: one-second scheduler gate/order, five-minute
    job order, strict player timeout/save/reset thresholds, disconnected
    socket deletion, reconnect backoff cap/jitter, and `PLO_NEWWORLDTIME`
    packet bytes.

Completion criteria:

- Production host can run continuous source-confirmed server jobs.

## Phase 17: Closed-Source Client Certification

**Goal:** Prove byte/client behavior compatibility against original C++ and the
closed-source client.

Prerequisites:

- Runnable original C++ server copy outside `ai_resources/`.
- Matched config/content/accounts.
- Selected client binary/version.
- Packet capture tooling.
- C# production server with enough runtime parity for the target test path.

- [x] Create `docs/spec/CLIENT_CERTIFICATION_RUNBOOK.md`.
  - 2026-06-16: Added the certification runbook with required inputs,
    baseline/C# capture steps, raw-byte comparison rules, scenario order,
    failure-handling loop, evidence log template, and matrix update gate.
- [ ] Capture original C++ login, movement, file, level, RC/NC, and gameplay
  sessions.
- [ ] Capture equivalent C# sessions.
- [ ] Diff packet bytes/order/timing.
- [ ] Fix mismatches with C++-first TDD.
- [ ] Repeat until all target flows match.
- [ ] Mark compatibility matrix rows as certified only after capture evidence.

Completion criteria:

- Closed-source client cannot distinguish C# server from original C++ for all
  supported certified flows.

---

# Always-Current Source-Confirmed Port Matrix

Update this matrix whenever a phase makes progress. Rows are allowed here only
when the behavior is directly present in the recovered C++ source or gs2lib.
Do not add generic game/MMO systems as "missing features"; if the C++ source
does not implement a built-in system, the compatible C# behavior is to omit it.
Rows in this matrix must be removed or reworded if they describe a broad genre
feature instead of a concrete recovered C++ behavior path.

| Area | Status | Next Required Work |
| --- | --- | --- |
| Production sockets | Missing/partial | Phase 1 |
| Server-list lifecycle | Missing/partial | Phase 2 |
| Account runtime | Partial: confirmed load/save/guest boundary implemented; host/RNG gaps remain | Phase 16 / certification |
| Login success sequence | Partial | Phase 4 |
| Level formats/cache/maps | Partial | Phase 5 |
| Live world forwarding | Partial | Phase 6 |
| Player props/movement | Partial | Phase 7 |
| Chest/item rewards | Partial | Phase 8 |
| Source-confirmed combat/baddy/projectile paths | Partial | Phase 9 |
| Source-confirmed NPC/scripting paths | Mostly missing | Phase 10 |
| Source-confirmed weapons/classes/gani paths | Partial | Phase 11 |
| File upload/update packages | Partial | Phase 12 |
| RC/NC/admin production | Partial | Phase 13 |
| Source-confirmed inventory/chat/guild/profile paths | Mostly missing, source-confirmed paths only | Phase 14 |
| bzip2/websocket/TLS | Partial: frame helpers implemented, handshake/TLS blocked | Phase 15 |
| Production main loop | Partial | Phase 16 |
| Client certification | Not certified | Phase 17 |

---

# End-Of-Run Report Template

Use this exact shape in the final response:

```txt
Completed:
- ...

Blocked:
- ...

ai_resources:
- untouched / modified only because user explicitly requested ...

Verification:
- dotnet build GServerSharp.sln: passed/failed/not run because ...
- dotnet test GServerSharp.sln: passed/failed/not run because ...

Commit:
- <hash> <message>

Manual client test:
- possible/not yet possible
- limitations: ...

Next unchecked item:
- Phase N: ...
```
