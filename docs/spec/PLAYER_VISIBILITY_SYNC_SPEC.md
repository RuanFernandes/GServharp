# Player Visibility Sync Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Map.h`

## Level Entry Visibility

During modern `sendLevel`, player visibility sync happens after active-level and
NPC packets. It has two client-visible effects:

1. Existing nearby clients receive the joining player's `PLO_OTHERPLPROPS`.
2. The joining client receives each nearby existing player's `PLO_OTHERPLPROPS`.

Singleplayer levels skip both effects.

## Same-Level Filtering

When the level has no map, C++ uses the level player list:

- self id is excluded
- broadcast direction checks `other->isClient()`
- joining-player direction iterates level player ids and sends each other
  player's props

The packet-only C# boundary models this with `IsOnSameLevel` snapshots. The
minimal runtime selector now models the same branch with `RuntimeLevel`
membership and `RuntimeLevel.PlayerIds` order.

## Map Filtering

When the player has a map:

- self is excluded
- non-client players are skipped
- the other player must point at the same map object
- group maps require matching group
- `abs(otherX - selfX) < 2`
- `abs(otherY - selfY) < 2`

The packet-only C# boundary models the map object identity with `MapKey`. The
minimal runtime selector now models the same branch with `RuntimeMap` object
identity, group names, map coordinates, and `RuntimePlayerKind.Client`.

## Ordering

For the joining player's received props:

- map branch uses server player-list iteration order
- no-map branch uses level player-list order

The packet-only C# boundary preserves the caller-provided snapshot order. The
minimal runtime selector preserves `RuntimeLevel.PlayerIds` order for no-map
levels. The live forwarding boundary can now deliver confirmed packets to
session sinks using the same selector. Server-wide map iteration order remains a
compatibility risk until `std::unordered_map` ordering is golden-tested against
the original compiled server.

## Current Stop Point

After this sync, C++ `sendLevel` returns `true`. The C# session state
`LevelEntryPlayerPropsSynchronized` maps to this point and stops before live
runtime simulation, movement updates, combat, item interactions, NPC AI, and
scripting callbacks.

## Movement Prop Forwarding

`Player::setProps(... PLSETPROPS_FORWARD ...)` forwards local player property
changes through the same `Server::sendPacketToLevelArea` filtering used by
nearby level sync, excluding the sender id.

For the confirmed movement subset, C# can now build the forwarded packet body:

```txt
PLO_OTHERPLPROPS
GSHORT playerId
levelBuff2 then levelBuff for sender version >= CLVER_2_3
levelBuff then levelBuff2 for older sender versions
```

The implemented builder covers `X/Y/Z`, `X2/Y2/Z2`, `Sprite`, `CurrentLevel`,
and `Gani`. `LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps`
now applies these confirmed mutations and forwards the resulting
`PLO_OTHERPLPROPS` packet to live session sinks selected by the source-confirmed
level-area routing rules. Real sockets/file queues remain a production
integration blocker.

## Tests

`tests/Game.Tests/LevelEntryVisibilitySelectionTests.cs` covers:

- no-map same-level selection using level player-list order
- singleplayer levels skipping visibility sync
- GMAP/group-map filtering by client type, same map object, group, and
  `abs(delta) < 2`

`tests/Protocol.Tests/IncomingPlayerPropsParserTests.cs` covers:

- decoded incoming movement/player prop parsing
- stopping at the first unconfirmed property
- forwarded `PLO_OTHERPLPROPS` bytes for a precise-movement sender

`tests/Network.Tests/LiveWorldSessionForwardingTests.cs` covers:

- live level-area packet delivery in level membership order
- map/group/distance filtering
- confirmed movement prop mutation plus forwarded packet delivery
