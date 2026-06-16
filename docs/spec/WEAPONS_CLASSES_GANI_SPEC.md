# Weapons, Classes, And Gani Spec

## Source Map

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Weapon.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Weapon.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/ScriptClass.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/scripting/ScriptClass.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerScripts.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `external/gs2lib/include/IEnums.h`

## Startup Loading Order

`Server::init` loads this area in order:

1. `loadWeapons(true)`
2. `loadClasses(true)`
3. maps, DB NPCs when `V8NPCSERVER`, map levels, translations, word filter

Weapon and class loading therefore happen before map level runtime loading.

## Weapon Filesystem Loading

`Server::loadWeapons` creates two file systems:

- `weapons`, pattern `weapon*.txt`
- `weapon_bytecode`, pattern `*`

For each `weapon*.txt` file, `Weapon::loadWeapon(fileName)` is called.

`Weapon::loadWeapon` behavior:

- path is `serverPath + "weapons/" + pWeapon`;
- file load failure returns `nullptr`;
- removes all `"\r"`;
- first line must be exactly `GRAWP001`;
- otherwise returns `nullptr`;
- recognized lines:
  - `REALNAME <rest>` -> weapon name;
  - `IMAGE <rest>` -> image;
  - `BYTECODE <rest>` -> bytecode file name under `weapon_bytecode/`;
  - `SCRIPT` -> reads lines until a line exactly `SCRIPTEND`, preserving each
    script line with `"\n"`;
- empty `REALNAME` returns `nullptr`;
- if file contains `SCRIPTEND` but parser never finds standalone `SCRIPTEND`,
  C++ logs a warning;
- if both non-empty script and bytecode data are present, C++ logs a warning,
  clears the script, and uses bytecode.

Mod time selection after loading:

- if no bytecode file is used, weapon mod time comes from the `weapons` file;
- if bytecode file is used, weapon mod time comes from `weapon_bytecode`.

Cache/update behavior:

- if the weapon name is not in `m_weaponList`, C++ inserts it;
- if already present and the new disk mod time is newer, C++ replaces it and
  calls `updateWeaponForPlayers`;
- otherwise it logs/skips when print is enabled.

After disk load, missing default weapons are inserted:

```txt
bow
bomb
superbomb
fireball
fireblast
nukeshot
joltbomb
```

Each default weapon is constructed from the matching `LevelItem` id and sends
`PLO_DEFAULTWEAPON + GCHAR(default item id)`.

## Weapon Save/Delete

`Weapon::saveWeapon`:

- refuses default weapons;
- refuses empty names;
- refuses weapons backed by bytecode files, treating them as read-only;
- sanitizes filename:
  - `\` and `/` -> `_`
  - `*` -> `@`
  - `:` -> `;`
  - `?` -> `!`
- saves as `weapons/weapon<Name>.txt`;
- writes `GRAWP001`, `REALNAME`, `IMAGE`, optional `SCRIPT`, and `SCRIPTEND`;
- uses CRLF line endings;
- appends a newline before `SCRIPTEND` when the source does not already end in
  `"\n"`.

`Server::NC_DelWeapon`:

- refuses missing/default weapons;
- sanitizes the filename the same way;
- removes `weapons/weapon<Name>.txt`;
- removes the weapon from memory;
- sends `PLO_NPCWEAPONDEL + name` to all clients.

## Weapon Packet Construction

`Weapon::getWeaponPacket(clientVersion)`:

- default weapon:

```txt
PLO_DEFAULTWEAPON
GCHAR default item id
```

- non-default starts with:

```txt
PLO_NPCWEAPONADD
GCHAR weaponName.length
weaponName bytes
GCHAR NPCPROP_IMAGE
GCHAR image.length
image bytes
```

For clients `>= CLVER_4_0211`:

- if bytecode exists:
  - appends `NPCPROP_CLASS + GSHORT(0) + "\n"`;
  - reads bytecode header length from the bytecode buffer;
  - appends `PLO_UNKNOWN197 + header + "," + time(0) + "\n"`;
  - returns immediately.
- if client version is `> CLVER_5_07`, GS1 script is disabled and C++ returns
  only the base weapon packet.

Otherwise C++ appends:

```txt
GCHAR NPCPROP_SCRIPT
GSHORT formattedClientGS1.length
formattedClientGS1 bytes
```

`setClientScript` formatting:

- removes comments through `removeComments`;
- if formatted script does not begin with `//#CLIENTSIDE`, prefixes
  `//#CLIENTSIDE` followed by byte `0xA7`;
- tokenizes by `"\n"`;
- trims each line;
- joins every line with byte `0xA7`.

## NC Weapon Inspection

`Player::msgPLI_NC_WEAPONGET`:

- reads trailing weapon name;
- requires NC access;
- if weapon exists and is not a default weapon:
  - uses `weapon->getFullScript()`;
  - replaces every `'\n'` with byte `0xa7`;
  - for NC clients older than `NCVER_2_1`, sends the legacy
    `PLO_NPCWEAPONADD` packet shape:

```txt
PLO_NPCWEAPONADD
GCHAR weaponName.length
weaponName
GCHAR NPCPROP_IMAGE
GCHAR image.length
image
GCHAR NPCPROP_SCRIPT
GSHORT script.length
script_with_0xa7_newlines
```

  - otherwise sends the modern NC packet:

```txt
PLO_NC_WEAPONGET
GCHAR weaponName.length
weaponName
GCHAR image.length
image
script_with_0xa7_newlines
```

- if missing or default, sends an RC chat error to all NC users.

## Weapon Runtime Updates

`Weapon::updateWeapon`:

- reads `gs2default` from settings;
- creates `SourceCode`;
- sets image;
- sets mod time to supplied mod time, or `time(0)` when supplied value is `0`;
- when `V8NPCSERVER` is enabled, frees old script resources and may execute
  server-side weapon script, queuing `weapon.created`;
- clears bytecode and formatted GS1;
- if client GS2 exists, calls `Server::compileGS2Script`; on success:
  - stores joined class names;
  - creates bytecode header with type `"weapon"`, weapon name, save flag `true`;
- if client GS1 exists, formats it with `setClientScript`;
- saves the weapon when requested.

`Server::updateWeaponForPlayers`:

- iterates all players;
- skips non-clients;
- for players that already have the weapon:
  - sends `PLO_NPCWEAPONDEL + weaponName`;
  - sends the new `weapon->getWeaponPacket(playerVersion)`.

## Class Filesystem Loading And Cache

`Server::loadClasses`:

- uses `scripts`, pattern `*.txt`;
- class name is filename without the last four characters (`.txt`);
- loads the full file as class source;
- stores `m_classList[className] = ScriptClass(className, scriptData)`;
- calls `updateClassForPlayers(getClass(className))`.

`ScriptClass::parseScripts`:

- reads `gs2default` from settings;
- creates `SourceCode`;
- if client GS2 exists, compiles through `Server::compileGS2Script`;
- on success, creates bytecode header with type `"class"`, class name, save flag
  `true`;
- stores bytecode.

`ScriptClass::getClassPacket`:

- returns empty when bytecode is empty;
- otherwise reads the bytecode header length/header;
- appends `PLO_UNKNOWN197 + header + "," + gtokenize(time(0)) + "\n"`.

`Player::msgPLI_NC_CLASSEDIT`:

- requires NC access;
- reads trailing class name;
- if the class exists:
  - reads `classObj->getSource().getSource()`;
  - sends:

```txt
PLO_NC_CLASSGET
GCHAR className.length
className
classCode.gtokenize()
```

- if the class is missing, it returns true without response.

`Player::msgPLI_NC_CLASSADD`:

- requires NC access;
- reads `GCHAR name.length + name + guntokenized script`;
- calls `Server::updateClass`;
- calls `Server::updateClassForPlayers`;
- only if the class did not already exist, broadcasts:

```txt
PLO_NC_CLASSADD
className
```

`Player::msgPLI_NC_CLASSDELETE`:

- requires NC access;
- reads trailing class name;
- if `Server::deleteClass(className)` succeeds, broadcasts:

```txt
PLO_NC_CLASSDELETE
className
```

- if deletion fails, sends only NC log/chat text; no class-delete packet is
  emitted.

`Server::updateClass`:

- replaces `m_classList[className]`;
- saves source to `scripts/<className>.txt`.

`Server::deleteClass`:

- refuses missing classes;
- erases from memory;
- removes `scripts/<className>.txt`.

`Server::updateClassForPlayers`:

- iterates all clients;
- only sends to versions `>= CLVER_4_0211`;
- if class exists, sends:

```txt
PLO_RAWDATA
GINT bytecode.length
"\n"
PLO_NPCWEAPONSCRIPT
bytecode
```

## Client Requests

`Player::msgPLI_UPDATESCRIPT`:

- reads trailing weapon name;
- logs it;
- if weapon exists:
  - sends `PLO_RAWDATA + GINT(bytecode.length) + "\n"`;
  - sends `PLO_NPCWEAPONSCRIPT + bytecode`.

`Player::msgPLI_UPDATECLASS`:

- reads `GINT5 modTime`, then trailing class name;
- logs it;
- if class exists:
  - sends raw-data bytecode packet as above;
- if class is missing:
  - builds a tokenized empty bytecode header for `class,className,1,...`;
  - sends `PLO_NPCWEAPONSCRIPT + GSHORT(header length) + header`;
  - comment says this should technically be `PLO_UNKNOWN197`, but the alternate
    packet fixes client breakage with `player.join()` scripts.

For the missing-class fallback, header fields are retokenized with
`utilities::retokenizeCStringArray`:

```txt
class
className
1
GINT5(0) + GINT5(0)
GINT5(0)
```

The GINT5 zero strings are spaces (`0x20`), so `CString::gtokenize` quotes them
as complex/whitespace tokens.

`Player::msgPLI_UPDATEGANI`:

- reads `GUInt5 checksum`;
- reads trailing gani name;
- appends `.gani`;
- loads from animation manager with `findOrAddResource`;
- missing animation returns without response;
- if CRC32 of current bytecode differs from client checksum, sends
  `animation.getBytecodePacket()`;
- always sends `PLO_UNKNOWN195 + GCHAR(gani.length) + gani +
  "\"SETBACKTO " + setback + "\""`.

`GameAni::getBytecodePacket()`:

- strips a trailing `.gani` suffix from the animation filename;
- returns an empty packet if the stripped gani name is empty or bytecode is
  empty;
- otherwise sends:

```txt
PLO_RAWDATA
GINT(bytecode.length + gani.length + 1)
"\n"
PLO_GANISCRIPT
GCHAR(gani.length)
gani
bytecode
```

## Current C# Status

Implemented:

- selected default/non-default weapon packet wrapper fixtures;
- `PLO_NPCWEAPONDEL + weaponName + "\n"`;
- `PLO_RAWDATA + GINT(bytecode.length) + "\n" + PLO_NPCWEAPONSCRIPT +
  bytecode` for confirmed script/class bytecode response shape;
- missing-class `PLI_UPDATECLASS` fallback packet:
  `PLO_NPCWEAPONSCRIPT + GSHORT(header length) + retokenized empty
  bytecode header + "\n"`;
- legacy NC weapon-get response packet for clients older than `NCVER_2_1`,
  including newline-to-`0xa7` conversion and `PLO_NPCWEAPONADD` property order;
- NC class edit/get response packet:
  `PLO_NC_CLASSGET + GCHAR(className.length) + className +
  classCode.gtokenize() + "\n"`;
- NC class add/delete broadcast packet builders:
  `PLO_NC_CLASSADD + className + "\n"` and
  `PLO_NC_CLASSDELETE + className + "\n"`;
- `PLI_UPDATEGANI` parser for `GUInt5 checksum + trailing gani name`;
- GANI CRC mismatch decision using the source-confirmed CRC32 primitive;
- `PLO_RAWDATA + PLO_GANISCRIPT` bytecode response wrapper from
  `GameAni::getBytecodePacket()`;
- `PLO_LOADGANI + GCHAR(gani.length) + gani + "\"SETBACKTO " + setback +
  "\""` response packet;
- pre-warp login boundary can queue supplied weapon/class packets in the C++
  order;
- source and compiler blockers are explicit.

Blocked:

- production filesystem weapon/class repositories;
- exact GS1 formatting fixtures beyond the currently documented rules;
- exact GS2 bytecode compiler commit and bytecode golden outputs;
- live weapon/class mutation commands;
- production class repository update/delete side effects;
- dynamic `time(0)` packet fixture strategy;
- production `PLI_UPDATEGANI` animation manager/repository wiring and real
  compiled bytecode generation.
