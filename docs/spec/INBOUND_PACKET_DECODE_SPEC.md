# Inbound Packet Decode Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/src/CString.cpp`

## C++ Receive Path

`Player::doMain` reads raw two-byte big-endian socket frame lengths from
`m_recvBuffer`. Once the full frame payload is buffered, it removes the frame
from `m_recvBuffer` and decodes it according to `m_encryptionCodecIn.getGen()`.

Confirmed frame decode:

- `ENCRYPT_GEN_1`: payload is already plain packet bytes.
- `ENCRYPT_GEN_2`: zlib-decompress the whole frame payload.
- `ENCRYPT_GEN_3`: zlib-decompress the whole frame payload, then each
  newline-delimited inner packet is decrypted individually in `parsePacket`.
- `ENCRYPT_GEN_4`: set encryption limit from `COMPRESS_BZ2`, decrypt the whole
  payload, then bzip2-decompress.
- `ENCRYPT_GEN_5+`: first byte is compression type, remove it, set encryption
  limit from that type, decrypt the remaining bytes, then decompress according
  to the type.

Confirmed gen5 compression type values:

```txt
COMPRESS_UNCOMPRESSED = 0x02
COMPRESS_ZLIB = 0x04
COMPRESS_BZ2 = 0x06
```

After frame decode, `parsePacket` splits normal packets by newline. `PLI_RAWDATA`
sets `m_nextIsRaw` and the next packet is read by exact byte length instead of
newline. `m_nextIsRaw` and `m_rawPacketSize` are fields on `Player`, so the raw
length transition is session state, not a local one-shot helper.

For gen3, `parsePacket` decrypts each extracted packet after line/raw extraction
and before reading the packet id. For gen4/gen5+, `decryptPacket` runs before
`parsePacket` and packet extraction operates on already-decrypted/decompressed
combined payload bytes.

`PLI_BUNDLE = 253` is confirmed in `IEnums.h`, but this source snapshot does not
assign `TPLFunc[PLI_BUNDLE]` in `Player.cpp`; unassigned packet ids use
`msgPLI_NULL`. The existing C# raw big-endian bundle reader remains a utility
for the confirmed length-prefix shape, but the dev shell does not expand inbound
bundles as gameplay/session behavior because the authoritative C++ server does
not show a bound handler in this snapshot.

## Captured Inbound Fixtures

The `tools/gs2lib-fixtures` harness now emits inbound decode fixtures by taking
the source-confirmed socket output, removing the two-byte socket length prefix,
and applying the same decode steps as `Player::decryptPacket`.

Confirmed fixtures:

```txt
inbound-gen2-short-abc-newline
framePayload=78 9C 4B 4C 4A E6 02 00 03 7E 01 31
decoded=61 62 63 0A
```

```txt
inbound-gen4-short-abc-newline
framePayload=5A 42 B9 E7 49 99 18 A5 0B 43 0A 60 ED 35
             98 E2 00 C1 00 00 10 38 00 20 00 21 9A 68
             33 4D 13 3C 5D C9 14 E1 42 42 B5 9D 57 58
decoded=61 62 63 0A
```

```txt
inbound-gen5-short-abc-newline
framePayload=02 79 7A B2 DC
decoded=61 62 63 0A
```

```txt
inbound-gen5-zlib-56a-newline
framePayload=04 60 84 9A 9A 5C D3 31 82 58 46 1C 13 5A
decoded=61 61 61 61 61 61 61 61 61 61 61 61 61 61 61 61
        61 61 61 61 61 61 61 61 61 61 61 61 61 61 61 61
        61 61 61 61 61 61 61 61 61 61 61 61 61 61 61 61
        61 61 61 61 61 61 61 0A
```

## C# Boundary

Implemented:

- `InboundPacketDecoder`
- decoded-frame API that exposes raw decoded payload bytes before client packet
  framing
- gen1/gen6 passthrough
- gen2 zlib frame decode
- gen3 zlib frame decode plus per-packet gen3 decrypt after newline splitting
- gen4 bzip2 frame decode from the source-confirmed
  `inbound-gen4-short-abc-newline` fixture
- gen5 uncompressed frame decode
- gen5 zlib frame decode
- gen5 bzip2 frame decode from the source-confirmed
  `inbound-gen5-bz2-8193a-newline` fixture
- gen5 invalid compression type behavior: `CEncryption::limitFromType` leaves
  the prior encryption limit unchanged, C++ logs
  `Client gave incorrect packet compression type`, and continues without
  decompression; C# returns the decrypted payload plus a warning
- newline splitting into inner packets without the trailing newline
- `ClientPacketStreamFramer` statefully preserves the confirmed `PLI_RAWDATA`
  next-packet length transition across decoded payload calls
- local-debug TCP shell integration after login using the session's inbound
  generation and login encryption key

The local-debug shell now feeds decoded post-login packets into the existing
`PLI_PLAYERPROPS` boundary. Unsupported packet ids still stop before gameplay
runtime dispatch.

## Blockers

- inbound `PLI_BUNDLE` expansion in the dev shell remains blocked because this
  C++ snapshot does not assign a handler for `PLI_BUNDLE`
- production socket buffering, multi-session forwarding, and gameplay handlers
