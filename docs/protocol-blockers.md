# Protocol Blockers

This document is the implementation gate for protocol work. `gs2lib` has now been recovered at commit `63b1ae96491c188905b50c6b61c8532c601a2122`, removing the original missing-header blocker. Remaining blockers are now narrower parity-fixture and integration blockers.

## Hard Blockers

| Area | Blocked by | Why it matters |
| --- | --- | --- |
| Full login/session implementation | golden byte fixtures or an instrumented original C++ run | Packet IDs, codecs, and queue primitives are recovered, but production login still needs byte-for-byte request/response fixtures for supported client generations. |
| Version gates | `IEnums.h` and version helpers | `CLVER_*`, `RCVER_*`, `NCVER_*` values drive login, file transfer, maps, shooting, and old-client behavior. |
| Compression/file queue integration | byte fixtures for `CFileQueue::sendCompress` | The algorithm is recovered, but zlib/bz2 output and queue flushing need fixtures before connecting it to live sessions. |
| Websocket compatibility | `IUtil` analysis and fixtures | `CSocket` delegates websocket framing/masking to `IUtil`; this still needs a focused pass. |
| Socket lifecycle parity | live integration tests | `CSocket` behavior is recovered, but .NET networking must be tested against equivalent non-blocking, partial-send, disconnect, and error paths. |

## Partial Non-Blocking Facts

- The C++ server expects the protocol headers from `gs2lib`, and the exact dependency was recovered under `external/gs2lib/`.
- Packet symbol names, numeric IDs, and handler ownership are confirmed by visible C++ plus recovered `IEnums.h`.
- `PLI_RAWDATA = 50`, `PLO_RAWDATA = 100`, `PLO_BOARDPACKET = 101`, `PLO_FILE = 102`, `PLO_SET_ENC_KEY = 252`, and `PLO_BUNDLE = 253`.
- Bundle length prefixes are confirmed as raw big-endian `CString::writeShort`/`readShort`.
- Graal printable integer codecs are confirmed in `CString.cpp`.
- Legacy encryption generation constants and primitive algorithms are confirmed in `CEncryption.cpp`.
- `SVF_HEAD = 0`, `SVF_BODY = 1`, `SVF_SWORD = 2`, `SVF_SHIELD = 3`, and `SVF_FILE = 4` are directly defined in `ServerList.h`.
- Outbound newline behavior is confirmed in `Player::sendPacket` and `ServerList::sendPacket`.
- Login first-byte decoding is confirmed: `m_type = 1 << pPacket.readGChar()`.
- Encryption/compression generation dispatch is confirmed at the flow level in `IPacketHandler.h`.

## Safe Work While Blocked

- Improve documentation.
- Add source-status tests.
- Add tests for behavior directly visible in C++ call sites.
- Add more packet ID constants from `IEnums.h` as implementation work reaches each packet family.
- Port `CFileQueue` behind tests using captured or generated C++ fixture bytes.
- Prepare fixture harnesses that can replay captured C++ bytes for login, file transfer, and server-list interactions.

## Unsafe Work While Blocked

- Assigning any numeric IDs from Rust/Python when they disagree with recovered `IEnums.h`.
- Implementing production login against closed-source clients before fixture validation.
- Treating zlib/bz2 output compatibility as proven without fixtures.
- Replacing C++ queue/socket edge behavior with idiomatic .NET behavior that changes partial sends, disconnects, packet ordering, or compression thresholds.
