# Golden Fixtures

All byte values are decimal unless noted.

## Login Packets

### PLO_SIGNATURE

C++:

```cpp
sendPacket(CString() >> (char)PLO_SIGNATURE >> (char)73);
```

Packet bytes before queue newline/compression:

```txt
[57, 105]
```

With `Player::sendPacket` newline append:

```txt
[57, 105, 10]
```

### PLO_UNKNOWN168

C++:

```cpp
sendPacket(CString() >> (char)PLO_UNKNOWN168);
```

`PLO_UNKNOWN168 = 168`; `GCHAR 168` writes `168 + 32 = 200`.

Packet bytes before queue newline/compression:

```txt
[200]
```

With `Player::sendPacket` newline append:

```txt
[200, 10]
```

### PLO_DISCMESSAGE

C++:

```cpp
sendPacket(CString() >> (char)PLO_DISCMESSAGE << "No");
```

Packet bytes before queue newline/compression:

```txt
[48, 78, 111]
```

With `Player::sendPacket` newline append:

```txt
[48, 78, 111, 10]
```

### Unknown Login Type

Input login prelude:

```txt
GCHAR 9 => raw byte [41], m_type = 1 << 9 = 512
```

Confirmed response:

```txt
[48] + ASCII("Your client type is unknown.  Please inform the OpenGraal Team.  Type: 512.") + [10]
```

## Player::sendLogin Pre-World Boundary

Normal client success continuation before `Server::playerLoggedIn`:

```txt
[57, 105, 10, 200, 10]
```

This is:

```txt
PLO_SIGNATURE, GCHAR 73, "\n", PLO_UNKNOWN168, "\n"
```

Login-named server continuation is a confirmed blocked negative fixture for the
C# port. C++ references missing `PLO_FULLSTOP` before `PLO_GHOSTICON`, but no
authoritative opcode exists in the recovered source set. The C# boundary reports
`LoginServerFullStopBlocked` and emits only the source-confirmed surrounding
client bytes:

```txt
[57, 105, 10, 200, 10]
```

Active duplicate client rejection preserves C++ ordering, with early client packets already queued:

```txt
[57, 105, 10, 200, 10] + [48] + ASCII("Account is already in use.") + [10]
```

Banned account example with ban reason `"cheating"`:

```txt
[48] + ASCII("You have been banned.  Reason: cheating") + [10]
```

## Server::playerLoggedIn / sendLoginClient Pre-Warp Boundary

### SVO_PLYRADD

Input fixture:

```txt
playerId=7
type=PLTYPE_CLIENT3
account prop=GCHAR length 7 + "pc:Ruan"
nickname prop=GCHAR length 4 + "Ruan"
current level prop=GCHAR length 8 + "start.nw"
x prop=[70]
y prop=[71]
alignment prop=[72]
ip prop=[32,32,32,32,33]
```

Packet body before list-server queue newline/compression:

```txt
[46, 32, 39, 64,
 66, 39, 112, 99, 58, 82, 117, 97, 110,
 32, 36, 82, 117, 97, 110,
 52, 40, 115, 116, 97, 114, 116, 46, 110, 119,
 47, 70,
 48, 71,
 64, 72,
 62, 32, 32, 32, 32, 33]
```

Notes:

- `46` is `GCHAR SVO_PLYRADD` (`14 + 32`).
- `[32, 39]` is `GSHORT playerId 7`.
- `64` after player id is `GCHAR PLTYPE_CLIENT3` (`32 + 32`).
- Property ids are also written as `GCHAR`.

### Old Client BIGMAP Workaround

For `CLVER_2_31` and map list entries:

```txt
BIGMAP "worldmap.txt"
GMAP "ignored.gmap"
```

`sendLoginClient` calls `msgPLI_WANTFILE("worldmap.txt")` immediately after
`sendProps(__sendLogin)` and before `PLO_CLEARWEAPONS`; the GMAP is skipped.
The resulting bytes are the existing `PLO_RAWDATA` plus `PLO_FILE` file-transfer
fixture for `"worldmap.txt"`, because this branch delegates to `sendFile`.

## Player Property Serialization

### `__sendLogin` Property ID Table

C++ source:

```txt
ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp::__sendLogin
ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp::sendProps
```

The true property IDs emitted by `sendProps(__sendLogin)` are:

```txt
[1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 17, 18, 21, 22, 23,
 25, 26, 32, 34, 35, 36, 37, 38, 39, 40, 41, 46, 47, 48,
 49, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
 67, 68, 69, 70, 71, 72, 73, 74, 82]
```

For clients older than `CLVER_2_1`, `sendProps` forces `pCount = 37`, yielding:

```txt
[1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 17, 18, 21, 22, 23,
 25, 26, 32, 34, 35, 36]
```

### Confirmed Login Subset Payload

Input:

```txt
properties: PLPROP_MAXPOWER, PLPROP_CURPOWER, PLPROP_ACCOUNTNAME
maxPower=3
hitpoints=4.0
accountName="pc:Ruan"
```

Payload:

```txt
[33, 35, 34, 40, 66, 39, 112, 99, 58, 82, 117, 97, 110]
```

Wrapped as `PLO_PLAYERPROPS` with newline:

```txt
[41, 33, 35, 34, 40, 66, 39, 112, 99, 58, 82, 117, 97, 110, 10]
```

## Legacy `.graal` Level Parser Fixtures

Source:

```txt
ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp::loadGraal
```

Confirmed version headers:

```txt
GR-V1.00 -> 12-bit tile codes, no chest section
GR-V1.01 -> 13-bit tile codes
GR-V1.02 -> 13-bit tile codes
GR-V1.03 -> 13-bit tile codes
```

Tile codes are packed least-significant-bit first. The fixture in
`GraalLevelParserTests.ParseAcceptsConfirmedVersionsAndDecodesTileRle` encodes:

```txt
tile 1
regular repeat count 3, tile 7
double repeat count 2, tiles 8 and 9
zero-fill remaining tiles
```

Expected first eight tiles:

```txt
[1, 7, 7, 7, 8, 9, 8, 9]
```

The static-section fixture encodes sections in C++ order:

```txt
links: "target level.graal 1 2 3 4 5.5 6.5\nmissing.graal ...\n#\n"
baddy: raw [42, 43, 44] + "see\\hurt\n" + raw sentinel [255,255,255] + "\n"
npc: GCHAR 10, GCHAR 11, "image.png#if (created) {§}\n#\n"
chest: GCHAR 12, GCHAR 13, GCHAR redrupee-id, GCHAR 4, "\n#\n"
sign: GCHAR 14, GCHAR 15, "Hello sign\n\n"
```

Expected preserved records:

```txt
link: target level.graal, 1,2,3,4, 5.5,6.5
baddy: x=42 y=43 type=44 verses=["see","hurt"]
npc: image=image.png x=10 y=11 code="if (created) {\n}"
chest: x=12 y=13 item=redrupee sign=4
sign: x=14 y=15 text="Hello sign"
```

The `GR-V1.00` fixture proves the C++ chest-section skip: bytes that would be a
chest line for newer versions are parsed as the first sign line instead.

## Legacy `.zelda` Level Parser Fixtures

Source:

```txt
ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp::loadZelda
```

Confirmed version headers:

```txt
Z3-V1.03 -> 12-bit tile codes, baddy verses are not consumed
Z3-V1.04 -> 12-bit tile codes, baddy verses are consumed
GR*      -> delegated to loadGraal
```

The fixture in
`ZeldaLevelParserTests.ParseAcceptsConfirmedVersionsAndDecodesTwelveBitTileRle`
encodes:

```txt
tile 2
regular repeat count 4, tile 6
double repeat count 2, tiles 8 and 9
zero-fill remaining tiles
```

Expected first nine tiles:

```txt
[2, 6, 6, 6, 6, 8, 9, 8, 9]
```

The static-section fixture encodes:

```txt
links: "target level.zelda 1 2 3 4 5 6\nmissing.zelda ...\n#\n"
baddy: raw [42, 43, 44] + "see\\hurt\n" + raw sentinel [255,255,255] + "\n"
sign: GCHAR 14, GCHAR 15, "Hello zelda sign\n\n"
```

Expected preserved records:

```txt
link: target level.zelda, 1,2,3,4, 5,6
baddy: x=42 y=43 type=44 verses=["see","hurt"]
sign: x=14 y=15 text="Hello zelda sign"
```

The `Z3-V1.03` fixture proves that baddy verses are not consumed for that
version: a baddy triple followed immediately by the sentinel yields a baddy with
an empty verse list and then the sign section begins.

## BIGMAP / GMAP Parser Fixtures

Source:

```txt
ai_resources/GServer-CPP-ORIGINAL/server/src/level/Map.cpp
```

BIGMAP fixture:

```txt
start.nw, second.nw,,
"level, with comma.nw", THIRD.NW
```

Expected:

```txt
type=BIGMAP
width=2
height=2
levels:
  (0,0)=start.nw
  (1,0)=second.nw
  (0,1)=level, with comma.nw
  (1,1)=third.nw
```

The trailing empty cells in the first row do not increase width. Interior empty
cells are preserved for BIGMAP because C++ splits with `keepEmpty = true`.

GMAP fixture:

```txt
WIDTH 3
HEIGHT 2
MAPIMG map.png
MINIMAPIMG mini.png
LOADFULLMAP
LEVELNAMES
start.nw, SECOND.NW
"third level.nw", , fourth.nw
LEVELNAMESEND
```

Expected:

```txt
type=GMAP
width=3
height=2
mapImage=map.png
miniMapImage=mini.png
loadFullMap=true
levels:
  (0,0)=start.nw
  (1,0)=second.nw
  (2,0)=empty
  (0,1)=third level.nw
  (1,1)=fourth.nw
  (2,1)=empty
preload selection=[start.nw, second.nw, third level.nw, fourth.nw]
```

The blank middle entry in the second GMAP row is compressed because C++
`tokenize("\n")` uses `keepEmpty = false`.

`LOADATSTART` fixture:

```txt
LOADFULLMAP
...
LOADATSTART
START.NW, "Second Level.NW"
LOADATSTARTEND
```

Expected: `LOADATSTART` resets `loadFullMap=false` and preload selection becomes
`[start.nw, second level.nw]`.

### GINT Property Example

`PLPROP_RUPEESCOUNT` with value `1234`:

```txt
[35, 32, 41, 114]
```

`35` is `GCHAR PLPROP_RUPEESCOUNT`; `[32, 41, 114]` is `GINT 1234`.

### End-Of-Buffer `GCHAR`

Recovered `CString::read(char*, int)` zero-fills the destination when no bytes
are left before a read. `readGChar()` then subtracts 32 from raw zero and
`readGUChar()` exposes the wrapped byte:

```txt
empty buffer + readGUChar() => 224

PLPROP_STATUS without its one-byte payload:
PLPROP_STATUS => parsed status value 224
```

Do not extend this fixture to truncated multi-byte scalar reads without a C++
harness: partial `CString::read` copies the available bytes but does not prove
zero-fill for the missing suffix.

Modern inbound `PLPROP_GANI` uses `GCHAR len` plus
`CString::readChars(len)`. Terminal truncated payloads parse the remaining
bytes and do not invent the blocked `"spin"` side-effect packets:

```txt
PLPROP_GANI + GCHAR(4) + "wa" => "wa"
```

### Old Client `PLPROP_GANI` / `PLPROP_BOWGIF`

For clients older than `CLVER_2_1`, property id `10` is still written as
`GCHAR 10` (`42`) but the value is the legacy bow payload.

Empty bow image with `bowPower = 3`:

```txt
[42, 35]
```

Bow image `"bow.gif"`:

```txt
[42, 49, 98, 111, 119, 46, 103, 105, 102]
```

Incoming old-client `PLI_PLAYERPROPS` `PLPROP_GANI` bow power payload:

```txt
[42, 36]
```

Incoming old-client `PLI_PLAYERPROPS` `PLPROP_GANI` extensionless bow image
payload `"bow1"` mutates to runtime image `"bow1.gif"`:

```txt
[42, 46, 98, 111, 119, 49]
```

The old-client bow-image path also uses `CString::readChars(sp - 10)`, so a
terminal truncated image still appends `.gif` when extensionless:

```txt
PLPROP_GANI + GCHAR(14) + "bo" => bowPower 10, bowImage "bo.gif"
```

Forwarded `PLO_OTHERPLPROPS` for player id `7`, old-client bow power `4`:

```txt
[40, 32, 39, 42, 36, 10]
```

Forwarded `PLO_OTHERPLPROPS` for player id `7`, old-client bow image
`"bow1.gif"`:

```txt
[40, 32, 39, 42, 50, 98, 111, 119, 49, 46, 103, 105, 102, 10]
```

Old-client pre-warp login boundary with only `PLPROP_GANI`/bow power followed
by the confirmed fixed pre-warp packets:

```txt
[41, 42, 35, 10,
 226, 10,
 66, 66, 111, 109, 98, 10,
 66, 66, 111, 119, 10,
 222, 10]
```

### Minimal Client Pre-Warp Packet Sequence

Input fixture:

```txt
login prop ids: PLPROP_MAXPOWER, PLPROP_CURPOWER
maxPower=3
hitpoints=4.0
player flags: client.flag=yes, empty.flag
server flags: server.flag=1
no weapons/classes/protected weapons/zlib-fix branch
```

Queued bytes before socket compression/encryption:

```txt
[41, 33, 35, 34, 40, 10,
 226, 10,
 60, 99, 108, 105, 101, 110, 116, 46, 102, 108, 97, 103, 61, 121, 101, 115, 10,
 60, 101, 109, 112, 116, 121, 46, 102, 108, 97, 103, 10,
 60, 115, 101, 114, 118, 101, 114, 46, 102, 108, 97, 103, 61, 49, 10,
 66, 66, 111, 109, 98, 10,
 66, 66, 111, 119, 10,
 222, 10]
```

This is:

```txt
PLO_PLAYERPROPS + serialized confirmed login subset + "\n"
PLO_CLEARWEAPONS + "\n"
PLO_FLAGSET "client.flag=yes" + "\n"
PLO_FLAGSET "empty.flag" + "\n"
PLO_FLAGSET "server.flag=1" + "\n"
PLO_NPCWEAPONDEL "Bomb" + "\n"
PLO_NPCWEAPONDEL "Bow" + "\n"
PLO_SERVERLISTCONNECTED/PLO_UNKNOWN190 + "\n"
```

The C# boundary stops here before `warp(m_levelName, getX(), getY())`.

### Modern Client Pre-Warp Packet-Order Fixture

Modern client `CLVER_4_0211`, login props `PLPROP_MAXPOWER` and
`PLPROP_CURPOWER`, one player flag, one server flag, one supplied player weapon,
one missing protected default weapon, and one supplied class packet:

```txt
[41, 33, 35, 34, 40, 10,
 226, 10,
 60, 99, 108, 105, 101, 110, 116, 46, 102, 108, 97, 103, 61, 121, 101, 115, 10,
 60, 115, 101, 114, 118, 101, 114, 46, 102, 108, 97, 103, 61, 49, 10,
 66, 66, 111, 109, 98, 10,
 66, 66, 111, 119, 10,
 65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 32, 10,
 75, 39, 10,
 229, 99, 108, 97, 115, 115, 10,
 222, 10]
```

This locks the confirmed order:

```txt
PLO_PLAYERPROPS
PLO_CLEARWEAPONS
PLO_FLAGSET player flag
PLO_FLAGSET server flag
PLO_NPCWEAPONDEL Bomb
PLO_NPCWEAPONDEL Bow
player weapon packet
missing protected weapon packet
modern class packet
PLO_SERVERLISTCONNECTED/PLO_UNKNOWN190
```

### Old Client Pre-Warp Packet-Order Fixture

Old client `CLVER_1_411`, login prop `PLPROP_GANI` as bow power, supplied
modern-only `PLPROP_COMMUNITYNAME` filtered out by the C++ `pCount = 37`
cutoff, and one BIGMAP file:

```txt
[41, 42, 35, 10,
 132, 32, 32, 53, 10,
 134, 44, 119, 111, 114, 108, 100, 109, 97, 112, 46, 116, 120, 116, 109, 97, 112, 100, 97, 116, 97,
 226, 10,
 66, 66, 111, 109, 98, 10,
 66, 66, 111, 119, 10,
 222, 10]
```

The old file chunk has no mod-time field and no file-packet newline because
clients older than `CLVER_2_1` use the legacy `sendFile` shape. `PLO_CLEARWEAPONS`
follows immediately after the raw file payload.

## Framing

Outer socket frame:

```txt
[0, 3, 97, 98, 99] => one inner frame "abc"
```

Raw-data transition:

```txt
PLI_RAWDATA GINT(4) "\n" "abc\n"
```

With client/newer-RC raw newline stripping enabled, raw payload becomes:

```txt
"abc"
```

## CFileQueue Passthrough Flush

For `ENCRYPT_GEN_1`/`ENCRYPT_GEN_6`, `CFileQueue::sendCompress` appends queued
bytes directly to `oBuffer` before `CSocket::sendData`.

Normal queued bytes:

```txt
ASCII("abc\nxyz\n") => [97, 98, 99, 10, 120, 121, 122, 10]
```

Raw-data board payload stays in the normal queue:

```txt
PLO_RAWDATA GINT(2) "\n" PLO_BOARDPACKET "x"
=> [132, 32, 32, 34, 10, 133, 120]
```

Partial socket send fixture:

```txt
queued ASCII("abcdef\n")
first send max 3 => ASCII("abc")
next send => ASCII("def\n")
```

## CFileQueue Gen5 Socket Flush

For `ENCRYPT_GEN_5`, `CFileQueue::sendCompress` uses compression type
`COMPRESS_UNCOMPRESSED = 0x02` when the pending payload length is `<= 55`.
The socket frame is:

```txt
GSHORT(encryptedLength + 1), raw compression type byte, encrypted payload
```

Fixture with key `0` and queued payload `ASCII("abc\n")`:

```txt
queued payload:
[97, 98, 99, 10]

encrypted payload:
[121, 122, 178, 220]

socket bytes:
[0, 5, 2, 121, 122, 178, 220]
```

Partial socket send fixture:

```txt
first send max 3 => [0, 5, 2]
next send => [121, 122, 178, 220]
```

Gen4 bzip2, gen5 zlib, and gen5 bzip2 outbound payloads are now covered below.

## CFileQueue zlib Socket Flush

Fixtures captured with `tools/gs2lib-fixtures` against recovered `gs2lib`.

Gen2 and gen3 both use the same zlib-only `CFileQueue::sendCompress` branch.

`ENCRYPT_GEN_2`, key `0`, queued payload `ASCII("abc\n")`:

```txt
[0, 12, 120, 156, 75, 76, 74, 230, 2, 0, 3, 126, 1, 49]
```

`ENCRYPT_GEN_3`, key `0`, queued payload `ASCII("abc\n")`:

```txt
[0, 12, 120, 156, 75, 76, 74, 230, 2, 0, 3, 126, 1, 49]
```

`ENCRYPT_GEN_2`, key `0`, queued payload `ASCII("a" repeated 100 + "\n")`:

```txt
[0, 13, 120, 156, 75, 76, 164, 61, 224, 2, 0, 160, 54, 37, 239]
```

`ENCRYPT_GEN_5`, key `0`, queued payload
`ASCII("a" repeated 55 + "\n")`, total length 56:

```txt
[0, 14, 4, 96, 132, 154, 154, 92, 211, 49, 130, 88, 70, 28, 19, 90]
```

`ENCRYPT_GEN_4`, key `0`, queued payload `ASCII("abc\n")`:

```txt
[0, 42, 90, 66, 185, 231, 73, 153, 24, 165, 11, 67, 10, 96, 237, 53,
 152, 226, 0, 193, 0, 0, 16, 56, 0, 32, 0, 33, 154, 104, 51, 77,
 19, 60, 93, 201, 20, 225, 66, 66, 181, 157, 87, 88]
```

Gen5 bzip2 threshold fixture, implemented in C#:

```txt
input: ASCII("a" repeated 8192 + "\n"), total length 8193
output:
[0, 50, 6, 90, 66, 185, 231, 73, 153, 24, 165, 11, 67, 212, 75, 100,
 153, 152, 226, 18, 225, 0, 128, 16, 0, 4, 32, 0, 0, 8, 32, 0,
 48, 205, 52, 10, 163, 31, 10, 11, 0, 97, 119, 36, 83, 133, 9, 7,
 52, 205, 199, 160]
```

## WebSocket Frame Fixtures

Fixtures captured with `tools/gs2lib-fixtures` against recovered `gs2lib`
`webSocketFixOutgoingPacket` / `webSocketFixIncomingPacket`.

Outgoing small binary frame:

```txt
input: [97, 98, 99]
output: [130, 3, 97, 98, 99]
```

Outgoing extended-126 binary frame:

```txt
input: ASCII("a" repeated 126)
output prefix: [130, 126, 0, 126]
output payload: ASCII("a" repeated 126)
```

Incoming masked small binary frame:

```txt
input: [130, 131, 1, 2, 3, 4, 96, 96, 96]
result: 3
output: [97, 98, 99]
```

Incoming extended-126 frame with declared length 3 and four available payload
bytes:

```txt
input: [130, 254, 0, 3, 1, 2, 3, 4, 96, 96, 96, 101]
result: 4
output: [97, 98, 99, 97]
```

Incoming close frame on the local MSVC fixture build:

```txt
input: [136, 128, 0, 0, 0, 0]
result: -1
output: [136, 128, 0, 0, 0, 0]
```

## File Transfer Cache Boundary

`PLO_FILESENDFAILED + "miss.png" + "\n"`:

```txt
[62, 109, 105, 115, 115, 46, 112, 110, 103, 10]
```

`PLO_FILEUPTODATE + "head.png" + "\n"`:

```txt
[77, 104, 101, 97, 100, 46, 112, 110, 103, 10]
```

Modern `sendFile` chunk for `file="test.txt"`, `modTime=1`,
`data="abc"`:

```txt
PLO_RAWDATA GINT(19) "\n"
PLO_FILE GINT5(1) GCHAR(8) "test.txt" "abc" "\n"

=> [132, 32, 32, 51, 10,
    134, 32, 32, 32, 32, 33, 40,
    116, 101, 115, 116, 46, 116, 120, 116,
    97, 98, 99, 10]
```

Old-client `sendFile` chunk for `file="test.txt"`, `data="abc"`:

```txt
PLO_RAWDATA GINT(13) "\n"
PLO_FILE GCHAR(8) "test.txt" "abc"

=> [132, 32, 32, 45, 10,
    134, 40,
    116, 101, 115, 116, 46, 116, 120, 116,
    97, 98, 99]
```

Large-file markers for `file="big.bin"`, size `32001`:

```txt
PLO_LARGEFILESTART "big.bin" "\n"
=> [100, 98, 105, 103, 46, 98, 105, 110, 10]

PLO_LARGEFILESIZE GINT5(32001) "\n"
=> [116, 32, 32, 33, 154, 33, 10]

PLO_LARGEFILEEND "big.bin" "\n"
=> [101, 98, 105, 103, 46, 98, 105, 110, 10]
```

Update package boundary packets for `packageName="pkg"`, total size `100`:

```txt
PLO_UPDATEPACKAGESIZE GCHAR(3) "pkg" GINT5(100) "\n"
=> [137, 35, 112, 107, 103, 32, 32, 32, 32, 132, 10]

PLO_UPDATEPACKAGEDONE "pkg" "\n"
=> [138, 112, 107, 103, 10]
```

## Account Loading Fixtures

These fixtures assert state transitions and persistence side-effect requests,
not network bytes.

Existing account lookup:

```txt
input account: "pc-ruan"
account fs findi: "pc-ruan.txt" => "C:\GServer\accounts\PC-Ruan.TXT"
file header: GRACC001
result: LoadedFromDefault=false, SourcePath="C:\GServer\accounts\PC-Ruan.TXT"
result: ShouldSaveCreatedAccount=false
```

Missing account fallback:

```txt
input account: "NewAccount"
account fs findi: "NewAccount.txt" => empty
fallback path: "C:\GServer\accounts\defaultaccount.txt"
settings: startlevel=onlinestartlocal.nw, startx=30, starty=30.5
result: LoadedFromDefault=true
result level: onlinestartlocal.nw
result pixel x/y: 480, 488
result: ShouldSaveCreatedAccount=true
result: AccountFileToAdd="accounts/NewAccount.txt"
```

Load-only fallback:

```txt
fallback account contains: LOADONLY 1
result: ShouldSaveCreatedAccount=false
result: AccountFileToAdd=null
```

Guest account boundary:

```txt
input account: "guest"
result: IsLoadOnly=true
result: RequiresGuestIdentityGeneration=true
```

The exact random `pc:` guest identity is not a golden fixture yet because it
depends on `srand(time(0))`, C `rand()`, and connected-player uniqueness checks.

## Account Save Fixtures

These fixtures assert source-confirmed `Account::saveAccount` text and
side-effect behavior.

Representative account save text uses CRLF line endings and begins:

```txt
GRACC001\r\n
NAME pc:Ruan\r\n
NICK Ruan\r\n
COMMUNITYNAME pc:Ruan\r\n
LEVEL start.nw\r\n
X 30\r\n
Y 30.5\r\n
Z 1.5\r\n
```

Confirmed full-order fixture is locked by
`tests/Persistence.Tests/AccountFileSerializerTests.cs`.

Load-only account save:

```txt
input: LOADONLY 1
result: Account::saveAccount returns false
result: no write attempted
```

Existing case-preserved filename:

```txt
account name: pc:Ruan
fileExistsAs("pc:Ruan.txt") => "PC-Ruan.TXT"
write path: C:\GServer\accounts\PC-Ruan.TXT
```

Disk write failure after serialization:

```txt
input: non-load-only account
save(...) => false
result: Account::saveAccount still returns true after logging
```

Default-account creation side effect:

```txt
loadedFromDefault=true, LOADONLY 0
save path: <serverPath>\accounts\NewAccount.txt
addFile: accounts/NewAccount.txt
```

## Warp Packet Bodies

These are packet bodies before `Player::sendPacket` newline append and before
`CFileQueue` flush.

`PLO_WARPFAILED + "missing.nw"`:

```txt
[47, 109, 105, 115, 115, 105, 110, 103, 46, 110, 119]
```

`PLO_PLAYERWARP`, `x=30.5`, `y=31.25`, `level="start.nw"`:

```txt
[46, 93, 94, 115, 116, 97, 114, 116, 46, 110, 119]
```

`PLO_PLAYERWARP2`, `x=30.5`, `y=31.25`, `z=1.5`, `mapX=4`, `mapY=5`,
`map="world.gmap"`:

```txt
[81, 93, 94, 85, 36, 37, 119, 111, 114, 108, 100, 46, 103, 109, 97, 112]
```

`PLO_LEVELNAME + "start.nw"`:

```txt
[38, 115, 116, 97, 114, 116, 46, 110, 119]
```

## BeginSetLevel Pre-Runtime Packet Sequences

These fixtures include the `Player::sendPacket` newline append.

Missing level:

```txt
PLO_WARPFAILED + "missing.nw" + "\n"
=> [47, 109, 105, 115, 115, 105, 110, 103, 46, 110, 119, 10]
```

Modern client, single level, `modTime == 0`:

```txt
PLO_PLAYERWARP, x=30.5, y=31.25, level="start.nw", "\n"
=> [46, 93, 94, 115, 116, 97, 114, 116, 46, 110, 119, 10]
```

Modern client, GMAP, `modTime == 0`:

```txt
PLO_PLAYERWARP2, x=30.5, y=31.25, z=1.5, mapX=4, mapY=5, map="world.gmap", "\n"
=> [81, 93, 94, 85, 36, 37, 119, 111, 114, 108, 100, 46, 103, 109, 97, 112, 10]
```

Modern client, non-zero mod time:

```txt
no PLO_PLAYERWARP/PLO_PLAYERWARP2 before sendLevel runtime boundary
=> []
```

Old client (`CLVER_1_411 < CLVER_2_1`), non-zero mod time:

```txt
PLO_PLAYERWARP, x=30.5, y=31.25, level="start.nw", "\n"
=> [46, 93, 94, 115, 116, 97, 114, 116, 46, 110, 119, 10]
```

Same-level `warp("start.nw", 30.5, 31.25)` position update:

```txt
PLO_PLAYERPROPS
PLPROP_X GCHAR(61)
PLPROP_Y GCHAR(62)
"\n"

=> [41, 47, 93, 48, 94, 10]
```

Missing target fallback to previous level:

```txt
PLO_WARPFAILED "missing.nw" "\n"
PLO_PLAYERWARP oldX=30.5 oldY=31.25 "start.nw" "\n"

=> [47, 109, 105, 115, 115, 105, 110, 103, 46, 110, 119, 10,
    46, 93, 94, 115, 116, 97, 114, 116, 46, 110, 119, 10]
```

Missing target fallback to default unstick level:

```txt
PLO_WARPFAILED "missing.nw" "\n"
PLO_PLAYERWARP x=30.0 y=35.0 "onlinestartlocal.nw" "\n"

=> [47, 109, 105, 115, 115, 105, 110, 103, 46, 110, 119, 10,
    46, 92, 102, 111, 110, 108, 105, 110, 101, 115, 116, 97,
    114, 116, 108, 111, 99, 97, 108, 46, 110, 119, 10]
```

## Modern sendLevel Static Payload Boundary

These fixtures include `Player::sendPacket` newline behavior.

`PLO_LEVELMODTIME`, `modTime=1`:

```txt
PLO_LEVELMODTIME + GINT5(1) + "\n"
=> [71, 32, 32, 32, 32, 33, 10]
```

Raw board header for `Level::getBoardPacket()`:

```txt
PLO_RAWDATA + GINT(8194) + "\n"
=> [132, 32, 96, 34, 10]
```

The following board payload length is exactly `8194` bytes:

```txt
PLO_BOARDPACKET + 4096 raw short tiles + "\n"
```

Modern level payload when cache is empty and requested mod time matches level
mod time:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELMODTIME GINT5(1)
opaque links packet "links\n"
opaque signs packet "signs\n"
empty board-changes packet

=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    71, 32, 32, 32, 32, 33, 10,
    108, 105, 110, 107, 115, 10,
    115, 105, 103, 110, 115, 10,
    32, 10]
```

Modern level payload when cache is empty and requested mod time differs:

```txt
PLO_LEVELNAME "start.nw"
PLO_RAWDATA GINT(8194)
PLO_BOARDPACKET + 8192 tile bytes + "\n"
for each non-zero layer: PLO_RAWDATA GINT(layer.length), layer packet
PLO_LEVELMODTIME GINT5(1)
links packet if non-empty
signs packet if non-empty
```

If `getCachedLevelModTime(level) != 0`, the C# boundary currently emits only:

```txt
PLO_LEVELNAME "start.nw"
empty board-changes packet
=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10, 32, 10]
```

When `fromAdjacent == true`, dynamic board changes/chests/horses/baddies are
skipped:

```txt
PLO_LEVELNAME "start.nw"
=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10]
```

## Modern sendLevel Dynamic And Runtime Boundary

These fixtures include `Player::sendPacket` newline behavior.

Empty board-change list when `fromAdjacent == false`:

```txt
PLO_LEVELBOARD + "\n"
=> [32, 10]
```

Board-change filtering with cached mod time `10` includes changes whose
`modTime >= 10`:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELBOARD
change 10: x=1 y=2 width=3 height=4 raw tiles [80,81]
change 11: x=5 y=6 width=7 height=8 raw tiles [82]

=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    32, 33, 34, 35, 36, 80, 81, 37, 38, 39, 40, 82, 10]
```

Chest list with one unopened and one already opened chest:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELBOARD empty
PLO_LEVELCHEST has=0 x=10 y=11 item=2 sign=3
PLO_LEVELCHEST has=1 x=12 y=13

=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    32, 10,
    36, 32, 42, 43, 34, 35, 10,
    36, 33, 44, 45, 10]
```

Horse and baddy packets using pre-serialized runtime payloads:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELBOARD empty
PLO_HORSEADD raw "horse"
PLO_BADDYPROPS id=5 raw props [70,71]

=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    32, 10,
    49, 104, 111, 114, 115, 101, 10,
    34, 37, 70, 71, 10]
```

Post-dynamic non-GMAP continuation:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELBOARD empty
PLO_GHOSTICON GCHAR(0)
PLO_NEWWORLDTIME GINT4(1)
PLO_SETACTIVELEVEL "start.nw"
opaque NPC packet [70,10]

=> [38, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    32, 10,
    206, 32, 10,
    74, 32, 32, 32, 33, 10,
    188, 115, 116, 97, 114, 116, 46, 110, 119, 10,
    70, 10]
```

Adjacent GMAP continuation with map context and leader:

```txt
PLO_LEVELNAME "inside.nw"
PLO_LEVELNAME "world.gmap"
PLO_GHOSTICON GCHAR(0)
PLO_ISLEADER
PLO_NEWWORLDTIME GINT4(1)
PLO_SETACTIVELEVEL "world.gmap"

=> [38, 105, 110, 115, 105, 100, 101, 46, 110, 119, 10,
    38, 119, 111, 114, 108, 100, 46, 103, 109, 97, 112, 10,
    206, 32, 10,
    42, 10,
    74, 32, 32, 32, 33, 10,
    188, 119, 111, 114, 108, 100, 46, 103, 109, 97, 112, 10]
```

## Level-Entry Player Props

`PLO_OTHERPLPROPS` wrapper for `playerId=7` and a minimal payload containing
`PLPROP_JOINLEAVELVL`, `PLPROP_NICKNAME`, and `PLPROP_X`:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_JOINLEAVELVL GCHAR(1)
PLPROP_NICKNAME GCHAR(4) "Ruan"
PLPROP_X GCHAR(70)
"\n"

=> [40, 32, 39,
    82, 33,
    32, 36, 82, 117, 97, 110,
    47, 102,
    10]
```

Inbound `PLPROP_NICKNAME` uses `GCHAR len` plus `CString::readChars(len)`.
`readChars` clamps to bytes remaining, so a terminal truncated nickname payload
parses the available bytes. Runtime mutation remains blocked by default until
the word filter is ported, but the explicit clean no-guild boundary preserves
C++ `setNick` normalization:

```txt
PLPROP_NICKNAME + GCHAR(4) + "Ru" => "Ru"
PLPROP_NICKNAME + GCHAR(10) + "  **pc:7  " with account "pc:7" => nickname "*pc:7"
```

When the clean no-guild boundary forwards player id `7` changing nickname to
`"Ruan"`, C++ places the prop in `globalBuff` and calls `sendPacketToAll`,
which excludes only self and NPC-Server players. It does not emit an empty
level-area `PLO_OTHERPLPROPS` packet when `levelBuff` is empty:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_NICKNAME + GCHAR(4) + "Ruan" + "\n"
bytes: 40 32 39 32 36 82 117 97 110 10
```

Inbound `PLPROP_RUPEESCOUNT` uses `CString::readGUInt()` in the recovered
C++ branch, then clamps the unsigned value to `9999999u`. Because `CString`
zero-fills missing scalar bytes, a terminal property id with no value bytes
decodes as unsigned `4294438880`, then clamps to the maximum:

```txt
PLPROP_RUPEESCOUNT => 9999999
```

Inbound `PLPROP_CARRYNPC` also uses `CString::readGUInt()`. A terminal
malformed carry-NPC prop with no payload therefore preserves the unsigned
decoded value instead of a signed negative fallback:

```txt
PLPROP_CARRYNPC => 4294438880
```

Inbound `PLPROP_ATTACHNPC` reads `GUChar object_type` followed by
`GUInt npcID`. With no payload bytes, recovered `CString` EOF behavior makes
the object type decode to `224` and the NPC id decode to the same unsigned
zero-filled scalar value:

```txt
PLPROP_ATTACHNPC => object_type 224, npcID 4294438880
```

Inbound one-byte inventory/stat props that use `CString::readGUChar()` inherit
the same EOF value. For `PLPROP_ARROWSCOUNT`, the parser observes `224`; the
runtime mutation then applies the recovered C++ `clip(arrows, 0, 99)` rule:

```txt
PLPROP_ARROWSCOUNT => parsed 224 => stored arrows 99
```

Modern `sendLevel` no-map visibility tail fixture:

```txt
joining player id: 7
self props packet: [1]
same-level client 8 props: [65]
same-level non-client 9 props: [66]
outside-level client 10 props: [67]
```

Confirmed effects:

```txt
broadcasts to existing clients:
  player 8 receives [1,10]

joining player's outbound suffix:
  [65,10,66,10]
```

The non-client appears in the joining player's no-map received list because the
C++ no-map receive branch iterates `level->getPlayers()` and does not repeat the
`isClient()` check there. Broadcast direction still uses
`Server::sendPacketToLevelArea`, which checks `isClient()`.

Modern GMAP/group-map visibility tail fixture:

```txt
joining player id: 7, map "world.gmap", group "red", map pos (4,4)
player 8: same map, group "red", pos (5,4), props [65]
player 9: same map, group "blue", pos (5,4), props [66]
player 10: same map, group "red", pos (6,4), props [67]
player 11: other map, group "red", pos (4,4), props [68]
```

Confirmed effects:

```txt
broadcasts:
  player 8 receives [1,10]

joining player's outbound suffix:
  [65,10]
```

## Minimal Level Runtime Ownership

These fixtures assert source-confirmed ownership/list ordering, not packet
bytes.

`Level::addPlayer` append behavior:

```txt
add 7 => returned index 0, players [7]
add 8 => returned index 1, players [7,8]
leader 7 => true
leader 8 => false
```

`Level::removePlayer` all-matching-id erase behavior:

```txt
players before remove: [7,8,7]
remove 7
players after remove: [8]
leader 8 => true
```

`Server::addPlayer` requested-id overwrite behavior:

```txt
add first player with id 7
add replacement player with id 7
server lookup 7 => replacement player
```

`Server::deletePlayer` deferred cleanup boundary:

```txt
delete player 7
lookup 7 before cleanup => player still present
cleanup deleted players
lookup 7 after cleanup => missing
```

## Level Format Detection

Extension-first selection:

```txt
start.nw => NW
start.graal => Graal
start.zelda => Zelda
start.txt => unknown, inspect header
START.NW => unknown, inspect header
```

Eight-byte signatures:

```txt
GLEVNW01 => NW
GR-V1.03 => Graal
GR-V1.02 => Graal
GR-V1.01 => Graal
Z3-V1.04 => Zelda
Z3-V1.03 => Zelda
UNKNOWN! => unknown
```

The confirmed signature and representative level packet fixtures are also
cataloged in `tests/Game.Tests/LevelGoldenFixtures.cs` and verified by
`LevelGoldenFixtureCatalogTests`. The catalog intentionally stays in the test
project because it is compatibility evidence, not runtime behavior.

## NW Level Parser

`BOARD` tile decoding:

```txt
input: BOARD 1 2 3 0 AB+/@?
tile at (1,2): "AB" => 1
tile at (2,2): "+/" => 4031
tile at (3,2): "@?" => 0
```

`SIGN` body preservation:

```txt
SIGN 4 5
first line
second line
SIGNEND

parsed text: "first line\nsecond line\n"
```

`NPC` image-with-spaces preservation:

```txt
NPC image with spaces.png 12.5 13.25
if (created) {
}
NPCEND

image: "image with spaces.png"
x: 12.5
y: 13.25
code: "if (created) {\n}\n"
```

`BADDY` verse preservation:

```txt
BADDY 10 11 2
see
hurt
attack
BADDYEND

x=10, y=11, type=2, verses=["see","hurt","attack"]
```

`getBoardPacket` from parsed `.nw` board:

```txt
input: BOARD 0 0 2 0 AB+/
packet prefix:
[133, 1, 0, 191, 15]
```

`133` is `GCHAR PLO_BOARDPACKET`. Tile `1` is raw little-endian `[1,0]`.
Tile `4031` is raw little-endian `[191,15]`.

`getLayerPacket(1)` from parsed `.nw` layer:

```txt
input: BOARD 0 0 1 1 +/
packet prefix:
[139, 1, 0, 0, 64, 64, 191, 15]
```

`139` is `GCHAR PLO_BOARDLAYER`, followed by raw layer header
`[1,0,0,64,64]` and raw little-endian tile bytes.

`sendLevel` integration prefix from parsed board:

```txt
PLO_RAWDATA GINT(8194) "\n" parsed board packet prefix
=> [132, 32, 96, 34, 10, 133, 1, 0, 191, 15]
```

Parsed `.nw` link packet:

```txt
LINK target level.nw 1 2 3 4 5.5 6.5
=> [33] + ASCII("target level.nw 1 2 3 4 5.5 6.5") + [10]
```

Parsed `.nw` sign packet:

```txt
SIGN 4 5
A
SIGNEND

=> [37, 36, 37, 32, 128, 10]
```

`37` is `GCHAR PLO_LEVELSIGN`, `36`/`37` are `GCHAR x/y`, `32` is encoded
`A`, and `128` is encoded newline.

Level item catalog examples:

```txt
greenrupee => 0
bombs => 3
spinattack => 24
GREENRUPEE => INVALID
```

Parsed `.nw` chest packet with one unopened and one opened chest:

```txt
CHEST 10 11 redrupee 3
CHEST 12 13 bluerupee 4
player has chest key "12:13:start.nw"

=> [36, 32, 42, 43, 34, 35, 10,
    36, 33, 44, 45, 10]
```

Parsed `.nw` static `sendLevel` sequence through chests when board is current:

```txt
PLO_LEVELNAME "start.nw"
PLO_LEVELMODTIME GINT5(1)
PLO_LEVELLINK "next.nw 1 2 3 4 5 6"
PLO_LEVELSIGN x=4 y=5 text="A\n"
PLO_LEVELBOARD empty
PLO_LEVELCHEST unopened redrupee at 10,11 sign 3
```

Filesystem-loaded `.nw` static payload:

```txt
world/start.nw indexed via FileSystem addDir("world", "*.nw")
requested level: start.nw
cache time: 0
requested modTime: loaded stat mtime
board already current

sequence:
PLO_LEVELNAME "start.nw"
PLO_LEVELMODTIME GINT5(loaded stat mtime)
PLO_LEVELLINK "next.nw 1 2 3 4 5 6"
PLO_LEVELSIGN x=4 y=5 text="A\n"
PLO_LEVELBOARD empty
PLO_LEVELCHEST unopened redrupee at 10,11 sign 3
```

The exact five bytes after `PLO_LEVELMODTIME` vary with the file mtime and are
encoded through the existing source-confirmed `GINT5` writer.

## Dev-Only Local Pipeline

Input frame:

```txt
raw big-endian length prefix
Client3 login packet body:
GCHAR 5
GCHAR 42
"G3D0311C"
GCHAR 4 "Ruan"
GCHAR 2 "pw"
"win"
```

With `EnableLocalDebugAuth=true` and a filesystem-loaded `start.nw`, the diagnostic
pipeline reaches:

```txt
SessionLifecycle.DynamicLevelPayloadSent
LocalDebugStopPoint.BeforeRuntimeWorldSimulation
```

The outbound byte stream includes:

```txt
gen5 socket frame with compression type 0x04 for small/medium responses
```

The decrypted/decompressed queue payload is the confirmed login/pre-runtime
packet sequence. The dev shell currently selects the source-confirmed
current-modtime `sendLevel` branch, so a tiny `.nw` diagnostic response does not
include the raw board `PLO_RAWDATA` packet. Full board/resource transfer remains
uncertified against live C++ and client captures even though bzip2 socket
framing has isolated fixture coverage.

Unsupported second length-prefixed frame after the login boundary:

```txt
input: first Client3 login frame, then a second frame [0x20, 0x0A]
result: Accepted=true
log contains:
"Unsupported post-login frame received by local-debug shell; continuous loop stopped before gameplay/runtime packet handling."
```

Decoded post-login `PLI_PLAYERPROPS` movement frame:

```txt
packet body:
PLI_PLAYERPROPS
PLPROP_X GCHAR(70)
PLPROP_Y GCHAR(71)

bytes:
[34, 47, 102, 48, 103]

local-debug applied state:
x=560
y=568
```

Decoded post-login read-only/no-op `PLI_PLAYERPROPS` body:

```txt
PLPROP_ID GSHORT(7)
PLPROP_KILLSCOUNT GINT(111)
PLPROP_DEATHSCOUNT GINT(222)
PLPROP_ONLINESECS GINT(333)
PLPROP_JOINLEAVELVL
PLPROP_PCONNECTED
PLPROP_UNKNOWN81 GCHAR(3)
PLPROP_X GCHAR(70)
```

Expected parsed property order:

```txt
ID, KILLSCOUNT, DEATHSCOUNT, ONLINESECS, JOINLEAVELVL, PCONNECTED,
UNKNOWN81, X
```

Forwarding only read-only/no-op updates with no local-forwarded props emits
only the `PLO_OTHERPLPROPS` wrapper for player id `7` plus newline:

```txt
28 20 27 0a
```

Scalar inventory/stat `PLI_PLAYERPROPS` body slice:

```txt
PLPROP_MAXPOWER GCHAR(15)
PLPROP_CURPOWER GCHAR(11)
PLPROP_RUPEESCOUNT GINT(3000000)
PLPROP_ARROWSCOUNT GCHAR(150)
PLPROP_BOMBSCOUNT GCHAR(151)
PLPROP_GLOVEPOWER GCHAR(9)
PLPROP_BOMBPOWER GCHAR(8)
PLPROP_APCOUNTER GSHORT(123)
PLPROP_MAGICPOINTS GCHAR(200)
PLPROP_ADDITFLAGS GCHAR(77)
PLPROP_ALIGNMENT GCHAR(120)
PLPROP_CARRYSPRITE GCHAR(12)
PLPROP_HORSEBUSHES GCHAR(6)
```

Runtime clamps:

```txt
maxPower=15 when heartLimit=20
hitpoints=15 after max power
rupees=9999999 when incoming runtime value exceeds the C++ cap
arrows=99
bombs=99
glovePower=3
bombPower=3
apCounter=123
magicPoints=100
additionalFlags=77
alignment=100
carrySprite=12
horseBombCount=6
```

Non-V8 forwarded `PLPROP_MAXPOWER` mirrors the C++ special branch: setting max
power to `15` also sets current power to max and appends only
`PLPROP_CURPOWER + GCHAR(30)` to the level buffer:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_CURPOWER + GCHAR(30) + "\n"
bytes: 40 32 39 34 62 10
```

Live `PLPROP_CURPOWER` forwarding uses post-mutation runtime state. With AP
`39`, current HP `2.0`, and incoming `PLPROP_CURPOWER + GCHAR(8)`, the C++
healing gate refuses the increase and forwarding emits the existing HP byte
`GCHAR(4)`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_CURPOWER + GCHAR(4) + "\n"
bytes: 40 32 39 34 36 10
```

Live `PLPROP_CURLEVEL` forwarding uses `getProp(PLPROP_CURLEVEL)` semantics.
For a client on a singleplayer level, stored level `"start.nw"` is emitted as
`"start.nw.singleplayer"`:

```txt
PLO_OTHERPLPROPS + GSHORT(7)
+ PLPROP_CURLEVEL + GCHAR(21) + "start.nw.singleplayer"
+ "\n"
bytes: 40 32 39 52 53 115 116 97 114 116 46 110 119 46 115 105 110 103 108 101 112 108 97 121 101 114 10
```

Inbound `PLPROP_CURLEVEL` uses `GCHAR len` plus `CString::readChars(len)` in
the non-V8 branch. Terminal truncated level-name payloads parse the remaining
bytes:

```txt
PLPROP_CURLEVEL + GCHAR(8) + "start" => "start"
```

Forwarded `PLPROP_APCOUNTER` uses C++ `getProp` semantics, so stored `123`
emits `GSHORT(124)`:

```txt
28 20 27 39 20 9c 0a
```

Forwarded local scalar props follow the C++ `__sendLocal` table. In this
confirmed fixture `PLPROP_CARRYSPRITE` and clamped `PLPROP_ALIGNMENT` are
forwarded, while `PLPROP_MAGICPOINTS`, `PLPROP_ADDITFLAGS`, and
`PLPROP_HORSEBUSHES` are not:

```txt
PLO_OTHERPLPROPS + GSHORT(7)
+ PLPROP_CARRYSPRITE + GCHAR(12)
+ PLPROP_ALIGNMENT + GCHAR(100)
+ "\n"
bytes: 40 32 39 51 44 64 132 10
```

Forwarded `PLPROP_UDPPORT` generic tail uses the C++ `GInt` payload for the
stored port. For port `14900`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_UDPPORT + GINT(14900) + "\n"
bytes: 40 32 39 63 32 148 84 10
```

Live `PLPROP_ACCOUNTNAME` forwarding ignores the consumed client-sent value and
uses current runtime account state. For account `"pc:7"`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_ACCOUNTNAME + GCHAR(4) + "pc:7" + "\n"
bytes: 40 32 39 66 36 112 99 58 55 10
```

Live `PLPROP_IPADDR` forwarding ignores the consumed client-sent value and uses
current runtime account-IP state. For account IP `1`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_IPADDR + GInt5(1) + "\n"
bytes: 40 32 39 62 32 32 32 32 33 10
```

Live `PLPROP_COMMUNITYNAME` forwarding ignores the consumed client-sent value
and uses current runtime community-name state. For community name `"Ruan"`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_COMMUNITYNAME + GCHAR(4) + "Ruan" + "\n"
bytes: 40 32 39 114 36 82 117 97 110 10
```

Live `PLPROP_RATING` forwarding ignores the consumed client-sent value and uses
current runtime ELO state. For rating `1500` and deviation `50`, C++ packs:

```txt
((1500 & 0xFFF) << 9) | (50 & 0x1FF)
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_RATING + GInt(768050) + "\n"
bytes: 40 32 39 68 78 144 82 10
```

## Combat Runtime Fixtures

Inbound `PLI_HURTPLAYER` fixture:

```txt
PLI_HURTPLAYER
GSHORT victimPlayerId=8
GCHAR hurtDx=18
GCHAR hurtDy=30
GCHAR power=5
GINT npcId=200
```

Decoded values:

```txt
victimPlayerId=8
hurtDx=18
hurtDy=30
power=5
npcId=200
```

`PLI_BADDYHURT` leader-forward fixture:

```txt
in:  [16, 48, 49, 50]
out: [59, 48, 49, 50, 10]
```

`PLI_CLAIMPKER` fixture:

```txt
in:  [46, 58, 57]
    ^ PLI_CLAIMPKER (14 + 32)
      58,57 = GSHORT(25) decoded as 25
out: parsed payload: KillerPlayerId = 25
```

`PLI_CLAIMPKER` EOF fallback fixture:

```txt
in: [46]
out: parsed payload: KillerPlayerId = 61408
```

`PLI_BADDYHURT` parse payload safety fixture:

```txt
in: [48, 16, 32, 33, 100]
out: parsed payload bytes [16, 32, 33, 100] are preserved for leader-forward
```

Non-spar `CLAIMPKER` AP-loss examples:

```txt
killerAp=80, loserAp=20 => killerAp=75, apCounter=600
killerAp=10, loserAp=20 => killerAp=9, apCounter=30
killerAp=1, loserAp=99 => killerAp=0, apCounter=30
killerAp=90, loserAp=19 => killerAp=90, apCounter=0
```

## Weapon/Class Packet Fixtures

`PLO_NPCWEAPONDEL "Tool" + "\n"`:

```txt
[66, 84, 111, 111, 108, 10]
```

`PLO_RAWDATA + GINT(3) + "\n" + PLO_NPCWEAPONSCRIPT + "ABC"`:

```txt
[132, 32, 32, 35, 10, 172, 65, 66, 67]
```

`PLI_UPDATEGANI + GINT5(0x01020304) + "walk"` parses as:

```txt
checksum = 0x01020304
gani = "walk"
ganiFile = "walk.gani"
```

`PLO_RAWDATA + GINT(8) + "\n" + PLO_GANISCRIPT + GCHAR(4) + "walk" + "ABC"`:

```txt
[132, 32, 32, 40, 10, 166, 36, 119, 97, 108, 107, 65, 66, 67]
```

`PLO_LOADGANI + GCHAR(4) + "walk" + "\"SETBACKTO idle\"" + "\n"`:

```txt
[227, 36, 119, 97, 108, 107, 34, 83, 69, 84, 66, 65, 67, 75, 84, 79, 32, 105, 100, 108, 101, 34, 10]
```

Missing `PLI_UPDATECLASS` response for class `foo`:

```txt
headerData:
  class
  foo
  1
  GINT5(0) + GINT5(0)
  GINT5(0)

retokenizeCStringArray(headerData):
  class,foo,1,"          ","     "

PLO_NPCWEAPONSCRIPT + raw short header length + header + "\n"
=> [172, 0, 32, 99, 108, 97, 115, 115, 44, 102, 111, 111, 44, 49, 44, 34, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 34, 44, 34, 32, 32, 32, 32, 32, 34, 10]
```

## Scripting Bytecode Fixture Boundary

No GS2 bytecode golden fixture is currently considered canonical.

The original C++ source proves that `GS2Context::CreateHeader` prefixes compiled
bytecode with a header containing script type, script name, save-to-disk flag,
and ten random Graal bytes. However, the exact original `gs2compiler` gitlink is
not available in the recovered source snapshot. The recovered
`external/gs2compiler` checkout is a supporting reference only.

Until the exact compiler commit or original C++ bytecode captures are recovered,
the C# port keeps compiler/runtime adapters blocked and tests only the explicit
blocked boundary. Packet fixtures for already-confirmed NPC, baddy, combat, and
weapon wrapper bytes remain valid because they come from the C++ server and
`gs2lib` packet definitions, not from guessed bytecode output.

Gen5 socket-framed post-login `PLI_PLAYERPROPS` movement frame with key `42`:

```txt
decoded payload:
[34, 47, 102, 48, 103, 10]

socket frame:
GSHORT(7)
COMPRESS_UNCOMPRESSED
encrypted payload

local-debug result:
log contains "Decoded inbound gen5 frame into 1 packet(s)."
log contains "Applied decoded PLI_PLAYERPROPS"
```

Inbound decode fixtures from `tools/gs2lib-fixtures`:

```txt
gen2 framePayload:
[120, 156, 75, 76, 74, 230, 2, 0, 3, 126, 1, 49]
decoded:
[97, 98, 99, 10]
```

```txt
gen4 framePayload:
[90, 66, 185, 231, 73, 153, 24, 165, 11, 67, 10, 96, 237, 53,
 152, 226, 0, 193, 0, 0, 16, 56, 0, 32, 0, 33, 154, 104,
 51, 77, 19, 60, 93, 201, 20, 225, 66, 66, 181, 157, 87, 88]
decoded:
[97, 98, 99, 10]
```

```txt
gen5 uncompressed framePayload:
[2, 121, 122, 178, 220]
decoded:
[97, 98, 99, 10]
```

```txt
gen5 zlib framePayload:
[4, 96, 132, 154, 154, 92, 211, 49, 130, 88, 70, 28, 19, 90]
decoded:
ASCII("a" repeated 55 + "\n")
```

Confirmed precise-movement forwarding packet for `playerId=7`,
`pixelX=560`, `pixelY=560`, sender version `>= CLVER_2_3`:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_X GCHAR(70)
PLPROP_Y GCHAR(70)
PLPROP_X2 GSHORT(1120)
PLPROP_Y2 GSHORT(1120)
"\n"

=> [40, 32, 39,
    47, 102,
    48, 102,
    110, 40, 128,
    111, 40, 128,
    10]
```

Confirmed legacy movement forwarding packet for `playerId=7`, `pixelX=560`,
`pixelY=568`, sender version `>= CLVER_2_3`. `PLPROP_X/Y` add precise
`PLPROP_X2/Y2` mirrors to `levelBuff2`, so modern sender order emits the
precise mirrors first:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_X2 GSHORT(1120)
PLPROP_Y2 GSHORT(1136)
PLPROP_X GCHAR(70)
PLPROP_Y GCHAR(71)
"\n"

=> [40, 32, 39,
    110, 40, 128,
    111, 40, 144,
    47, 102,
    48, 103,
    10]
```

For sender versions older than `CLVER_2_3`, the same legacy X/Y input writes
`levelBuff` before `levelBuff2`, so the legacy props precede the precise
mirrors:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_X GCHAR(70)
PLPROP_Y GCHAR(71)
PLPROP_X2 GSHORT(1120)
PLPROP_Y2 GSHORT(1136)
"\n"

=> [40, 32, 39,
    47, 102,
    48, 103,
    110, 40, 128,
    111, 40, 144,
    10]
```

Confirmed precise Z forwarding packet for `playerId=7`, `pixelZ=-39`, sender
version `>= CLVER_2_3`. C++ writes the legacy mirror to `levelBuff2` first, so
modern sender order emits `PLPROP_Z` before `PLPROP_Z2`:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_Z GCHAR(46)
PLPROP_Z2 GSHORT(79)
"\n"

=> [40, 32, 39,
    77, 78,
    112, 32, 111,
    10]
```

For sender versions older than `CLVER_2_3`, the same precise Z input writes
`levelBuff` before `levelBuff2`, so `PLPROP_Z2` precedes the legacy `PLPROP_Z`
mirror:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_Z2 GSHORT(79)
PLPROP_Z GCHAR(46)
"\n"

=> [40, 32, 39,
    112, 32, 111,
    77, 78,
    10]
```

Confirmed legacy Z forwarding packet for `playerId=7`, `pixelZ=-32`, sender
version `>= CLVER_2_3`. `PLPROP_Z` adds a precise `PLPROP_Z2` mirror to
`levelBuff2`, so modern sender order emits the mirror first:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_Z2 GSHORT(65)
PLPROP_Z GCHAR(46)
"\n"

=> [40, 32, 39,
    112, 32, 97,
    77, 78,
    10]
```

For sender versions older than `CLVER_2_3`, the same legacy Z input writes
`levelBuff` before `levelBuff2`, so `PLPROP_Z` precedes the precise `PLPROP_Z2`
mirror:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_Z GCHAR(46)
PLPROP_Z2 GSHORT(65)
"\n"

=> [40, 32, 39,
    77, 78,
    112, 32, 97,
    10]
```

For sender versions older than `CLVER_2_3`, C++ writes `levelBuff` before
`levelBuff2`, so the same precise input emits the precise props before the
legacy mirrors:

```txt
PLO_OTHERPLPROPS
GSHORT 7
PLPROP_X2 GSHORT(1120)
PLPROP_Y2 GSHORT(1120)
PLPROP_X GCHAR(70)
PLPROP_Y GCHAR(70)
"\n"

=> [40, 32, 39,
    110, 40, 128,
    111, 40, 128,
    47, 102,
    48, 102,
    10]
```

Confirmed precise movement runtime decode:

```txt
PLPROP_X2 GSHORT(1120) => pixelX=560
PLPROP_Y2 GSHORT(1121) => pixelY=-560
PLPROP_Z2 GSHORT(79) => pixelZ=-39
```

Confirmed precise movement serialization from `Player::getProp`:

```txt
X=-560 => PLPROP_X2 GSHORT(1121) => bytes 110 40 129
Y=560  => PLPROP_Y2 GSHORT(1120) => bytes 111 40 128
Z=-39  => PLPROP_Z2 GSHORT(79)   => bytes 112 32 111
```

## Movement Links Chests Boundary

`Level::getLink` inclusive tile bounds:

```txt
link: x=1, y=2, width=3, height=4
inside: (1,2), (4,6)
outside: (5,6), (4,7)
```

When multiple links match, C++ returns the first stored link.

Inbound `PLI_LEVELWARP`, x `30.5`, y `31.0`, level `"start.nw"`:

```txt
PLI_LEVELWARP
GCHAR(61)
GCHAR(62)
"start.nw"

=> [32, 93, 94, 115, 116, 97, 114, 116, 46, 110, 119]
```

Inbound `PLI_LEVELWARPMOD`, mod time `123`, x `40.0`, y `40.5`,
level `"world_a01.nw"`:

```txt
PLI_LEVELWARPMOD
GINT5(123)
GCHAR(80)
GCHAR(81)
"world_a01.nw"

=> [62, 32, 32, 32, 32, 155, 112, 113,
    119, 111, 114, 108, 100, 95, 97, 48, 49, 46, 110, 119]
```

Chest key formatting from `Level::getChestStr`:

```txt
chest x=10, y=11, level="start.nw"
=> "10:11:start.nw"
```

`Player::msgPLI_OPENCHEST` unopened chest acknowledgement:

```txt
PLO_LEVELCHEST, hasChest=1, x=10, y=11, "\n"
=> [36, 33, 42, 43, 10]
```

Chest reward application after that acknowledgement:

```txt
state.rupees=5
chest item=redrupee
LevelItem::getItemPlayerProp -> PLPROP_RUPEESCOUNT + GINT(35)
```

Expected state after applying the payload:

```txt
state.rupees=35
opened chest key contains "10:11:start.nw"
ack bytes [36, 33, 42, 43, 10]
```

Static sign encoding for an unknown ASCII character `@`:

```txt
PLO_LEVELSIGN, x=4, y=5, "#K(64)", encoded newline, "\n"
=> [37, 36, 37, 118, 42, 101, 90, 88, 102, 128, 10]
```

Runtime sign touch response for unformatted sign text `"First\nLine\n"`:

```txt
PLO_SAY2 "First#bLine#b" "\n"
=> [185, 70, 105, 114, 115, 116, 35, 98, 76, 105, 110, 101, 35, 98, 10]
```

`Player::testSign` sends one `PLO_SAY2` packet for each matching sign. It only
runs when `serverside=true` and `sprite % 4 == 0`; the X range
`[sign.x - 1.5, sign.x + 0.5]` is inclusive and Y must exactly equal
`sign.y`.

Movement-triggered sign helper fixture:

```txt
runtime pixelX=160, pixelY=176
C++ getX/getY equivalent: 10, 11
serverside=true
sprite=0
sign: x=10, y=11, text="Hello\n"
```

Expected packet bytes:

```txt
[185, 72, 101, 108, 108, 111, 35, 98, 10]
```

## Server-List Auth

### SVO_VERIACC2

Input:

```txt
account="Ruan", password="pw", playerId=7, type=PLTYPE_CLIENT3, identity="win"
```

Packet body before `ServerList::sendPacket` newline and `CFileQueue` compression:

```txt
[49, 36, 82, 117, 97, 110, 34, 112, 119, 32, 39, 64, 32, 35, 119, 105, 110]
```

Notes:

- `49` is `GCHAR SVO_VERIACC2` (`17 + 32`).
- `64` is `GCHAR PLTYPE_CLIENT3` (`32 + 32`), because C++ sends the type bitfield, not the login exponent.

### SVI_VERIACC2 Failure

For `message != "SUCCESS"`, C++ sends the message directly as:

```txt
PLO_DISCMESSAGE + message + "\n"
```

Example message `"Bad password."`:

```txt
[48] + ASCII("Bad password.") + [10]
```

The confirmed response payload before dispatch is:

```txt
account="Ruan", playerId=7, type=PLTYPE_CLIENT3, message="Bad password."
=> [36, 82, 117, 97, 110, 32, 39, 64, 66, 97, 100, 32, 112, 97, 115, 115, 119, 111, 114, 100, 46]
```

### SVI_VERIACC2 Success

For `message == "SUCCESS"`, C++ overwrites the local account name and continues
to `Player::sendLogin()` without sending a disconnect packet.

Confirmed response payload before dispatch:

```txt
account="pc:Ruan", playerId=7, type=PLTYPE_CLIENT3, message="SUCCESS"
=> [39, 112, 99, 58, 82, 117, 97, 110, 32, 39, 64, 83, 85, 67, 67, 69, 83, 83]
```

## RC/NC/Admin Boundary

### `PLO_RC_CHAT`

`PLO_RC_CHAT = 74`; packet id is written as GCHAR `74 + 32 = 106`.

```txt
PLO_RC_CHAT "New RC: Ruan" "\n"
=> [106, 78, 101, 119, 32, 82, 67, 58, 32, 82, 117, 97, 110, 10]
```

### `PLO_RC_MAXUPLOADFILESIZE`

`Player::sendLogin` sends `PLO_RC_MAXUPLOADFILESIZE` to RC users with
`1048576 * 20`.

```txt
PLO_RC_MAXUPLOADFILESIZE GINT5(20971520) "\n"
=> [135, 32, 42, 32, 32, 32, 10]
```

### `PLO_RC_FILEBROWSER_MESSAGE`

```txt
PLO_RC_FILEBROWSER_MESSAGE "Welcome to the File Browser." "\n"
=> ASCII("cWelcome to the File Browser.\n")
```

### `PLO_RC_FILEBROWSER_DIR`

Input:

```txt
current folder: levels/
entry: start.nw, rights rw, size 100, modified time 1
```

Packet:

```txt
[98,
 39, 108, 101, 118, 101, 108, 115, 47,
 32,
 54,
 40, 115, 116, 97, 114, 116, 46, 110, 119,
 34, 114, 119,
 32, 32, 32, 32, 132,
 32, 32, 32, 32, 33,
 10]
```

### `PLO_NC_WEAPONLISTGET`

```txt
PLO_NC_WEAPONLISTGET "SwordTool" "Gui" "\n"
=> [199, 41, 83, 119, 111, 114, 100, 84, 111, 111, 108, 35, 71, 117, 105, 10]
```

### `PLO_NC_WEAPONGET`

`PlayerNC.cpp::msgPLI_NC_WEAPONGET` replaces script newlines with byte `0xA7`.

```txt
PLO_NC_WEAPONGET "Tool" "tool.png" "a\nb" "\n"
=> [224, 36, 84, 111, 111, 108, 40, 116, 111, 111, 108, 46, 112, 110, 103, 97, 167, 98, 10]
```

For NC clients older than `NCVER_2_1`, the same request uses the legacy
`PLO_NPCWEAPONADD` shape:

```txt
PLO_NPCWEAPONADD "Tool" NPCPROP_IMAGE "tool.png" NPCPROP_SCRIPT GSHORT(3) "a\xa7b" "\n"
=> [65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 35, 97, 167, 98, 10]
```

### `PLO_NC_CLASSGET`

`PlayerNC.cpp::msgPLI_NC_CLASSEDIT` sends `classCode.gtokenize()`.

```txt
PLO_NC_CLASSGET "foo" gtokenize("a\nb,c") "\n"
=> [194, 35, 102, 111, 111, 97, 44, 34, 98, 44, 99, 34, 10]
```

`msgPLI_NC_CLASSADD` broadcasts only when the class did not already exist:

```txt
PLO_NC_CLASSADD "foo" "\n"
=> [195, 102, 111, 111, 10]
```

`msgPLI_NC_CLASSDELETE` broadcasts only when `Server::deleteClass` succeeds:

```txt
PLO_NC_CLASSDELETE "foo" "\n"
=> [220, 102, 111, 111, 10]
```

### `PLO_NPCSERVERADDR`

```txt
PLO_NPCSERVERADDR GSHORT(7) "127.0.0.1,14950" "\n"
=> ASCII("o '127.0.0.1,14950\n")
```

### Server-List Operational Packets

```txt
SVO_PING
=> [48]
```

```txt
SVO_REQUESTLIST GSHORT(7) "accounts"
=> [58, 32, 39, 97, 99, 99, 111, 117, 110, 116, 115]
```

## Entity Runtime Boundary

### Items

`PLO_ITEMADD`, encoded x `21`, encoded y `23`, item type `redrupee = 2`:

```txt
[54, 53, 55, 34, 10]
```

`PLO_ITEMDEL`, encoded x `21`, encoded y `23`:

```txt
[55, 53, 55, 10]
```

Runtime item drop/take boundary fixtures:

```txt
PLI_ITEMADD payload: encodedX=21, encodedY=23, item=redrupee
decoded position: 10.5, 11.5
playerDrop=true
state.rupees=30
```

Expected:

```txt
state.rupees=0
level item redrupee at 10.5,11.5
forward PLO_ITEMADD [54, 53, 55, 34, 10]
```

```txt
PLI_ITEMTAKE payload: encodedX=21, encodedY=23
level item redrupee at 10.5,11.5
state.rupees=5
```

Expected:

```txt
level item removed
state.rupees=35
forward PLO_ITEMDEL [55, 53, 55, 10]
```

Level cleanup item delete for level coordinates `10.5, 11.5` multiplies both
coordinates by 2 and produces the same delete fixture:

```txt
[55, 53, 55, 10]
```

### Horses

`PLO_HORSEADD`, x `30.5`, y `31.0`, direction `2`, bushes `1`,
image `horse.png`:

```txt
[49, 61, 94, 38, 104, 111, 114, 115, 101, 46, 112, 110, 103, 10]
```

The x byte `61` is raw `(char)(x * 2)`, while y and dir/bush are GCHAR.

`PLO_HORSEDEL`, x `30.5`, y `31.0`:

```txt
[50, 93, 94, 10]
```

### Baddy Defaults

Default baddy type `2`, id `1`, x `10`, y `11`:

```txt
[34, 33,
 33, 52,
 34, 54,
 35, 34,
 36, 36, 44, 98, 97, 100, 100, 121, 114, 101, 100, 46, 112, 110, 103,
 37, 32,
 38, 32,
 39, 42,
 40, 32,
 41, 32,
 42, 32,
 10]
```

### Baddy Drop Mapping (source-confirmed `rand() % 12`)

`baddy_drop = rand() % 12` mapping:

- `0` => greenrupee (`0`)
- `1` => bluerupee (`1`)
- `2` => redrupee (`2`)
- `3` => bombs (`3`)
- `4` => darts (`4`)
- `5` => heart (`5`)
- `6..9` => greenrupee (`0`)
- `10..11` => no drop

For `rand()` values `6` (drop), then `3`,`3` coordinates:

`PLO_ITEMADD` for a greenrupee at computed encoded `(x*2=25, y*2=50)` (from `12.5,25.0` and GChar offset):

```txt
[86, 57, 82, 32, 10]
```

The same drop coordinates repeated once (`3`,`3`) for the next item in the
`drop_gralats == 6` decomposition:

```txt
[86, 57, 82, 33, 10]
```

### Player Death Drops

With C++-style inputs (`max=50`, `min=1`, `m_character.gralats=30`,
`m_character.arrows=25`, `m_character.bombs=15`) and deterministic `rand()`
sequence `6,0,0,3,3,3,3`:

`drop_gralats = 6`, `drop_arrows = 0`, `drop_bombs = 0`, then two coordinates at
`(57,78)` GChar-encoded bytes (raw `(12.5,23.0)`).

```txt
[86, 57, 78, 33, 10, 86, 57, 78, 32, 10]
```

### NPC And Weapon Packets

`PLO_DEFAULTWEAPON`, default item id `bow = 7`:

```txt
[75, 39, 10]
```

`PLO_NPCWEAPONADD`, name `Tool`, image `tool.png`, empty GS1 script:

```txt
[65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 32, 10]
```

### `sendLoginClient` Weapon/Class Boundary

For a modern `CLVER_4_0211` client with no login props/flags, one player
weapon packet `Tool`, one missing protected default weapon `bow`, and one
already-built class packet `[229, "class", "\n"]`, the C# boundary locks the
source-confirmed ordering:

```txt
PLO_PLAYERPROPS "\n"
PLO_CLEARWEAPONS "\n"
PLO_NPCWEAPONDEL "Bomb" "\n"
PLO_NPCWEAPONDEL "Bow" "\n"
PLO_NPCWEAPONADD Tool/tool.png empty-script "\n"
PLO_DEFAULTWEAPON bow "\n"
PLO_LOADSCRIPT/UNKNOWN197 opaque fixture "\n"
PLO_SERVERLISTCONNECTED "\n"
```

Exact bytes:

```txt
[41, 10,
 226, 10,
 66, 66, 111, 109, 98, 10,
 66, 66, 111, 119, 10,
 65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 32, 10,
 75, 39, 10,
 229, 99, 108, 97, 115, 115, 10,
 222, 10]
```

`PLO_NPCDEL`, npc id `7`:

```txt
[61, 32, 32, 39, 10]
```

`PLO_NPCPROPS`, npc id `7`, opaque props `[70,71]`:

```txt
[35, 32, 32, 39, 70, 71, 10]
```

`PLO_NPCDEL2`, level `start.nw`, npc id `7`:

```txt
[182, 115, 116, 97, 114, 116, 46, 110, 119, 32, 32, 39, 10]
```

## Runtime Player Props

Source-confirmed `PLPROP_NICKNAME` player-origin echo under `PLSETPROPS_SETBYPLAYER | PLSETPROPS_FORWARD`:

For player id `7`, runtime nickname `Ruan`, sender id `7`:

```txt
PLO_PLAYERPROPS + PLPROP_NICKNAME + GCHAR(4) + "Ruan" + "\n"
[41, 0, 4, 82, 117, 97, 110, 10]
```

This payload is sent after the global broadcast for the same property and is
sent to sender via `PLO_PLAYERPROPS` in the same packet flow.

Source-confirmed `PLPROP_GATTRIB1` generic forwarding from
`Player::setProps`, player id `7`, value `sword`, newline appended by
`sendPacket`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_GATTRIB1 + GCHAR(5) + "sword" + "\n"
[40, 32, 39, 69, 37, 115, 119, 111, 114, 100, 10]
```

Inbound `PLPROP_GATTRIB1..30` each use `GCHAR len` plus
`CString::readChars(len)`. A terminal truncated attribute payload parses the
remaining bytes:

```txt
PLPROP_GATTRIB1 + GCHAR(5) + "sw" => "sw"
```

Inbound `PLPROP_PLANGUAGE` and `PLPROP_OSTYPE` are local-only environment
strings. Each uses `GCHAR len` plus `CString::readChars(len)` and clamps to
remaining packet bytes:

```txt
PLPROP_PLANGUAGE + GCHAR(4) + "pt" => "pt"
PLPROP_OSTYPE + GCHAR(4) + "wi" => "wi"
```

Source-confirmed `PLPROP_COLORS` generic forwarding from `Player::setProps`,
player id `7`, colors `[1, 2, 3, 4, 5]`, newline appended by `sendPacket`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_COLORS + five GUChar color bytes + "\n"
[40, 32, 39, 45, 33, 34, 35, 36, 37, 10]
```

Source-confirmed `PLPROP_EFFECTCOLORS` consume-only update:

```txt
PLPROP_EFFECTCOLORS + GCHAR(0)
PLPROP_EFFECTCOLORS + GCHAR(1) + GInt4(0x01020304)
```

Both forms parse successfully and produce no forwarded `PLO_OTHERPLPROPS`
payload because `__sendLocal[23]` is false and the recovered branch does not
mutate visible runtime state.

Source-confirmed `PLPROP_BODYIMG` generic forwarding from `Player::setProps`,
player id `7`, body image `body.png`, newline appended by `sendPacket`:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_BODYIMG + GCHAR(8) + "body.png" + "\n"
[40, 32, 39, 67, 40, 98, 111, 100, 121, 46, 112, 110, 103, 10]
```

Inbound `PLPROP_BODYIMG` uses `GCHAR len` plus `CString::readChars(len)`.
A terminal truncated body-image payload parses the remaining bytes:

```txt
PLPROP_BODYIMG + GCHAR(8) + "body" => "body"
```

Source-confirmed `PLPROP_RATING` consume-only update:

```txt
PLPROP_RATING + GInt(123456)
```

The incoming value is consumed, but the recovered C++ runtime mutation is
commented out. The live state-backed forwarding path uses the current runtime
ELO/deviation state and preserves the C++ bit packing; sparring/runtime ELO
mutation remains blocked until that source-confirmed gameplay path is ported.

Source-confirmed `PLPROP_ACCOUNTNAME` consume-only update:

```txt
PLPROP_ACCOUNTNAME + GCHAR(4) + "Ruan"
```

The incoming account-name bytes are consumed, but C++ `setProps` discards them.
Full generic forwarding for this property must use the current
`getProp(PLPROP_ACCOUNTNAME)` account state and remains blocked until that
state-backed forwarding path exists.

Terminal truncated account-name payloads still parse as consume-only updates
because `CString::readChars` clamps to bytes remaining:

```txt
PLPROP_ACCOUNTNAME + GCHAR(4) + "Ru" => consume-only/no mutation value
```

Source-confirmed `PLPROP_COMMUNITYNAME` consume-only update:

```txt
PLPROP_COMMUNITYNAME + GCHAR(8) + "commname"
```

The incoming community-name bytes are consumed, but C++ `setProps` discards
them. The live state-backed forwarding path uses the current runtime community
name; isolated stateless forwarding remains blocked because it must not echo
client-sent community-name bytes.

Terminal truncated community-name payloads follow the same consume-only clamp:

```txt
PLPROP_COMMUNITYNAME + GCHAR(8) + "comm" => consume-only/no mutation value
```

Source-confirmed `PLPROP_IPADDR` consume-only update:

```txt
PLPROP_IPADDR + GInt5(0x7F000001)
```

The incoming IP bytes are consumed, but C++ `setProps` discards them. The live
state-backed forwarding path uses the current runtime account IP; isolated
stateless forwarding remains blocked because it must not echo client-sent IP
bytes.

Source-confirmed `PLPROP_UDPPORT` update:

```txt
PLPROP_UDPPORT + GInt(14900)
```

C++ stores the decoded UDP port in `m_udpport`. If the player is loaded and has
a valid id, C++ also emits a direct `PLO_OTHERPLPROPS` UDP-port packet to every
client except self, then the generic forwarding tail can emit the same payload
to level-area clients:

```txt
direct global packet:
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_UDPPORT + GINT(14900) + "\n"
bytes: 40 32 39 63 32 148 84 10

same-level client sees the direct packet and the later generic local tail.
other-level client sees only the direct packet.
RC/NC sessions are not `PLTYPE_ANYCLIENT` recipients.
```

Source-confirmed `PLPROP_GMAPLEVELX/Y` parser boundary:

```txt
PLPROP_GMAPLEVELX + GCHAR(4)
PLPROP_GMAPLEVELY + GCHAR(5)
PLPROP_X + GCHAR(70)
=> parser updates: GMAPLEVELX=4, GMAPLEVELY=5, X=70
```

The recovered C++ runtime then calls `Map::getLevelAt`, `leaveLevel`, and
`setLevel` only when the current level belongs to a GMAP. C# currently covers
the byte-consumption boundary; live level switching and forwarding remain
blocked until the runtime map/level transition can preserve that exact C++
behavior.

Source-confirmed `PLPROP_PSTATUSMSG` update:

```txt
PLPROP_PSTATUSMSG + GCHAR(4)
```

C++ stores the decoded player-list status-message index in `m_statusMsg`. If
the player is loaded and has a valid id, C++ emits a direct
`PLO_OTHERPLPROPS + id + PLPROP_PSTATUSMSG + GCHAR(status)` packet to every
client except self. Because `__sendLocal[53]` is true, the generic local tail
can then emit the same payload to level-area clients:

```txt
direct global packet:
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_PSTATUSMSG + GCHAR(4) + "\n"
bytes: 40 32 39 85 36 10

same-level client sees the direct packet and the later generic local tail.
other-level client sees only the direct packet.
RC/NC sessions are not `PLTYPE_ANYCLIENT` recipients.
```

Source-confirmed modern `PLPROP_HORSEGIF` update:

```txt
PLPROP_HORSEGIF + GCHAR(9) + "horse.png"
```

C++ stores the decoded horse image in `m_character.horseImage`. For clients
older than `CLVER_2_1`, extensionless horse images append `.gif`:

```txt
PLPROP_HORSEGIF + GCHAR(5) + "horse" => "horse.gif"
```

C++ reads at most 219 bytes from the declared string length. If `len` is larger,
remaining bytes are parsed as subsequent properties:

```txt
PLPROP_HORSEGIF + GCHAR(222) + 219 * "h" + PLPROP_X + GCHAR(70)
=> horse image length 219, then PLPROP_X is parsed normally
```

`CString::readChars` also clamps the requested length to the bytes remaining in
the packet. A truncated terminal horse-image payload therefore parses the
available bytes instead of failing:

```txt
PLPROP_HORSEGIF + GCHAR(9) + "horse" => "horse"
```

Generic local forwarding uses C++ `getProp(PLPROP_HORSEGIF)` shape:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_HORSEGIF + GCHAR(9) + "horse.png" + "\n"
bytes: 40 32 39 53 41 104 111 114 115 101 46 112 110 103 10
```

Loaded/global recipient routing remains blocked until production session
routing can match the original exactly.

Source-confirmed `PLPROP_HEADGIF` updates:

```txt
modern default head:
PLPROP_HEADGIF + GCHAR(25) => "head25.png"

old-client default head:
PLPROP_HEADGIF + GCHAR(25) => "head25.gif"

modern custom head with embedded newline after byte zero:
PLPROP_HEADGIF + GCHAR(114) + "headcustom\nbad" => "headcustom"

modern custom head with embedded newline at byte zero keeps the newline because
the recovered C++ only truncates when `find("\n") > 0`:
PLPROP_HEADGIF + GCHAR(105) + "\nhead" => "\nhead"

modern custom head with declared custom length longer than terminal payload
uses `CString::readChars` clamping to the packet bytes remaining:
PLPROP_HEADGIF + GCHAR(108) + "head" => "head"

old-client extensionless custom head:
PLPROP_HEADGIF + GCHAR(104) + "head" => "head.gif"

no-change sentinel:
PLPROP_HEADGIF + GCHAR(100) => no invented mutation value
```

`Account::setHeadImage` truncates stored head images to 123 bytes/chars. Generic
local forwarding uses C++ `getProp(PLPROP_HEADGIF)` shape:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_HEADGIF + GCHAR(108) + "head.png" + "\n"
bytes: 40 32 39 43 140 104 101 97 100 46 112 110 103 10
```

Source-confirmed `PLPROP_SWORDPOWER` and `PLPROP_SHIELDPOWER` updates:

```txt
modern custom sword:
PLPROP_SWORDPOWER + GCHAR(35) + GCHAR(5) + "slash"
=> raw power 35, image "slash"; runtime power is 35 - 30 before settings clamp

old-client extensionless custom sword:
PLPROP_SWORDPOWER + GCHAR(35) + GCHAR(5) + "slash"
=> image "slash.gif"

modern custom shield:
PLPROP_SHIELDPOWER + GCHAR(12) + GCHAR(6) + "guard1"
=> raw power 12, image "guard1"; runtime power is 12 - 10 before settings clamp

old-client extensionless custom shield:
PLPROP_SHIELDPOWER + GCHAR(12) + GCHAR(6) + "guard1"
=> image "guard1.gif"

old-client truncated extensionless custom images still append `.gif` after
`CString::readChars` clamps to remaining bytes:
PLPROP_SWORDPOWER + GCHAR(35) + GCHAR(5) + "sl" => image "sl.gif"
PLPROP_SHIELDPOWER + GCHAR(12) + GCHAR(6) + "gu" => image "gu.gif"

old 1.41 shield bug:
PLPROP_SHIELDPOWER + GCHAR(11) with no bytes left
=> no invented mutation value
```

Runtime default-image fixture with `clientVersion < CLVER_2_1`, `swordlimit=2`,
and `shieldlimit=2`:

```txt
PLPROP_SWORDPOWER + GCHAR(4) => swordPower 2, swordImage "sword2.gif"
PLPROP_SHIELDPOWER + GCHAR(3) => shieldPower 2, shieldImage "shield2.gif"
```

Generic local forwarding uses C++ `getProp` power offsets and image strings:

```txt
PLO_OTHERPLPROPS + GSHORT(7)
+ PLPROP_SWORDPOWER + GCHAR(32) + GCHAR(10) + "sword2.png"
+ PLPROP_SHIELDPOWER + GCHAR(11) + GCHAR(11) + "shield1.png"
+ "\n"
bytes:
40 32 39
40 64 42 115 119 111 114 100 50 46 112 110 103
41 43 43 115 104 105 101 108 100 49 46 112 110 103
10
```

The `healswords=true` negative-power branch remains blocked because the C++
stores `Character::swordPower` as `uint8_t`; wrap/serialization behavior needs
dedicated fixture proof before the C# port should expose it as completed.

Source-confirmed `PLPROP_CURCHAT` update:

```txt
PLPROP_CURCHAT + GCHAR(226) + 223 * "c" + PLPROP_X + GCHAR(70)
=> chat message length 223, then PLPROP_X is parsed normally
```

Like other `CString::readChars` paths, a terminal truncated payload clamps to
the remaining packet bytes:

```txt
PLPROP_CURCHAT + GCHAR(8) + "hello" => "hello"
```

Generic local forwarding uses C++ `getProp(PLPROP_CURCHAT)` shape:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_CURCHAT + GCHAR(5) + "hello" + "\n"
bytes: 40 32 39 44 37 104 101 108 108 111 10
```

The current C# slice stores and serializes the current chat message only.
`m_lastChat`, `processChat`, word-filter replacement/warning echo, and V8 NPC
chat event dispatch remain blocked until those source-confirmed systems are
ported.

Source-confirmed `PLPROP_ATTACHNPC` update:

```txt
incoming:
PLPROP_ATTACHNPC + GCHAR(99) + GINT(123)
=> object_type byte is read, attached NPC id becomes 123
```

`Player::getProp(PLPROP_ATTACHNPC)` always emits object type `0` followed by the
current attached NPC id:

```txt
PLO_OTHERPLPROPS + GSHORT(7) + PLPROP_ATTACHNPC + GCHAR(0) + GINT(123) + "\n"
bytes: 40 32 39 74 32 32 32 155 10
```

This fixture covers only the source-confirmed property payload boundary. NPC
existence, attachment validation, and exact level recipient routing remain
blocked on the NPC/runtime systems.

Source-confirmed `PLPROP_CARRYNPC` parser boundary:

```txt
PLPROP_CARRYNPC + GUInt(123)
PLPROP_X + GCHAR(70)
=> parser updates: CARRYNPC=123, X=70
```

`gs2lib` proves `readGUInt()` delegates to the same three-byte Graal integer
encoding as `readGInt()`. The recovered C++ runtime then performs duplicate
carry ownership checks, may send self `PLO_PLAYERPROPS`, may send `PLO_NPCDEL2`,
may broadcast `PLO_OTHERPLPROPS`, and finally stores `m_carryNpcId`. C# covers
only the source-confirmed byte-consumption boundary here; mutation and
client-visible side effects remain blocked until NPC/runtime ownership is
ported.

