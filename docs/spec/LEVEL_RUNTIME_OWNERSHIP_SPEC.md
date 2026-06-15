# Level Runtime Ownership Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Level.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`

## Confirmed C++ Behavior

`Server::addPlayer(player, id)` assigns the requested id when one is provided,
sets that id on the player, and stores the player in `m_playerList[id]`. The
assignment behaves like a map overwrite for an already-present id.

When no explicit id is supplied, C++ asks `m_playerIdGenerator` for the next
available id. The generator allocation details are not implemented in C# yet.

`Server::deletePlayer(player)` is deferred. A non-null player is inserted into
`m_deletedPlayers` and server-list deletion is requested. The player remains in
`m_playerList` until later cleanup.

During cleanup, deleted players leave their current level and are removed from
`m_playerList`.

`Level::addPlayer(id)` appends the id to `m_players`, a `std::deque<uint16_t>`,
and returns `m_players.size() - 1`.

`Level::removePlayer(id)` uses `std::erase(m_players, id)`, removing every
matching id from the deque rather than only the first occurrence.

`Level::isPlayerLeader(id)` returns true only when the deque is non-empty and
the first stored id equals `id`.

`Player::leaveLevel()` removes the player from the level, promotes the new
front player by sending `PLO_ISLEADER` when one exists, broadcasts join/leave
props to nearby players, and clears the current level pointer. The C# runtime
ownership model currently implements only the list ownership portion; packets
remain in the existing sendLevel/visibility boundaries.

## C# Mapping

Implemented source-confirmed ownership types:

- `RuntimeServer`
- `RuntimeLevel`
- `RuntimePlayer`
- `RuntimeMap`
- `RuntimePlayerKind`

The implementation keeps these deliberately small. They model ids, account
names, player type, current level, level player id order, deferred deletion, and
map/group metadata needed by the visibility boundary.

`RuntimePlayer.JoinLevel(level)` leaves the old level before appending the
player id to the new level. This maps to the confirmed C++ warp boundary where
the player leaves one level before entering another.

## Compatibility Risks

`Server::playerLoggedIn` also notifies the list server and may invoke a V8
`npc.playerlogin` hook. Those side effects are not implemented in the runtime
ownership model.

`Server::deletePlayer` has list-server side effects and cleanup timing tied to
the main server loop. The C# model only exposes the deferred remove semantics
needed by tests.

The global server player-list iteration order in C++ comes from the underlying
player-list container and must be validated again before live multi-session
visibility forwarding is implemented.

## Tests

`tests/GServ.Game.Tests/LevelRuntimeOwnershipTests.cs` covers:

- append order and returned index from `Level::addPlayer`
- all-matching-id erase behavior from `Level::removePlayer`
- leader detection from the front of the level player list
- requested-id assignment and overwrite behavior in `Server::addPlayer`
- deferred deletion until cleanup
- leaving the previous level before joining a new level
