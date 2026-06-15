# gs2lib Recovery

## Result

`gs2lib` was recovered successfully.

- Original C++ build source: `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt`
- CMake repository URL: `https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git`
- Target commit: `63b1ae96491c188905b50c6b61c8532c601a2122`
- Recovered checkout: `external/gs2lib/`
- Verified checkout HEAD: `63b1ae96491c188905b50c6b61c8532c601a2122`

The original C++ build exposes this dependency through `gs2lib_SOURCE_DIR/include`. `ai_resources/` was not modified.

## Headers Found

All protocol-critical missing headers were found in the recovered dependency:

- `external/gs2lib/include/IEnums.h`
- `external/gs2lib/include/CString.h`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/include/CSocket.h`

Matching implementation files were also found:

- `external/gs2lib/src/CString.cpp`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/src/CSocket.cpp`

## Protocol Discoveries

`IEnums.h` confirms numeric IDs for:

- client-to-server `PLI_*`
- server-to-client `PLO_*`
- server-to-listserver `SVO_*`
- listserver-to-server `SVI_*`
- player flags, status flags, and player/session type masks

`CString.h` and `CString.cpp` confirm:

- raw `writeShort` and `readShort` are big-endian two-byte values
- raw `writeInt`, `readInt`, and `writeInt3` are big-endian
- Graal printable integers use a byte offset of `32`
- `writeGChar` clamps unsigned input at `223`
- `writeGCharUnsafe` wraps after adding `32` and rejects byte value `233`
- `writeGShort` clamps at `28,767`
- `writeGInt` clamps at `3,682,399`
- `writeGInt4` clamps at `471,347,295`
- `writeGInt5` clamps at `0xFFFFFFFF`
- `CString` stores raw bytes and null-terminates its internal buffer without counting that terminator in `length()`

`CEncryption.h` and `CEncryption.cpp` confirm:

- `ENCRYPT_GEN_1 = 0`
- `ENCRYPT_GEN_2 = 1`
- `ENCRYPT_GEN_3 = 2`
- `ENCRYPT_GEN_4 = 3`
- `ENCRYPT_GEN_5 = 4`
- `ENCRYPT_GEN_6 = 5`
- `COMPRESS_UNCOMPRESSED = 0x02`
- `COMPRESS_ZLIB = 0x04`
- `COMPRESS_BZ2 = 0x06`
- default generation is gen 3
- iterator starts are `{0, 0, 0x04A80B38, 0x04A80B38, 0x04A80B38, 0}`
- iterator update is `iterator = iterator * 0x08088405 + key`
- gen 3 inserts/removes one `')'` byte at `((iterator & 0x0FFFF) % packetLength)`
- gen 4/5 XOR packet bytes with little-endian iterator bytes, refreshing the iterator every 4 bytes
- encryption limits are selected by compression type: uncompressed `0x0C`, zlib `0x04`, bz2 `0x04`

`CFileQueue.h` and `CFileQueue.cpp` confirm:

- outbound packets are separated into normal and file queues
- `PLO_RAWDATA` announces exact raw byte payload size and the following payload is routed by first packet ID
- `PLO_BOARDPACKET` raw payloads stay in the normal queue; other raw payloads go to the file queue
- `PLO_LARGEFILESTART`, `PLO_LARGEFILEEND`, and `PLO_LARGEFILESIZE` go to the file queue
- normal sends target 48 KiB and avoid exceeding 60 KiB
- file queue is forced after more than 32 KiB without file data, explicit force, or 4 empty send calls
- gen 1 and gen 6 send raw queued bytes
- gen 2 and gen 3 zlib-compress and prefix a raw big-endian short length
- gen 4 bz2-compresses, encrypts, and prefixes a raw big-endian short length
- gen 5 chooses uncompressed, zlib, or bz2, writes one compression-type byte, encrypts, and prefixes a raw big-endian short length

`CSocket.h` and `CSocket.cpp` confirm:

- sockets are non-blocking
- TCP sockets disable Nagle using `TCP_NODELAY`
- `sendData` attempts one `send`, reduces the caller-provided remaining byte count, and disconnects on hard network errors
- `getData` and `peekData` use static 32 KiB buffers
- socket states are `DISCONNECTED = 0`, `CONNECTING = 1`, `CONNECTED = 2`, `LISTENING = 3`, `TERMINATING = 4`
- websocket packet fixups are delegated to `IUtil` helpers when `CSocket::webSocket` is true

## Remaining Risk

The dependency recovery unblocks protocol constants and primitive codecs, but a production login/session implementation should still wait for golden byte fixtures or focused parity tests for:

- complete `CFileQueue` compression output, including zlib/bz2 library settings
- websocket incoming/outgoing fixups in `IUtil`
- full login packet byte sequences for each supported client generation
- server-list packet flows and any external service dependencies
