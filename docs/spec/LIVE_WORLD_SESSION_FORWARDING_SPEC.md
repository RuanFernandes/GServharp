# Live World Session Forwarding Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/utilities/IdGenerator.h`

## Player Id Allocation

`Server` owns:

```cpp
IdGenerator<uint16_t> m_playerIdGenerator{ PLAYERID_INIT };
```

where `PLAYERID_INIT = 2`.

`IdGenerator<T>::getAvailableId()`:

1. If the free-id set is not empty, returns and removes the smallest free id.
2. Otherwise returns `m_nextId++`.

`Server::addPlayer(player, id)`:

- if `id == USHRT_MAX`, allocates from the id generator
- sets the player id
- stores the player in `m_playerList[id]`
- overwrites an existing map entry for the same id

The C# `RuntimeServer.AddPlayer(player)` now mirrors default allocation from id
`2` and reuses freed ids after deletion cleanup.

## Deferred Deletion

`Server::deletePlayer(player)`:

- returns true for null
- inserts the player into `m_deletedPlayers`
- on first insert, requests server-list deletion
- does not remove the player from `m_playerList` immediately

`cleanupDeletedPlayers()` later:

- frees the player id
- unregisters the socket
- erases the player from `m_playerList`
- calls player cleanup

The C# runtime preserves the deferred player-list removal and id reuse after
`CleanupDeletedPlayers()`. Server-list deletion and socket unregister are still
outside the minimal runtime model.

## Level Membership

`Level::addPlayer(id)` appends to a deque and returns the zero-based index.
`Level::removePlayer(id)` erases every matching id from the deque.
`Level::isPlayerLeader(id)` checks whether the deque front equals `id`.

The C# `RuntimeLevel` preserves these list semantics.

## Level-Area Forwarding

`Server::sendPacketToLevelArea(packet, player, exclude, sendIf)` is the
authoritative path for forwarding local player prop changes such as movement.

No map:

- iterate `level->getPlayers()` order
- skip excluded ids
- require `other->isClient()`
- apply optional predicate
- send the packet

With map:

- iterate server player-list container
- skip excluded ids
- require `other->isClient()`
- apply optional predicate
- require same map object
- group maps require matching group
- require `abs(otherMapX - senderMapX) < 2`
- require `abs(otherMapY - senderMapY) < 2`
- send the packet

The C# `LiveWorldForwardingSelector.SelectLevelAreaRecipients(...)` implements
this routing for explicit runtime player snapshots. No-map ordering follows
level membership order. Map ordering follows the current C# runtime server
collection order; exact C++ `std::unordered_map` iteration order remains a
compatibility risk until container behavior is golden-tested.

## Unordered Map Iteration Compatibility

`Server::m_playerList` is declared as:

```cpp
std::unordered_map<uint16_t, std::shared_ptr<Player>> m_playerList;
```

Several client-visible paths iterate this container directly:

- `sendPacketToAll`
- `sendPacketToLevelArea` when the source level/player is on a map
- `sendPacketToLevelOnlyGmapArea` when the source level/player is on a non-BIGMAP map
- `Player::leaveLevel` when sending existing same-level player props back to
  the leaving player
- multiple login, RC/NC, duplicate-session, and scripting-visible list paths

The C++ standard does not guarantee a portable `std::unordered_map` iteration
order. The original server's observed order may depend on compiler, standard
library implementation, bucket count, hash implementation, insertion/erase
history, and runtime player ids. Because the closed-source client observes
packet order, map-area recipient order must be treated as compatibility-risky.

Confirmed safe order boundaries:

- Same-level/no-map forwarding uses `Level::m_players`, a `std::deque<uint16_t>`.
  The C# port preserves this as level membership order.
- `sendPacketToOneLevel` also uses `Level::m_players`, so its implemented C#
  boundary preserves level membership order.
- `LevelEntryVisibilitySelector` no-map behavior keeps the C++ distinction where
  broadcasts are client-only but the joining player can receive props for
  non-client same-level entries.

Blocked order boundaries:

- GMAP/map-area forwarding order from `m_playerList`.
- `sendPacketToAll` order from `m_playerList`.
- player-list scans during login, leave-level, RC/NC display, scripting
  `server.players`, and duplicate-account/session handling.

C# implementation rule: do not replace these with sorted player-id order and
call it compatible. For now, docs and tests should state that map-area order is
not certified. A future certification pass needs a C++ fixture/capture harness
using the recovered original build toolchain and representative insertion/erase
history, then either reproduce the observed order or prove that the client does
not depend on it for that packet family.

## C++ Forwarding Function Matrix

The recovered C++ exposes several related packet-routing functions. They are
similar, but not interchangeable.

`Server::sendPacketToAll(packet, exclude)`:

- iterates `m_playerList`
- skips excluded ids
- skips only NPC-server players
- sends to clients, RC, NC, control clients, and other non-NPC-server sessions

`Server::sendPacketToLevelArea(packet, weak_ptr<Level>, exclude, sendIf)`:

- locks the level and returns if expired
- no map: iterates `level->getPlayers()` in deque order
- map: iterates `m_playerList`
- requires `other->isClient()`
- applies the optional `sendIf(Player*)` predicate when supplied
- with map, requires the same map object
- does not apply group-map group filtering in the level overload
- sends when `abs(otherMapX - sourceMapX) < 2` and
  `abs(otherMapY - sourceMapY) < 2`

`Server::sendPacketToLevelArea(packet, weak_ptr<Player>, exclude, sendIf)`:

- locks the player and current level, returning if either is missing
- no map: iterates `level->getPlayers()` in deque order
- map: iterates `m_playerList`
- requires `other->isClient()`
- applies the optional predicate
- requires the same map object
- if the map is a group map, requires matching `player->getGroup()`
- sends only inside the strict `< 2` tile-map coordinate window

`Server::sendPacketToLevelOnlyGmapArea(...)`:

- has both level and player overloads
- treats no-map and BIGMAP levels like ordinary same-level forwarding
- only uses map-area iteration for non-BIGMAP maps
- preserves the same optional predicate and client checks
- player overload applies group-map group filtering

`Server::sendPacketToOneLevel(packet, level, exclude)`:

- locks the level and returns if expired
- iterates only `level->getPlayers()` in deque order
- skips excluded ids
- requires `player->isClient()`
- does not inspect maps, groups, distance, or predicates

## Hidden-Client Rules

Hidden clients are identified by:

```cpp
bool Player::isHiddenClient() const { return (m_type & PLTYPE_NONITERABLE) != 0; }
```

The forwarding helpers above do not explicitly call `isHiddenClient()`. They
filter with `isClient()`, exclusion sets, optional predicates, map identity,
group, and distance.

Hidden-client behavior is confirmed at separate visibility/list boundaries:

- `Player::getLevel()` returns an empty level pointer for hidden clients.
- `Player::setLevel` checks `getLevel()` in several player-list visibility
  paths, so hidden clients disappear from those comparisons through the empty
  level result.
- V8 `server.players` skips `isHiddenClient()` players explicitly.

Compatibility implication: do not add a blanket hidden-client filter to
`sendPacketToLevelArea` or `sendPacketToOneLevel` unless a specific C++ call
site proves it. Hidden-client exclusion must be modeled at the same boundary as
the original C++ call site.

The C# runtime model exposes `RuntimePlayer.IsHiddenClient` so tests can lock
this boundary. Current forwarding tests prove the confirmed negative behavior:
already-built `sendPacketToOneLevel`/level-area style forwarding does not
exclude a hidden client solely because that flag is set.

## Confirmed Call Sites

`PlayerProps.cpp::setProps` forwards local property updates with:

```cpp
PLO_OTHERPLPROPS
GSHORT playerId
levelBuff/levelBuff2 ordered by sender precise-movement support
```

through `sendPacketToLevelArea(packet, shared_from_this(), { m_id })`.

Projectile forwarding uses the optional predicate to split client versions:

- `PLO_SHOOT` to clients with `version < CLVER_5_07`
- `PLO_SHOOT2` to clients with `version >= CLVER_5_07`

`Player::leaveLevel` first broadcasts the leaving player's
`PLPROP_JOINLEAVELVL = 0` through `sendPacketToLevelArea`, then separately
iterates `m_server->getPlayerList()` to send same-level existing-player props
back to the leaving player. That second loop uses `player->getLevel() !=
getLevel()` and therefore inherits the hidden-client `getLevel()` behavior.

`Player::msgPLI_SHOWIMG` forwards `PLO_SHOWIMG` through the player overload of
`sendPacketToLevelArea`, excluding the sender.

Many gameplay packets use `sendPacketToOneLevel` instead of map-area routing:
board modify, bombs, horses, arrows, firespy, carried throws, item add/delete,
baddy props, explosions, trigger actions, and similar single-level events.
Those packet families are not automatically equivalent to movement forwarding.

## Confirmed Packet Delivery Boundary

`LiveWorldSessionForwarder` is intentionally narrow:

- `ForwardConfirmedLevelAreaPacket(...)` forwards an already-built packet to the
  source-confirmed level-area recipients.
- `ForwardConfirmedOneLevelPacket(...)` forwards an already-built packet to the
  source-confirmed `sendPacketToOneLevel` recipients: level membership order,
  explicit exclude set only, client sessions only, no map/group/distance checks.
- `ApplyAndForwardConfirmedPlayerProps(...)` applies the confirmed incoming
  movement/player-prop subset, builds the confirmed `PLO_OTHERPLPROPS` movement
  packet, and forwards it to level-area recipients.

Confirmed forwarded player-prop subset:

- `PLPROP_X`
- `PLPROP_Y`
- `PLPROP_Z`
- `PLPROP_X2`
- `PLPROP_Y2`
- `PLPROP_Z2`
- `PLPROP_SPRITE`
- `PLPROP_CURLEVEL`
- `PLPROP_GANI`

Unsupported gameplay packet types are not forwarded through this boundary.

## Implemented C# Types

- `RuntimeUShortIdGenerator`
- `RuntimeServer`
- `RuntimeLevel`
- `RuntimePlayer`
- `LiveWorldForwardingSelector`
- `ILiveWorldSessionSink`
- `LiveWorldSessionForwarder`

## Tests

Tests cover:

- player id allocation starts at `2`
- smallest freed id is reused after deletion cleanup
- level list append/remove/leader behavior
- no-map level-area forwarding in level membership order
- map/group/distance filtering
- one-level forwarding in level membership order, with explicit exclusions,
  non-client filtering, and no map-area filtering
- hidden clients are not blanket-filtered by the forwarding helpers
- deleted players remain visible to forwarding until `CleanupDeletedPlayers`
  removes them from the server and level lists
- confirmed movement prop mutation and forwarded `PLO_OTHERPLPROPS` bytes

## Remaining Blockers

- Real socket/file-queue integration for live sessions.
- C++ `std::unordered_map` iteration order golden tests for map-area forwarding.
- Optional `sendIf` predicates beyond the confirmed client check.
- `sendPacketToAll`, `sendPacketToLevelOnlyGmapArea`, predicate-split
  projectile forwarding, and type-specific forwarding.
- Full gameplay packet dispatch.
- Server-list delete side effects during deferred cleanup.
- V8 NPC login/logout script hooks.
