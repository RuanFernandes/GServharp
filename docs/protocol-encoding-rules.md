# Protocol Encoding Rules

This document records C++-confirmed protocol encoding behavior.

Recovered dependency source: `external/gs2lib/` at commit `63b1ae96491c188905b50c6b61c8532c601a2122`, originally declared by `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt`.

## Packet Bundle Framing

Source: `server/include/network/IPacketHandler.h`.

Inbound client data is read from a socket into a receive buffer. `IPacketHandler::retrievePacketBundle` reads a two-byte raw length with `CString::readShort()`, then reads that many bytes as a packet bundle and removes `length + 2` bytes from the receive buffer.

`CString::readShort()` is confirmed by `external/gs2lib/src/CString.cpp` as big-endian: `(val[0] << 8) + val[1]`. `CString::writeShort()` writes the same big-endian two-byte order. This confirms the bundle length prefix byte order.

## Packet Delimiting

Source: `IPacketHandler::parsePacketsFromBundle` and `Player::sendPacket`.

- Normal packets inside a bundle are read with `readString("\n")`.
- Outbound player packets append `'\n'` unless `appendNL` is false or the packet already ends in newline.
- Empty outbound packets are ignored.
- Server-list outbound packets also append `'\n'` when missing.
- Login packet parsing is special: before normal packet count exists, the bundle is read as a login packet with `readString("\n")` and passed to `handlePacket(std::nullopt, packet)`.

## Raw Data

Source: `IPacketHandler::parsePacketsFromBundle` and `Player::sendFile`.

- When a packet ID equals `PLI_RAWDATA`, the handler reads a `GUInt` from the packet and marks the next packet as raw.
- The next packet is read by exact byte count instead of newline delimiting.
- `RemoveNewlinesFromRawPacket` can remove a trailing newline from the raw packet.
- File sending uses `PLO_RAWDATA` to announce the following `PLO_FILE` packet byte size.
- Older client versions may receive file packets without mod time and with different raw-data length calculation.

Numeric IDs are now confirmed by `external/gs2lib/include/IEnums.h`: `PLI_RAWDATA = 50`, `PLO_RAWDATA = 100`, `PLO_BOARDPACKET = 101`, and `PLO_FILE = 102`.

## Graal Printable Integer Encoding

Confirmed from `external/gs2lib/src/CString.cpp`:

- Packet IDs are read with `readGUChar()`.
- Packet logging reconstructs output packet IDs as `static_cast<uint8_t>(pPacket[0]) - 32`, confirming the printable-byte offset for GChar packet IDs.
- `PlayerLogin::msgLoginPacket` computes `m_type = (1 << pPacket.readGChar())`, so the login type byte is a GChar index.
- `writeGChar(unsigned char)` writes `min(value, 223) + 32`.
- `writeGCharUnsafe(unsigned char)` writes `value + 32` with byte wrapping, but throws when `value == 233` because it would encode to newline.
- `writeGShort(unsigned short)` clamps above `28,767` and writes two 7-bit chunks plus `32`.
- `writeGInt(unsigned int)` clamps above `3,682,399` and writes three 7-bit chunks plus `32`.
- `writeGInt4(unsigned int)` clamps above `471,347,295` and writes four 7-bit chunks plus `32`.
- `writeGInt5(unsigned long long)` clamps above `0xFFFFFFFF`; the first encoded byte uses 4 payload bits and the remaining four use 7 payload bits.
- `readGShort`, `readGInt`, and `readGInt4` decode by combining raw bytes and subtracting the accumulated ASCII-space offset.
- `readGInt5` returns a full client-side `uint32_t`.
- `operator >> (char/signed char/unsigned char)` maps to `writeGChar`.
- `operator >> (short/unsigned short)` maps to `writeGShort`.
- `operator >> (int/unsigned int)` maps to `writeGInt`.
- `operator >> (long long)` maps to `writeGInt5`.

## String Encoding

Confirmed usage:

- Fixed-width version strings are read with `readChars(8)`.
- Account/password-style strings are often encoded as GChar length followed by raw bytes: `readChars(readGUChar())`.
- Some server-list and RC fields use raw `short` length followed by raw bytes.
- Remaining packet tail strings are often read with `readString("")`.

Confirmed from `external/gs2lib/include/CString.h` and `external/gs2lib/src/CString.cpp`:

- `CString` is a raw byte buffer with tracked `sizec`, `readc`, and `writec`; internal null termination is maintained but not counted in `length()`.
- `readChars(length)` clamps to available bytes and advances `readc` by the number actually read.
- `readString(separator)` reads until `separator`; if the separator is absent or past the remaining bytes, it reads through `bytesLeft()`, then advances by `len + separator.length()`.
- `readString("")` reads all remaining bytes because an empty separator disables the `find` length.
- Constructor overloads from numeric types format decimal text with `sprintf`; `float` uses `"%.2f"` and `double` uses `"%f"`.
- `gtokenize`, `guntokenize`, and comma token helpers are byte/string transformations in `CString.cpp`; port them directly when packet handlers require them.

The future C# port should continue using byte-preserving string APIs at the wire boundary. Human text decoding should be delayed until packet-specific behavior is known.

## Encryption And Compression

Source: `IPacketHandler::processPacketBundle`, `PlayerClient::handleLogin`, `PlayerRC::handleLogin`, `PlayerNC::handleLogin`, `ServerList::connectServer`.

- Generation 1: no encryption or compression for inbound bundles.
- Generation 2: zlib-compressed bundle, no encryption.
- Generation 3: zlib-compressed bundle; individual packets are decrypted after splitting.
- Generation 4: bundle is BZ2-compressed and encrypted; decryption occurs before BZ2 decompression.
- Generation 5 and later: first byte is compression type; remove it, limit encryption by compression type, decrypt, then decompress zlib/BZ2 or leave uncompressed.
- Client login chooses generations based on `PLTYPE_*`.
- Server-list registration sends one packet with file queue codec generation 1, then switches to generation 2.

Confirmed from `external/gs2lib/include/CEncryption.h`, `external/gs2lib/src/CEncryption.cpp`, and `external/gs2lib/src/CFileQueue.cpp`:

- `ENCRYPT_GEN_1 = 0`: no encryption, no compression.
- `ENCRYPT_GEN_2 = 1`: no encryption, zlib compression.
- `ENCRYPT_GEN_3 = 2`: single byte insertion/removal and zlib compression in file queue.
- `ENCRYPT_GEN_4 = 3`: partial packet encryption and bz2 compression.
- `ENCRYPT_GEN_5 = 4`: partial packet encryption with uncompressed/zlib/bz2 modes.
- `ENCRYPT_GEN_6 = 5`: v6 placeholder; file queue sends raw queued bytes.
- `COMPRESS_UNCOMPRESSED = 0x02`, `COMPRESS_ZLIB = 0x04`, `COMPRESS_BZ2 = 0x06`.
- `CEncryption` defaults to gen 3 and iterator start `0x04A80B38`.
- `reset(key)` stores the byte key, resets iterator from `ITERATOR_START[gen]`, and clears the encryption limit to `-1`.
- Iterator update is `iterator = iterator * 0x08088405 + key`.
- Gen 3 inserts or removes byte `')'` at `((iterator & 0x0FFFF) % packetLength)`.
- Gen 4/5 XOR packet bytes with little-endian iterator bytes, refreshing every four bytes; `limitFromType` limits encrypted 4-byte blocks by compression type.

Directly confirmed server-list file type values are not packet opcodes, but are protocol-adjacent constants: `SVF_HEAD = 0`, `SVF_BODY = 1`, `SVF_SWORD = 2`, `SVF_SHIELD = 3`, and `SVF_FILE = 4`.

## C# Tests Added So Far

- GChar offset and round-trip tests.
- GShort/GInt/GInt4/GUInt5 round-trip tests based on current C++ call-site interpretation.
- Packet newline framing tests from `Player::sendPacket`.
- Login type byte to bit-mask test from `PlayerLogin::msgLoginPacket`.

- C++-confirmed GChar, GShort, GInt, GInt4, and GUInt5 tests.
- C++-confirmed packet ID source-status tests and protocol-critical numeric packet ID tests.
- C++-confirmed legacy encryption primitive tests for gen 3 insertion/removal and gen 4/5 XOR/limit behavior.
