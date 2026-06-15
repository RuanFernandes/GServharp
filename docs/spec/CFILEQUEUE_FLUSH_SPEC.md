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
  `<= 0xFFFD`, prefix raw big-endian short length, then send.
- `ENCRYPT_GEN_4`: bzip2-compress, require length `<= 0xFFFD`, set encryption
  limit for BZ2, encrypt, prefix raw big-endian short length, then send.
- `ENCRYPT_GEN_5`: choose uncompressed for length `<= 55`, zlib for `> 55`, BZ2
  for `> 0x2000`; require encrypted payload length `<= 0xFFFC`; prefix raw
  big-endian short `(encryptedLength + 1)`, then raw compression type byte, then
  encrypted payload.

## Socket Semantics

`CSocket::sendData` calls nonblocking `send`. On `EAGAIN`, it returns 0 without
disconnecting. On connection-loss errors it disconnects. It subtracts sent bytes
from `*dsize`; `CFileQueue` removes exactly the returned sent byte count from
`oBuffer`, so unsent bytes remain queued for the next flush.

## C# Boundary

The C# `GraalFileQueue` currently implements the source-confirmed passthrough
flush path for `ENCRYPT_GEN_1`/`ENCRYPT_GEN_6`:

- normal newline packet splitting
- `PLO_RAWDATA` length transition
- `PLO_BOARDPACKET` raw-data routing to the normal queue
- file-buffer routing for non-board raw data and large-file packets
- queue selection thresholds used before compression
- output buffering across partial sends

Production compressed/encrypted flush behavior remains blocked until
zlib/bzip2/encryption/websocket fixtures are byte-exact.

## Current Pass Status

No new `CFileQueue` production behavior was implemented in the account-loading
pass. Queue thresholds, compression/encryption generations, websocket wrapping,
and partial socket semantics remain as documented above.
