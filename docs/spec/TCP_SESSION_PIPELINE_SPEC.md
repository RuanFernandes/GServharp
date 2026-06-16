# TCP Session Pipeline Spec

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/src/CFileQueue.cpp`

See also `docs/spec/PRODUCTION_SOCKET_SESSION_SPEC.md` for the recovered
production listener/session lifecycle. This file documents the currently
implemented dev-only diagnostic pipeline and shared decode/queue boundaries.

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
  - decodes post-login inbound frames for confirmed gen1/gen2/gen3/gen5
    uncompressed/zlib branches
  - for gen1/gen2/gen5/gen6 decoded post-login bytes, preserves the
    source-confirmed stateful `PLI_RAWDATA` length transition through
    `ClientPacketStreamFramer`
  - for gen5 invalid compression types, logs the compatibility warning exposed
    by the protocol decoder and continues with decrypted bytes, matching the
    C++ log-and-continue branch
  - accepts decoded post-login `PLI_PLAYERPROPS` packets for the confirmed
    movement/player-prop subset and applies local runtime state mutation only
- `DevOnlyLocalTcpServer`
  - accepts one TCP client at a time
  - reads length-prefixed frames in a continuous per-connection loop
  - reuses one `ClientSessionSkeleton` for all frames on that connection
  - writes outbound bytes through `GraalFileQueue.FlushSocket`
  - stops clearly on the first unsupported post-login frame before gameplay,
    NPC, script, or map runtime handling

This is a diagnostic shell, not a production session loop.

## Socket Flush Boundary

`GraalFileQueue.FlushSocket` now covers the source-confirmed socket-level paths
that do not depend on unverified compression output:

- `ENCRYPT_GEN_1` and `ENCRYPT_GEN_6`: queued bytes are emitted directly.
- `ENCRYPT_GEN_2` and `ENCRYPT_GEN_3`: queued bytes are zlib-compressed and
  prefixed by raw big-endian compressed length.
- `ENCRYPT_GEN_5` with payload length `<= 55`: emits big-endian length,
  compression type `0x02`, and iterator-XOR encrypted payload bytes.
- `ENCRYPT_GEN_5` with payload length `56..0x2000`: emits big-endian length,
  compression type `0x04`, and iterator-XOR encrypted zlib payload bytes.
- Partial socket writes preserve remaining framed bytes for the next flush.

The dev-only TCP shell now routes confirmed login/level boundary output through
`FlushSocket`.

For Client3 and RC2 login packets with a confirmed login encryption key, the
shell configures `ENCRYPT_GEN_5` and therefore emits socket bytes with the
confirmed gen5 length prefix, compression type, zlib/uncompressed choice, and
iterator-XOR encryption. For web-client login packets, it uses confirmed gen1
passthrough behavior.

The shell deliberately asks the send-level boundary for the source-confirmed
"client already has this level modtime" branch. That keeps small diagnostic
`.nw` payloads in the gen5 zlib range and avoids the still-blocked bzip2 branch.
It also means a fresh closed-source client may not receive a full board payload
yet.

## Known Gaps

- A production listener/session loop is still missing. The recovered C++ shape
  is documented in `docs/spec/PRODUCTION_SOCKET_SESSION_SPEC.md`. The C# port
  now has `ProductionSocketReceiveBuffer` for the source-confirmed raw
  two-byte length frame buffering portion, but it is not wired into a
  production listener yet.
- The TCP shell processes multiple frames for one connection and can decode
  confirmed gen5 post-login client frames before applying `PLI_PLAYERPROPS`
  movement/player-prop updates.
- Unsupported post-login packet ids still stop before gameplay/runtime
  dispatch.
- Inbound gen4 and gen5 bzip2 frame payloads are explicitly blocked.
- Inbound `PLI_BUNDLE` expansion is not wired into the shell because the
  authoritative C++ `Player.cpp` snapshot does not bind `TPLFunc[PLI_BUNDLE]`.
- Outbound bzip2 socket framing for gen4 and gen5 payloads over `0x2000` bytes
  is still blocked.
- Websocket wrapping is not implemented.
- Touch/link traversal, reconnect cleanup, and live multi-session forwarding
  are not implemented.
