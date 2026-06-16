# Server-List Lifecycle Spec

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/ServerList.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/include/IEnums.h`

## Constructor / Socket Shape

`ServerList::ServerList` constructs a client TCP socket:

- `m_fileQueue` is bound to `m_socket`
- socket protocol is `SOCKET_PROTOCOL_TCP`
- socket type is `SOCKET_TYPE_CLIENT`
- description is `"listserver"`
- `m_lastData` and `m_lastTimer` are set to `time(0)`
- `ServerList::createFunctions` initializes the `TSLFunc` dispatch table once

`canRecv()` returns false only when the socket state is disconnected.
`canSend()` delegates to `m_fileQueue.canSend()`.
`onSend()` calls `m_fileQueue.sendCompress()` and returns true.

## Receive / Parse Flow

`onRecv()`:

1. calls `m_socket.getData(&size)`
2. appends non-empty bytes to `m_readBuffer`
3. returns false if size is zero and the socket is disconnected
4. calls `main()`
5. returns true

`main()`:

1. returns false immediately when not connected
2. while the read buffer has at least two bytes:
   - set `m_lastData = time(0)`
   - read raw big-endian frame length
   - stop if the full frame has not arrived
   - copy the frame bytes, remove length plus payload from `m_readBuffer`
   - zlib-decompress the frame
   - call `parsePacket(unBuffer)`
3. calls `m_server->getSocketManager().updateSingle(this, false, true)`
4. returns current connected state

`parsePacket()`:

- if `m_nextIsRaw`, reads exactly `m_rawPacketSize` bytes
- otherwise reads one newline-delimited packet
- reads packet id with `readGUChar`
- dispatches through `TSLFunc[id]`

`SVI_RAWDATA` currently has its raw-size code commented out in this C++ snapshot,
so real raw-data transition behavior is not active for server-list packets.

## Timed Reconnect Flow

`Server::doTimedEvents()` calls `m_serverlist.doTimedEvents()` every second.

`ServerList::doTimedEvents()`:

1. sets `m_lastTimer = time(0)`
2. checks `getConnected()`
3. if disconnected and `difftime(m_lastTimer, m_nextConnectionAttempt) >= 0`,
   calls `connectServer()`
4. if connect fails:
   - increments `m_connectionAttempts` while it is below 8
   - computes `waitTime = min(pow(2, m_connectionAttempts), 300)`
   - sets `m_nextConnectionAttempt = m_lastTimer + waitTime + (rand() % 5)`
5. if connect succeeds, resets `m_connectionAttempts = 0`

The C# `ServerListReconnectState` already models the confirmed backoff math and
accepts an injectable jitter source for the `rand() % 5` value.

## connectServer Flow

`connectServer()` returns true immediately if already connected.

When disconnected:

1. initialize the socket with settings `listip` and `listport`
2. return false if initialization fails
3. connect the socket
4. return false if connection fails
5. register the `ServerList` socket with the socket manager
6. read settings:
   - `name`
   - `description`
   - `language`, default `"English"`
   - `APP_VERSION`
   - `url`, default `"http://www.graal.in/"`
   - `serverip`, default `"AUTO"`
   - `serverport`, default `"14900"`
   - `localip`
7. if `localip` is empty or `"AUTO"`, use `m_socket.getLocalIp()`
8. if local IP is `"127.0.1.1"` or `"127.0.0.1"`, log a warning and send an
   empty local IP string
9. clear outgoing file-queue buffers
10. set queue codec to `ENCRYPT_GEN_1`
11. send `SVO_REGISTERV3 + APP_VERSION` with immediate flush
12. set queue codec to `ENCRYPT_GEN_2`
13. send `SVO_SERVERHQPASS + hq_password`
14. send `SVO_NEWSERVER` with length-prefixed server info fields
15. send `SVO_SERVERHQLEVEL`, using `0` when `onlystaff=true`, otherwise
   `hq_level` with default `1`
16. call `sendVersionConfig()`
17. call `sendPlayers()`
18. return connected state

The ordering is compatibility-significant. C++ comments note that
`SVO_SERVERHQPASS` must be sent before `SVO_NEWSERVER` or the server gets an
incorrect name.

## Initial Packet Order

After successful socket connection, packet body order is:

```txt
SVO_REGISTERV3 + APP_VERSION             // gen1, sendNow=true
SVO_SERVERHQPASS + hq_password           // gen2
SVO_NEWSERVER + server info fields       // gen2
SVO_SERVERHQLEVEL + level byte           // gen2
SVO_SENDTEXT allowedversions             // gen2
SVO_SETPLYR                              // gen2
SVO_PLYRADD for each non-NC player       // gen2, later slice
```

`ServerList::sendPacket` appends `\n` before queueing when the packet does not
already end with newline.

## Ping

`msgSVI_PING` replies with:

```txt
SVO_PING
```

This packet body is represented by `ServerListAuthPackets.Ping()`.

## Login Auth Boundary

`Player::msgPLI_LOGIN` eventually calls:

```cpp
ServerList::sendLoginPacketForPlayer(player, password, identity)
```

That sends `SVO_VERIACC2` through the server-list queue. The C# production auth
boundary queues the confirmed packet body through
`IProductionServerListGateway`.

`msgSVI_VERIACC2` parses:

```txt
GCHAR account length
account bytes
GSHORT player id
GCHAR player type
remaining bytes as message
```

It overwrites the player's account name. Non-`SUCCESS` messages are forwarded
as `PLO_DISCMESSAGE`, set load-only, and disconnect. `SUCCESS` calls
`Player::sendLogin()`.

The C# `ProductionServerListAuthResponseHandler` now parses this response
payload and applies it to the pending `ClientSessionSkeleton` found by id/type.
It queues the confirmed disconnect message for rejection, and marks success as
`ServerListAuthAcceptedPreWorld` so the account/login continuation can proceed
without local fake auth. The concrete zlib-framed list-server receive loop is
still blocked.

## C# Mapping

Implemented:

- `ProductionServerListLifecycle`
  - depends on `IProductionServerListSocket`
  - initializes/connects/registers the socket boundary
  - clears outgoing buffers on successful connect
  - switches codec gen1 -> gen2 in the source-confirmed order
  - sends the confirmed registration/HQ/server/version/player-clear packet
    sequence
  - resolves `localip` from configured value or socket-local value and clears
    loopback values
- `ServerListReconnectState`
  - models the confirmed exponential reconnect backoff and jitter window
- `ServerListAuthPackets`
  - builds confirmed server-list packet bodies
- `ProductionServerListAuthResponseHandler`
  - parses confirmed `SVI_VERIACC2` payloads
  - resolves the pending player session by id/type
  - applies the C++ success/rejection boundary without inventing auth

Not implemented:

- a concrete production TCP client implementation of `IProductionServerListSocket`
- real `CSocketManager`-style readiness polling for the list-server socket
- zlib frame receive loop for live list-server responses
- `SVO_PLYRADD` replay from live production player repositories inside
  `ProductionServerListLifecycle`
- full handling for `SVI_*` packets beyond auth response parsing and ping
  packet body construction
- production connection to an actual remote list server

## Tests

Current tests cover:

- successful connect packet order and codec switch sequence
- `localip` auto resolution and loopback clearing
- initialization failure stopping before register/send
- reconnect backoff math through `ServerListReconnectState`
- packet body builders for registration, HQ pass/level, new-server,
  allowed-versions text, request-list text, ping, and verify-account
- auth response success, rejection, and missing-session branches through
  `ProductionServerListAuthResponseHandler`
