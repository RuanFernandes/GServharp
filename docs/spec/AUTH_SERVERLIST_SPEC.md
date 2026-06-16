# Auth And Server-List Spec

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `external/gs2lib/include/IEnums.h`
- `external/gs2lib/src/CFileQueue.cpp`

## Pre-Server-List Checks

After `msgPLI_LOGIN` parses type/version/account/password/identity, the C++ server checks:

1. Player count: if `playerList.size() >= maxplayers`, send `PLO_DISCMESSAGE "This server has reached its player limit."`.
2. IP ban: if `Server::isIpBanned(remoteIp)` and the account does not have `PLPERM_MODIFYSTAFFACCOUNT`, send `PLO_DISCMESSAGE "You have been banned from this server."`.
3. Allowed versions for clients only: exact version tokens or inclusive `start:end` ranges are compared through `getVersionID`. Rejection sends `PLO_DISCMESSAGE "Your client version is not allowed on this server.\rAllowed: {allowedVersionString}"`.
4. Server-list connection: if not connected, send `PLO_DISCMESSAGE "The login server is offline.  Try again later."`.

Only after these checks does C++ call `ServerList::sendLoginPacketForPlayer`.

## Server-List Auth Request

`ServerList::sendLoginPacketForPlayer` sends `SVO_VERIACC2`:

```txt
GCHAR SVO_VERIACC2
GCHAR account length
account bytes
GCHAR password length
password bytes
GSHORT player id
GCHAR player type bitfield
GSHORT identity length
identity bytes
```

The packet is queued through `ServerList::sendPacket`, which appends `\n` if missing. The current C# builder emits the packet body before queue newline/compression so tests can lock the field order and raw bytes.

## Server-List Registration Boundary

See also `docs/spec/SERVERLIST_LIFECYCLE_SPEC.md` for the dedicated
connect/reconnect/register/ping lifecycle documentation.

`ServerList::connectServer` performs these confirmed packet steps after the
socket connects:

1. Clear pending `CFileQueue` buffers.
2. Set the list-server queue codec to `ENCRYPT_GEN_1`.
3. Send `SVO_REGISTERV3` with raw `APP_VERSION`, flushing immediately.
4. Set the list-server queue codec to `ENCRYPT_GEN_2`.
5. Send `SVO_SERVERHQPASS` with `adminconfig.txt` key `hq_password`.
6. Send `SVO_NEWSERVER` with GCHAR-length strings for `name`, `description`,
   `language`, `APP_VERSION`, `url`, `serverip`, `serverport`, and `localip`.
7. Send `SVO_SERVERHQLEVEL`; the value is `0` when `onlystaff=true`, otherwise
   `adminconfig.txt` key `hq_level` with default `1`.
8. Send allowed-version config through `SVO_SENDTEXT` as
   `Listserver,settings,allowedversions,{comma-separated gtokenized versions}`.
9. Send current players through `SVO_SETPLYR`, followed by `SVO_PLYRADD` for
   each current player.

Implemented C# packet body builders:

- `RegisterV3`
- `ServerHqPass`
- `NewServer`
- `ServerHqLevel`
- `AllowedVersionsText`
- `SetPlayers`

The C# port now has `ProductionServerListLifecycle` behind
`IProductionServerListSocket`, which preserves this source-confirmed ordering
and local-IP behavior without implementing the concrete remote TCP client yet.

## Server-List Auth Response

`ServerList::msgSVI_VERIACC2` reads:

```txt
GCHAR account length
account bytes
GSHORT player id
GCHAR player type
remaining bytes as message
```

The response overwrites the local player account name with the server-list account name.

If `message != "SUCCESS"`, C++ sends `PLO_DISCMESSAGE` with that message, sets load-only, disconnects, and does not call `Player::sendLogin`.

If `message == "SUCCESS"`, C++ calls `Player::sendLogin`. The C# implementation now continues through the source-confirmed beginning of `Player::sendLogin` in `PlayerSendLoginContinuation`, then stops at `ReadyForWorldEntry` before `Server::playerLoggedIn`.

## Production C# Auth Boundary

`ProductionAuthServerListBoundary` models the production shape without inventing
account validation:

- Runs the source-confirmed pre-world checks through `PreWorldAuthBoundary`.
- Reads list-server connectivity from `IProductionServerListGateway`.
- Queues `SVO_VERIACC2` through `SendLoginPacketForPlayer` only when the
  pre-world checks accept the login.
- Does not inject a fake success response.

`ProductionServerListAuthResponseHandler` models the source-confirmed
`SVI_VERIACC2` response boundary:

- Parses the payload after the list-server packet id with
  `ServerListAuthPackets.ParseVerifyAccount2Response`.
- Looks up the pending session by the response player id and session type,
  matching the C++ `getPlayer(id, type)` lookup shape.
- Overwrites the local account name through
  `ClientSessionSkeleton.ReceiveServerListAuthResponse`.
- For non-`SUCCESS` messages, queues `PLO_DISCMESSAGE + message + "\n"` and
  moves the session to `Rejected`.
- For `SUCCESS`, moves the session to `ServerListAuthAcceptedPreWorld`, which
  is the C# stop point immediately before the production account/login
  continuation invokes the confirmed beginning of `Player::sendLogin`.
- Does nothing when no matching pending session exists; no fake account
  validation or fallback success is created.

The dev-only TCP shell still performs an explicit fake server-list success, but
that remains isolated behind `EnableDevOnlyAuth=true` and is not production
behavior.

## `Player::sendLogin` Pre-World Checks

`Player::sendLogin` starts by loading the account file with `loadAccount(account, isRC || isNC)`. Confirmed rejection cases before the signature/world-entry path include:

- Account file marks `BANNED` and the account lacks `PLPERM_MODIFYSTAFFACCOUNT`: `"You have been banned.  Reason: {banReason with newline converted to carriage return after guntokenize}"`.
- RC/NC without staff rights or admin IP: `"You do not have RC rights."`.
- Client on `onlystaff=true` without staff rights: `"This server is currently restricted to staff only."`.
- Client admin IP mismatch unless `IPRANGE` contains `0.0.0.0`: `"Your IP doesn't match one of the allowed IPs for this account."`.
- Same non-guest account already in use by the same client family and active within 30 seconds: `"Account is already in use."`.

The implemented success-boundary path now has a production account-login wrapper:
`ProductionAccountLoginBoundary` loads the server-list-approved account through
`AccountLoadService`, saves/adds a default-created account when C++ would do so,
maps `BANNED`, `BANREASON`, `LOCALRIGHTS`, staff-list membership, and `IPRANGE`
wildcards into `PlayerSendLoginAccount`, then runs
`PlayerSendLoginContinuation`.

`PlayerSendLoginContinuation` sends `PLO_SIGNATURE`, skips the unresolved
login-server-name branch unless the missing `PLO_FULLSTOP` opcode is recovered,
sends `PLO_UNKNOWN168` for clients, checks duplicate account sessions, and stops
before registering the player with the list server through
`Server::playerLoggedIn`. Everything after that point is beyond this milestone
because it begins world, level, props, RC, NC, and scripting behavior.
