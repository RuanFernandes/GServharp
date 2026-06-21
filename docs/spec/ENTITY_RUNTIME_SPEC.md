# Entity Runtime Boundary Spec

## Source Files

Source of truth:

- `external/gs2lib/include/IEnums.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelItem.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelItem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelHorse.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelBaddy.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelBaddy.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Level.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Weapon.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Weapon.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`

Rust/Python were not used as canonical sources.

## Confirmed Packet IDs

From `IEnums.h`:

```txt
PLO_BADDYPROPS    2
PLO_NPCPROPS      3
PLO_HORSEADD      17
PLO_HORSEDEL      18
PLO_ITEMADD       22
PLO_ITEMDEL       23
PLO_NPCDEL        29
PLO_NPCWEAPONADD  33
PLO_NPCWEAPONDEL  34
PLO_DEFAULTWEAPON 43
PLO_NPCBYTECODE   131
PLO_NPCDEL2       150
```

All packet ids are serialized with Graal GCHAR encoding before payload bytes.

## Level Items

`LevelItemType` ids and names are source-confirmed in `LevelItem.cpp` from
`greenrupee = 0` through `spinattack = 24`; unknown/out-of-range ids map to
`INVALID = -1`.

`Level::addItem` appends a `LevelItem` and returns true in the non-scripting
path. `Level::removeItem(x, y)` removes the first item whose float coordinates
match exactly and returns its item type; missing coordinates return
`LevelItemType::INVALID`.

Confirmed rupee counts:

```txt
greenrupee 1
bluerupee  5
redrupee   30
goldrupee  100
other      0
```

Packet behavior:

- player item add/drop forwarding uses `PLO_ITEMADD` plus the already encoded
  coordinate/item bytes from the incoming packet
- item delete forwarding uses `PLO_ITEMDEL` plus the incoming encoded
  coordinate bytes
- level cleanup/timed delete sends `PLO_ITEMDEL` with `item.x * 2` and
  `item.y * 2`

The C# port implements the confirmed inert item runtime boundary as
`LevelItemRuntime`:

- `SpawnLevelItem` mirrors `Player::spawnLevelItem` for decoded
  `PLI_ITEMADD` payloads: invalid ids do nothing; player drops first call the
  source-confirmed `removeItem` resource rules; successful adds append to the
  runtime level and produce `PLO_ITEMADD` with the already encoded x/y/item
  payload.
- `DeleteOrTakeLevelItem` mirrors `Player::msgPLI_ITEMDEL`: it always produces
  the source-confirmed forwarded `PLO_ITEMDEL` bytes, removes the first matching
  item by decoded float coordinates, and only applies the item reward when the
  caller marks the packet as `PLI_ITEMTAKE`.

Production recipient selection and socket emission remain part of live session
dispatch; this boundary only returns the bytes that the C++ handler forwards.

## Horses

`Level::addHorse` appends a `LevelHorse`; lifetime comes from the
`horselifetime` setting and is not implemented in this inert boundary.

`Level::removeHorse(x, y)` removes the first horse whose float coordinates match
exactly.

`LevelHorse::getHorseStr()` caches this payload:

```txt
raw byte(x * 2)
GCHAR(y * 2)
GCHAR((bushes << 2) | (dir & 0x03))
raw image bytes
```

`Level::getHorsePacket()` wraps each horse as:

```txt
PLO_HORSEADD + horse.getHorseStr() + "\n"
```

Timed horse deletion sends:

```txt
PLO_HORSEDEL + GCHAR(x * 2) + GCHAR(y * 2)
```

## Baddies

`LevelBaddy` reset defaults are confirmed:

```txt
images:
  0 baddygray.png
  1 baddyblue.png
  2 baddyred.png
  3 baddyblue.png
  4 baddygray.png
  5 baddyhare.png
  6 baddyoctopus.png
  7 baddygold.png
  8 baddylizardon.png
  9 baddydragon.png
start modes: [0,0,0,0,6,7,0,0,0,0]
powers:      [2,3,4,3,2,1,1,6,12,8]
dir:         (2 << 2) | 2 = 10
ani:         0
verses:      three empty strings
```

Compatibility note: C++ checks `if (pType > baddytypes)` rather than
`>= baddytypes`; this means `type == 10` is risky/out-of-bounds and is not
modeled as safe behavior in C# yet.

`Level::addBaddy` has a source-confirmed limit check:

```cpp
if (m_baddies.size() > 50) return nullptr;
```

This allows a 51st baddy and rejects the next add. Baddy ids start at 1 and
freed ids are reused by `IdGenerator<uint8_t>`.

`Level::removeBaddy(id)` ignores ids `< 1` and `> 50`, so an id 51 baddy cannot
be removed through this method.

`Level::getBaddyPacket` iterates `m_baddies`, wraps each baddy:

```txt
PLO_BADDYPROPS
GCHAR baddy.id
baddy.getProps(clientVersion)
"\n"
```

`LevelBaddy::getProps` serializes property ids `1..10` in order. The C# boundary
implements reset/default props only and does not implement AI, damage, death,
drop, timeout, or respawn behavior. The confirmed timeout/death/drop behavior is
cataloged in `docs/spec/BADDY_COMBAT_SPEC.md`.

## NPC IDs And NPC Packets

`Level` stores NPC ids in `std::set<uint32_t>`, so insertion is unique and
iteration is sorted by id. The C# boundary mirrors this with a sorted set for
the inert level container.

Confirmed packet builders:

```txt
PLO_NPCPROPS + GINT npcId + props + "\n"
PLO_NPCDEL + GINT npcId + "\n"
PLO_NPCDEL2 + levelName + GINT npcId + "\n"
```

Full `NPC::getProps`, NPC bytecode, NPC movement/action, and scripting hooks
remain blocked for the scripting milestone.

## Weapons

`Weapon::getWeaponPacket` confirms:

- default weapons send `PLO_DEFAULTWEAPON + GCHAR(default item id)`
- non-default weapons begin with:
  - `PLO_NPCWEAPONADD`
  - `GCHAR(name length) + name`
  - `GCHAR(NPCPROP_IMAGE) + GCHAR(image length) + image`
- for currently safe non-bytecode GS1 payloads, it then appends:
  - `GCHAR(NPCPROP_SCRIPT)`
  - `GSHORT(formattedClientGS1 length)`
  - formatted client GS1 bytes

Formatting GS1 source, compiling GS2, bytecode packet 197, joined classes, and
weapon script execution remain blocked.

## C# Mapping

Implemented:

- `Preagonal.GServer.Game.RuntimeLevelItem`
- `Preagonal.GServer.Game.LevelItemRuntime`
- `Preagonal.GServer.Game.RuntimeHorse`
- `Preagonal.GServer.Game.RuntimeBaddy`
- inert `RuntimeLevel` item/horse/NPC/baddy containers
- `LevelItemCatalog.GetRupeeCount`
- `Preagonal.GServer.Protocol.EntityPackets`
- `Preagonal.GServer.Game.EntityRuntimePackets.BaddyProps`

Tests:

- `tests/Game.Tests/EntityRuntimeBoundaryTests.cs`
- `tests/Protocol.Tests/EntityRuntimePacketTests.cs`

## Blocked

- script VM execution and events
- NPC property serialization beyond packet wrapper fixtures
- baddy AI/combat/drop/respawn/timers
- production item packet dispatch to live recipients
- weapon grant side effects beyond the source-confirmed state addition
- horse lifetime timers
- GS1 formatting and GS2/bytecode compilation
- production integration with live level-area forwarding beyond existing packet
  sink boundaries
