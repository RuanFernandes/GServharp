# Known Blockers

- Exact original `gs2compiler` submodule commit is not present in this fresh source snapshot. The repository URL is confirmed and current source was cloned, but scripting work should recover the exact commit before implementing runtime behavior.
- Full `IEnums.h` packet catalog is large; only foundation-critical IDs are implemented in C# so far.
- Production startup now has source-confirmed command-line/environment server
  selection plus `CSettings`-compatible `serveroptions.txt` and
  `adminconfig.txt` parsing. It still stops before production sockets,
  list-server auth, full `Server::init`, filesystem runtime loaders, and
  gameplay because those belong to later milestones.
- Full login success is blocked on production account/default account loading side effects, remaining `sendLoginClient` branches, `sendLoginRC`/`sendLoginNC`, and world warp behavior.
- The login packet parse boundary, server-list auth boundary, source-confirmed beginning of `Player::sendLogin`, `Server::playerLoggedIn` list-server add side effect, minimal pre-warp `sendLoginClient` packet order, the confirmed `__sendLogin` property ID table, and login property serialization are implemented. The current stop point is `ReadyForLevelWarp`, immediately before `warp(m_levelName, getX(), getY())`.
- Old-version map-file workaround, `flaghack_ip`, weapons, protected weapons, classes, and zlib-fix NPC weapon branches in `sendLoginClient` are traced but not implemented.
- The login-server-name branch in `Player::sendLogin` is blocked because C++ references `PLO_FULLSTOP`, but recovered `IEnums.h` only defines `PLO_FULLSTOP2 = 177`.
- `CFileQueue` queue selection, gen1/gen6 socket passthrough, gen2/gen3 zlib
  framing, gen5 uncompressed socket framing for payloads up to 55 bytes, and
  gen5 zlib framing for payloads through `0x2000` bytes are implemented.
  Gen4 bzip2/encryption framing, gen5 bzip2 payload framing, and websocket
  wrapping remain blocked.
- A dev-only TCP/session shell exists for diagnostic login -> filesystem `.nw`
  -> `sendLevel` boundary. It is not production-compatible and must remain
  opt-in because it uses fake local auth, stops on unsupported post-login frames
  before gameplay/runtime dispatch, and selects the current-modtime level branch
  so small/medium responses can use confirmed gen5 zlib `FlushSocket` framing
  without entering blocked bzip2 board payload output.
- Production account loading has a pure `GRACC001` parser, but full service behavior is blocked on exact filesystem/default-account save behavior and guest RNG.
- Isolated warp packet builders are implemented. A source-confirmed
  `setLevel` pre-runtime boundary now handles missing levels, `PLO_PLAYERWARP`,
  `PLO_PLAYERWARP2`, and the modern non-zero-modtime no-warp-packet branch.
  Modern `sendLevel` is implemented through confirmed dynamic packet wrappers:
  board changes, chests, horses, baddies, GMAP correction, ghost icon, leader,
  new world time, active level, opaque NPC packet bytes, and nearby
  `PLO_OTHERPLPROPS` visibility sync from snapshots. Full `warp(...)`,
  fallback to previous/unstick levels, singleplayer/group-map cloning, old
  `sendLevel141`, production horse/baddy/NPC state construction, and live
  multi-session player-list forwarding remain blocked because they enter
  level/map/NPC/player-list runtime.
- Minimal level/player ownership is implemented only for confirmed id
  assignment, same-level membership order, all-matching-id removal, leader
  detection, deferred deletion cleanup, and visibility selection. List-server
  side effects, scripting hooks, player-id generation, and live multi-session
  forwarding remain blocked.
- Level file format detection is implemented for confirmed extension and
  signature selection. A source-confirmed read-only indexed filesystem boundary
  and filesystem-backed `.nw` loading path now feed static
  board/layer/link/sign/chest payloads into `sendLevel`. Production
  `foldersconfig.txt` parsing, full `Level::findLevel` cache/map ownership,
  `.graal`/`.zelda` parsing, and file/resource transfer remain blocked.
- Pure `.nw` parsing is implemented for confirmed board tiles, links with an
  explicit target resolver, signs, chests with source-confirmed item names, NPC
  payload preservation, and baddy verse payload preservation. Board/layer/link/
  sign/chest packet builders exist. Player sign translation, NPC runtime props,
  baddy runtime props/AI, and chest opening gameplay remain blocked.
- Incoming decoded `PLI_PLAYERPROPS` movement/property parsing is implemented
  for the confirmed X/Y/Z, X2/Y2/Z2, sprite, current-level, and gani subset.
  Safe local runtime mutation and a packet builder for confirmed movement
  `PLO_OTHERPLPROPS` forwarding bytes exist. Confirmed inbound gen1/gen2/gen3
  and gen5 uncompressed/zlib frame decode exists, gen5 invalid compression
  type now follows the C++ log-and-continue decrypted-payload behavior, and the
  dev-only TCP shell can preserve source-confirmed `PLI_RAWDATA` length state
  for decoded gen1/gen2/gen5/gen6 post-login payloads. Inbound bzip2 branches,
  inbound bundle dispatch, live multi-session forwarding, full `setProps`,
  touch/link traversal,
  NPC/chest/combat side effects, and invalid-update behavior remain blocked.
- WebSocket handling is gated by `WOLFSSL_ENABLED` code paths and needs a dedicated pass.
- `Server::doMain()` timing branches need a dedicated timing recovery pass.
- Gameplay systems, account persistence, RC/NC file browser, server-list protocol, and scripting bindings are not implemented.
