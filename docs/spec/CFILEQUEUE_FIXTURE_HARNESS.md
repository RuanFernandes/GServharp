# CFileQueue Fixture Harness

Authoritative sources:

- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CString.cpp`

## Harness Location

The isolated harness lives at:

```txt
tools/gs2lib-fixtures/
```

It does not modify `ai_resources/` or patch `external/gs2lib`. The harness
compiles the recovered `gs2lib` source files into a local executable, creates a
loopback TCP socket pair, gives the sending socket handle to `CSocket`, queues
payloads through `CFileQueue`, calls `sendCompress`, and reads exact socket
bytes from the receiving socket.

Run command:

```powershell
tools\gs2lib-fixtures\build.ps1
```

Confirmed local toolchain for this capture:

- Visual Studio 2022 MSVC `19.44.35227.0`
- Ninja
- vcpkg at `C:\vcpkg`
- `bzip2:x64-windows@1.0.8#6`
- `zlib:x64-windows@1.3.2`

## Captured Fixtures

All fixture keys below use `key=0`.

### Gen2 zlib, short payload

```txt
name: gen2-short-abc-newline
gen: ENCRYPT_GEN_2
input: ASCII("abc\n") => 61 62 63 0A
output: 00 0C 78 9C 4B 4C 4A E6 02 00 03 7E 01 31
```

### Gen2 zlib, repeated payload

```txt
name: gen2-long-100a-newline
gen: ENCRYPT_GEN_2
input: ASCII("a" repeated 100 + "\n")
output: 00 0D 78 9C 4B 4C A4 3D E0 02 00 A0 36 25 EF
```

### Gen3 zlib, short payload

```txt
name: gen3-short-abc-newline
gen: ENCRYPT_GEN_3
input: ASCII("abc\n") => 61 62 63 0A
output: 00 0C 78 9C 4B 4C 4A E6 02 00 03 7E 01 31
```

`CFileQueue::sendCompress` uses the same zlib-only branch for gen2 and gen3.
The gen3 byte-insertion encryption primitive is not applied to outbound
`CFileQueue` packets in this branch.

### Gen3 zlib, repeated payload

```txt
name: gen3-long-100a-newline
gen: ENCRYPT_GEN_3
input: ASCII("a" repeated 100 + "\n")
output: 00 0D 78 9C 4B 4C A4 3D E0 02 00 A0 36 25 EF
```

### Gen4 bzip2, short payload

```txt
name: gen4-short-abc-newline
gen: ENCRYPT_GEN_4
input: ASCII("abc\n") => 61 62 63 0A
output:
00 2A 5A 42 B9 E7 49 99 18 A5 0B 43 0A 60 ED 35
98 E2 00 C1 00 00 10 38 00 20 00 21 9A 68 33 4D
13 3C 5D C9 14 E1 42 42 B5 9D 57 58
```

### Gen5 uncompressed, short payload

```txt
name: gen5-short-abc-newline
gen: ENCRYPT_GEN_5
input: ASCII("abc\n") => 61 62 63 0A
output: 00 05 02 79 7A B2 DC
```

### Gen5 uncompressed threshold

```txt
name: gen5-threshold-55a-newline
gen: ENCRYPT_GEN_5
input: ASCII("a" repeated 54 + "\n"), total length 55
output:
00 38 02 79 79 B0 B7 19 B9 20 E2 39 7B C6 66 D9
82 F9 83 F9 33 46 41 99 9D 7B 5D B9 B1 D7 DF 59
15 60 25 79 44 D5 14 19 78 04 79 39 3E BA 47 D9
5D 53 FB 61 61 61 61 61 61 0A
```

### Gen5 zlib threshold

```txt
name: gen5-zlib-56a-newline
gen: ENCRYPT_GEN_5
input: ASCII("a" repeated 55 + "\n"), total length 56
output: 00 0E 04 60 84 9A 9A 5C D3 31 82 58 46 1C 13 5A
```

### Gen5 bzip2 threshold

```txt
name: gen5-bz2-8193a-newline
gen: ENCRYPT_GEN_5
input: ASCII("a" repeated 8192 + "\n"), total length 8193
output:
00 32 06 5A 42 B9 E7 49 99 18 A5 0B 43 D4 4B 64
99 98 E2 12 E1 00 80 10 00 04 20 00 00 08 20 00
30 CD 34 0A A3 1F 0A 0B 00 61 77 24 53 85 09 07
34 CD C7 A0
```

The bzip2 fixture is implemented in C# with SharpZipLib using block size `1`,
matching the recovered `CString::bzcompress` call to
`BZ2_bzBuffToBuffCompress(..., 1, 0, 30)`.

## Inbound Decode Fixtures

The harness also emits deterministic inbound decode fixtures. These are built
from the same source-confirmed socket output by stripping the two-byte socket
length prefix and applying the `Player::decryptPacket` / `CString`
decompression order.

```txt
name: inbound-gen2-short-abc-newline
gen: ENCRYPT_GEN_2
framePayload: 78 9C 4B 4C 4A E6 02 00 03 7E 01 31
decoded: 61 62 63 0A
```

```txt
name: inbound-gen4-short-abc-newline
gen: ENCRYPT_GEN_4
framePayload:
5A 42 B9 E7 49 99 18 A5 0B 43 0A 60 ED 35
98 E2 00 C1 00 00 10 38 00 20 00 21 9A 68 33 4D
13 3C 5D C9 14 E1 42 42 B5 9D 57 58
decoded: 61 62 63 0A
```

```txt
name: inbound-gen5-short-abc-newline
gen: ENCRYPT_GEN_5
framePayload: 02 79 7A B2 DC
decoded: 61 62 63 0A
```

```txt
name: inbound-gen5-zlib-56a-newline
gen: ENCRYPT_GEN_5
framePayload: 04 60 84 9A 9A 5C D3 31 82 58 46 1C 13 5A
decoded: ASCII("a" repeated 55 + "\n")
```

```txt
name: inbound-gen5-bz2-8193a-newline
gen: ENCRYPT_GEN_5
framePayload:
06 5A 42 B9 E7 49 99 18 A5 0B 43 D4 4B 64
99 98 E2 12 E1 00 80 10 00 04 20 00 00 08 20 00
30 CD 34 0A A3 1F 0A 0B 00 61 77 24 53 85 09 07
34 CD C7 A0
decoded: ASCII("a" repeated 8192 + "\n")
```

## C# Match Status

Implemented and covered by tests:

- gen2 zlib socket flush
- gen3 zlib socket flush
- gen4 bzip2 socket flush
- gen5 uncompressed socket flush for payloads `<= 55`
- gen5 zlib socket flush for payloads `56..0x2000`
- gen5 bzip2 socket flush for payloads `> 0x2000`
- inbound gen2 zlib frame decode
- inbound gen4 bzip2 frame decode
- inbound gen5 uncompressed frame decode
- inbound gen5 zlib frame decode
- inbound gen5 bzip2 frame decode

Still blocked:

- websocket wrapping
- full dev TCP shell integration for login/level payloads that cross into
  bzip2-sized sends; the current dev shell uses confirmed gen5 zlib framing
  only when the queued diagnostic response stays at or below `0x2000` bytes
