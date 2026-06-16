# Spec Blockers

- Full `Player::sendLogin` is blocked on production account file loading, player property serialization, world/level entry, file queue flush behavior, RC/NC login packet families, and optional scripting hooks.
- The beginning of `Player::sendLogin` is implemented only through the pre-world continuation boundary. C# stops at `ReadyForWorldEntry`, immediately before `Server::playerLoggedIn(shared_from_this())`.
- `Server::playerLoggedIn` and the beginning of `sendLoginClient` are implemented only through a source-confirmed pre-warp boundary. C# stops at `ReadyForLevelWarp`, immediately before `warp(m_levelName, getX(), getY())`.
- The `sendProps(__sendLogin)` property ID table/order is implemented as a source-confirmed C# constant. Full production emission remains blocked on account/default-account data loading, old-client `PLPROP_GANI` behavior, and runtime-dependent properties outside the login set.
- The C# pre-warp boundary now uses the confirmed property serializer for explicit property IDs instead of inventing defaults.
- Production startup now resolves the server name from overrides,
  `startupserver.txt`, or exactly one `servers/` directory, and loads
  source-confirmed `CSettings` syntax for `config/serveroptions.txt` and
  `config/adminconfig.txt`. Production runtime remains blocked before sockets,
  list-server auth, full config loaders, filesystem scans, and gameplay.
- Old-version map-file workaround, flaghack mutation, weapons, protected weapons, classes, and zlib-fix NPC weapon branches in `sendLoginClient` are traced but not implemented.
- The login-server-name branch is blocked because C++ references `PLO_FULLSTOP`, but recovered `IEnums.h` only defines `PLO_FULLSTOP2 = 177`. Do not assume they are equivalent without source proof.
- Exact `CString::guntokenize()` behavior for ban reasons remains blocked; current C# tests cover plain reasons and the confirmed newline-to-carriage-return replacement path only.
- Real account/password validation must not be invented. The C++ server delegates password/auth verification to the list server through `SVO_VERIACC2`/`SVI_VERIACC2`.
- Account file parsing for confirmed `GRACC001` fields/defaults is implemented.
  The C# account loading boundary now also performs source-confirmed
  case-insensitive lookup, default-account fallback, startlevel/startx/starty
  overrides, and save/add side-effect signalling. Real `saveAccount` disk writes,
  full filesystem resync behavior, and guest random `pc:` identity generation
  remain blocked.
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
  source-confirmed `setLevel` pre-runtime boundary for missing levels,
  `PLO_PLAYERWARP`, `PLO_PLAYERWARP2`, and modern non-zero-modtime no-warp
  packet behavior. Modern `sendLevel` is implemented through dynamic
  board-change/chest/horse/baddy packet wrappers and the first post-dynamic
  packets (`PLO_GHOSTICON`, optional `PLO_ISLEADER`, `PLO_NEWWORLDTIME`,
  `PLO_SETACTIVELEVEL`, opaque NPC packet bytes, and nearby
  `PLO_OTHERPLPROPS` visibility sync from snapshots). Full `warp(...)`,
  fallback to previous/unstick levels, singleplayer/group-map cloning, old
  `sendLevel141`, production horse/baddy/NPC state construction, and live
  multi-session player-list forwarding remain blocked because they enter
  level/map/NPC/player-list runtime.
- Minimal level/player ownership is implemented for source-confirmed id
  assignment, level player-list append/remove, leader detection, deferred
  deletion cleanup, and runtime visibility filtering. It does not implement
  list-server side effects, scripting hooks, player-id generation, production
  server player-list iteration compatibility, movement, or live forwarding.
- Level format detection is implemented for the exact C++ extension checks and
  eight-byte signatures. A read-only indexed filesystem boundary and
  filesystem-backed `.nw` loading path now exist for static
  board/layer/link/sign/chest payloads. Full production `Level::findLevel`
  cache/map ownership, `foldersconfig.txt` parsing, `.graal`/`.zelda` parsing,
  horse/baddy/NPC runtime construction, and file-transfer behavior remain
  blocked.
- Pure `.nw` parsing is implemented for confirmed `BOARD`, `LINK`, `SIGN`,
  `CHEST`, `NPC`, and `BADDY` source-line behavior, plus board/layer/link/sign
  and chest packet builders. Player sign translation, NPC runtime creation,
  baddy ids/props/AI, chest opening gameplay, and `.graal`/`.zelda` parsers
  remain blocked.
- Incoming decoded `PLI_PLAYERPROPS` movement/property parsing is implemented
  for the confirmed X/Y/Z, X2/Y2/Z2, sprite, current-level, and gani subset.
  Safe local runtime mutation and a packet builder for confirmed movement
  `PLO_OTHERPLPROPS` forwarding bytes exist. Confirmed inbound gen1/gen2/gen3
  and gen5 uncompressed/zlib frame decode exists, and the dev-only TCP shell can
  decode gen5 movement frames after login. Inbound bzip2 branches, live
  multi-session forwarding, full `setProps`, touch/link traversal,
  NPC/chest/combat side effects, and invalid-update behavior remain blocked.
- Server-list connection lifecycle, reconnect backoff, registration, and text/listserver side channels need a dedicated milestone.
