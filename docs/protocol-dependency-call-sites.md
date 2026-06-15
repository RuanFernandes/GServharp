# Protocol Dependency Call Sites

This file records where the external `gs2lib` protocol dependencies are used by the original C++ server. The dependency has been recovered under `external/gs2lib/`; the lists below remain the C++ source-of-truth call-site inventories for the original checkout.

## `IEnums.h`

Included by:

- `server/include/network/IPacketHandler.h`
- `server/include/object/Player.h`
- `server/include/Server.h`
- `server/src/animation/GameAni.cpp`
- `server/src/level/Level.cpp`
- `server/src/level/LevelBaddy.cpp`
- `server/src/level/LevelBoardChange.cpp`
- `server/src/level/LevelItem.cpp`
- `server/src/loader/flatfile/FlatFileAccountLoader.cpp`
- `server/src/misc/WordFilter.cpp`
- `server/src/npcserver/NPCServer.cpp`
- `server/src/npcserver/PlayerNPCServer.cpp`
- `server/src/object/NPC.cpp`
- `server/src/object/Weapon.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/PlayerClientOriginal.cpp`
- `server/src/player/PlayerExternalPlayers.cpp`
- `server/src/player/PlayerLogin.cpp`
- `server/src/player/PlayerNC.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/player/PlayerRC.cpp`
- `server/src/player/PlayerRequestText.cpp`
- `server/src/player/packets/PlayerClientPackets.cpp`
- `server/src/player/packets/PlayerNCPackets.cpp`
- `server/src/player/packets/PlayerRCPackets.cpp`
- `server/src/scripting/ScriptClass.cpp`
- `server/src/scripting/gs1/GS1Commands.cpp`
- `server/src/scripting/gs1/GS1Flags.cpp`
- `server/src/scripting/gs1/GS1Functions.cpp`
- `server/src/Server.cpp`
- `server/src/ServerList.cpp`
- `server/src/TriggerCommandHandlers.cpp`

Referenced symbol families:

- Client input packets: `PLI_*`
- Server output packets: `PLO_*`
- Server-list input/output packets: `SVI_*`, `SVO_*`
- Player session types: `PLTYPE_*`
- Client/RC/NC versions: `CLVER_*`, `RCVER_*`, `NCVER_*`
- Permissions, flags, and statuses: `PLPERM_*`, `PLFLAG_*`, `PLSTATUS_*`
- Compression/encryption constants: `COMPRESS_*`, `ENCRYPT_GEN_*`

Numeric values are not present in visible C++ call sites.

## `CString.h`

Included by:

- `Catch_tests/CString/CString_tests.cpp`
- `server/include/animation/GameAni.h`
- `server/include/level/Level.h`
- `server/include/level/LevelBaddy.h`
- `server/include/level/LevelBoardChange.h`
- `server/include/level/LevelHorse.h`
- `server/include/level/LevelItem.h`
- `server/include/level/LevelLink.h`
- `server/include/level/LevelSign.h`
- `server/include/level/LevelTiles.h`
- `server/include/misc/WordFilter.h`
- `server/include/network/IPacketHandler.h`
- `server/include/npcserver/PlayerNPCServer.h`
- `server/include/object/NPC.h`
- `server/include/object/Player.h`
- `server/include/object/ShowImg.h`
- `server/include/object/Weapon.h`
- `server/include/player/PlayerClient.h`
- `server/include/player/PlayerLogin.h`
- `server/include/player/PlayerNC.h`
- `server/include/player/PlayerRC.h`
- `server/include/scripting/ScriptClass.h`
- `server/include/Server.h`
- `server/include/ServerList.h`
- `server/include/utilities/Log.h`
- `server/include/utilities/PropertySerializers.h`
- `server/include/utilities/StringUtils.h`
- `server/src/animation/GameAni.cpp`
- `server/src/level/Level.cpp`
- `server/src/level/LevelBaddy.cpp`
- `server/src/level/LevelBoardChange.cpp`
- `server/src/level/LevelItem.cpp`
- `server/src/level/LevelLink.cpp`
- `server/src/level/LevelSign.cpp`
- `server/src/level/Map.cpp`
- `server/src/loader/flatfile/FlatFileAccountLoader.cpp`
- `server/src/loader/flatfile/FlatFileNPCLoader.cpp`
- `server/src/main.cpp`
- `server/src/misc/WordFilter.cpp`
- `server/src/npcserver/NPCServer.cpp`
- `server/src/npcserver/PlayerNPCServer.cpp`
- `server/src/object/NPC.cpp`
- `server/src/object/ShowImg.cpp`
- `server/src/object/Weapon.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/PlayerClientOriginal.cpp`
- `server/src/player/PlayerExternalPlayers.cpp`
- `server/src/player/PlayerLogin.cpp`
- `server/src/player/PlayerNC.cpp`
- `server/src/player/PlayerProps.cpp`
- `server/src/player/PlayerRC.cpp`
- `server/src/player/PlayerRequestText.cpp`
- `server/src/player/packets/PlayerClientPackets.cpp`
- `server/src/player/packets/PlayerNCPackets.cpp`
- `server/src/player/packets/PlayerRCPackets.cpp`
- `server/src/scripting/ScriptClass.cpp`
- `server/src/scripting/gs1/GS1Commands.cpp`
- `server/src/Server.cpp`
- `server/src/ServerList.cpp`
- `server/src/TriggerCommandHandlers.cpp`
- `server/src/UpdatePackage.cpp`
- `server/src/utilities/manager/TranslationManagerClassic.cpp`
- `server/src/utilities/PropertySerializers.cpp`
- `server/src/utilities/StringUtils.cpp`

Methods/operators used by the server or CString tests:

- Construction/conversion: `CString(...)`, assignment from strings/null, `toString`, `toStringView`, `text`
- Size/state: `length`, `isEmpty`, `bytesLeft`, `clear`
- Raw read/write: `read`, `write`, `readChars`, `writeChar`, `setRead`
- Graal integer codecs: `readGChar`, `readGUChar`, `readGShort`, `readGUShort`, `readGInt`, `readGUInt`, `readGUInt5`, `readGInto`, `writeGChar`, `writeGCharUnsafe`, `writeGShort`, `writeGInt`
- Raw integer read: `readShort`
- Packet/string parsing: `readString`, `tokenize`, `gCommaStrTokens`, `gtokenize`, `gtokenizeI`, `guntokenize`, `guntokenizeI`
- String mutation: `subString`, `left`, `right`, `trim`, `trimI`, `remove`, `removeI`, `replaceAll`, `find`, `findi`, `findl`, `starts_with`, `toLower`, `toUpper`, `comparei`
- Encoding/security/helpers: `escape`, `unescape`, `sha1I`, `base64encodeI`
- Compression/file helpers: `zuncompressI`, `bzuncompressI`, `load`, `save`, `loadToken`
- Operators: `operator <<` for raw/text append and `operator >>` for protocol writes

Behavior directly proven by visible C++:

- Packet IDs are printable-byte encoded because packet logging subtracts `32` from `pPacket[0]`.
- Login type is `1 << pPacket.readGChar()`.
- Many one-byte lengths are written as `(char)length` and then raw bytes.
- `CString` tests prove common string methods such as substring bounds, trim, case conversion, numeric constructors, append, escape/unescape round-trip, and case-insensitive comparison.

Still not proven without `CString.h`:

- exact byte order of `readShort`
- exact signed/unsigned behavior for all integer codecs
- exact operator-width mapping for `short`, `int`, and `long long`
- exact byte/string encoding and separator-missing behavior

## `CEncryption.h`

Included by:

- `server/include/network/IPacketHandler.h`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/PlayerNC.cpp`
- `server/src/player/PlayerRC.cpp`
- `server/src/ServerList.cpp`

Direct call sites:

- `IPacketHandler.h`: owns `CEncryption Encryption`.
- `IPacketHandler.h`: calls `Encryption.getGen()`, `Encryption.limitFromType(...)`, and `Encryption.decrypt(...)`.
- `PlayerClient.cpp`: calls `Encryption.setGen(...)`, `Encryption.reset(...)`, `Encryption.getGen()`, and uses the generation to set `m_fileQueue.setCodec(...)`.
- `PlayerRC.cpp`: same generation/key/file-queue pattern for RC.
- `PlayerNC.cpp`: same generation/key/file-queue pattern for NC, though visible logic currently accepts only `PLTYPE_NC`.
- `ServerList.cpp`: includes the header for `ENCRYPT_GEN_*` constants and file queue codec setup.

Proven flow:

- `ENCRYPT_GEN_1`: no inbound encryption/compression.
- `ENCRYPT_GEN_2`: zlib-compressed bundle, no decrypt call.
- `ENCRYPT_GEN_3`: zlib-compressed bundle, individual packets decrypted after newline splitting.
- `ENCRYPT_GEN_4`: `limitFromType(COMPRESS_BZ2)`, decrypt bundle, then BZ2 decompress.
- `ENCRYPT_GEN_5+`: first byte is compression type; remove it, `limitFromType(pType)`, decrypt bundle, then zlib/BZ2/no decompression depending on `pType`.

## `CFileQueue.h`

Included by:

- `server/include/object/Player.h`
- `server/include/ServerList.h`

Direct call sites:

- `Player` owns `CFileQueue m_fileQueue`.
- `ServerList` owns `CFileQueue m_fileQueue`.
- Constructors initialize file queues with socket pointers.
- `Player::sendPacket` adds newline as needed, then calls `m_fileQueue.addPacket(pPacket)`.
- `Player::cleanup`, `Player::onSend`, `Player::doTimedEvents`, and `Player::disconnect` call `sendCompress()`.
- `Player::onSend` and disconnected cleanup use `clearBuffers()`.
- `Player::canSend` delegates to `canSend()`.
- `PlayerClient::sendLogin` calls `m_fileQueue.sendCompress(true)` before warping.
- Client/RC/NC login calls `m_fileQueue.setCodec(...)` after encryption key setup where required.
- `ServerList::connectServer` clears buffers, sets generation 1 for registration, then generation 2 for later packets.
- `ServerList::sendPacket` appends newline, calls `addPacket`, and optionally `sendCompress()`.

Not proven:

- bundle length prefix byte order
- compression threshold and packet coalescing behavior
- exact send order when multiple packets are queued
- meaning of `sendCompress(true)`
- socket write retry/backpressure behavior

## `CSocket.h`

Included by:

- `server/include/npcserver/PlayerNPCServer.h`
- `server/include/object/Player.h`
- `server/include/player/PlayerClient.h`
- `server/include/player/PlayerClientOriginal.h`
- `server/include/player/PlayerLogin.h`
- `server/include/player/PlayerNC.h`
- `server/include/player/PlayerRC.h`
- `server/include/Server.h`
- `server/include/ServerList.h`
- `server/src/main.cpp`
- `server/src/npcserver/PlayerNPCServer.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerClient.cpp`
- `server/src/player/PlayerClientOriginal.cpp`
- `server/src/player/PlayerLogin.cpp`
- `server/src/Server.cpp`
- `server/src/ServerList.cpp`

Direct concepts and call sites:

- `Server`, `Player`, and `ServerList` derive from `CSocketStub`.
- `Server` owns listening `CSocket m_playerSock` and `CSocketManager m_sockManager`.
- `ServerList` owns client `CSocket m_socket`.
- `Player` owns accepted `CSocket* m_playerSock`.
- `CSocket::socketSystemDestroy()` is called during shutdown from `main.cpp`.
- Socket type/protocol/description are set with `setType`, `setProtocol`, `setDescription`.
- Server/list sockets use `init`, `connect`, `disconnect`, `accept`, `getHandle`, `getState`, `getData`, `sendData`, `getRemoteIp`, `getRemotePort`, and `getLocalIp`.
- `SOCKET_STATE_CONNECTED` and `SOCKET_STATE_DISCONNECTED` govern connection state checks.
- `SOCKET_TYPE_SERVER`, `SOCKET_TYPE_CLIENT`, and `SOCKET_PROTOCOL_TCP` are directly referenced.
- A public `webSocket` flag is checked/set during websocket handshake handling.

Not proven:

- socket state enum numeric values
- whether `connect()` on a server socket means bind/listen
- exact receive-buffer ownership and lifetime for `getData`
- socket manager polling behavior beyond visible `update(0, 5000)` calls
- websocket frame decoding behavior; `webSocketFixIncomingPacket` is called under `WOLFSSL_ENABLED`, but its implementation is not present
