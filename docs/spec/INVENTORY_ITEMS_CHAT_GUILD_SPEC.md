# Inventory, Items, Chat, And Guild Spec

## Scope Rule

This document covers only behavior found in the recovered C++ server source.
Anything not present in `ai_resources/GServer-CPP-ORIGINAL/` is outside the
C# port scope unless future recovered original C++ source or exact dependency
source proves that the original C++ server exposed it to the client.

Dedicated C++ core shop, trade, party, quest, and mission runtimes were not
found in this pass. They are not backlog items for the 1:1 port.

Scope re-check on 2026-06-16:

- `shop`, `party`, `quest`, and `mission` were not found as dedicated runtime
  handlers/classes/modules in `ai_resources/GServer-CPP-ORIGINAL/`.
- `trade` only matched license text and moderation text such as ban reasons,
  not a server-side trade runtime.
- `chat`, `private message`, `profile`, and `guild` have concrete C++ handlers
  and therefore remain in scope only for those source-confirmed paths.

## Status

Confirmed and partially ported:

- level item IDs and names from C++;
- item pickup property payloads from `LevelItem::getItemPlayerProp`;
- player-drop removal rules from `Player::removeItem`;
- default weapon side effects for weapon pickup items as source-confirmed state
  changes.

Confirmed but not fully ported:

- chat/PM/profile handlers in `Player.cpp`;
- guild verification and script-triggered guild helper commands.

## Source Map

### Inventory And Items

- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelItem.h`
  - `LevelItemType` IDs.
  - `getItemPlayerProp` declarations.
  - rupee count helper.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelItem.cpp`
  - `__itemList`.
  - `LevelItem::getItemId(signed char)`.
  - `LevelItem::getItemId(std::string)`.
  - `LevelItem::getItemName`.
  - `LevelItem::getItemPlayerProp`.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - packet routing for `PLI_ITEMADD`, `PLI_ITEMDEL`, and `PLI_ITEMTAKE`.
  - `Player::removeItem`.
  - `Player::msgPLI_ITEMADD`.
  - `Player::msgPLI_ITEMDEL`.
  - chest reward path calling `LevelItem::getItemPlayerProp`.

### Weapons

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::addWeapon(LevelItemType)`.
  - `Player::addWeapon(std::string)`.
  - `Player::addWeapon(std::shared_ptr<Weapon>)`.
  - `Player::deleteWeapon(...)`.
  - `Player::msgPLI_NPCWEAPONDEL`.
  - `Player::msgPLI_WEAPONADD`.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Weapon.cpp`
  - default weapon constructor using `LevelItem::getItemName`.
  - weapon packet construction.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
  - startup creation of default weapons for bow, bomb, superbomb, fireball,
    fireblast, nukeshot, and joltbomb.

### Chat, PM, Profiles, Guilds

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::msgPLI_TOALL`.
  - `Player::msgPLI_PRIVATEMESSAGE`.
  - `Player::msgPLI_PROFILEGET`.
  - `Player::msgPLI_PROFILESET`.
  - guild verification request through server-list `SVO_VERIGUILD`.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/TriggerCommandHandlers.cpp`
  - `gr.addweapon`.
  - `gr.deleteweapon`.
  - `gr.addguildmember`.
  - `gr.removeguildmember`.
  - `gr.removeguild`.
  - `gr.setguild`.
- `external/gs2lib/include/IEnums.h`
  - `SVO_VERIGUILD = 9`.

## Explicitly Out Of Scope Unless Proven Later

The recovered C++ server source does not contain dedicated built-in modules for:

- shops;
- trade;
- party;
- quests;
- missions.

These may exist in specific server resources through scripts, classes, flags, or
level data, but resource-defined behavior is not a new C# built-in feature. The
port should support the original script/resource mechanisms when those C++ paths
are implemented, not invent replacement gameplay systems.

## Confirmed Item Pickup Payloads

`LevelItem::getItemPlayerProp` returns a player-property payload, not a complete
`PLO_PLAYERPROPS` packet. Callers pass the payload into:

```cpp
Player::setProps(..., PLSETPROPS_FORWARD | PLSETPROPS_FORWARDSELF)
```

Confirmed payload rules:

- rupees:
  - green +1, blue +5, red +30, gold +100;
  - clamp resulting rupees to `0..9999999`;
  - payload: `PLPROP_RUPEESCOUNT + GINT rupeeCount`.
- bombs:
  - add `5`;
  - clamp to `0..99`;
  - payload: `PLPROP_BOMBSCOUNT + GCHAR bombCount`.
- darts/arrows:
  - add `5`;
  - clamp to `0..99`;
  - payload: `PLPROP_ARROWSCOUNT + GCHAR arrowCount`.
- heart:
  - add `1.0` HP;
  - clamp to `0..maxPower`;
  - payload: `PLPROP_CURPOWER + GCHAR(newPower * 2)`.
- glove1/glove2:
  - glove2 sets power `3`;
  - glove1 raises power to at least `2`;
  - payload: `PLPROP_GLOVEPOWER + GCHAR glovePower`.
- bow/bomb/superbomb/fireball/fireblast/nukeshot/joltbomb:
  - calls `Player::addWeapon(itemType)`;
  - returns empty payload.
- shield/mirrorshield/lizardshield:
  - candidate powers are `1`, `2`, and `3`;
  - existing higher shield power is preserved;
  - payload: `PLPROP_SHIELDPOWER + GCHAR shieldPower`.
- sword/battleaxe/lizardsword/goldensword:
  - candidate powers are `1`, `2`, `3`, and `4`;
  - existing higher sword power is preserved;
  - payload: `PLPROP_SWORDPOWER + GCHAR swordPower`.
- fullheart:
  - max hearts become `clip(maxPower + 1, 0, 20)`;
  - current power is set to full;
  - payload: `PLPROP_MAXPOWER + GCHAR max + PLPROP_CURPOWER + GCHAR(max * 2)`.
- spinattack:
  - if `PLSTATUS_HASSPIN` is already set, returns empty payload;
  - otherwise sets that bit and returns `PLPROP_STATUS + GCHAR status`.

### Complete `getItemPlayerProp` Item Catalog

| ID | Name | C++ `LevelItemType` | Confirmed player-prop result |
| ---: | --- | --- | --- |
| 0 | `greenrupee` | `GREENRUPEE` | `PLPROP_RUPEESCOUNT + GINT(clip(rupees + 1, 0, 9999999))` |
| 1 | `bluerupee` | `BLUERUPEE` | `PLPROP_RUPEESCOUNT + GINT(clip(rupees + 5, 0, 9999999))` |
| 2 | `redrupee` | `REDRUPEE` | `PLPROP_RUPEESCOUNT + GINT(clip(rupees + 30, 0, 9999999))` |
| 3 | `bombs` | `BOMBS` | `PLPROP_BOMBSCOUNT + GCHAR(clip(bombs + 5, 0, 99))` |
| 4 | `darts` | `DARTS` | `PLPROP_ARROWSCOUNT + GCHAR(clip(arrows + 5, 0, 99))` |
| 5 | `heart` | `HEART` | `PLPROP_CURPOWER + GCHAR(clip(power + 1.0, 0.0, maxPower) * 2.0)` |
| 6 | `glove1` | `GLOVE1` | `PLPROP_GLOVEPOWER + GCHAR(max(glovePower, 2))` |
| 7 | `bow` | `BOW` | Calls `Player::addWeapon(BOW)` and returns an empty payload. |
| 8 | `bomb` | `BOMB` | Calls `Player::addWeapon(BOMB)` and returns an empty payload. |
| 9 | `shield` | `SHIELD` | `PLPROP_SHIELDPOWER + GCHAR(max(currentShieldPower, 1))` |
| 10 | `sword` | `SWORD` | `PLPROP_SWORDPOWER + GCHAR(max(currentSwordPower, 1))` |
| 11 | `fullheart` | `FULLHEART` | `PLPROP_MAXPOWER + GCHAR(clip(maxPower + 1, 0, 20)) + PLPROP_CURPOWER + GCHAR(newMax * 2)` |
| 12 | `superbomb` | `SUPERBOMB` | Calls `Player::addWeapon(SUPERBOMB)` and returns an empty payload. |
| 13 | `battleaxe` | `BATTLEAXE` | `PLPROP_SWORDPOWER + GCHAR(max(currentSwordPower, 2))` |
| 14 | `goldensword` | `GOLDENSWORD` | `PLPROP_SWORDPOWER + GCHAR(4)` |
| 15 | `mirrorshield` | `MIRRORSHIELD` | `PLPROP_SHIELDPOWER + GCHAR(max(currentShieldPower, 2))` |
| 16 | `glove2` | `GLOVE2` | `PLPROP_GLOVEPOWER + GCHAR(3)` |
| 17 | `lizardshield` | `LIZARDSHIELD` | `PLPROP_SHIELDPOWER + GCHAR(max(currentShieldPower, 3))` |
| 18 | `lizardsword` | `LIZARDSWORD` | `PLPROP_SWORDPOWER + GCHAR(max(currentSwordPower, 3))` |
| 19 | `goldrupee` | `GOLDRUPEE` | `PLPROP_RUPEESCOUNT + GINT(clip(rupees + 100, 0, 9999999))` |
| 20 | `fireball` | `FIREBALL` | Calls `Player::addWeapon(FIREBALL)` and returns an empty payload. |
| 21 | `fireblast` | `FIREBLAST` | Calls `Player::addWeapon(FIREBLAST)` and returns an empty payload. |
| 22 | `nukeshot` | `NUKESHOT` | Calls `Player::addWeapon(NUKESHOT)` and returns an empty payload. |
| 23 | `joltbomb` | `JOLTBOMB` | Calls `Player::addWeapon(JOLTBOMB)` and returns an empty payload. |
| 24 | `spinattack` | `SPINATTACK` | If `PLSTATUS_HASSPIN` is already set, returns empty; otherwise returns `PLPROP_STATUS + GCHAR(status | PLSTATUS_HASSPIN)`. |

Invalid ids and unknown item names resolve to `LevelItemType::INVALID`; the
default branch returns an empty payload.

## Confirmed Player-Drop Removal Rules

`Player::removeItem` is used by `spawnLevelItem` under `V8NPCSERVER` for
player-dropped items. Confirmed rules:

- rupees require and subtract their rupee value: `1`, `5`, `30`, or `100`;
- bombs require/subtract `5`;
- darts require/subtract `5`;
- heart requires HP `> 1.0`, then subtracts `1.0`;
- glove1/glove2 removal exists only in the non-`V8NPCSERVER` branch and
  decrements glove power when current power is `> 1`;
- spinattack clears `PLSTATUS_HASSPIN` if set;
- weapon, shield, sword, fullheart, and most equipment removal is commented out
  or returns false in the confirmed C++ path.

## C# Mapping

- `src/Game/InventoryItemRules.cs`
  - `DurablePlayerInventoryState`
  - `InventoryItemRules.BuildPickupPlayerProps`
  - `InventoryItemRules.ApplyPickupPlayerProps`
  - `InventoryItemRules.TryRemoveForPlayerDrop`
- `src/Game/LevelInteraction.cs`
  - `LevelInteraction.TryOpenChestAndApplyReward`
- `src/Game/EntityRuntime.cs`
  - `LevelItemRuntime.SpawnLevelItem`
  - `LevelItemRuntime.DeleteOrTakeLevelItem`
- `tests/Game.Tests/InventoryItemRulesTests.cs`
  - golden byte payload tests for confirmed item rules.

## Chest Reward Application

The C++ chest path in `Player::msgPLI_OPENCHEST` calls:

```cpp
setProps(CString() << LevelItem::getItemPlayerProp(chestItem, this),
         PLSETPROPS_FORWARD | PLSETPROPS_FORWARDSELF);
```

The C# port mirrors this boundary for the confirmed durable item state:

1. `LevelInteraction.TryOpenChest` performs the source-confirmed exact chest
   lookup, unopened key check, opened-key record, and `PLO_LEVELCHEST` ack.
2. `InventoryItemRules.BuildPickupPlayerProps` builds the same property payload
   that `LevelItem::getItemPlayerProp` would return.
3. `InventoryItemRules.ApplyPickupPlayerProps` applies only those confirmed
   reward payload properties to `DurablePlayerInventoryState`.

This does not implement a new inventory system. It only applies the property
payloads already confirmed from C++: rupees, bombs, arrows, current/max power,
glove power, shield power, sword power, and spinattack status.

## Remaining Source-Confirmed Work

- Full live inventory runtime wiring through production `setProps`, session
  forwarding, and persistence save timing.
- Real weapon packets for default and NPC weapons beyond already-existing packet
  builders.
- Chat/PM word-filter, jail, external-player, NPC-server PM, server-list IRC,
  and profile behavior from `Player.cpp`.
- Guild filesystem mutation and server-list verification side effects from
  C++/script trigger handlers.
- Script/content mechanisms that may drive server-specific gameplay.
