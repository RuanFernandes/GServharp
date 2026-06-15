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
