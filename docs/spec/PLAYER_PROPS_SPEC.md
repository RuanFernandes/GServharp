# Player Property Serialization Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `external/gs2lib/include/CString.h`
- `external/gs2lib/include/IEnums.h`

## sendProps

`Player::sendProps` constructs a property payload, then sends:

```cpp
sendPacket(CString() >> (char)PLO_PLAYERPROPS << propPacket);
```

Confirmed behavior:

1. If the player is a client older than `CLVER_2_1`, `pCount` is forced to `37`.
2. Iterate property IDs from `0` to `pCount - 1`.
3. If `pProps[i]` is true, append `GCHAR i` and then `getProp(i)`.
4. Wrap the payload with `PLO_PLAYERPROPS`.
5. `Player::sendPacket` appends `\n` if the packet does not already end in newline.

## CString Operators Used By getProp

Recovered `CString.h` confirms:

- `operator>>(char)` -> `writeGChar`
- `operator>>(short)` -> `writeGShort`
- `operator>>(int)` -> `writeGInt`
- `operator>>(long long)` -> `writeGInt5`
- `operator<<(char)` -> raw byte
- `operator<<(short)` -> raw big-endian short
- `operator<<(int)` -> raw big-endian int
- `operator<<(CString)` -> raw string bytes

This is important: many player properties use Graal-packed integers, not raw big-endian integers.

## __sendLogin

`__sendLogin` is an 83-entry boolean table in `Player.cpp`. True entries are sent in ascending property ID order.

Confirmed true property IDs:

```txt
1,2,3,4,5,6,8,9,10,11,13,17,18,21,22,23,25,26,32,34,35,36,
37,38,39,40,41,46,47,48,49,54,55,56,57,58,59,60,61,62,63,64,
65,66,67,68,69,70,71,72,73,74,82
```

The C# `SendLoginPropertySet.All` locks this exact order. For clients older than
`CLVER_2_1`, `Player::sendProps` forces `pCount = 37`, so the emitted login set
is the same true table filtered to property IDs `< 37`:

```txt
1,2,3,4,5,6,8,9,10,11,13,17,18,21,22,23,25,26,32,34,35,36
```

The `__sendLogin` table does not dynamically skip individual true entries based
on account/player state. Values may be empty/default depending on loaded account
data, but the property IDs above are still emitted by `sendProps` for matching
client-version windows.

## __getLogin

`__getLogin` is an 83-entry boolean table in `Player.cpp`. It is used by
`Player::getProps` during level-entry player visibility sync.

Confirmed true property IDs:

```txt
0,8,9,10,11,12,13,15,16,17,18,19,20,21,24,30,31,32,34,35,36,
37,38,39,40,41,43,44,45,46,47,48,49,50,53,54,55,56,57,58,59,
60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,78,79,80,82
```

`Player::getProps` wraps the payload as:

```txt
GCHAR PLO_OTHERPLPROPS
GSHORT playerId
```

Then, when `PLPROP_JOINLEAVELVL` is enabled for clients, it injects:

```txt
GCHAR PLPROP_JOINLEAVELVL
GCHAR 1
```

before iterating the table. The normal ascending loop skips
`PLPROP_JOINLEAVELVL`.

The C# `GetLoginPropertySet.All` locks this exact order, and
`PlayerPropertySerializer.BuildOtherPlayerPropsPacket` emits the confirmed
`PLO_OTHERPLPROPS` wrapper.

## Confirmed Serializer Subset

The C# `PlayerPropertySerializer` implements source-confirmed encodings for the
`__sendLogin` and `__getLogin` sets and other previously confirmed supporting
properties.

Examples:

- `PLPROP_MAXPOWER`: `GCHAR m_maxHitpoints`
- `PLPROP_CURPOWER`: `GCHAR (hitpoints * 2)`
- `PLPROP_RUPEESCOUNT`: `GINT gralats`
- `PLPROP_SWORDPOWER`: `GCHAR (swordPower + 30)`, `GCHAR swordImage.length`, raw sword image bytes
- `PLPROP_CURLEVEL`: `GCHAR levelName.length`, raw level name bytes for the non-GMAP/non-singleplayer fixture path
- `PLPROP_IPADDR`: `GINT5 accountIp`
- `PLPROP_UDPPORT`: `GINT udpPort`
- `PLPROP_ACCOUNTNAME`: `GCHAR accountName.length`, raw account name bytes
- `PLPROP_GMAPLEVELX/Y`: `GCHAR level gmap coordinate`
- `PLPROP_Z`: clamped `GCHAR ((z / 8) + 50)` with C++ range limits
- `PLPROP_X2/Y2/Z2`: signed Graal short coordinate encoding from C++ `writeGShort`
- `PLPROP_PCONNECTED` and `PLPROP_UNKNOWN81`: property id only, no value bytes

The serializer takes explicit property IDs and sorts them ascending to match `sendProps`.

## Side Effects

`getProp` itself is serialization only for the confirmed subset. Side effects are outside this milestone.

`setProps` can mutate gameplay/account state and forward packets; it was traced only to understand serialization conventions and is not implemented.

## Current Pass Status

The C# port now supports the `__getLogin` table and `PLO_OTHERPLPROPS` packet
builder needed by the modern `sendLevel` tail. Live player-list ownership and
actual multi-session forwarding are still modeled through explicit snapshots.
