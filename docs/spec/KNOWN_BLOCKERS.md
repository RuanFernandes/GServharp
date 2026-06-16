# Spec Blockers

- Full `Player::sendLogin` is blocked on production account file loading, player property serialization, world/level entry, file queue flush behavior, RC/NC login packet families, and optional scripting hooks.
- The beginning of `Player::sendLogin` is implemented only through the pre-world continuation boundary. C# stops at `ReadyForWorldEntry`, immediately before `Server::playerLoggedIn(shared_from_this())`.
- `Server::playerLoggedIn` and the beginning of `sendLoginClient` are implemented only through a source-confirmed pre-warp boundary. C# stops at `ReadyForLevelWarp`, immediately before `warp(m_levelName, getX(), getY())`.
- The `sendProps(__sendLogin)` property ID table/order is implemented as a source-confirmed C# constant. Full production emission remains blocked on account/default-account data loading and runtime-dependent properties outside the login set. The old-client `PLPROP_GANI`/`PLPROP_BOWGIF` serialization branch is implemented for confirmed login/property serializer paths.
- `Player::setProps` runtime branches are now cataloged in
  `PLAYER_PROPS_RUNTIME_CATALOG.md`. Only the previously confirmed movement
  subset is implemented; most runtime mutations remain blocked on word-filter,
  NPC, combat/death, RC permission, GMAP, chat, and scripting prerequisites.
- The C# pre-warp boundary now uses the confirmed property serializer for explicit property IDs instead of inventing defaults.
- Production startup now resolves the server name from overrides,
  `startupserver.txt`, or exactly one `servers/` directory, and loads
  source-confirmed `CSettings` syntax for `config/serveroptions.txt` and
  `config/adminconfig.txt`. Production runtime remains blocked before sockets,
  list-server auth, full config loaders, filesystem scans, and gameplay. The
  C++ production listener/session lifecycle is now documented in
  `PRODUCTION_SOCKET_SESSION_SPEC.md`, and the C# port has an accept-one
  `ProductionTcpServer` skeleton plus `ProductionSocketReceiveBuffer` for
  confirmed TCP chunk buffering and raw two-byte length-prefixed frame
  extraction. It also has decoded post-login dispatcher/frame-handler
  boundaries for the confirmed `PLI_PLAYERPROPS` subset plus C++ `msgPLI_NULL`
  invalid-packet counting. Multi-session scheduling, deferred deletion cleanup,
  concrete production auth socket-loop wiring, and gameplay dispatch are not
  implemented yet.
- Old-version BIGMAP file-send workaround is implemented for supplied map
  snapshots through the confirmed file-transfer boundary. `flaghack_ip` is
  traced, but full duplicate flag emission is blocked on
  `std::unordered_map` ordering. `sendLoginClient` can now queue supplied
  source-confirmed player weapon, missing protected weapon, and modern class
  packets in the traced C++ order. Modern and old-client pre-warp packet-order
  fixtures cover the currently confirmed safe branches. Production live weapon lookup, default weapon
  conversion through `msgPLI_WEAPONADD`, script/bytecode compilation, class
  `time(0)` packet construction, concrete class-list ordering, and zlib-fix NPC
  weapon remain blocked.
- The login-server-name branch is permanently blocked for the recovered source
  set because C++ references `PLO_FULLSTOP`, but the C++ tree and recovered
  `IEnums.h` only define `PLO_FULLSTOP2 = 177`. The C# boundary reports
  `LoginServerFullStopBlocked` and must not substitute `PLO_FULLSTOP2` without
  recovered original source or exact dependency proof.
- Exact `CString::guntokenize()` behavior for ban reasons remains blocked; current C# tests cover plain reasons and the confirmed newline-to-carriage-return replacement path only.
- Real account/password validation must not be invented. The C++ server delegates password/auth verification to the list server through `SVO_VERIACC2`/`SVI_VERIACC2`.
- Production auth now has confirmed list-server packet body builders for
  registration/HQ/version config and a non-fake `IProductionServerListGateway`
  boundary for `SVO_VERIACC2`. A source-confirmed
  `ProductionServerListLifecycle` now sequences connect/register packets,
  local-IP selection, and gen1-to-gen2 codec transition timing behind
  `IProductionServerListSocket`. The production auth response boundary now
  parses `SVI_VERIACC2` success/rejection messages and applies them to pending
  sessions without fake local validation. The concrete remote TCP client, live
  zlib-framed list-server receive loop, reconnect host wiring, and player
  replay from live repositories remain blocked.
- Account file parsing for confirmed `GRACC001` fields/defaults is implemented.
  The C# account loading boundary now also performs source-confirmed
  case-insensitive lookup, default-account fallback, startlevel/startx/starty
  overrides, save-format serialization, case-preserved filename selection, disk
  write attempt reporting, default-account add-file signalling, account DTO
  mapping into the pre-world login boundary, and deterministic guest `pc:`
  candidate selection behind an explicit selector. Full filesystem resync
  behavior, exact unusual `CString(float)` save formatting,
  `std::unordered_map` flag order guarantees, live socket-host integration, and
  the exact C `rand()`/`time(0)` guest candidate stream remain blocked.
- `CFileQueue` queue selection, gen1/gen6 socket passthrough, gen2/gen3 zlib
  framing, gen5 uncompressed socket framing for payloads up to 55 bytes, and
  gen5 zlib framing for payloads through `0x2000` bytes are implemented.
  Gen4 bzip2/encryption framing, gen5 bzip2 payload framing, and websocket
  wrapping remain blocked.
- A dev-only TCP/session shell exists for length-prefixed TCP input and a
  filesystem-backed `.nw` `sendLevel` boundary. It is not production-compatible:
  it uses explicit fake auth, stops on unsupported post-login frames before
  gameplay/runtime dispatch, and selects the current-modtime level branch so
  small/medium responses can use confirmed gen5 zlib `FlushSocket` framing
  without entering blocked bzip2 board payload output.
- First isolated warp packet builders are implemented. The C# port now has a
  source-confirmed `warp`/`setLevel` pre-runtime boundary for same-level X/Y
  updates, missing levels, previous-level fallback, unstick fallback,
  `PLO_PLAYERWARP`, `PLO_PLAYERWARP2`, and modern non-zero-modtime no-warp
  packet behavior. Modern `sendLevel` is implemented through dynamic
  board-change/chest/horse/baddy packet wrappers and the first post-dynamic
  packets (`PLO_GHOSTICON`, optional `PLO_ISLEADER`, `PLO_NEWWORLDTIME`,
  `PLO_SETACTIVELEVEL`, opaque NPC packet bytes, and nearby
  `PLO_OTHERPLPROPS` visibility sync from snapshots). Singleplayer/group-map
  cloning, live level-area forwarding from `setProps`, sparring-zone AP mutation,
  old `sendLevel141`, production horse/baddy/NPC state construction, and live
  multi-session player-list forwarding remain blocked because they enter
  level/map/NPC/player-list runtime.
- Minimal level/player ownership is implemented for source-confirmed id
  assignment, automatic player-id generation/reuse, level player-list
  append/remove, leader detection, deferred deletion cleanup, runtime visibility
  filtering, and live forwarding of already-confirmed level-area/player-prop
  packets to session sinks. It does not implement list-server delete side
  effects, scripting hooks, real socket/file-queue integration, or arbitrary
  gameplay packet forwarding. The C++ forwarding matrix, hidden-client
  boundaries, and `std::unordered_map` player-list iteration compatibility risk
  are documented in `LIVE_WORLD_SESSION_FORWARDING_SPEC.md`; map-area/global
  player-list order remains uncertified until C++ capture evidence exists. C#
  still needs dedicated implementation for `sendPacketToLevelOnlyGmapArea`,
  predicate-split projectile forwarding, and call-site-specific hidden-client
  behavior. The `sendPacketToOneLevel` sink boundary is implemented for already
  built source-confirmed packet bytes.
- Level format detection is implemented for the exact C++ extension checks and
  eight-byte signatures. A read-only indexed filesystem boundary,
  source-confirmed `loadAllFolders`/`loadFolderConfig` bucket setup, and
  filesystem-backed `.nw` loading path now exist for static
  board/layer/link/sign/chest payloads. The in-memory `RuntimeLevelCache`
  implements confirmed `Level::findLevel` list ownership, case-insensitive
  first-match lookup, no-append-on-failure, source-confirmed `loadAbsolute`
  callback sequencing, first-map-wins attachment, map reload remapping, and
  missing-map clearing. Pure `.graal` and `.zelda` parsing are implemented for
  confirmed static payloads, but production legacy-format filesystem/runtime
  wiring remains blocked. Pure BIGMAP/GMAP parsing, map lookup, group-map
  metadata, and preload selection are implemented for confirmed metadata
  behavior, but production `Server::loadMaps`/`loadMapLevels` wiring remains
  blocked.
  Horse/baddy/NPC runtime construction and file-transfer behavior remain
  blocked. Write/delete filesystem mutation is explicitly blocked in
  `docs/spec/LEVEL_MUTATION_BLOCKERS.md`; the recovered C++ confirms
  `PLI_BOARDMODIFY`, `Level::alterBoard`, `LevelBoardChange`,
  `Level::saveLevel`, and script `level.savelevel` entry points, but the C#
  port must not implement them before a dedicated fixture pass covers board
  modify validation, respawn timing, item drops, save text, filesystem index
  side effects, and rights/path behavior.
- Pure `.nw` parsing is implemented for confirmed `BOARD`, `LINK`, `SIGN`,
  `CHEST`, `NPC`, and `BADDY` source-line behavior, plus board/layer/link/sign
  and chest packet builders. Player sign translation, NPC runtime creation,
  baddy ids/props/AI, chest opening gameplay, and production legacy-format
  loader wiring remain blocked.
- File transfer cache boundary is implemented for confirmed `PLI_WANTFILE` and
  `PLI_VERIFYWANTSEND` behavior, including file failed/up-to-date packets,
  modern and old-client raw-data `PLO_FILE` chunks, large-file markers, CRC32,
  and `.gupd` checksum-ignore behavior. Upload/write paths, production package
  manager parsing, `PLI_UPDATEFILE` default-file cache behavior, and full
  update-package lifecycle remain blocked.
- Incoming decoded `PLI_PLAYERPROPS` movement/property parsing is implemented
  for the confirmed X/Y/Z, X2/Y2/Z2, sprite, current-level, and gani subset.
  Safe local runtime mutation, a packet builder for confirmed movement
  `PLO_OTHERPLPROPS` forwarding bytes, and a live session sink forwarder for
  that confirmed subset exist. Confirmed inbound gen1/gen2/gen3 and gen5
  uncompressed/zlib frame decode exists, gen5 invalid compression
  type now follows the C++ log-and-continue decrypted-payload behavior, and the
  dev-only TCP shell preserves source-confirmed `PLI_RAWDATA` state for decoded
  gen1/gen2/gen5/gen6 post-login payloads. Inbound bzip2 branches, inbound
  bundle dispatch, full `setProps`, NPC/combat side effects, and invalid-update
  behavior remain blocked. Pure inclusive link hit-testing, client-triggered
  `PLI_LEVELWARP`/`PLI_LEVELWARPMOD` parsing, static sign encoding, chest key
  formatting, runtime `PLO_SAY2` sign-touch packet construction, and the
  unopened chest acknowledgement packet are implemented; automatic player
  movement-to-link warp, live movement-loop sign-touch invocation, player sign
  translation, and chest item reward mutation remain blocked.
- Server-list connection lifecycle, reconnect backoff, registration, auth
  request/response boundaries, and text/listserver side-channel packet builders
  are partially implemented behind interfaces. Concrete remote TCP integration,
  live receive dispatch, and replaying current player records remain blocked.
- Production timing boundaries now cover the source-confirmed `Server::doMain`
  one-second gate, 5s/60s/180s/300s periodic server jobs, server-list reconnect
  backoff, `Player::doTimedEvents` idle/no-data/save/reset gates, and
  `PLO_NEWWORLDTIME` packet bytes. The real infinite host loop, concrete
  player/level/server-list repository wiring, `cleanupDeletedPlayers` V8
  retention, AP/singleplayer runtime execution, and production shutdown
  side effects remain blocked until the surrounding runtime services exist.
- WolfSSL/websocket HTTP upgrade behavior is traced and documented, but frame
  wrapping/unwrapping and TLS integration remain blocked pending a dedicated
  byte-level compatibility pass.
- Client certification is not complete. A passive byte-exact comparison harness
  exists for future C++ vs C# packet captures, and the compatibility matrix is
  documented, but no closed-source client run was performed. Certification
  remains blocked on a runnable original C++ server copy outside `ai_resources`,
  matched test content/config, selected client binary/version, raw capture
  tooling, and complete runtime/gameplay/admin parity.
- RC/NC/admin is implemented only as a confirmed boundary: rights constants,
  RC/NC login gate decision text, selected RC/NC packet builders, protected
  file download checks, folder-right parsing, and server-list ping/request-list
  packet bodies. Production RC/NC sockets, account/admin-IP wiring,
  server-option mutations, file browser disk mutations, NPC/class/weapon
  mutation, script execution, and complex `gtokenize()` payloads remain blocked.
- Entity runtime is implemented only as an inert boundary: source-confirmed
  item/horse/NPC id/baddy containers, baddy reset/default prop serialization,
  selected item/horse/NPC/weapon packet builders, rupee count constants, and the
  C++ baddy add limit quirk are tested. Script VM events, NPC full props,
  baddy AI/combat/drop/respawn/timers, item pickup inventory mutation, horse
  timers, GS1 formatting, GS2/bytecode compilation, and live production
  forwarding integration remain blocked.
- Scripting runtime is still blocked. The C# port now documents the recovered
  `gs2compiler` URL/commit, implements only source-confirmed `SourceCode`
  classification, and has explicit guards that reject compile/execute calls.
  The current reference snapshot cannot prove the exact original gs2compiler
  gitlink commit, so native compiler invocation,
  bytecode header golden fixtures, V8 bindings, lifecycle scheduling,
  exception behavior, and all script-visible gameplay APIs remain blocked.
- Combat/player gameplay is implemented only for deterministic source-confirmed
  packet builders, resource clamps, AP thresholds, and death/revive status
  transitions. Full combat simulation, projectile conversion/routing, death item
  drops, persistence writes, hit validation, sparring-zone warp integration, and
  script/NPC side effects remain blocked until their surrounding runtime
  behavior is ported.
- Inventory/chat/guild/profile work is implemented only for confirmed level
  item pickup payloads, player-drop removal rules, and weapon pickup state side
  effects. Full inventory runtime wiring, chat/PM/profile behavior, guild
  filesystem mutation, and script/content-driven behavior from the C++ runtime
  remain blocked. Dedicated built-in shop, trade, party, quest, and mission
  runtimes were not found in the recovered C++ core. They are not blockers,
  missing features, or future implementation work for the faithful port; their
  absence is the compatible behavior unless future recovered original C++ source
  or exact dependency source shows a client-facing C++ path.
