# CFileQueue Flush Specification

Authoritative sources:

- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CSocket.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`

## Player::sendPacket Boundary

`Player::sendPacket(CString pPacket, bool appendNL = true)`:

1. Returns immediately for an empty packet.
2. If `appendNL` is true and the packet does not already end with `\n`, appends
   `\n`.
3. Passes the packet to `m_fileQueue.addPacket(pPacket)`.

This means packet builders normally produce packet bodies, not socket bytes.
`CFileQueue` owns queueing, compression/encryption framing, websocket wrapping,
partial socket retry buffering, and file/normal ordering.

## addPacket

`CFileQueue::addPacket` consumes one or more newline-delimited packets:

- If the next unread packet ID byte is `< 0x20`, parsing stops.
- `PLO_RAWDATA` is special: the queue reads the raw-data header through newline,
  stores it in `pack100`, sets `prev100`, and reads `size100` from the header.
- The next packet after a raw-data header is read as exactly `size100` bytes.
  If that raw packet begins with `PLO_BOARDPACKET`, `pack100 + packet` goes to
  `normalBuffer`; otherwise it goes to `fileBuffer`.
- `PLO_LARGEFILESTART`, `PLO_LARGEFILEEND`, and `PLO_LARGEFILESIZE` are queued
  in `fileBuffer`.
- Other normal packets go to `normalBuffer`.
- For `ENCRYPT_GEN_6`, non-raw parsing takes the remaining `pPacket` instead of
  reading to newline.

## sendCompress Queue Selection

Confirmed thresholds:

- A front normal packet larger than `0xF000` is sent alone.
- A file packet is forced when `bytesSentWithoutFile > 0x7FFF`,
  `forceSendFiles` is true, or `sendCallsWithoutData >= 4`.
- Normal packets are accumulated while total length is `< 0xC000`, but the next
  packet is skipped if it would push the send buffer over `0xF000`.
- If the send buffer is `< 0x4000`, one file packet may be appended if total
  length remains `<= 0xF000`.
- Empty sends increment `sendCallsWithoutData` up to 5.

## Compression / Encryption Generations

- `ENCRYPT_GEN_1` and `ENCRYPT_GEN_6`: append `pSend` directly to `oBuffer` and
  call `sendData`.
- `ENCRYPT_GEN_2` and `ENCRYPT_GEN_3`: zlib-compress `pSend`, require length
  `<= 0xFFFD`, prefix raw big-endian short length, then send. The fixture
  harness confirms gen2 and gen3 produce identical socket bytes for the same
  payload; gen3 byte insertion is not applied by this branch.
- `ENCRYPT_GEN_4`: bzip2-compress, require length `<= 0xFFFD`, set encryption
  limit for BZ2, encrypt, prefix raw big-endian short length, then send.
- `ENCRYPT_GEN_5`: choose uncompressed for length `<= 55`, zlib for `> 55`, BZ2
  for `> 0x2000`; require encrypted payload length `<= 0xFFFC`; prefix raw
  big-endian short `(encryptedLength + 1)`, then raw compression type byte, then
  encrypted payload.

Confirmed compression type constants from `CFileQueue.cpp` / `CEncryption.cpp`:

- `COMPRESS_UNCOMPRESSED = 0x02`
- `COMPRESS_ZLIB = 0x04`
- `COMPRESS_BZ2 = 0x06`

Confirmed gen5 threshold behavior:

- payload length `<= 55`: compression type `0x02`, no zlib/bzip2 compression
- payload length `> 55`: zlib path
- payload length `> 0x2000`: bzip2 path

Confirmed gen5 encryption limit behavior:

- compression type `0x02`: limit `0x0C`
- compression type `0x04`: limit `0x04`
- compression type `0x06`: limit `0x04`

## Socket Semantics

`CSocket::sendData` calls nonblocking `send`. On `EAGAIN`, it returns 0 without
disconnecting. On connection-loss errors it disconnects. It subtracts sent bytes
from `*dsize`; `CFileQueue` removes exactly the returned sent byte count from
`oBuffer`, so unsent bytes remain queued for the next flush.

## C# Boundary

The C# `GraalFileQueue` currently implements these source-confirmed flush
paths:

- normal newline packet splitting
- `PLO_RAWDATA` length transition
- `PLO_BOARDPACKET` raw-data routing to the normal queue
- file-buffer routing for non-board raw data and large-file packets
- queue selection thresholds used before compression
- output buffering across partial sends
- socket-level passthrough for `ENCRYPT_GEN_1`/`ENCRYPT_GEN_6`
- socket-level zlib compression for `ENCRYPT_GEN_2`/`ENCRYPT_GEN_3`
- socket-level gen5 uncompressed payload framing for payloads `<= 55`
- socket-level gen5 zlib framing for payloads `56..0x2000`
- gen5 uncompressed compression type byte `0x02`
- gen5 zlib compression type byte `0x04`
- gen5 bzip2 compression type byte `0x06`
- gen5 big-endian socket length prefix equal to `encryptedLength + 1`
- gen5 iterator-XOR encryption using the recovered `CEncryption` behavior
- source-confirmed gen4 bzip2 socket framing
- source-confirmed gen5 bzip2 payload framing for payloads over `0x2000`

Gen4 bzip2 and gen5 uncompressed, zlib, and bzip2 socket bytes are now covered
by the isolated `tools/gs2lib-fixtures` harness and C# golden tests.

## Current Pass Status

This pass implemented the first socket-level flush boundary that does not
require unproven compression output:

- gen1/gen6 socket flush emits the queued bytes directly.
- gen2/gen3 socket flush zlib-compresses the queued bytes and prefixes the
  compressed length as a raw big-endian short.
- gen5 payloads up to 55 bytes are framed as:
  `GSHORT(encryptedLength + 1) + compressionType + encryptedPayload`.
- gen5 payloads from 56 through `0x2000` bytes use zlib, compression type
  `0x04`, gen5 iterator-XOR encryption with zlib limit `0x04`, and the same
  length framing.
- gen5 payloads over `0x2000` bytes use bzip2, compression type `0x06`, bzip2
  block size `1`, gen5 iterator-XOR encryption with bzip2 limit `0x04`, and
  the same length framing.
- gen4 payloads always use bzip2, bzip2 block size `1`, gen4 iterator-XOR
  encryption with bzip2 limit `0x04`, and a big-endian socket length prefix
  equal to the encrypted payload length with no compression-type byte.
- partial socket writes leave remaining framed bytes buffered for the next
  flush, matching the `oBuffer` / `sendData` retry model in `CFileQueue`.

The C# boundary still queues normal newline packets, `PLO_RAWDATA` headers,
pre-serialized board/layer payload bytes, dynamic level packets, and first
post-dynamic runtime packets in the same order C++ calls `Player::sendPacket`.

The dev-only TCP shell now uses this boundary for its confirmed small/medium
login and pre-runtime level response. Client3/RC2 sessions with a login key are
sent through gen5 socket framing; web sessions use gen1 passthrough.

Still blocked:

- websocket wrapping
- production file transfer through `PLO_FILE`
- level resource transfer beyond pre-serialized board/layer and runtime payloads
