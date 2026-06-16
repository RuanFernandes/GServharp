# Known Blockers

- Exact original `gs2compiler` submodule commit is not present in this fresh source snapshot. The repository URL is confirmed and `external/gs2compiler` is recovered at `4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9`, but scripting work should recover the exact gitlink commit before implementing runtime behavior.
- Full `IEnums.h` packet catalog is large; only foundation-critical IDs are implemented in C# so far.
- Production startup now has source-confirmed command-line/environment server
  selection plus `CSettings`-compatible `serveroptions.txt` and
  `adminconfig.txt` parsing. It still stops before production sockets,
  list-server auth, full `Server::init`, filesystem runtime loaders, and
  gameplay because those belong to later milestones. The C++ production
  listener/session lifecycle is documented in
  `docs/spec/PRODUCTION_SOCKET_SESSION_SPEC.md`, but the production C# listener
  and multi-session socket manager are not implemented yet. A small
  source-confirmed `ProductionSocketReceiveBuffer` exists for arbitrary TCP
  chunk buffering and raw two-byte length-prefixed frame extraction.
- Production auth now has source-confirmed list-server packet body builders for
  registration, HQ settings, allowed-version text, and `SVO_VERIACC2`, plus a
  gateway boundary that queues auth requests without fake validation. Real
  list-server sockets, reconnects, local IP discovery, and gen1-to-gen2
  connection sequencing remain blocked.
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
- Production account loading has a pure `GRACC001` parser plus source-confirmed
  default-account fallback, start overrides, save-format serialization,
  case-preserved filename selection, disk write attempt reporting, and
  default-account add-file signalling. Live login repository wiring, full
  filesystem resync behavior, exact unusual `CString(float)` save formatting,
  `std::unordered_map` flag order guarantees, and guest random `pc:` identity
  generation remain blocked.
- Isolated warp packet builders are implemented. A source-confirmed
  `warp`/`setLevel` pre-runtime boundary now handles same-level X/Y updates,
  missing levels, previous-level fallback, unstick fallback, `PLO_PLAYERWARP`,
  `PLO_PLAYERWARP2`, and the modern non-zero-modtime no-warp-packet branch.
  Modern `sendLevel` is implemented through confirmed dynamic packet wrappers:
  board changes, chests, horses, baddies, GMAP correction, ghost icon, leader,
  new world time, active level, opaque NPC packet bytes, and nearby
  `PLO_OTHERPLPROPS` visibility sync from snapshots. Singleplayer/group-map
  cloning, live level-area forwarding from `setProps`, sparring-zone AP mutation,
  old `sendLevel141`, production horse/baddy/NPC state construction, and live
  multi-session player-list forwarding remain blocked because they enter
  level/map/NPC/player-list runtime.
- Minimal level/player ownership is implemented for confirmed id assignment,
  automatic player-id generation/reuse, same-level membership order,
  all-matching-id removal, leader detection, deferred deletion cleanup,
  visibility selection, and live forwarding of already-confirmed
  level-area/player-prop packets to session sinks. List-server delete side
  effects, scripting hooks, real socket/file-queue integration,
  `std::unordered_map` map-area iteration golden compatibility, and arbitrary
  gameplay packet forwarding remain blocked.
- Level file format detection is implemented for confirmed extension and
  signature selection. A source-confirmed read-only indexed filesystem boundary,
  `loadAllFolders`/`loadFolderConfig` bucket setup, and filesystem-backed `.nw`
  loading path now feed static board/layer/link/sign/chest payloads into
  `sendLevel`. Full `Level::findLevel` cache/map ownership, `.graal`/`.zelda`/
  `.gmap` parsing, write/delete filesystem mutation, and file/resource transfer
  remain blocked.
- Pure `.nw` parsing is implemented for confirmed board tiles, links with an
  explicit target resolver, signs, chests with source-confirmed item names, NPC
  payload preservation, and baddy verse payload preservation. Board/layer/link/
  sign/chest packet builders exist. Player sign translation, NPC runtime props,
  baddy runtime props/AI, and chest opening gameplay remain blocked.
- Incoming decoded `PLI_PLAYERPROPS` movement/property parsing is implemented
  for the confirmed X/Y/Z, X2/Y2/Z2, sprite, current-level, and gani subset.
  Safe local runtime mutation, a packet builder for confirmed movement
  `PLO_OTHERPLPROPS` forwarding bytes, and a live session sink forwarder for
  that confirmed subset exist. Confirmed inbound gen1/gen2/gen3 and gen5
  uncompressed/zlib frame decode exists, gen5 invalid compression
  type now follows the C++ log-and-continue decrypted-payload behavior, and the
  dev-only TCP shell can preserve source-confirmed `PLI_RAWDATA` length state
  for decoded gen1/gen2/gen5/gen6 post-login payloads. Inbound bzip2 branches,
  inbound bundle dispatch, full `setProps`, touch/link traversal,
  NPC/chest/combat side effects, and invalid-update behavior remain blocked.
- WebSocket handling is gated by `WOLFSSL_ENABLED` code paths and needs a dedicated pass.
- `Server::doMain()` timing branches need a dedicated timing recovery pass.
- Gameplay systems, account persistence, RC/NC file browser, server-list protocol, and scripting bindings are not implemented.
