# Original C++ Server Technical Spec

This document records the initial source analysis for a future C#/.NET port. The goal is not a modernized protocol or a similar server. The C# server must reproduce the original C++ server's client-facing behavior exactly.

## Source Authority

Primary source of truth:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/packets/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/object/*.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/player/*.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/loader/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/loader/flatfile/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/npcserver/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/*.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/utilities/*.cpp`

Supporting references only:

- `ai_resources/GServer-Rust-Outdated/crates/*`
- `ai_resources/GServer-Python-Outdated/pygserver/*`

The Rust and Python ports are useful for proposed module boundaries, but they are incomplete and sometimes behaviorally divergent. C++ wins on every compatibility question.

## Important Repository Finding

The C++ source includes external headers such as `IEnums.h`, `CString.h`, `CEncryption.h`, `CFileQueue.h`, and `CSocket.h`, but those authoritative definitions are not present in this checkout. The C++ code references packet names and serializer calls, but the numeric enum definitions and exact `CString` codec implementation must be recovered before implementing packets.

Until those headers are found, packet names from C++ are authoritative, while packet numbers from Rust/Python are non-authoritative hints.

## Overall C++ Architecture

The C++ server is centered on `Server`, a large orchestrator that owns:

- TCP listen socket and socket manager.
- Player registry keyed by `PlayerID`.
- Level cache, map list, GMAP/bigmap state, and static level data.
- NPC list and NPC-server integration.
- Weapon registry.
- Server flags through `ScriptContainer`.
- Flat-file account and NPC loaders.
- File-system watchers for config/world/server resources.
- Server list connection.
- Timed event generators.

Player sessions are represented by subclasses of `Player`:

- `PlayerLogin`: temporary connection state before the first login byte decides the session type.
- `PlayerClientOriginal`: old client handling path.
- `PlayerClient`: normal game client session.
- `PlayerRC`: remote-control/admin session.
- `PlayerNC`: NPC-control session.
- `PlayerNPCServer`: internal NPC-server session.

The C++ architecture is monolithic, but the behavior boundaries are clear enough for a cleaner port.

## Startup Flow

Source files:

- `server/src/main.cpp`
- `server/src/Server.cpp`

Startup order:

1. Parse command-line arguments unless `USE_ENV` is set.
2. Determine the server directory from, in order:
   - explicit `--server`/`-s` or `SERVER`
   - current working directory if it contains `config/serveroptions.txt`
   - `startupserver.txt`
   - a single directory under `servers/`
3. Change the working directory to the selected server directory.
4. Instantiate `Server` and `GuildManager` through `BabyDI`.
5. Log program metadata and selected server directory.
6. Call `Server::init(serverip, port, localip, interface)`.
7. Optionally add an override staff account and seed it from `YOURACCOUNT`.
8. Enter `Server::operator()`, which loops until restart or shutdown.

`Server::init` does the following:

- Records server start time.
- Loads config files.
- Loads the NPC server.
- Loads server objects.
- Applies command-line/env overrides to `serverip`, `serverport`, `localip`, and `serverinterface`.
- Initializes a TCP listen socket using `serverport` and optional bind interface.
- Starts UPnP forwarding if enabled at compile time and config allows it.
- Registers the server socket with the socket manager.
- Starts timers:
  - `m_timedEvents`: 1 second
  - `m_timedNWTime`: 5 seconds
  - `m_timedSave`: 1 minute
  - `m_timedMaintenance`: 5 minutes

Shutdown sends `PLO_DISCMESSAGE` with `Server is shutting down.`, cleans up players, saves server flags, saves NPCs, saves translations, saves weapons, clears registries, and disconnects sockets.

## Main Loop and Timing

Source files:

- `server/src/Server.cpp`
- `server/src/player/PlayerClient.cpp`

`Server::doMain`:

- Calls `m_sockManager.update(0, 5000)`, documented as a 5 ms wait.
- Updates frame timestamps using both `precise_clock::now()` and `currentTime()`.
- Updates the NPC server with the high-precision frame time.
- Updates all timeout generators.
- Runs `level->doFrameEvents(...)` for each loaded level.
- Executes delayed scheduled tasks by subtracting elapsed frame duration.

`Server::doTimedEvents`, every 1 second:

- Updates server/world file-system watchers.
- Updates guild manager.
- Runs server list timed events.
- Calls `player->doTimedEvents()` for all non-NPC-server players.
- Deletes players whose timed event returns false.
- Runs `level->doTimedEvents()` for all loaded levels.

`PlayerClient::doTimedEvents`, every 1 second:

- Increments `account.onlineSeconds`.
- Disconnects after 300 seconds with no data.
- Optionally disconnects idle clients using `disconnectifnotmoved` and `maxnomovement`.
- Applies AP regeneration if enabled and not paused/in a sparring zone.
- Runs singleplayer level timed events.
- Saves client account every 300 seconds.
- Resets invalid packet count every 60 seconds.

New World time:

- `Server::calculateNWTime` computes `(time(nullptr) - 981048814) / 5`.
- `PLO_NEWWORLDTIME` is sent every 5 seconds with `writeGInt4(getNWTime())`.

## Network and Session Flow

Source files:

- `server/src/Server.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerLogin.cpp`
- `server/src/player/PlayerClient.cpp`

Accept flow:

- `Server::onRecv` accepts a new socket.
- A `PlayerLogin` is created and added to the player list.
- The first login packet is handled by `PlayerLogin::msgLoginPacket`.
- The first byte is read with `readGChar`, then converted to a type bit mask with `(1 << value)`.
- The temporary login object is swapped into the correct subclass.

Login session type handling:

- Client types create `PlayerClientOriginal` or `PlayerClient`.
- RC types create `PlayerRC`.
- NC types create `PlayerNC`.
- NPC-server type creates `PlayerNPCServer`.
- Unknown types log and fail the login.

`PlayerClient::handleLogin`:

- Reads connection IP.
- Reads client type again from the packet.
- Selects encryption generation:
  - `PLTYPE_CLIENT`: `ENCRYPT_GEN_2`
  - `PLTYPE_CLIENT2`: `ENCRYPT_GEN_4`
  - `PLTYPE_CLIENT3`: `ENCRYPT_GEN_5`
  - `PLTYPE_WEB`: `ENCRYPT_GEN_1`, file queue also uses gen 1
- Older `PLTYPE_CLIENT` reads an 8-byte version directly.
- Newer clients read an encryption key, reset encryption, then read an 8-byte version.
- Reads account and password as length-prefixed strings.
- Reads identity as the remainder of the packet.
- Checks allowed versions.
- Checks max players.
- Requires the server list to be connected before normal login verification.
- Sends verification to the server list.

`Player::sendLogin` applies account loading and generic checks:

- Loads account through `FlatFileAccountLoader`.
- Rejects IP bans unless account has `PLPERM_MODIFYSTAFFACCOUNT`.
- Rejects banned accounts unless account has `PLPERM_MODIFYSTAFFACCOUNT`.
- RC/NC must be staff and pass admin IP checks.
- Clients must pass `onlystaff` and admin IP checks.
- Sends `PLO_SIGNATURE` with value `73`.
- For login server names, sends `PLO_DISABLECLASSICMODE` and `PLO_GHOSTICON`.
- For clients, sends `PLO_HASNPCSERVER` if applicable and `PLO_UNKNOWN168`.
- Rejects duplicate login of the same account and session kind unless the old session is stale for more than 30 seconds.

## Packet and Protocol Behavior

Source files:

- `server/src/player/Player.cpp`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/packets/PlayerClientPackets.cpp`
- `server/src/player/packets/PlayerRCPackets.cpp`
- `server/src/player/packets/PlayerNCPackets.cpp`
- `server/include/network/IPacketHandler.h`
- Missing authoritative dependency headers: `IEnums.h`, `CString.h`, `CEncryption.h`, `CFileQueue.h`

Observed C++ rules:

- Outbound packets are built with `CString() >> (char)PLO_*`.
- `Player::sendPacket` appends `'\n'` unless told not to or the packet already ends in newline.
- File payloads and raw data deliberately bypass ordinary newline handling in places.
- `PLO_RAWDATA` announces the size of following raw packet/file data.
- Unknown player packets go through `Player::msgPLI_NULL`, increment `InvalidPackets`, and disconnect after more than 5 invalid packets with `Disconnected for sending invalid packets.`
- `PLI_PACKETCOUNT` compares the client count to `PacketCount` and logs a warning when mismatched or greater than 10000, then resets `PacketCount` to 0.
- Packet logging names many outbound packets under `FOR_OUTPUT_PACKETS`; those names are useful inventory but numeric IDs still require `IEnums.h`.

Important packet handlers implemented for game clients:

- Level and board: `PLI_LEVELWARP`, `PLI_LEVELWARPMOD`, `PLI_BOARDMODIFY`, `PLI_REQUESTUPDATEBOARD`, `PLI_ADJACENTLEVEL`
- Player state: `PLI_PLAYERPROPS`, `PLI_LANGUAGE`, `PLI_PACKETCOUNT`
- NPCs: `PLI_NPCPROPS`, `PLI_PUTNPC`, `PLI_NPCDEL`, `PLI_TRIGGERACTION`
- Objects/combat: `PLI_BOMBADD`, `PLI_BOMBDEL`, `PLI_ARROWADD`, `PLI_FIRESPY`, `PLI_THROWCARRIED`, `PLI_HURTPLAYER`, `PLI_EXPLOSION`, `PLI_HITOBJECTS`, `PLI_SHOOT`, `PLI_SHOOT2`
- Items: `PLI_ITEMADD`, `PLI_ITEMDEL`, `PLI_ITEMTAKE`, `PLI_OPENCHEST`
- Baddies: `PLI_BADDYPROPS`, `PLI_BADDYHURT`, `PLI_BADDYADD`
- Weapons/files: `PLI_WEAPONADD`, `PLI_NPCWEAPONDEL`, `PLI_WANTFILE`, `PLI_UPDATEFILE`, `PLI_VERIFYWANTSEND`, `PLI_UPDATEGANI`, `PLI_UPDATESCRIPT`, `PLI_UPDATECLASS`, `PLI_UPDATEPACKAGEREQUESTFILE`
- Social/profile: `PLI_TOALL`, `PLI_PRIVATEMESSAGE`, `PLI_PROFILEGET`, `PLI_PROFILESET`, `PLI_REQUESTTEXT`, `PLI_SENDTEXT`
- Misc: `PLI_SERVERWARP`, `PLI_PROCESSLIST`, `PLI_ENTERLEVEL`, `PLI_TAMPERCHECK`

Early C# task: recover or reconstruct authoritative enum and codec definitions from the C++ dependency source before implementing protocol code.

## Core Models and Game State

Source files:

- `server/include/Account.h`
- `server/include/object/Character.h`
- `server/include/object/Player.h`
- `server/include/object/NPC.h`
- `server/include/object/Weapon.h`
- `server/include/player/PlayerProps.h`
- `server/src/player/PlayerProps.cpp`
- `server/include/level/*.h`

Player/account state includes:

- Account name and community name.
- Current level, local pixel position, GMAP map coordinates, Z position.
- Nick, head/body/sword/shield images, gani, sprite/direction, colors, 30 gani attributes.
- HP/max HP, AP/AP counter, MP, gralats, arrows, bombs, glove/sword/shield/bomb/bow powers.
- Kills, deaths, ELO rating/deviation, last spar time.
- Weapons, chests, player flags, ban/admin metadata, folder rights.

`PlayerProp` is explicitly defined in C++ from 0 through 83. The IDs must be preserved. Important examples:

- `NICKNAME = 0`
- `MAXPOWER = 1`
- `CURPOWER = 2`
- `RUPEESCOUNT = 3`
- `GANI = 10`
- `HEADGIF = 11`
- `SPRITE = 17`
- `STATUS = 18`
- `CURLEVEL = 20`
- `ALIGNMENT = 32`
- `ACCOUNTNAME = 34`
- `BODYIMG = 35`
- `RATING = 36`
- `GMAPLEVELX = 43`
- `GMAPLEVELY = 44`
- `X2 = 78`
- `Y2 = 79`
- `Z2 = 80`
- `COMMUNITYNAME = 82`
- `UNKNOWN83 = 83`

Property serialization and limits are behavior-critical. The future port must preserve:

- Property IDs and order.
- Which props are sent on login to self, nearby clients, RC, and NC.
- Which props are forwarded to all players, nearby players, or only the source.
- Generation-specific filtering: `ServerGeneration::ORIGINAL` does not send properties above `RATING`.
- Restrictions when an NPC server is present: many client-set props are ignored unless set by the server.

## Gameplay Systems

Source files:

- `server/src/player/packets/PlayerClientPackets.cpp`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/level/Level.cpp`
- `server/src/level/LevelBaddy.cpp`
- `server/src/level/LevelItem.cpp`
- `server/src/object/NPC.cpp`
- `server/src/object/Weapon.cpp`

Important preserved behaviors:

- Board modification may drop items when specific old tiles are destroyed.
- Drop tiles include `0x002`, `0x1a4`, `0x1ff`, `0x7ff`, `0x3ff`, `0x5d9`, `0x34f`.
- Vase tile `0x2ac` drops a heart when `vasesdrop` is enabled.
- Older clients before `CLVER_2_1` drop destroyed-object items client-side.
- Item dropping can be disabled; when disabled the server sends `PLO_ITEMDEL` and may restore item inventory.
- `itemdropevents` can redirect drops to the Control-NPC and suppress actual client item drops.
- Death drops use `mindeathgralats`, `maxdeathgralats`, `deathitemtypes`, `dropitemsdead`, random offsets, and allowed gralat denominations.
- AP system increments over time and decreases on PK according to legacy thresholds.
- Spar rating uses the C++ Glicko-like formula and caps ratings to 0-4000 and deviation to 50-350.
- `CURPOWER` healing is blocked when AP is below 40 and power delta is positive.
- Dead-to-alive status transition heals to 3 hearts under 20 AP, 5 hearts under 40 AP, otherwise max HP.
- Movement props clear paused status and trigger link/sign/NPC touch tests.
- `lockplayerz` forces Z prop changes to be rejected and echoed back from latest server state.
- NPC carry behavior has special GMAP restrictions and timing for thrown NPC events.
- Shoot packets have v1/v2 forms. Server chooses `PLO_SHOOT` for clients below `CLVER_5_07` and `PLO_SHOOT2` for newer clients.

## Level, Map, and File Behavior

Source files:

- `server/src/loader/LevelLoader.cpp`
- `server/src/level/Level.cpp`
- `server/src/level/Map.cpp`
- `server/include/level/*.h`
- `bin/servers/default/world/*`

Supported level formats:

- Zelda binary: `Z3-V1.03`, `Z3-V1.04`
- Graal binary: `GR-V1.00`, `GR-V1.01`, `GR-V1.02`, `GR-V1.03`, `GR-V1.05`
- NW text: `GLEVNW01`
- Web level: `GWEBL001` is mentioned in comments and needs further verification
- Server level save: `GSERVL01` is mentioned in comments and needs further verification

Level details:

- Levels are 64x64 tiles.
- Binary Graal tile encodings use 12-bit or 13-bit codes with RLE control codes.
- Additional layer empty tiles are represented as `0xFFFF`.
- NW `BOARD` rows use base64 tile pairs.
- Links preserve level names with spaces by joining all tokens before the final six link fields.
- Links are ignored if the target level is not present in the file system.
- Binary NPC scripts convert `\xa7` to newline.
- Sign loading differs between binary and NW formats.
- GMAPs are refused in `ServerGeneration::ORIGINAL`.
- GMAP level positions use 1024-pixel sublevel origins.

File sending:

- `Player::sendFile` tracks known files for clients.
- Missing file sends `PLO_FILESENDFAILED`.
- Files over 3 MB log a warning.
- Files over 32000 bytes use large-file framing where supported.
- Clients before 2.14 do not support large files.
- Clients before 2.1 do not receive mod time in file packets.
- `PLI_VERIFYWANTSEND` uses CRC32 except `.gupd` files force resend behavior.

## Database and Persistence Behavior

The C++ implementation uses flat files, not a relational database.

Source files:

- `server/include/Account.h`
- `server/src/loader/flatfile/FlatFileAccountLoader.cpp`
- `server/src/loader/flatfile/FlatFileNPCLoader.cpp`
- `server/src/Server.cpp`

Account file format:

- Header must be `GRACC001`.
- Missing account loads `accounts/defaultaccount.txt`.
- Missing non-load-only accounts are saved back as new account files.
- Guest account names are generated as `pc:{:6}` with legacy random logic and `communityName = guest`.
- Account saving writes CRLF line endings.
- Empty/default values are omitted for many fields.

Important account keys:

- `NAME`, `NICK`, `COMMUNITYNAME`, `LEVEL`, `GROUPNAME`
- `X`, `Y`, `Z`, `MAPX`, `MAPY`
- `MAXHP`, `HP`, `GRALATS`/`RUPEES`, `ARROWS`, `BOMBS`
- `GLOVEP`, `SWORDP`, `SHIELDP`, `BOMBP`, `BOWP`
- `BOW`, `HEAD`, `BODY`, `SWORD`, `SHIELD`, `COLORS`
- `SPRITE`, `STATUS`, `MP`, `AP`, `APCOUNTER`
- `ONSECS`, `KILLS`, `DEATHS`, `RATING`, `DEVIATION`, `LASTSPARTIME`
- `IP`, `LANGUAGE`, `PLATFORM`, `CODEPAGE`
- `FLAG`, `ATTR1` through `ATTR30`, `WEAPON`, `CHEST`
- `BANNED`, `BANREASON`, `BANLENGTH`, `COMMENTS`, `EMAIL`
- `LOCALRIGHTS`, `IPRANGE`, `LOADONLY`, `FOLDERRIGHT`, `LASTFOLDER`

Save timing:

- Player accounts save every 5 minutes while online.
- Player cleanup saves account unless `loadOnly`.
- Server flags save every minute and during cleanup.
- Guilds save every minute.
- NPCs save during cleanup.

## Configuration and Runtime Files

Source files:

- `server/src/Server.cpp`
- `server/include/Server.h`
- `bin/servers/default/config/*`

Loaded configuration:

- `config/serveroptions.txt`
- `config/adminconfig.txt`
- `config/allowedversions.txt`
- `config/foldersconfig.txt`
- `config/servermessage.html`
- `config/ipbans.txt`
- `config/rules.txt`
- `serverflags.txt`
- `guilds/*`
- `weapons/weapon*.txt`
- `npcs/npc*.txt`
- `scripts/*.txt`
- `translations/*`

Default cached settings in `ExternalServerCachedSettings` are part of behavior. Examples:

- `maxplayers = 128`
- `sleepwhennoplayers = true`
- `unstickmelevel = onlinestartlocal.nw`
- `unstickmex = 30.0`, `unstickmey = 30.5`, `unstickmetime = 30`
- `bushitems = true`, `vasesdrop = true`, `disableitemdropping = false`
- `syncdistancex = 192`, `syncdistancey = 192`
- `eventdistance = 64`, `triggerdistance = 10`
- `cropflags = true`
- `respawntime = 15`
- `disconnectifnotmoved = true`, `maxnomovement = 1200`
- `savelevels = false`, `levelsautosave = false`
- `defaultweapons = true`
- `heartlimit = 3`
- `apsystem = true`
- AP thresholds: 30, 90, 300, 600, 1200 seconds

File watchers hot-reload config, NPCs, classes, translations, weapons, levels, and update packages. The port should reproduce externally visible reload behavior after the base protocol works.

## Important Constants, Flags, and Magic Values

- New World time epoch: `981048814`, divided by 5.
- Listen update wait: 5 ms.
- Timed events: 1 second.
- Server flag/guild save: 1 minute.
- New World time broadcast: 5 seconds.
- Maintenance reload/unload pass: 5 minutes.
- Level unload default: 600 seconds after last player leaves.
- Player no-data timeout: 300 seconds.
- Player save interval: 300 seconds.
- Invalid packet disconnect threshold: more than 5 invalid packets.
- PM message limit: 1024 characters.
- Server signature byte: 73.
- File chunk threshold: 32000 bytes.
- Large-file warning: over 3 MB.
- Account format header: `GRACC001`.
- Level tile count: 64 * 64 = 4096.
- GMAP sublevel pixel span: 1024.
- Player bounding box: 48x48x48.
- Player collision box: offset 8,16 with size 32x32x48.
- `PLO_DISCMESSAGE` strings should be preserved verbatim.

## Differences Seen in Rust/Python References

Rust:

- Splits the project into crates for accounts, config, core, game, levels, network, protocol, resources, scripting, server, and storage.
- Good structural hint for C# project boundaries.
- Protocol code in outdated supporting ports is not authoritative. The missing C++ `gs2lib` headers have since been recovered, so future protocol work should follow recovered `external/gs2lib/` plus the original C++ server, not Rust/Python assumptions.
- Server loop and listserver integration are simplified compared to C++.

Python:

- Uses clear manager classes for combat, items, baddies, horse, RC, NC, filesystem, accounts, profiles, classes, weapons, and listserver.
- Good readability hint for subsystem inventory.
- Many handlers are stubs, simplified, or use different packet parsing from C++.
- Login flow is notably different and must not be used as canonical behavior.

## Unknowns and Risky Compatibility Areas

Highest priority unknowns:

- Recover authoritative `IEnums.h`, `CString.h`, `CEncryption.h`, `CFileQueue.h`, and related socket/codec sources.
- Verify exact PLI/PLO/PLTYPE/PLSTATUS/PLFLAG/CLVER/RCVER/NCVER numeric values.
- Verify exact GInt/GShort/GChar signed/unsigned behavior from `CString`.
- Verify encryption generations and file queue compression behavior.
- Verify packet splitting behavior around raw data, file transfer, and websocket support.
- Verify server list protocol packet IDs and login verification responses.

Risk areas:

- Version-specific client quirks (`CLVER_2_1`, `2.14`, `2.21-2.31`, `2.31`, `5.07`).
- NPC-server behavior and event ordering.
- Level cache invalidation and GMAP/bigmap transitions.
- Property forwarding destinations and old/new prop aliases.
- File-system hot reload behavior.
- Flat-file account defaults and omitted fields.
- Randomness differences for drops, guest IDs, and death item placement.
- Bugs intentionally preserved by comments, such as clientside zlib fix and Bomb/Bow capitalization cleanup.
