# Player Props Runtime Catalog

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Player.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`

This catalog covers every `PLPROP_*` branch handled by
`Player::setProps(CString& pPacket, uint8_t options, Player* rc)`. It is a
runtime mutation/forwarding catalog, not permission to implement every branch at
once. Each C# implementation slice must still add focused byte fixtures and keep
blocked side effects guarded.

## Option Flags

`Player.h` defines the option bits passed to `setProps`:

| Flag | Value | Confirmed meaning |
| --- | ---: | --- |
| `PLSETPROPS_SETBYPLAYER` | `0x01` | Player-originated update; some V8 builds reject server-only fields under this flag. |
| `PLSETPROPS_FORWARD` | `0x02` | Forward eligible props to other players. |
| `PLSETPROPS_FORWARDSELF` | `0x04` | Echo eligible props back through `PLO_PLAYERPROPS`. |

`msgPLI_PLAYERPROPS` calls:

```cpp
setProps(pPacket, PLSETPROPS_SETBYPLAYER | PLSETPROPS_FORWARD);
```

## Forwarding Shape

`Player.cpp` defines `__sendLocal[propscount]`. After each recognized property
branch, `setProps` appends `GCHAR propId + getProp(propId)` to `levelBuff` when
`PLSETPROPS_FORWARD` is set and `__sendLocal[propId]` is true. When
`PLSETPROPS_FORWARDSELF` is set, it appends the same property to `selfBuff`.

Several branches append extra packets before this generic tail:

- `PLPROP_NICKNAME` appends to `globalBuff`, and also to `selfBuff` unless
  `PLSETPROPS_FORWARDSELF` is set.
- `PLPROP_HEADGIF` appends to `globalBuff` when the head actually changes.
- legacy movement `PLPROP_X/Y/Z` appends precise mirror props
  `PLPROP_X2/Y2/Z2` to `levelBuff2`.
- precise movement `PLPROP_X2/Y2/Z2` appends legacy mirror props
  `PLPROP_X/Y/Z` to `levelBuff2`.
- `PLPROP_MAXPOWER`, revive from `PLPROP_STATUS`, `PLPROP_ATTACHNPC`,
  `PLPROP_CARRYNPC`, `PLPROP_UDPPORT`, and `PLPROP_PSTATUSMSG` have additional
  direct sends/side effects described below.

At the end, only logged-in and loaded players flush buffers:

```txt
globalBuff -> PLO_OTHERPLPROPS + GSHORT playerId + globalBuff to all except self
levelBuff/levelBuff2 -> PLO_OTHERPLPROPS + GSHORT playerId to level area except self
selfBuff -> PLO_PLAYERPROPS + selfBuff to self
```

For sender client versions `>= CLVER_2_3`, C++ sends `levelBuff2` before
`levelBuff`; older clients see `levelBuff` before `levelBuff2`.

## Runtime Property Branches

| ID | C++ symbol | Read encoding | Direct mutation / behavior | Extra forwarding and side effects | C# status |
| ---: | --- | --- | --- | --- | --- |
| 0 | `PLPROP_NICKNAME` | `GCHAR len` + bytes | Applies word filter, defaults empty warned nickname to `unknown`, then `setNick`. | Adds global `PLO_OTHERPLPROPS`; echoes to self unless `FORWARDSELF` is set. | Parser consumes the confirmed string bytes. Runtime word-filter, `setNick`, global/self forwarding, and nickname persistence side effects remain blocked. |
| 1 | `PLPROP_MAXPOWER` | `GUChar` | Sets max power and current power to max in non-V8 path. | Adds `PLPROP_CURPOWER` to level/self buffers; V8 also adds max power. | Implemented as runtime mutation using explicit heart-limit input. Non-V8 generic level forwarding emits the confirmed `PLPROP_CURPOWER` payload. Self-buffer and V8 max-power forwarding remain blocked. |
| 2 | `PLPROP_CURPOWER` | `GUChar / 2` | Refuses healing when AP `< 40`; otherwise `setPower`. | Generic local/self forwarding only. | Implemented as runtime mutation with AP heal gate. Live forwarding emits the post-mutation current-power byte when runtime state is available; stateless forwarding remains blocked. |
| 3 | `PLPROP_RUPEESCOUNT` | `GUInt`, capped at `9,999,999` | Sets gralats. RC path checks `normaladminscanchangegralats` or `PLPERM_SETRIGHTS`. | Generic forwarding only if `__sendLocal` permits; this prop is not local-forwarded. | Implemented for player-origin runtime mutation. RC permission/config mutation remains blocked. |
| 4 | `PLPROP_ARROWSCOUNT` | `GUChar` | Stores arrows clipped `0..99`. | No local forwarding in `__sendLocal`. | Implemented as scalar mutation with C++ clamp. |
| 5 | `PLPROP_BOMBSCOUNT` | `GUChar` | Stores bombs clipped `0..99`. | No local forwarding in `__sendLocal`. | Implemented as scalar mutation with C++ clamp. |
| 6 | `PLPROP_GLOVEPOWER` | `GUChar` | Stores glove power capped at `3` in non-V8 path. | No local forwarding in `__sendLocal`. | Implemented as scalar mutation with C++ clamp. |
| 7 | `PLPROP_BOMBPOWER` | `GUChar` | Stores bomb power clipped `0..3`. | No local forwarding in `__sendLocal`. | Implemented as scalar mutation with C++ clamp. |
| 8 | `PLPROP_SWORDPOWER` | `GUChar`, optional `GCHAR len` + image | `<=4` selects default `swordN` capped by `swordlimit`; `>4` subtracts `30` and reads custom image. Old clients append `.gif` when extensionless. `Account::setSwordImage` truncates to 223. | Generic local/self forwarding via `getProp`: `GCHAR(swordPower + 30)`, `GCHAR(swordImage.length)`, image bytes. | Implemented for default `healswords=false` settings, custom-image parsing, truncation, and generic local forwarding. The `healswords=true` negative-power/wrap behavior remains blocked until fixture-confirmed. Loaded/global recipient routing remains blocked. |
| 9 | `PLPROP_SHIELDPOWER` | `GUChar`, optional `GCHAR len` + image | `<=3` selects default `shieldN` capped by `shieldlimit`; `>3` subtracts `10` and reads custom image. One-byte 1.41 bug path continues without mutation when no bytes remain. `Account::setShieldImage` truncates to 223. | Generic local/self forwarding via `getProp`: `GCHAR(shieldPower + 10)`, `GCHAR(shieldImage.length)`, image bytes. | Implemented for default/custom parsing, settings limit, 1.41 no-change bug, truncation, and generic local forwarding. Loaded/global recipient routing remains blocked. |
| 10 | `PLPROP_GANI` | old client: `GUChar`/image bytes; modern: `GCHAR len` + bytes | Old clients mutate bow power/image. Modern clients call `setGani`. | `"spin"` sends four `PLO_HITOBJECTS` packets around the player. Generic forwarding applies. | Implemented for modern gani string plus old-client bow power/image parsing, mutation, and generic forwarding. Modern `"spin"` side-effect packets remain blocked. |
| 11 | `PLPROP_HEADGIF` | `GUChar len`, optional image bytes | `<100` default head, `>100` custom `len - 100`, trims embedded newline only when the newline index is greater than zero, old clients append `.gif`; `100` means no change. `Account::setHeadImage` truncates stored images to 123. | Adds global prop when changed; generic local/self forwarding also applies. `getProp` serializes `GCHAR(headImage.length + 100) + headImage`. | Implemented for parser/runtime mutation and generic local forwarding. Loaded/global forwarding recipient routing remains blocked. |
| 12 | `PLPROP_CURCHAT` | `GCHAR len`, stores up to `223` bytes | Sets chat, updates last-chat time, runs `processChat`, applies word filter if unprocessed. If `len > 223`, C++ reads only 223 bytes and leaves remaining bytes for following props. | May echo filtered chat to self; V8 sends chat to level NPCs. Generic local/self forwarding uses `GCHAR(chatMessage.length) + chatMessage`. | Implemented for parser/runtime current-message storage and generic local forwarding. `m_lastChat`, `processChat`, word filter echo, and V8 NPC chat events remain blocked. |
| 13 | `PLPROP_COLORS` | five `GUChar` values | Stores five character colors. | Generic forwarding. | Implemented as runtime mutation and generic local forwarding for exactly five `GUChar` color slots. |
| 14 | `PLPROP_ID` | `GUShort` | Reads and ignores. | No local forwarding. | Implemented as confirmed no-op/read-only parse. |
| 15 | `PLPROP_X` | `GUChar` | Stores `m_x = value * 8`, clears paused bit, movement timestamp/update, enables touch test. | Adds `PLPROP_X2` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 16 | `PLPROP_Y` | `GUChar` | Stores `m_y = value * 8`, clears paused bit, movement timestamp/update, enables touch test. | Adds `PLPROP_Y2` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 17 | `PLPROP_SPRITE` | `GUChar` | Stores sprite; non-V8 enables touch test. | Generic forwarding. | Implemented for confirmed movement subset. |
| 18 | `PLPROP_STATUS` | `GUChar` | Stores status; revive restores hearts by AP; death increments deaths/drops outside sparring; may rotate level leader. | Revive appends `PLPROP_CURPOWER`; may send `PLO_ISLEADER`. Generic forwarding. | Parser consumes the confirmed `GUChar` status byte. Runtime mutation, death/revive/drop/leader side effects, and forwarding remain blocked. |
| 19 | `PLPROP_CARRYSPRITE` | `GUChar` | Stores carry sprite. | Generic forwarding. | Implemented as state mutation and generic local forwarding. |
| 20 | `PLPROP_CURLEVEL` | `GCHAR len` + bytes | Non-V8 stores `m_levelName`; V8 reads/discards. | Generic forwarding via `getProp`: GMAP clients receive map name, singleplayer levels append `.singleplayer`, otherwise level name. | Implemented for parser/runtime mutation plus live forwarding for normal and singleplayer level names. GMAP map-name forwarding remains blocked until the live GMAP state path is fixture-confirmed. |
| 21 | `PLPROP_HORSEGIF` | `GCHAR len` + bytes, max read `219`, clamped to packet bytes remaining | Stores horse image; old clients append `.gif` when extensionless. If `len > 219`, C++ reads only 219 bytes and leaves any remaining bytes to be parsed as following properties. If the packet ends first, `CString::readChars` returns the remaining bytes. | Generic forwarding via `getProp`: `GCHAR(horseImage.length)`, image bytes. | Implemented for modern/old parse, truncated terminal payloads, 219-byte read cap, runtime mutation, and generic local forwarding. Loaded/global recipient routing remains blocked. |
| 22 | `PLPROP_HORSEBUSHES` | `GUChar` | Stores horse bomb count. | No local forwarding. | Implemented as state mutation. |
| 23 | `PLPROP_EFFECTCOLORS` | `GCHAR len`, optional `GInt4` | Reads effect color bytes when length is positive; no visible field mutation in recovered branch. | No local forwarding. | Implemented as consume-only parser branch with no invented runtime state. |
| 24 | `PLPROP_CARRYNPC` | `GUInt` | Stores carried NPC id, with duplicate-carry ownership checks unless `duplicatecanbecarried` is true. | May send self `PLO_PLAYERPROPS`, self/global `PLO_NPCDEL2`, and level `PLO_OTHERPLPROPS`. | Parser consumes the confirmed `GUInt`/`GInt` encoding. Runtime mutation, duplicate ownership checks, NPC deletion packets, and forwarding remain blocked on NPC/runtime ownership. |
| 25 | `PLPROP_APCOUNTER` | `GUShort` | Stores AP counter. | Generic forwarding. `getProp` serializes `m_apCounter + 1`. | Implemented as scalar mutation and local forwarding using the C++ plus-one serialization. |
| 26 | `PLPROP_MAGICPOINTS` | `GUChar` | Stores magic points capped at `100` in non-V8 path. | No local forwarding. | Implemented as scalar mutation with C++ clamp. |
| 27 | `PLPROP_KILLSCOUNT` | `GInt` | Reads and ignores. | No local forwarding. | Implemented as confirmed no-op/read-only parse. |
| 28 | `PLPROP_DEATHSCOUNT` | `GInt` | Reads and ignores. | No local forwarding. | Implemented as confirmed no-op/read-only parse. |
| 29 | `PLPROP_ONLINESECS` | `GInt` | Reads and ignores. | No local forwarding. | Implemented as confirmed no-op/read-only parse. |
| 30 | `PLPROP_IPADDR` | `GInt5` | Reads and ignores. | Generic forwarding from current `getProp(PLPROP_IPADDR)`. | Parser/applier implemented as consume-only no-op. Live forwarding uses runtime account-IP state instead of the discarded incoming bytes. Stateless forwarding remains blocked. |
| 31 | `PLPROP_UDPPORT` | `GInt` | Stores UDP port. | If loaded and id valid, sends `PLO_OTHERPLPROPS + id + PLPROP_UDPPORT + raw int` to any client except self. Generic tail also applies. | Parser/applier implemented as state mutation, and the generic local forwarding tail emits the confirmed `GInt` payload. The loaded/id-gated direct broadcast remains blocked on production session routing/recipient selection. |
| 32 | `PLPROP_ALIGNMENT` | `GUChar` | Stores AP capped at `100` in non-V8 path. | Generic forwarding. | Implemented as runtime mutation with C++ clamp and generic local forwarding. |
| 33 | `PLPROP_ADDITFLAGS` | `GUChar` | Stores additional flags. | No local forwarding. | Implemented as scalar mutation. |
| 34 | `PLPROP_ACCOUNTNAME` | `GCHAR len` + bytes | Reads and ignores. | Generic forwarding from current `getProp(PLPROP_ACCOUNTNAME)`. | Parser/applier implemented as consume-only no-op. Live forwarding uses runtime account state instead of the discarded incoming bytes. Stateless forwarding remains blocked. |
| 35 | `PLPROP_BODYIMG` | `GCHAR len` + bytes | Calls `setBodyImage`, which stores `newImage.subString(0, 223)`. | Generic forwarding. | Implemented as runtime mutation and generic local forwarding using the current body image string. |
| 36 | `PLPROP_RATING` | `GInt` | Reads into `len`; ELO mutation is commented out. | Generic forwarding from current `getProp(PLPROP_RATING)`: `((rating & 0xFFF) << 9) \| (deviation & 0x1FF)`. | Parser/applier implemented as consume-only no-op. Live forwarding uses runtime ELO/deviation state instead of the discarded incoming bytes. Sparring mutation remains blocked. |
| 37-41 | `PLPROP_GATTRIB1..5` | `GCHAR len` + bytes | Stores `ganiAttributes[0..4]`. | Generic forwarding. | Implemented as runtime mutation and generic local forwarding using the original string payload. |
| 42 | `PLPROP_ATTACHNPC` | `GUChar object_type` + `GUInt npcID` | Stores attached NPC id. Object type is read but ignored for state; `getProp` always serializes object type `0` because only NPC attachments are supported. | Explicitly appends attach prop to `levelBuff`; generic local tail is false for id 42. | Implemented for parser/runtime attached-id storage and source-confirmed outgoing payload bytes. NPC attach semantics, validation, and recipient routing remain blocked. |
| 43 | `PLPROP_GMAPLEVELX` | `GUChar` | If current level has GMAP map, leaves level and sets level at `(newX, currentMapY)`. | Generic forwarding after the level switch. | Parser consumes the confirmed byte. Runtime level switching and forwarding remain blocked on source-compatible `Map::getLevelAt`/`setLevel` wiring. |
| 44 | `PLPROP_GMAPLEVELY` | `GUChar` | If current level has GMAP map, leaves level and sets level at `(currentMapX, newY)`. | Generic forwarding after the level switch. | Parser consumes the confirmed byte. Runtime level switching and forwarding remain blocked on source-compatible `Map::getLevelAt`/`setLevel` wiring. |
| 45 | `PLPROP_Z` | `GUChar` | Stores `m_z = (value - 50) * 8`, clears paused bit, movement timestamp/update, enables touch test. | Adds `PLPROP_Z2` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 46-49 | `PLPROP_GATTRIB6..9` | `GCHAR len` + bytes | Stores `ganiAttributes[5..8]`. | Generic forwarding. | Implemented as runtime mutation and generic local forwarding using the original string payload. |
| 50 | `PLPROP_JOINLEAVELVL` | none in active branch | Commented-out unknown branch; active code has no read/mutation. | No local forwarding. | Implemented as confirmed no-byte/no-op branch. |
| 51 | `PLPROP_PCONNECTED` | none | No-op. | No local forwarding. | Implemented as confirmed no-byte/no-op branch. |
| 52 | `PLPROP_PLANGUAGE` | `GCHAR len` + bytes | Stores language string. | No local forwarding in `__sendLocal`. | Implemented as local runtime mutation. |
| 53 | `PLPROP_PSTATUSMSG` | `GUChar` | Stores player-list status message. | If loaded and id valid, sends `PLO_OTHERPLPROPS + id + PLPROP_PSTATUSMSG + GCHAR status` to all except self. | Parser/applier implemented as state mutation. Direct broadcast remains blocked on production player-list recipient routing. |
| 54-74 | `PLPROP_GATTRIB10..30` | `GCHAR len` + bytes | Stores `ganiAttributes[9..29]`. | Generic forwarding. | Implemented as runtime mutation and generic local forwarding using the original string payload. |
| 75 | `PLPROP_OSTYPE` | `GCHAR len` + bytes | Stores OS string. | No local forwarding. | Implemented as local runtime mutation. |
| 76 | `PLPROP_TEXTCODEPAGE` | `GInt` | Stores environment code page. | No local forwarding. | Implemented as local runtime mutation. |
| 77 | `PLPROP_UNKNOWN77` | not handled | Falls through default invalid branch. | Stops parsing and returns before invalid-disconnect tail is reached. | Must remain unsupported. |
| 78 | `PLPROP_X2` | `GUShort` | Stores signed pixel X: low bit is sign, remaining bits are absolute value shifted right one; clears paused/movement/touch. | Adds `PLPROP_X` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 79 | `PLPROP_Y2` | `GUShort` | Same signed pixel encoding for Y. | Adds `PLPROP_Y` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 80 | `PLPROP_Z2` | `GUShort` | Same signed pixel encoding for Z. | Adds `PLPROP_Z` mirror to `levelBuff2`; generic forwarding. | Implemented for confirmed movement subset. |
| 81 | `PLPROP_UNKNOWN81` | `GUChar` | Reads and ignores. | No local forwarding. | Implemented as confirmed no-op/read-only parse. |
| 82 | `PLPROP_COMMUNITYNAME` | `GCHAR len` + bytes | Reads and ignores. | Generic forwarding from current `getProp(PLPROP_COMMUNITYNAME)`. | Parser/applier implemented as consume-only no-op. Live forwarding uses runtime community-name state instead of the discarded incoming bytes. Stateless forwarding remains blocked. |

## Invalid Property Behavior

The default branch logs the unidentified property id and a packet hex dump, sets
`sentInvalid = true`, then immediately `return`s from `setProps`. Because the
function returns before the later `if (sentInvalid)` block, the invalid-packet
disconnect counter at the bottom is not reached from this default path in the
recovered source.

This differs from `msgPLI_NULL`, which has its own invalid-packet counter and
disconnect behavior. C# must keep these paths separate.

## Implementation Guidance

Recommended safe slices:

1. no-op/read-only props with no local forwarding: `ID`, `KILLSCOUNT`,
   `DEATHSCOUNT`, `ONLINESECS`, `UNKNOWN81`, `JOINLEAVELVL`, `PCONNECTED`.
   This first slice is implemented in `IncomingPlayerPropsParser`,
   `RuntimePlayerPropsApplier`, and forwarding tests; it consumes the exact C++
   bytes but does not mutate or forward invented state.
2. isolated scalar inventory/stat props without gameplay side effects:
   arrows, bombs, glove, bomb power, AP counter, magic points, additional flags.
   This slice is implemented with parser/applier tests. `PLPROP_APCOUNTER`
   forwarding is also implemented with the C++ `getProp` plus-one behavior.
   A follow-up scalar/state slice also implements `MAXPOWER`, `CURPOWER`,
   player-origin `RUPEESCOUNT`, `ALIGNMENT`, `CARRYSPRITE`, and
   `HORSEBUSHES` runtime mutation. It deliberately keeps `STATUS` blocked
   because the C++ branch includes death/revive/drop/leader side effects.
3. string identity/visual props with forwarding fixtures:
   body/head/sword/shield/horse/language/community/gani attributes.
4. movement and map props only after `testSign`, `testTouch`, GMAP level change,
   and forwarding order fixtures are expanded.
5. status/carry/chat/NPC props only after combat, filter, chat, NPC, and
   scripting prerequisites exist.

Do not implement side effects that depend on word filtering, NPC ownership,
script events, combat/death, RC permissions, or map ownership until those
systems have their own C++-confirmed fixtures.

## Production Dispatcher Guard

`ProductionPostLoginPacketDispatcher` applies parsed player-property updates in
wire order, one confirmed update at a time. If a parsed update reaches a C++
branch whose byte encoding is known but whose runtime side effects are still
blocked in C#, the dispatcher returns a blocked result instead of allowing
`NotSupportedException` to escape the production session path. Confirmed earlier
updates stay applied, matching the C++ function's sequential mutation shape as
closely as the current guarded boundary allows.

This is not a substitute implementation for the blocked C++ branch. For
example, `PLPROP_NICKNAME` remains blocked until word-filter behavior,
`setNick`, global `PLO_OTHERPLPROPS`, self echo, and persistence side effects
are ported together from `PlayerProps.cpp`. `PLPROP_CARRYNPC` remains blocked
until NPC ownership, duplicate carry checks, `PLO_PLAYERPROPS`, `PLO_NPCDEL2`,
`PLO_OTHERPLPROPS`, and `m_carryNpcId` mutation are ported from the same file.
Likewise, `PLPROP_STATUS` remains blocked after byte parsing until the C++
death/revive/drop/leader side effects and related packet order are ported.

## Live Forwarding Guard

`LiveWorldSessionForwarder.TryApplyAndForwardConfirmedPlayerProps` mirrors the
same guarded boundary for live-world packet forwarding. It applies confirmed
updates sequentially, returns `Blocked` with the C++ `PLPROP_*` name when an
unported side-effect branch is reached, and deliberately emits no
`PLO_OTHERPLPROPS` forwarding bytes for that packet. Earlier confirmed mutations
remain applied, matching the sequential shape of `Player::setProps` without
inventing the blocked branch's forwarding or side effects.

The throwing `ApplyAndForwardConfirmedPlayerProps` entry point remains strict
for callers that only pass fully ported properties. Production or diagnostic
callers that may receive parsed-but-unported properties should use the `Try*`
entry point and surface the blocked result.
