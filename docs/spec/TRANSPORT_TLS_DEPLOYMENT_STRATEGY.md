# Transport TLS/WolfSSL Deployment Strategy

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::onRecv()`
  - `Player::doMain()`
- `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/docker/GServer-x86_64-linux-gnu.dockerfile`
- `ai_resources/GServer-CPP-ORIGINAL/docker/GServer-x86_64-linux-musl.dockerfile`
- `ai_resources/GServer-CPP-ORIGINAL/docker/GServer-x86_64-w64-mingw.dockerfile`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/include/CString.h`
- `external/gs2lib/src/CString.cpp`

## Confirmed C++ Behavior

`CSocket` is a plain socket wrapper. The normal client transport is raw TCP
using the Graal two-byte length-prefixed frame protocol.

When `WOLFSSL_ENABLED` is compiled, `Player::onRecv()` and `Player::doMain()`
add an HTTP/WebSocket compatibility path:

1. Received socket bytes are appended to `m_recvBuffer`.
2. If `m_playerSock->webSocket` is already true, C++ calls
   `webSocketFixIncomingPacket(m_recvBuffer)` before normal frame parsing.
3. If the receive buffer contains `GET /` and `HTTP/1.1\r\n`, C++ treats it as
   an HTTP/WebSocket request.
4. If `Sec-WebSocket-Key:` is missing:
   - C++ sends a simple `HTTP/1.1 200 OK` HTML response.
   - `Player::doMain()` returns `false`, which leads to unregister/delete.
5. If `Sec-WebSocket-Key:` is present:
   - C++ sets `m_playerSock->webSocket = true`.
   - It appends GUID `258EAFA5-E914-47DA-95CA-C5AB0DC85B11` to the key.
   - It SHA1-hashes and base64-encodes the result.
   - It sends `HTTP/1.1 101 Switching Protocols`.
   - It includes `Sec-WebSocket-Protocol: binary`.
   - It clears the receive buffer and returns `true`.

The recovered C++ server CMake links OpenSSL only in the `V8NPCSERVER` branch.
The Docker build commands pass `-DWOLFSSL=ON`, while the recovered server CMake
also links `OpenSSL::SSL`/`OpenSSL::Crypto` when V8 support is enabled. This
source set does not prove a C# TLS implementation strategy beyond preserving the
client-facing WebSocket/TLS boundary.

## C# Compatibility Strategy

The C# server must not claim WebSocket/TLS compatibility until byte-level
behavior is implemented and certified against the original C++ server.

Current production-compatible deployment mode:

- Expose the C# server directly as raw TCP for classic/native clients.
- Use the confirmed Graal socket frame protocol implemented in
  `ClientTcpServer`, `SocketReceiveBuffer`,
  `InboundPacketDecoder`, and `GraalFileQueue`.
- Keep WebSocket/TLS disabled by default.

Allowed temporary deployment strategy for encrypted transport:

- Put TLS termination in front of the C# raw TCP listener only when the selected
  closed-source client is known to connect through that external transport and
  the bytes delivered to the C# server are the same raw Graal socket bytes the
  C++ server would parse after any TLS layer.
- Treat the external terminator as deployment infrastructure, not as certified
  C# protocol behavior.
- Do not mark WebSocket/WolfSSL rows as certified from proxy tests alone.

Blocked client-visible WebSocket behavior:

- HTTP `200 OK` response body bytes for non-WebSocket `GET /` requests.
- WebSocket `101 Switching Protocols` response bytes.
- Exact `Sec-WebSocket-Accept` formatting and header order.
- Production integration for `webSocketFixIncomingPacket` frame unwrapping.
- Production integration for outbound WebSocket frame wrapping.
- Full-session interactions between WebSocket framing and Graal
  encryption/compression generations.

## Implementation Gate

Before implementing C# WebSocket/TLS support:

1. Build a runnable original C++ server outside `ai_resources/` with the same
   `WOLFSSL_ENABLED` branch active.
2. Capture:
   - non-WebSocket HTTP `GET /` response bytes;
   - WebSocket handshake response bytes;
   - at least one encrypted/compressed post-login WebSocket flow.
3. Add golden fixtures from those captures.
4. Wire the already fixture-confirmed C# frame helpers behind an explicit
   WebSocket transport boundary.
5. Compare raw client-facing bytes against the original C++ captures.

Until those steps are complete, the faithful behavior is to keep WebSocket/TLS
marked blocked and to support only raw TCP for certified client flows.

## Checklist Mapping

This document satisfies the deployment-strategy half of:

```txt
Implement TLS/WolfSSL-equivalent behavior or document deployment compatibility strategy.
```

It does not implement WebSocket HTTP handshake or production socket integration.
WebSocket frame wrap/unwrap and gen4 bzip2 transport framing are covered
separately by the `tools/gs2lib-fixtures` harness and protocol golden tests.
