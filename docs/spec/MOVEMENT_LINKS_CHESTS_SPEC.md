# Movement Links Chests Spec

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelLink.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelLink.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelSign.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelSign.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelChest.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelItem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelItem.h`

## Confirmed Movement Property Side Effects

`Player::setProps` confirms the following movement subset:

- `PLPROP_X`: reads one GChar, stores `m_x = value * 8`, clears paused status,
  updates `m_lastMovement`, sets `m_grMovementUpdated`, requests touch testing,
  and prepares `PLPROP_X2` for cross-version forwarding.
- `PLPROP_Y`: same pattern, stores `m_y = value * 8`, prepares `PLPROP_Y2`.
- `PLPROP_Z`: reads one GChar, stores `m_z = (value - 50) * 8`, prepares
  `PLPROP_Z2`.
- `PLPROP_SPRITE`: reads one GChar into `m_character.sprite`. In the legacy
  non-V8 branch it also requests touch testing; in the V8NPCSERVER build,
  touch testing is still performed after parsing if another movement prop set
  `doTouchTest`.
- `PLPROP_X2`, `PLPROP_Y2`, `PLPROP_Z2`: read GShort. Bit `0x0001` is the
  negative flag; bits `0xFFFE` are shifted right by one to get the raw pixel
  coordinate. The decoded coordinate is not multiplied by 8.
- `PLPROP_CURRENTLEVEL` and `PLPROP_GANI` string parsing/mutation are confirmed,
  but they are not movement validation.

After parsing, when the player is logged in and loaded, C++ forwards
`PLO_OTHERPLPROPS` to the level area. For `CLVER_2_3+`, legacy X/Y/Z props are
placed before precise X2/Y2/Z2 props; older clients receive the reverse order.

The C# port currently implements this confirmed subset in
`RuntimePlayerPropsApplier` and `IncomingPlayerPropsForwarding`.

## Confirmed Sign Behavior

`Level::getSignsPacket` emits each sign as:

```txt
PLO_LEVELSIGN + LevelSign::getSignStr(player) + "\n"
```

`LevelSign::getSignStr` writes:

```txt
GCHAR x
GCHAR y
encoded sign text
```

If a player is supplied, the C++ server re-encodes
`player->translate(m_unformattedText)`. The current C# static level packet
builder only implements the no-translation branch because player language
translation is not implemented yet.

Unknown non-carriage-return characters are encoded as literal sign-code bytes
for `#K(decimal-code)`. For ASCII `@`, this produces:

```txt
GCHAR 86, GCHAR 10, GCHAR 69, GCHAR 58, GCHAR 56, GCHAR 70
```

followed by the line's encoded newline.

`Player::testSign` is gated by server-side mode and sprite direction:

- `serverside` setting must be true.
- `(m_character.sprite % 4) == 0`.
- A sign is triggered when `getY() == sign.y` and `getX()` is in
  `[sign.x - 1.5, sign.x + 0.5]`.
- The response is `PLO_SAY2` with unformatted sign text and newlines replaced
  by `#b`.

The range check uses `inrange`, which is inclusive at both ends. C++ iterates
all signs and sends one `PLO_SAY2` packet for every matching sign; it does not
stop after the first match.

The C# port implements the source-confirmed runtime sign-touch packet builder
as `LevelInteraction.BuildTouchedSignPackets`. Production wiring from the live
player movement path is still separate because server setting plumbing and the
broader runtime dispatch loop are not complete.

## Confirmed Link Behavior

`LevelLink::getLinkStr` emits:

```txt
newLevel x y width height newX newY
```

`Level::getLinksPacket` wraps each string as:

```txt
PLO_LEVELLINK + ASCII(link string) + "\n"
```

`Level::getLink(int pX, int pY)` scans `m_links` in stored order and returns
the first link whose tile-space bounds match:

```txt
pX >= link.x && pX <= link.x + link.width
pY >= link.y && pY <= link.y + link.height
```

The bounds are inclusive at both ends. The C# port implements this pure
hit-test as `LevelInteraction.FindTouchedLink`.

No direct C++ automatic player movement-to-link warp call path was confirmed in
this milestone. `NPC::testTouch` uses `Level::getLink` for NPC link traversal,
but player movement currently calls `Level::testTouch` for NPC touch events, not
`Level::getLink`.

The source-confirmed player-facing warp boundary is client-triggered:

- `Player.cpp` registers `TPLFunc[PLI_LEVELWARP] = &Player::msgPLI_LEVELWARP`.
- `Player.cpp` registers `TPLFunc[PLI_LEVELWARPMOD] = &Player::msgPLI_LEVELWARP`.
- `IEnums.h` confirms `PLI_LEVELWARP = 0` and `PLI_LEVELWARPMOD = 30`.
- `Player::parsePacket` consumes the packet id with `curPacket.readGUChar()`
  before calling the handler, while the handler still checks `pPacket[0] - 32`.
- `Player::msgPLI_LEVELWARP` reads optional `GUInt5 modTime` only for
  `PLI_LEVELWARPMOD`, then reads `x = readGChar() / 2.0f`,
  `y = readGChar() / 2.0f`, reads the rest of the packet as the level name, and
  calls `warp(newLevel, x, y, modTime)`.
- `Player::warp` sets X/Y but does not set Z before entering `setLevel`.

The C# port implements this confirmed inbound packet parser in
`LevelWarpPacketParser` and exposes `WarpWorldEntryBoundary.BeginClientLevelWarpPacket`
to convert the parsed packet into the existing `warp` boundary. The caller must
provide the current Z because the C++ inbound packet does not contain one and
`Player::warp` does not mutate it.

Automatic server-side player movement-to-link warp remains blocked until a
direct C++ player call path from movement/touch processing to `Level::getLink`
and `warp` is proven.

## Confirmed Chest Behavior

`Level::getChestPacket(Player*)` emits one packet per chest:

```txt
PLO_LEVELCHEST
GCHAR(hasChest ? 1 : 0)
GCHAR(chest.x)
GCHAR(chest.y)
if !hasChest:
  GCHAR(chest.itemIndex)
  GCHAR(chest.signIndex)
"\n"
```

The chest ownership key comes from `Level::getChestStr`:

```txt
"%i:%i:%s", chest.x, chest.y, levelName
```

`Player::msgPLI_OPENCHEST` confirms the open boundary:

1. Read client-sent GChar `cX`, then GChar `cY`.
2. Get the current level.
3. Look up a chest with exact `x == cX && y == cY`.
4. Build the chest key with `Level::getChestStr`.
5. If the player does not already have the key:
   - Get `chest->getItemIndex()`.
   - Apply `LevelItem::getItemPlayerProp(chestItem, this)` through
     `setProps(..., PLSETPROPS_FORWARD | PLSETPROPS_FORWARDSELF)`.
   - Send `PLO_LEVELCHEST, 1, cX, cY` through `Player::sendPacket`, which
     appends `"\n"` if missing.
   - Append the chest key to `m_chestList`.

The C# port implements the source-confirmed boundary result:

- exact chest lookup by x/y
- unopened chest key construction
- `PLO_LEVELCHEST, 1, x, y, "\n"` packet bytes
- recording the opened chest key
- preserving the chest item id in the result for a later item-prop milestone

`LevelItem::getItemPlayerProp` reward mutation is traced, but production C#
does not yet apply the reward because that touches broader player inventory,
weapon, status, and stat mutation behavior.

## Blocked Areas

- Automatic player movement-to-link warp is blocked. The milestone confirmed
  inclusive `Level::getLink` lookup and the client-triggered
  `PLI_LEVELWARP`/`PLI_LEVELWARPMOD` path, but did not find a direct player
  movement-to-link warp path.
- `Player::testSign` packet construction is implemented for the confirmed
  `PLO_SAY2` response. Live runtime invocation is blocked on server-side setting
  plumbing and the broader movement dispatch loop. Player translation remains
  blocked for static `Level::getSignsPacket(player)` serialization.
- Chest item reward mutation is blocked on a dedicated `LevelItem`/player stat
  mutation milestone.
- Chest persistence save timing remains blocked beyond recording the in-memory
  chest key boundary.
- NPC touch events from `Player::testTouch` are blocked because they enter
  script runtime through `npc.playertouchsme`.
