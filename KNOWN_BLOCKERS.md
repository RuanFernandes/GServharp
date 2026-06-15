# Known Blockers

- Exact original `gs2compiler` submodule commit is not present in this fresh source snapshot. The repository URL is confirmed and current source was cloned, but scripting work should recover the exact commit before implementing runtime behavior.
- Full `IEnums.h` packet catalog is large; only foundation-critical IDs are implemented in C# so far.
- Full login success is blocked on production account/default account loading side effects, remaining `sendLoginClient` branches, `sendLoginRC`/`sendLoginNC`, and world warp behavior.
- The login packet parse boundary, server-list auth boundary, source-confirmed beginning of `Player::sendLogin`, `Server::playerLoggedIn` list-server add side effect, minimal pre-warp `sendLoginClient` packet order, the confirmed `__sendLogin` property ID table, and login property serialization are implemented. The current stop point is `ReadyForLevelWarp`, immediately before `warp(m_levelName, getX(), getY())`.
- Old-version map-file workaround, `flaghack_ip`, weapons, protected weapons, classes, and zlib-fix NPC weapon branches in `sendLoginClient` are traced but not implemented.
- The login-server-name branch in `Player::sendLogin` is blocked because C++ references `PLO_FULLSTOP`, but recovered `IEnums.h` only defines `PLO_FULLSTOP2 = 177`.
- `CFileQueue` uncompressed passthrough queue/flush is implemented; compression, encryption, and websocket wrapping remain blocked.
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
- WebSocket handling is gated by `WOLFSSL_ENABLED` code paths and needs a dedicated pass.
- `Server::doMain()` timing branches need a dedicated timing recovery pass.
- Gameplay systems, account persistence, RC/NC file browser, server-list protocol, and scripting bindings are not implemented.
