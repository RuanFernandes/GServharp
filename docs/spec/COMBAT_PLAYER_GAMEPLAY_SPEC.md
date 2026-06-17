# Combat Player Gameplay Spec

## Status

Milestone 13 ports only source-confirmed deterministic boundaries:

- combat-related server-to-client packet builders for simple forwarding packets;
- player resource/property clamps for hearts, AP, bombs, arrows, glove/bomb power, and MP;
- AP timer threshold behavior;
- death/revive state transitions that do not require full level/drop/script runtime.

Full combat simulation, hit detection validation, projectile physics, death drops, persistence writes, and script/NPC side effects remain blocked until their surrounding runtime systems are fully ported.

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::doMain` AP timer: line 581.
  - `Player::dropItemsOnDeath`: line 913.
  - spar AP downgrade during warp: line 1654.
  - `Player::msgPLI_BOMBADD`: line 2572.
  - `Player::msgPLI_BOMBDEL`: line 2596.
  - `Player::msgPLI_ARROWADD`: line 2661.
  - `Player::msgPLI_HURTPLAYER`: line 3206.
  - `Player::msgPLI_EXPLOSION`: line 3227.
  - `Player::msgPLI_HITOBJECTS`: line 3517.
  - `Player::msgPLI_SHOOT`: line 3883.
  - `Player::msgPLI_SHOOT2`: line 3941.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
  - serialized player resource props: lines 31-187.
  - mutable resource props: lines 362-846.
  - status death/revive side effects: line 677.
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
  - `Account::setPower`: line 310.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
  - `Account::setMaxPower`: line 653.
- `external/gs2lib/include/IEnums.h`
  - combat packet IDs: lines 180-215 and 296-312.
  - player status flags: lines 333-339.

## Confirmed Packet IDs

From `external/gs2lib/include/IEnums.h`:

- `PLO_BOMBADD = 11`
- `PLO_BOMBDEL = 12`
- `PLO_ARROWADD = 19`
- `PLO_EXPLOSION = 36`
- `PLO_HURTPLAYER = 40`
- `PLO_HITOBJECTS = 46`
- `PLO_SHOOT = 175`
- `PLO_SHOOT2 = 191`

## Confirmed Packet Construction

`Player::msgPLI_BOMBADD` builds:

```txt
PLO_BOMBADD + GSHORT senderPlayerId + raw incoming PLI_BOMBADD bytes after the first opcode byte
```

`Player::msgPLI_BOMBDEL` builds:

```txt
PLO_BOMBDEL + raw incoming PLI_BOMBDEL bytes after the first opcode byte
```

`Player::msgPLI_ARROWADD` builds:

```txt
PLO_ARROWADD + GSHORT senderPlayerId + raw incoming PLI_ARROWADD bytes after the first opcode byte
```

`Player::msgPLI_HURTPLAYER` parses victim id, hurt dx, hurt dy, power, and npc id. If the victim exists and is not paused, it sends to that victim:

```txt
PLO_HURTPLAYER + GSHORT attackerPlayerId + GCHAR hurtDx + GCHAR hurtDy + GCHAR power + GINT npcId
```

No server-side HP mutation is performed by this handler.

`Player::msgPLI_EXPLOSION` checks setting `noexplosions`; if false, it sends to the current level excluding the sender:

```txt
PLO_EXPLOSION + GSHORT playerId + GCHAR radius + GCHAR encodedX + GCHAR encodedY + GCHAR power
```

`Player::msgPLI_HITOBJECTS` sends:

```txt
PLO_HITOBJECTS + GSHORT playerId + GCHAR encodedPower + GCHAR encodedX + GCHAR encodedY
```

If an NPC id is present on the inbound packet, the player id field becomes `0` and the packet appends `GINT npcId`.

## Confirmed Property Rules

`PLPROP_MAXPOWER`:

- reads one `GCHAR`;
- calls `setMaxPower(newMaxPower)`;
- `setMaxPower` clamps to `0..min(settings["heartlimit"], 20)`;
- current power is set to the resulting max power.

`PLPROP_CURPOWER`:

- reads one `GCHAR`;
- decoded hitpoints are `encoded / 2.0`;
- if AP/alignment is below 40 and the new value would increase HP, the update is ignored;
- otherwise hitpoints are clamped to `0..maxPower`.

`PLPROP_ARROWSCOUNT` and `PLPROP_BOMBSCOUNT` read one `GCHAR` and clamp to `0..99`.

`PLPROP_GLOVEPOWER` and `PLPROP_BOMBPOWER` read one `GCHAR` and clamp to `0..3`.

`PLPROP_MAGICPOINTS` reads one `GCHAR` and clamps to `0..100`.

`PLPROP_ALIGNMENT` reads one `GCHAR` and clamps to `0..100`.

`PLPROP_STATUS`:

- when transitioning from dead to alive, hitpoints become AP `< 20 ? 3 : AP < 40 ? 5 : maxPower`, then clamp to `0.5..maxPower`;
- when transitioning from alive to dead outside sparring levels, deaths increment and `dropItemsOnDeath()` runs;
- when dying inside sparring levels, deaths and drops are skipped.

## Confirmed AP Timer

When `apsystem` is enabled, the player has a current level, the player is not paused, and the level is not sparring:

1. decrement `m_apCounter`;
2. if counter reaches zero, increment AP if below `100`;
3. reset counter using `aptime0`/`aptime1`/`aptime2`/`aptime3`/`aptime4` at AP thresholds `<20`, `<40`, `<60`, `<80`, and otherwise.

Default C++ timer values are `30`, `90`, `300`, `600`, and `1200`.

When warping into a sparring zone with AP exactly `100`, C++ changes AP to `99`, sets `m_apCounter = 1`, and forwards `PLPROP_ALIGNMENT`.

## C# Mapping

- `src/Protocol/ProtocolIds.cs`
  - adds confirmed packet IDs.
- `src/Protocol/CombatPackets.cs`
  - source-confirmed packet builders for bomb, arrow, hurt, explosion, and hitobjects.
- `src/Game/CombatPlayerGameplay.cs`
  - isolated deterministic player state transitions and clamps.
- `tests/Protocol.Tests/CombatPacketTests.cs`
  - golden byte fixtures for packet IDs and packet construction.
- `tests/Game.Tests/CombatPlayerGameplayTests.cs`
  - formula/clamp/state-transition tests.

## Blocked Areas

- Real HP damage application from `PLO_HURTPLAYER` is client-side or elsewhere; no server HP mutation was confirmed in `msgPLI_HURTPLAYER`.
- `dropItemsOnDeath()` depends on random values, settings, level item insertion, outbound item packets, and persistence timing. It is documented but not ported here.
- `msgPLI_SHOOT` and `msgPLI_SHOOT2` were traced, but projectile conversion and recipient version routing need dedicated packet fixtures before implementation.
- Script/NPC side effects remain blocked by the scripting runtime boundary.
- Full hit detection remains client-reported forwarding in the confirmed C++ handlers; server anti-cheat validation must not be invented.
