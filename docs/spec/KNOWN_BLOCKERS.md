# Spec Blockers

- Full `Player::sendLogin` is blocked on production account file loading, player property serialization, world/level entry, file queue flush behavior, RC/NC login packet families, and optional scripting hooks.
- The beginning of `Player::sendLogin` is implemented only through the pre-world continuation boundary. C# stops at `ReadyForWorldEntry`, immediately before `Server::playerLoggedIn(shared_from_this())`.
- `Server::playerLoggedIn` and the beginning of `sendLoginClient` are implemented only through a source-confirmed pre-warp boundary. C# stops at `ReadyForLevelWarp`, immediately before `warp(m_levelName, getX(), getY())`.
- The `sendProps(__sendLogin)` property ID table/order is implemented as a source-confirmed C# constant. Full production emission remains blocked on account/default-account data loading, old-client `PLPROP_GANI` behavior, and runtime-dependent properties outside the login set.
- The C# pre-warp boundary now uses the confirmed property serializer for explicit property IDs instead of inventing defaults.
- Old-version map-file workaround, flaghack mutation, weapons, protected weapons, classes, and zlib-fix NPC weapon branches in `sendLoginClient` are traced but not implemented.
- The login-server-name branch is blocked because C++ references `PLO_FULLSTOP`, but recovered `IEnums.h` only defines `PLO_FULLSTOP2 = 177`. Do not assume they are equivalent without source proof.
- Exact `CString::guntokenize()` behavior for ban reasons remains blocked; current C# tests cover plain reasons and the confirmed newline-to-carriage-return replacement path only.
- Real account/password validation must not be invented. The C++ server delegates password/auth verification to the list server through `SVO_VERIACC2`/`SVI_VERIACC2`.
- Account persistence behavior from `Account.cpp` is traced for the login-loading boundary, but production account services remain blocked on exact filesystem/default-account save behavior and member defaults.
- `CFileQueue` queue selection, compression generation rules, and socket partial-send semantics are documented. Production C# flush behavior remains blocked on byte-level compression/encryption/websocket fixtures.
- `warp(m_levelName, getX(), getY())` is traced through the first `setLevel`/`sendLevel` boundary. C# still stops before world-entry runtime.
- Server-list connection lifecycle, reconnect backoff, registration, and text/listserver side channels need a dedicated milestone.
