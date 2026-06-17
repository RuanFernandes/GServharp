# Production Socket Session Spec

## Source Files

Authoritative C++ and recovered dependency sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Player.h`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/include/CString.h`

## Confirmed Listener Setup

`Server::init` configures the player listener as a `CSocket` server stub:

```cpp
m_playerSock.setType(SOCKET_TYPE_SERVER);
m_playerSock.setProtocol(SOCKET_PROTOCOL_TCP);
m_playerSock.setDescription("playerSock");
m_playerSock.init((oInter.isEmpty() ? 0 : oInter.text()), m_settings.getStr("serverport").text());
m_playerSock.connect();
m_sockManager.registerSocket((CSocketStub*)this);
```

Confirmed details:

- `serverinterface` is read from settings unless overridden.
- Interface value `"AUTO"` is treated as empty/null.
- The port comes from setting `serverport`.
- `CSocket::connect` binds, listens with `SOMAXCONN`, disables Nagle
  (`TCP_NODELAY`), sets the socket non-blocking, and transitions to
  `SOCKET_STATE_LISTENING`.
- `Server` itself implements `CSocketStub` for the listening socket.
- `Server::canRecv()` always returns `true`.
- `Server::canSend()` always returns `false`.
- `Server::onSend()` is a no-op that returns `true`.

## Confirmed Accept Flow

`Server::onRecv` is the accept callback for the listening socket:

```cpp
CSocket* newSock = m_playerSock.accept();
if (newSock == nullptr)
    return true;

auto newPlayer = std::make_shared<Player>(newSock, 0);
if (!addPlayer(newPlayer))
    return false;

m_sockManager.registerSocket((CSocketStub*)newPlayer.get());
return true;
```

Confirmed details:

- `CSocket::accept` only works for server-type TCP sockets.
- `accept` returns null for non-ready non-blocking cases such as
  `SOCK_EWOULDBLOCK`/`SOCK_EINPROGRESS`; this is not an error and
  `Server::onRecv` returns `true`.
- Accepted sockets are stored as client-type TCP sockets in connected state.
- The remote IP string is assigned as the socket description.
- Nagle is disabled on accepted sockets.
- Accepted sockets are non-blocking.
- New players are constructed with socket pointer and id `0`, then
  `Server::addPlayer` assigns the real id unless an explicit id is supplied.
- The new `Player` is registered with `CSocketManager`; registration queues the
  stub in `newStubs`, so the player becomes active after the current update pass.

## Confirmed Socket Manager Loop

`Server::operator()` runs:

```cpp
while (running)
{
    doMain();
    cleanupDeletedPlayers();
    ...
}
```

`Server::doMain` starts each loop by polling sockets:

```cpp
m_sockManager.update(0, 5000); // 5ms
```

`CSocketManager::update` behavior:

- Builds read and write fd sets from registered stubs.
- Adds a stub to the read set when `stub->canRecv()` returns true.
- Adds a stub to the write set when `stub->canSend()` returns true.
- Calls `select(fd_max + 1, ..., timeval{sec,usec})`.
- For each ready stub, calls `onRecv()` first, then `onSend()` only if
  `onRecv()` succeeded.
- If either callback returns false, calls `stub->onUnregister()` and nulls the
  stub entry.
- Removes null stubs after the pass.
- Appends `newStubs` only after the pass, preserving the deferred registration
  behavior.

`CSocketManager::updateSingle` is used by `Player::doMain` after receive parsing
to flush that same player's pending output immediately:

```cpp
m_server->getSocketManager().updateSingle(this, false, true);
```

Confirmed compatibility point:

- A production C# loop should preserve receive-before-send ordering for a ready
  player and should allow a receive pass to trigger an immediate write attempt
  for that same session.

## Confirmed Player Receive Flow

`Player::onRecv`:

- Returns false when the socket pointer is null or disconnected.
- Calls `m_playerSock->getData(&size)`.
- Appends non-empty data to `m_recvBuffer`.
- For websocket builds, optionally unwraps/fixes incoming websocket frames.
- If size is zero and socket state became disconnected, returns false.
- Calls `doMain()`.

`CSocket::getData` details:

- Reads up to `0x8000` bytes into a static buffer.
- Uses `recv` for TCP.
- On connection-loss errors, calls `disconnect`.
- If read size is zero and the error is not `SOCK_EWOULDBLOCK`, calls
  `disconnect`.
- Returns size zero for would-block without disconnect.

## Confirmed Frame Buffering

`Player::doMain` processes `m_recvBuffer` as raw two-byte big-endian
length-prefixed socket frames:

```cpp
m_recvBuffer.setRead(0);
while (m_recvBuffer.length() > 1)
{
    m_lastData = time(0);
    auto len = (unsigned short)m_recvBuffer.readShort();
    if ((unsigned int)len > (unsigned int)m_recvBuffer.length() - 2)
        break;

    unBuffer = m_recvBuffer.readChars(len);
    m_recvBuffer.removeI(0, len + 2);
    ...
}
```

Confirmed details:

- At least two buffered bytes are required before length is read.
- Length uses raw `CString::readShort`, matching the already-confirmed
  big-endian raw short behavior.
- If a full frame is not buffered, parsing stops and the partial frame remains
  in `m_recvBuffer`.
- `m_lastData` is updated after a two-byte length is available, before the full
  frame-size check.
- Each complete frame is removed from the front of the receive buffer after its
  payload is copied.

## Confirmed Inbound Decode Boundary

After extracting a frame payload, `Player::doMain` decodes based on
`m_encryptionCodecIn.getGen()`:

- `ENCRYPT_GEN_1`: no frame-level decompression or decryption.
- `ENCRYPT_GEN_2`: zlib-decompress whole frame.
- `ENCRYPT_GEN_3`: zlib-decompress whole frame; per-packet decryption happens
  later inside `parsePacket`.
- `ENCRYPT_GEN_4+`: call `decryptPacket`.

`Player::decryptPacket`:

- Gen3 client packets are decrypted per inner packet after line/raw extraction.
- Gen4 decrypts whole frame with `COMPRESS_BZ2` limit, then bzip2-decompresses.
- Gen5+ reads and removes the first compression type byte, sets encryption limit
  from that type, decrypts the remaining payload, and then:
  - zlib-decompresses for `COMPRESS_ZLIB`
  - bzip2-decompresses for `COMPRESS_BZ2`
  - leaves payload as-is for `COMPRESS_UNCOMPRESSED`
  - logs an error and continues for unknown compression types

This matches `docs/spec/INBOUND_PACKET_DECODE_SPEC.md`.

## Confirmed Inner Packet Dispatch

`Player::parsePacket` has one special first-packet path:

- If `m_type == PLTYPE_AWAIT`, increment packet count and call
  `msgPLI_LOGIN(CString() << pPacket.readString("\n"))`.
- This first login packet is read as newline-delimited from the decoded frame.

Then normal dispatch loops while bytes remain:

- If `m_nextIsRaw`, read exactly `m_rawPacketSize` bytes.
- For clients, and for RC newer than `RCVER_1_1`, strip one trailing newline
  from the raw packet when present.
- Otherwise read the next newline-delimited packet.
- For gen3, decrypt the extracted packet before reading its id.
- Read packet id with `curPacket.readGUChar()`.
- RC 1.1 has a special file-upload `"\\n"` cleanup branch for
  `PLI_RC_FILEBROWSER_UP`.
- Dispatch through `TPLFunc[id]`.
- If a handler returns false, `parsePacket` returns false and receive handling
  disconnects/deletes the player through the socket manager path.

Confirmed invalid-packet behavior:

- `msgPLI_NULL` increments `m_invalidPackets`.
- After more than five invalid packets, C++ sends
  `PLO_DISCMESSAGE "Disconnected for sending invalid packets."` and returns
  false.

## Confirmed Outbound Queue And Send Flow

`Player::sendPacket`:

- Ignores empty packets.
- Appends `'\n'` when `appendNL` is true and the packet does not already end in
  newline.
- Queues the resulting bytes through `m_fileQueue.addPacket`.

`Player::canSend()` returns `m_fileQueue.canSend()`.

`Player::onSend`:

- Returns false if the socket is null or disconnected.
- Calls `m_fileQueue.sendCompress()`.
- Returns true.

`CFileQueue` behavior:

- Splits queued packets between normal and file buffers.
- Preserves `PLO_RAWDATA` header plus exact following payload behavior.
- Sends normal data until 48KB, never exceeding 60KB unless the next normal
  packet alone is already huge.
- Sends file-buffer data when forced, after enough non-file bytes, after several
  empty send calls, or when the combined chunk is below thresholds.
- Keeps an internal `oBuffer` of already-framed data and removes only the bytes
  that `CSocket::sendData` reports as sent.
- Existing C# `GraalFileQueue` already covers the source-confirmed generation
  branches documented in `docs/spec/CFILEQUEUE_FLUSH_SPEC.md`.

`CSocket::sendData`:

- Returns zero and sets remaining size to zero when disconnected.
- Calls `send(..., MSG_NOSIGNAL)`.
- On connection-loss errors, disconnects.
- On `SOCK_EAGAIN`, returns zero without disconnecting.
- Decrements `*dsize` by the number of bytes sent and returns the sent count.

Confirmed compatibility point:

- A production C# socket writer must tolerate partial writes and retain unsent
  framed bytes for later write readiness, which `GraalFileQueue.FlushSocket`
  models with its partial-send tests.

## Confirmed Disconnect And Cleanup Flow

False return from `Player::onRecv` or `Player::onSend` causes
`CSocketManager` to call `Player::onUnregister`, which calls
`m_server->deletePlayer(shared_from_this())`.

`Player::disconnect` also calls `Server::deletePlayer`.

`Server::deletePlayer`:

- Returns true for null players.
- Inserts the player into `m_deletedPlayers`.
- On first insertion, asks the server list to delete the player.
- Does not immediately unregister the socket or erase from `m_playerList`.

`Server::cleanupDeletedPlayers` later:

- Handles V8 script logout retention when enabled.
- Frees the player id.
- Calls `m_sockManager.unregisterSocket(player.get())`.
- Erases from `m_playerList`.
- Calls `player->cleanup()`.
- Removes from `m_deletedPlayers`.

`Player::cleanup`:

- Calls `m_fileQueue.sendCompress()` first to try to flush unsent data such as
  disconnect messages.
- Saves non-load-only client accounts when loaded.
- Leaves level and performs other runtime cleanup outside the current production
  socket foundation scope.

## Websocket/TLS Branch

When `WOLFSSL_ENABLED` is compiled:

- `Player::doMain` detects HTTP `GET /` + `HTTP/1.1`.
- Without `Sec-WebSocket-Key`, it sends an HTML response and returns false.
- With `Sec-WebSocket-Key`, it performs a websocket handshake, clears
  `m_recvBuffer`, marks the socket as websocket, and returns true.
- `Player::onRecv` calls `webSocketFixIncomingPacket`.
- `CFileQueue::sendCompress` calls `webSocketFixOutgoingPacket` before sending.

The C# production socket skeleton must leave websocket HTTP handshake and TLS
branches blocked until a dedicated byte-level pass wires them into production
sessions. WebSocket binary frame wrap/unwrap bytes are covered separately by the
protocol fixture helpers.

## C# Production Mapping Guidance

Recommended production shape:

- Keep `LocalDebugTcpServer` explicitly local-debug.
- Add a production listener with a C++-named/session-oriented boundary, but use
  idiomatic .NET internals.
- Preserve one logical `Player` session object per accepted socket.
- Preserve a receive buffer per session and support partial two-byte frame
  headers and partial frame payloads.
- Reuse `PacketFramer`, `InboundPacketDecoder`, `ClientPacketStreamFramer`, and
  `GraalFileQueue` instead of duplicating codec logic.
- Dispatch only packet ids whose C++ behavior has been implemented.
- Unsupported packet ids should follow source-confirmed invalid-packet counting
  once `msgPLI_NULL` is modeled; until then, they must be explicit blocked
  results/logs rather than silently ignored.
- Preserve receive-before-send ordering.
- Preserve deferred deletion semantics: a disconnect marks a session for
  cleanup, and cleanup unregisters/removes it later.
- Do not wire fake auth into production; local-debug auth must stay behind explicit
  local-debug options.

## Implemented C# Production Foundation

`ClientTcpServer` implements the current source-confirmed listener skeleton:

- binds a `TcpListener` to a configured address/port
- accepts a TCP client
- disables Nagle on the accepted `TcpClient`, matching C++ `TCP_NODELAY`
- assigns the first dynamic player id as `2`, matching `PLAYERID_INIT`
- creates one logical `ClientSocketSessionContext` per accepted socket
- reads arbitrary chunks up to `0x8000` bytes, matching C++ `CSocket::getData`
  buffer size
- feeds chunks through `SocketReceiveBuffer`
- dispatches complete frame payloads, without the two-byte socket header, to
  `IClientSocketFrameHandler`
- writes handler-provided outbound bytes to the socket
- returns `ClientDisconnected` when the client closes the socket
- returns `HandlerStopped` when the handler asks to stop the session

This is not the full production socket manager yet. It is a safe skeleton for
confirmed accept/framing/lifecycle behavior and intentionally keeps packet
decode/auth/gameplay behind explicit handler boundaries.

`SocketReceiveBuffer` implements the source-confirmed frame buffering
part of `Player::doMain`:

- arbitrary received byte chunks can be appended
- fewer than two bytes are retained until a full header exists
- the raw two-byte big-endian length is read without consuming an incomplete
  frame
- complete frame payloads are returned without their two-byte socket headers
- partial trailing frame data remains buffered for the next read
- multiple complete frames in one received chunk are returned in order

This is intentionally below packet decode/dispatch. It does not decrypt,
decompress, split newline packets, apply `PLI_RAWDATA`, or dispatch packet ids.
Those behaviors remain in `InboundPacketDecoder`,
`ClientPacketStreamFramer`, and later production session-dispatch work.

`PostLoginPacketDispatcher` implements the first decoded post-login
dispatch boundary:

- accepts already-decoded inner packet bytes
- handles only the source-confirmed `PLI_PLAYERPROPS` movement/property subset
- returns blocked results for C++ `TPLFunc` ids that are assigned but not
  implemented in C# yet
- models `msgPLI_NULL` invalid-packet counting for unassigned ids
- returns the exact invalid-packet disconnect message after the sixth
  unassigned packet

It is not wired into a production auth/session loop yet.

`PostLoginFrameHandler` adapts that dispatcher to
`IClientSocketFrameHandler` for already-authenticated post-login sessions.
It decodes a socket frame, preserves the existing `PLI_RAWDATA` stream-framing
state through `ClientPacketStreamFramer`, logs dispatch statuses, continues for
handled packets, blocks assigned-but-unimplemented packet ids, and returns the
source-confirmed invalid-packet disconnect bytes when `msgPLI_NULL` exceeds
five invalid packets.

## Current C# Gaps For Phase 1

- The first production listener skeleton exists, but only as an accept-one
  boundary for confirmed framing/lifecycle tests.
- No production multi-session socket manager equivalent exists yet.
- The production receive buffer is wired into the listener skeleton.
- Local debug TCP currently reads exactly one full frame at a time from
  `NetworkStream`; C++ buffers arbitrary chunks and may receive partial headers,
  partial payloads, or multiple frames at once.
- Production unsupported packet handling now has a reusable
  `PostLoginPacketDispatcher` and
  `PostLoginFrameHandler` model for `msgPLI_NULL` invalid-packet
  counting/disconnect, but it is not wired into a production auth/session loop
  yet.
- Production deferred deletion and cleanup are not wired.
- Websocket HTTP handshake/session integration and TLS remain blocked.
