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

**Tech Stack:** C#/.NET, xUnit, PowerShell, C++ source tracing, recovered
`gs2lib`, optional fixture harnesses under `tools/`.

---

## Mandatory Run Rules

Read these files before every run:

- `AGENTS.md`
- `COMPATIBILITY_RULES.md`
- `SERVER_SPEC.md`
- `PORTING_PLAN.md`
- `KNOWN_BLOCKERS.md`
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
- Run `dotnet build GServharp.sln` and `dotnet test GServharp.sln` before the
  final report unless only non-code markdown changed and the task explicitly
  documents why a build was not needed.
- Always run `git status --short ai_resources` before committing.
- Automatically commit all pending changes at the end of each completed run.
- After committing, report commit hash, build/test result, untouched
  `ai_resources/` status, what remains blocked, and the next unchecked item.

## How To Execute This File

When the user says:

```txt
Run TASK_COMPLETE_1TO1_PORT.md
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

Major missing areas:

- Production socket host and full continuous session dispatch.
- Full server-list socket lifecycle/reconnect/registration.
- Full account/auth/list-server integration in production runtime.
- Full `sendLoginClient` old-client, weapons, classes, flags, and file branches.
- Full level runtime/cache/map ownership and `.graal`/`.zelda`/`.gmap` parsers.
- Full movement/player prop handling, blocked side effects, invalid update
  behavior, and live movement-loop invocation.
- NPC/script runtime, V8/GS2 compiler integration, bytecode, lifecycle, and
  script-visible APIs.
- Baddy AI, combat, projectiles, hit validation, damage, drops, death, respawn.
- Inventory, item pickup mutations, chest reward mutations, shops, trade,
  quests, party, guild, social/chat/PM/profile systems.
- RC/NC/admin production sockets and mutation commands.
- Upload/write paths and update-package lifecycle.
- Websocket/TLS/bzip2 blocked branches.
- Closed-source client certification against original C++ byte captures.

---

# Execution Backlog

## Phase 1: Production Socket And Session Dispatch

**Goal:** Replace dev-only post-login limitations with source-confirmed
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
  - 2026-06-16: Added `ProductionSocketReceiveBuffer` for source-confirmed
    arbitrary TCP chunk buffering and raw two-byte big-endian frame extraction.
  - 2026-06-16: Added accept-one `ProductionTcpServer` skeleton with
    TCP_NODELAY, player id `2` start, `0x8000` read chunks, frame dispatch, and
    handler-provided outbound writes. Still blocked/not done: multi-session
    socket-manager scheduling, deferred deletion cleanup, and real production
    auth dispatch.
- [x] Integrate confirmed post-login decoded packet dispatch without gameplay
  invention.
  - 2026-06-16: Added `ProductionPostLoginPacketDispatcher` for already-decoded
    post-login inner packet bytes. It handles only the confirmed
    `PLI_PLAYERPROPS` movement/property subset, blocks assigned-but-unimplemented
    C++ `TPLFunc` ids, and models `msgPLI_NULL` invalid-packet counting with the
    source-confirmed sixth-packet disconnect message. It is not yet wired into a
    production auth/session loop.
- [x] Keep unsupported packet ids logged/blocked, not faked.
  - 2026-06-16: Added `ProductionPostLoginFrameHandler`, which decodes
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
  `docs/spec/KNOWN_BLOCKERS.md`, and `KNOWN_BLOCKERS.md`.
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
  - 2026-06-16: Added `ProductionServerListLifecycle` behind
    `IProductionServerListSocket`, preserving confirmed initialize/connect/
    register, queue clear, gen1 register immediate send, gen2 follow-up packet
    order, and local-IP loopback clearing. Concrete remote TCP client remains
    blocked.
- [x] Wire real `SVO_VERIACC2` request/`SVI_VERIACC2` response path into
  production login.
  - 2026-06-16: Added `ProductionServerListAuthResponseHandler`, which parses
    confirmed `SVI_VERIACC2` payloads, resolves pending sessions by id/type,
    applies account-name overwrite, queues source-confirmed rejection
    disconnect bytes, and marks `SUCCESS` as `ServerListAuthAcceptedPreWorld`
    without local fake auth. Concrete live list-server receive loop remains
    blocked.
- [x] Preserve fake/dev auth only behind explicit dev-only settings.
  - 2026-06-16: Verified the only fake server-list success path remains inside
    `DevOnlyLocalSessionPipeline` and throws unless `EnableDevOnlyAuth=true`;
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

- [ ] Trace and document old-version map-file workaround.
- [ ] Trace and document flaghack mutation.
- [ ] Trace and implement confirmed weapon/protected-weapon/class login packet
  branches.
- [ ] Resolve or permanently block `PLO_FULLSTOP` vs `PLO_FULLSTOP2` with source
  proof.
- [ ] Implement old-client `PLPROP_GANI` behavior if confirmed.
- [ ] Add golden packet-order fixtures for modern and old clients.
- [ ] Update `docs/spec/SENDLOGINCLIENT_PACKET_FLOW.md`,
  `docs/spec/GOLDEN_FIXTURES.md`, and blockers.

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

- [ ] Document `Level::findLevel` cache, filesystem lookup, map ownership, and
  load failure behavior in `docs/spec/LEVEL_RUNTIME_SPEC.md`.
- [ ] Implement `.graal` parser from C++ fixtures.
- [ ] Implement `.zelda` parser from C++ fixtures.
- [ ] Implement `.gmap` parser/map adjacency/group behavior.
- [ ] Implement level cache ownership and invalidation only where confirmed.
- [ ] Add golden fixtures for all level format signatures and representative
  packets.
- [ ] Keep write/delete mutation blocked until source-confirmed.

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

- [ ] Document `sendPacketToLevelArea`, map area iteration, hidden-client rules,
  and player-list forwarding.
- [ ] Implement live session sink integration for confirmed packet families.
- [ ] Add tests for same-level, nearby map, hidden client, non-client, and
  deletion cleanup behavior.
- [ ] Add compatibility note for any unordered-map iteration that cannot be
  guaranteed.

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

- [ ] Catalog every `PLPROP_*` handled by `setProp`/`setProps`.
- [ ] Implement source-confirmed property parsing/mutation/forwarding in small
  tested subsets.
- [ ] Wire live `testSign` invocation through confirmed movement branches.
- [ ] Keep NPC touch script events blocked until scripting runtime exists.
- [ ] Confirm whether automatic player movement-to-link warp exists; implement
  only if a direct C++ path is found.
- [ ] Add golden tests for property order, forwarding version differences,
  blocked updates, and malformed values.

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

- [ ] Document `LevelItem::getItemPlayerProp` for every item id.
- [ ] Implement chest reward mutation through confirmed player props.
- [ ] Implement item pickup/removal mutation and packets.
- [ ] Add tests for rupees, bombs, arrows, hearts, weapons, spinattack, and
  invalid item ids where confirmed.
- [ ] Keep shops/trade/quests blocked for later phases.

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

- [ ] Document baddy AI timers, reset/default props, hurt/death/drop behavior.
- [ ] Document player hurt, AP thresholds, death/revive, sparring-zone behavior.
- [ ] Implement combat packet parsers/builders and deterministic formulas.
- [ ] Implement drops only after exact C++ RNG/timing behavior is confirmed.
- [ ] Add golden tests for packet bytes and gameplay rule outputs.

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

- [ ] Recover exact `gs2compiler` gitlink commit or document why impossible.
- [ ] Document SourceCode classification, GS1/GS2 compilation, bytecode headers,
  V8 bindings, script lifecycle, errors, and scheduling.
- [ ] Implement compiler invocation or compatible bytecode path only when
  source-confirmed.
- [ ] Implement NPC lifecycle, events, props, and packet forwarding in slices.
- [ ] Add explicit guards for all unimplemented script-visible APIs.
- [ ] Add golden bytecode/packet fixtures where possible.

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

- [ ] Document weapon/class filesystem loading and cache behavior.
- [ ] Implement `PLO_NPCWEAPONADD`, script payload, delete, class edit/get
  flows where confirmed.
- [ ] Implement gani checksum/script request behavior.
- [ ] Add fixtures for newline replacement, tokenization, and bytecode/script
  payloads.

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

- [ ] Document `PLI_UPDATEFILE`, package parsing, checksum comparison, and
  default-file cache behavior.
- [ ] Implement source-confirmed update package send/verify lifecycle.
- [ ] Implement upload/write paths only after exact rights/path behavior is
  confirmed.
- [ ] Add tests for checksums, package done/size, failed/up-to-date, and path
  guards.

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

- [ ] Document every RC/NC packet id, rights check, and response packet.
- [ ] Implement production RC/NC login/session sockets.
- [ ] Implement file browser mutations with exact path/right behavior.
- [ ] Implement account/admin-IP mutation commands.
- [ ] Implement NPC/class/weapon mutation commands after scripting/storage
  prerequisites are ready.
- [ ] Add golden tests for packet bytes, rights denials, and mutation results.

Completion criteria:

- RC/NC clients can perform source-confirmed admin workflows.

## Phase 14: Inventory, Shops, Trade, Quests, Party, Guild, Social

**Goal:** Port durable gameplay/social systems.

**Source files to trace first:**

- all shop/trade/quest/party/guild/social/chat/profile related files under
  `server/src/`
- related account/player persistence code

- [ ] Inventory durable runtime and save/load behavior.
- [ ] Shops and prices.
- [ ] Trade flow.
- [ ] Quests/missions and rewards.
- [ ] Party behavior.
- [ ] Guild filesystem mutation and display behavior.
- [ ] Chat/PM/profile/social packets.
- [ ] Add golden tests for every confirmed packet/rule branch.

Completion criteria:

- Durable gameplay/social systems match C++ for confirmed flows.

## Phase 15: Compression, Encryption, Websocket, TLS Remaining Branches

**Goal:** Complete protocol transport branches that are still blocked.

**Source files to trace first:**

- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/include/CSocket.h`
- C++ websocket/WolfSSL integration files

- [ ] Implement gen4 bzip2/encryption framing if fixture-confirmed.
- [ ] Implement gen5 bzip2 payload framing if fixture-confirmed.
- [ ] Implement inbound bzip2 branches if fixture-confirmed.
- [ ] Implement websocket frame wrap/unwrap behavior.
- [ ] Implement TLS/WolfSSL-equivalent behavior or document deployment
  compatibility strategy.
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

- [ ] Implement production infinite host loop safely.
- [ ] Wire player/level/server-list repositories.
- [ ] Implement `cleanupDeletedPlayers` V8 retention behavior.
- [ ] Implement AP/singleplayer timed runtime behavior only when prerequisites
  exist.
- [ ] Implement production shutdown side effects.
- [ ] Add timing tests with fake clocks.

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

- [ ] Create `docs/spec/CLIENT_CERTIFICATION_RUNBOOK.md`.
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

# Always-Current Missing Feature Matrix

Update this matrix whenever a phase makes progress.

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
| Combat/baddies/projectiles | Partial | Phase 9 |
| NPC/scripting | Mostly missing | Phase 10 |
| Weapons/classes/gani | Partial | Phase 11 |
| File upload/update packages | Partial | Phase 12 |
| RC/NC/admin production | Partial | Phase 13 |
| Inventory/shop/social/quests | Mostly missing | Phase 14 |
| bzip2/websocket/TLS | Missing/partial | Phase 15 |
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
- dotnet build GServharp.sln: passed/failed/not run because ...
- dotnet test GServharp.sln: passed/failed/not run because ...

Commit:
- <hash> <message>

Manual client test:
- possible/not yet possible
- limitations: ...

Next unchecked item:
- Phase N: ...
```
