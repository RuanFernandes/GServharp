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

### GINT Property Example

`PLPROP_RUPEESCOUNT` with value `1234`:

```txt
[35, 32, 41, 114]
```

`35` is `GCHAR PLPROP_RUPEESCOUNT`; `[32, 41, 114]` is `GINT 1234`.

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

## Account Loading Fixtures

These fixtures assert state transitions and persistence side-effect requests,
not network bytes.

Existing account lookup:

```txt
input account: "pc-ruan"
account fs findi: "pc-ruan.txt" => "C:\gserver\accounts\PC-Ruan.TXT"
file header: GRACC001
result: LoadedFromDefault=false, SourcePath="C:\gserver\accounts\PC-Ruan.TXT"
result: ShouldSaveCreatedAccount=false
```

Missing account fallback:

```txt
input account: "NewAccount"
account fs findi: "NewAccount.txt" => empty
fallback path: "C:\gserver\accounts\defaultaccount.txt"
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
