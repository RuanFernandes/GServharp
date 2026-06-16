# Baddy And Combat Spec

## Source Map

- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelBaddy.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelBaddy.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/include/IEnums.h`

## Baddy Constants

`LevelBaddy.h` defines baddy property ids:

| ID | Symbol |
| ---: | --- |
| 0 | `BDPROP_ID` |
| 1 | `BDPROP_X` |
| 2 | `BDPROP_Y` |
| 3 | `BDPROP_TYPE` |
| 4 | `BDPROP_POWERIMAGE` |
| 5 | `BDPROP_MODE` |
| 6 | `BDPROP_ANI` |
| 7 | `BDPROP_DIR` |
| 8 | `BDPROP_VERSESIGHT` |
| 9 | `BDPROP_VERSEHURT` |
| 10 | `BDPROP_VERSEATTACK` |

Modes:

| ID | Symbol |
| ---: | --- |
| 0 | `BDMODE_WALK` |
| 1 | `BDMODE_LOOK` |
| 2 | `BDMODE_HUNT` |
| 3 | `BDMODE_HURT` |
| 4 | `BDMODE_BUMPED` |
| 5 | `BDMODE_DIE` |
| 6 | `BDMODE_SWAMPSHOT` |
| 7 | `BDMODE_HAREJUMP` |
| 8 | `BDMODE_OCTOSHOT` |
| 9 | `BDMODE_DEAD` |

Confirmed packet ids:

| Direction | ID | Symbol |
| --- | ---: | --- |
| client -> server | 15 | `PLI_BADDYPROPS` |
| client -> server | 16 | `PLI_BADDYHURT` |
| client -> server | 17 | `PLI_BADDYADD` |
| server -> client | 2 | `PLO_BADDYPROPS` |
| server -> client | 27 | `PLO_BADDYHURT` |

## Reset Defaults

`LevelBaddy::reset` restores:

- start mode from `baddyStartMode[type]`;
- start X/Y from constructor coordinates;
- power from `baddyPower[type]`;
- image from `baddyImages[type]`;
- direction `(2 << 2) | 2`, which is `10`;
- animation `0`;
- `m_hasCustomImage = false`.

Confirmed tables:

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

start modes:
0,0,0,0,6,7,0,0,0,0

powers:
2,3,4,3,2,1,1,6,12,8
```

Compatibility risk: the constructor checks `if (pType > baddytypes)` instead of
`>= baddytypes`, so type `10` can index past the static tables in C++. The C#
runtime must not "fix" this silently in a client-visible path without a capture
decision.

## Property Serialization

`LevelBaddy::getProps(clientVersion)` serializes property ids `1` through `10`
in order. `BDPROP_ID` exists but is not included in the normal baddy props
payload because the wrapper packet carries the baddy id.

`BDPROP_POWERIMAGE` serializes:

```txt
GCHAR power
GCHAR image length
image bytes
```

For clients older than `CLVER_2_1`, default baddy images are rewritten from
`.png` to `.gif`.

## Client-Originated Baddy Packets

`Player::msgPLI_BADDYPROPS`:

1. Requires current level.
2. Reads `GCHAR id`.
3. Reads the remaining bytes as `props`.
4. Ignores missing baddy ids.
5. Forwards `PLO_BADDYPROPS + GCHAR id + props` to one level excluding the
   level leader.
6. Applies `baddy->setProps(props)` locally.

`Player::msgPLI_BADDYHURT`:

1. Uses current level's first player id as leader.
2. If the leader exists, sends `PLO_BADDYHURT + incoming packet bytes after the
   packet id` to that leader only.
3. The server does not compute damage in this handler.

`Player::msgPLI_BADDYADD`:

1. Requires current level.
2. Reads `x = GCHAR / 2.0`, `y = GCHAR / 2.0`, type, power, and trailing image.
3. Caps power with `MIN(power, 12)`.
4. Appends `.gif` to non-empty extensionless image names.
5. Calls `level->addBaddy(x, y, type)`.
6. Sets `m_canRespawn = false`.
7. Applies `BDPROP_POWERIMAGE`.
8. Sends `PLO_BADDYPROPS + id + baddy.getProps()` to one level.

## `LevelBaddy::setProps`

Confirmed property mutation:

- `BDPROP_ID`: reads signed `GCHAR` into id.
- `BDPROP_X/Y`: reads `GCHAR / 2.0`, then clips to `0.0..63.5`.
- `BDPROP_TYPE`: reads signed `GCHAR` into type.
- `BDPROP_POWERIMAGE`: reads signed power, optional image length and image. Empty
  image resets to the default image for the current type. Non-empty custom image
  is only accepted the first time while `m_hasCustomImage == false`.
- `BDPROP_MODE`: handles timers and death/drop state described below.
- `BDPROP_ANI`: reads signed `GCHAR`.
- `BDPROP_DIR`: reads signed `GCHAR`.
- `BDPROP_VERSESIGHT/HURT/ATTACK`: reads length and stores verse string at
  index `propId - BDPROP_VERSESIGHT`.

There is no default invalid-prop disconnect path in this method.

## Timers, Death, Drops, And Respawn

`BDPROP_MODE` side effects:

- Type `4` swamp arrow baddy entering `BDMODE_HURT` sets timeout to `2` seconds.
- Entering `BDMODE_DIE` sets timeout to `2` seconds.
- On `BDMODE_DIE`, if setting `baddyitems` is true, `dropItem()` is called
  immediately.
- Entering `BDMODE_DEAD`:
  - if `m_canRespawn` is true, sets timeout to `baddyrespawntime` setting,
    default `60`;
  - otherwise removes the baddy from the level, or deletes `this` if the level
    weak pointer cannot be locked.

`Level::doTimedEvents` processes baddy timeout completion:

- If type `4` is still in `BDMODE_HURT` and power is `1`, it sets mode back to
  `BDMODE_SWAMPSHOT` and sends `PLO_BADDYPROPS + id + BDPROP_MODE +
  BDMODE_SWAMPSHOT` to players at indices `1..end`. The level leader at index
  `0` is excluded.
- Else if mode is `BDMODE_DIE`, it sends `BDMODE_DEAD` props to players at
  indices `1..end`, then after the loop applies `BDMODE_DEAD` locally.
- Else it calls `reset()` and sends full `baddy.getProps(playerVersion)` to all
  players in the level.

`LevelBaddy::dropItem`:

- draws `rand() % 12`;
- ids `0..5` map to green rupee, blue rupee, red rupee, bombs, darts, heart;
- ids `6..9` map to green rupee;
- ids `10..11` drop nothing;
- when an item is created, sends `PLO_ITEMADD + GCHAR(x * 2) + GCHAR(y * 2) +
  GCHAR(item type id)` to one level.

The exact C RNG sequence is not ported yet. Drop runtime implementation remains
blocked until RNG/capture fixtures are created.

## Current C# Status

Implemented:

- inert `RuntimeBaddy` defaults and full default property serialization;
- baddy id generation/reuse and the source-confirmed 51-baddy boundary;
- baddy packet ids and selected packet builders.

Blocked:

- timeout scheduler integration;
- live `PLI_BADDYPROPS`, `PLI_BADDYHURT`, and `PLI_BADDYADD` session routing;
- `BDPROP_MODE` side effects;
- `dropItem` because exact C `rand()` compatibility/capture is not established;
- leader-exclusion recipient selection for baddy timeout events;
- out-of-bounds `type == 10` behavior decision.
