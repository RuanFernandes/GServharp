# TCP Session Pipeline Spec

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/src/CFileQueue.cpp`

## Confirmed C++ Receive Behavior

`Server::onRecv` accepts a TCP socket, constructs a `Player`, adds it to the
server player list, and registers it with the socket manager.

`Player::doMain` appends incoming socket bytes to `m_recvBuffer`, then reads
raw two-byte big-endian length-prefixed frames. If the full frame is not
buffered, it waits for more data. Once a frame is complete, it decompresses or
decrypts according to inbound encryption generation and calls
`Player::parsePacket`.

The first packet while `m_type == PLTYPE_AWAIT` is special: `parsePacket` reads
one newline-delimited login packet and calls `msgPLI_LOGIN` before normal
packet dispatch.

## Dev-Only C# Shell

Implemented:

- `DevOnlyLocalSessionPipeline`
  - reads source-confirmed length-prefixed frames through `PacketFramer`
  - parses the first login packet through `ClientSessionSkeleton`
  - runs existing pre-world auth checks
  - injects a clearly dev-only server-list success response only when
    `EnableDevOnlyAuth=true`
  - enters `PlayerSendLoginContinuation`, `PostLoginWorldEntryBoundary`,
    `WarpWorldEntryBoundary`, `NwLevelFileLoader`, and `SendLevelBoundary`
  - stops at `DynamicLevelPayloadSent` before live world simulation
- `DevOnlyLocalTcpServer`
  - accepts one TCP client at a time
  - reads exactly one length-prefixed frame without waiting for EOF
  - writes the uncompressed queued outbound bytes from `GraalFileQueue`

This is a diagnostic shell, not a production session loop.

## Known Gaps

- The TCP shell processes one login frame and then closes the connection.
- Outbound compressed/encrypted socket framing for gen2+ clients is still
  blocked on `CFileQueue::sendCompress` fixtures.
- Websocket wrapping is not implemented.
- Continuous packet streaming, movement, reconnect cleanup, and multi-session
  forwarding are not implemented.

