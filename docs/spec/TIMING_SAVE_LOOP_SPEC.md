# Timing, Save Loop, and Production Lifecycle Spec

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
  - `Server::operator()`
  - `Server::doMain()`
  - `Server::doTimedEvents()`
  - `Server::cleanupDeletedPlayers()`
  - `Server::cleanup()`
  - `Server::calculateServerTime()`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
  - signal handlers
  - startup argument/env parsing
  - process shutdown
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
  - `ServerList::doTimedEvents()`
  - `ServerList::connectServer()`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::onRecv()`
  - `Player::onSend()`
  - `Player::doTimedEvents()`
- `external/gs2lib/include/CSocket.h`
- `external/gs2lib/src/CSocket.cpp`

Rust/Python references were not used for this milestone.

## Server Main Loop

`Server::operator()` sets `running = true`, then repeats this order:

1. `doMain()`
2. `cleanupDeletedPlayers()`
3. If `m_doRestart` is set:
   - clear `m_doRestart`
   - `cleanup()`
   - `init(overrides...)`
   - break the loop if `init` fails
4. If global `shutdownProgram` is set, set `running = false`
5. After the loop exits, call `cleanup()`

The C# milestone implements this as ordered timing/lifecycle actions, not as a real infinite host loop yet.

## `Server::doMain()`

Every loop iteration performs:

1. `m_sockManager.update(0, 5000)`
   - The second argument is 5000 microseconds, a 5 ms `select()` timeout.
2. Capture `currentTimer = high_resolution_clock::now()`.
3. If `V8NPCSERVER` is compiled, run script queue hooks before timed events:
   - `m_scriptEngine.runScripts(currentTimer)`
   - `m_gs2ScriptManager.runQueue()` is present but commented out in C++.
4. If `currentTimer - m_lastTimer >= 1000ms`:
   - set `m_lastTimer = currentTimer`
   - call `doTimedEvents()`

The one-second timer resets to the current tick time, not by adding exactly one second.

## `Server::doTimedEvents()` Order

When the one-second gate opens, the C++ order is:

1. `m_serverlist.doTimedEvents()`
2. Iterate `m_playerList`; for each non-NPCServer player:
   - call `player->doTimedEvents()`
   - if it returns false, call `deletePlayer(player)`
3. Iterate `m_levelList`, calling `level->doTimedEvents()`
4. Iterate `m_groupLevels`, calling `level->doTimedEvents()` for live weak references
5. Every 5 seconds:
   - `calculateServerTime()`
   - `sendPacketToAll(PLO_NEWWORLDTIME + GInt4(getNWTime()))`
6. Every 60 seconds:
   - `saveServerFlags()`
7. Every 180 seconds:
   - `m_filesystemAccounts.resync()`
   - resync every filesystem in `m_filesystem`
8. Every 300 seconds:
   - `loadAllowedVersions()`
   - `loadServerMessage()`
   - `loadIPBans()`
   - `saveWeapons()`
   - if `V8NPCSERVER`, `saveNpcs()`
   - prune empty instanced group levels

The C# `ServerTimingScheduler` preserves this order as source-confirmed action values.

## New World Time

`Server::calculateServerTime()` computes:

```txt
m_serverTime = ((unsigned int)time(nullptr) - 981048814) / 5
```

The broadcast packet is:

```txt
PLO_NEWWORLDTIME + GINT4(serverTime)
```

Confirmed golden example:

```txt
unixSeconds = 981048819
serverTime = 1
packet bytes = 4A 20 20 20 21
```

`0x4A` is `PLO_NEWWORLDTIME` encoded with `GChar`; `20 20 20 21` is `GINT4(1)`.

## Server-List Timed Events

`ServerList::doTimedEvents()` is called by `Server::doTimedEvents()` every second.

It sets `m_lastTimer = time(0)`, checks `getConnected()`, and if disconnected:

1. If `difftime(m_lastTimer, m_nextConnectionAttempt) >= 0`, call `connectServer()`.
2. If connect fails:
   - increment `m_connectionAttempts` if it is less than `8`
   - set wait time to `min(pow(2, m_connectionAttempts), 300)`
   - set `m_nextConnectionAttempt = m_lastTimer + waitTime + (rand() % 5)`
3. If connect succeeds:
   - set `m_connectionAttempts = 0`

The exact `rand()` sequence is not ported as production behavior yet; the C# boundary accepts an injectable jitter source and tests only the confirmed `0..4` second jitter window and backoff math.

## Player Timed Events

`Player::doTimedEvents()` uses `time(0)` and runs in this order:

1. If socket is null or disconnected:
   - `m_server->deletePlayer(shared_from_this())`
   - return false
2. If not a client, return true.
3. Increment `m_onlineTime`.
4. If setting `disconnectifnotmoved` is true:
   - `maxnomovement = settings.getInt("maxnomovement", 1200)`
   - if both movement and chat idle durations are strictly greater than `maxnomovement`:
     - log the inactivity disconnect
     - send `PLO_DISCMESSAGE` with `You have been disconnected due to inactivity.`
     - return false
5. If last received data is strictly older than 300 seconds:
   - log timeout
   - return false
6. AP system processing runs if `apsystem` is enabled and the current level is live.
7. Each singleplayer level owned by the player receives `level->doTimedEvents()`.
8. If last save is strictly older than 300 seconds:
   - set `m_lastSave = currTime`
   - if client, loaded, and not load-only, call `saveAccount()`
9. If the invalid packet reset timer is strictly older than 60 seconds:
   - set `m_last1m = currTime`
   - set `m_invalidPackets = 0`
10. `m_fileQueue.sendCompress()`
11. return true

The strict `>` comparisons are compatibility-significant.

## Socket Manager And Cleanup

`CSocketManager::update(sec, usec)`:

- builds read/write fd sets from registered stubs
- calls `select(fd_max + 1, ..., timeval)`
- calls `onRecv()` then `onSend()` when ready
- if either callback fails, calls `onUnregister()` and removes the stub
- appends newly registered stubs after processing

`Server::cleanup()` order is source-confirmed:

1. UPnP removal if compiled
2. `TS_Save()`
3. `saveServerFlags()`
4. if `V8NPCSERVER`, `saveNpcs()` and clear NPC server pointer
5. `player->cleanup()` for every player
6. clear player lists and reset player ID generator to `PLAYERID_INIT`
7. clear levels, maps, and group levels
8. clear NPC lists and reset NPC ID generator to `NPCID_INIT`
9. `saveWeapons()` and clear weapon list
10. clean up script engine if compiled
11. disconnect player socket
12. disconnect server-list socket
13. `m_sockManager.cleanup(false)`

The C# milestone records this order in docs and scheduler actions only. Destructive production cleanup will be wired after the concrete runtime repositories exist.

## Signals And Shutdown

`main.cpp` wires `SIGINT`, `SIGTERM`, `SIGBREAK`, and `SIGABRT` to `shutdownServer`.

`shutdownServer` logs a shutdown banner and sets global atomic `shutdownProgram = true`. The main server loop observes this flag after each loop iteration and exits cleanly through `cleanup()`.

## WebSocket, WolfSSL, And TLS

`CSocket` itself is a plain socket wrapper. Conditional WolfSSL/websocket behavior appears in:

- `Player::onRecv()`
- `Player::doMain()`
- `CString::sha1*`
- `CString::rc4_*`

When `WOLFSSL_ENABLED` is compiled and incoming player data contains an HTTP `GET /` request:

- If `Sec-WebSocket-Key:` is absent, the server sends a simple HTML `HTTP/1.1 200 OK` response and returns false.
- If the key is present:
  - sets `m_playerSock->webSocket = true`
  - appends GUID `258EAFA5-E914-47DA-95CA-C5AB0DC85B11`
  - SHA1 hashes the result
  - base64 encodes it
  - sends `HTTP/1.1 101 Switching Protocols` with `Sec-WebSocket-Protocol: binary`
  - clears the receive buffer and returns true

Websocket frame conversion is handled by helpers such as
`webSocketFixIncomingPacket`; that byte behavior is covered by the dedicated
WebSocket frame fixture pass. Production HTTP handshake/session integration
remains blocked until a dedicated websocket compatibility pass.

## C# Mapping

- `ServerTimingScheduler`
  - source-confirmed server loop and `doTimedEvents()` periodic job order
- `ServerHostLoop`
  - now calls `IServerHostRuntime.Initialize()` before entering its loop and
    applies shutdown cleanup when initialization fails or the loop exits.
- `PlayerTimedEventState`
  - source-confirmed early `Player::doTimedEvents()` timing gates and save/reset/file-queue actions
- `ServerListReconnectState`
  - source-confirmed list-server exponential reconnect backoff
- `NewWorldTimeClock`
  - source-confirmed epoch and five-second divisor
- `ServerTimingPackets.BuildNewWorldTime`
  - source-confirmed `PLO_NEWWORLDTIME + GINT4` packet construction

## Open Questions / Blockers

- Real production host loop wiring should wait until the runtime service boundaries for players, levels, server-list sockets, and file systems are concrete.
- `Server::cleanupDeletedPlayers()` V8 script-object retention behavior is documented but not implemented.
- AP system internals are scheduled by player timed events but belong to gameplay/runtime milestones.
- Websocket frame wrap/unwrap bytes are fixture-covered; production
  handshake/session integration is blocked pending a dedicated byte-level trace.
- TLS/WolfSSL build integration is not implemented in C#; only source-confirmed handshake facts are documented.
