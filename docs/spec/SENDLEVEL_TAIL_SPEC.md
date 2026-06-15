# sendLevel Tail Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `external/gs2lib/include/IEnums.h`

## Tail Entry Point

This pass starts after modern `Player::sendLevel` has sent:

```txt
PLO_SETACTIVELEVEL
pLevel->getNpcsPacket(l_time, m_versionId)
```

The remaining modern C++ tail is:

```cpp
if (auto level = m_currentLevel.lock(); level && !level->isSingleplayer())
{
    m_server->sendPacketToLevelArea(
        this->getProps(__getLogin, sizeof(__getLogin) / sizeof(bool)),
        this->shared_from_this(),
        { m_id });

    if (auto map = m_pmap.lock(); map)
    {
        auto sgmap{ this->getMapPosition() };
        auto isGroupMap = map->isGroupMap();

        for (const auto& [otherid, other]: m_server->getPlayerList())
        {
            if (m_id == otherid) continue;
            if (!other->isClient()) continue;
            auto othermap = other->getMap().lock();
            if (!othermap || othermap != map) continue;
            if (isGroupMap && this->getGroup() != other->getGroup()) continue;
            auto ogmap{ other->getMapPosition() };
            if (abs(ogmap.first - sgmap.first) < 2 && abs(ogmap.second - sgmap.second) < 2)
                this->sendPacket(other->getProps(__getLogin, sizeof(__getLogin) / sizeof(bool)));
        }
    }
    else
    {
        for (auto otherid: level->getPlayers())
        {
            if (m_id == otherid) continue;
            auto other = m_server->getPlayer(otherid);
            this->sendPacket(other->getProps(__getLogin, sizeof(__getLogin) / sizeof(bool)));
        }
    }
}
```

After this block, modern `sendLevel` returns `true`. The current C# boundary
stops at `LevelEntryPlayerPropsSynchronized`, before live movement/gameplay
runtime updates.

## Packet Shape

`Player::getProps(__getLogin, count)` creates:

```txt
GCHAR PLO_OTHERPLPROPS
GSHORT playerId
if client and PLPROP_JOINLEAVELVL is enabled:
  GCHAR PLPROP_JOINLEAVELVL
  GCHAR 1
for each enabled prop id in ascending order except PLPROP_JOINLEAVELVL:
  GCHAR propId
  getProp(propId)
if m_isExternal:
  GCHAR 81
  raw "!"
```

`Player::sendPacket` appends a newline to this packet unless it already ends in
`\n`.

The C# implementation supports the source-confirmed `__getLogin` table and
`PLO_OTHERPLPROPS` wrapper. External-player `m_isExternal` handling is still
blocked until external player lifecycle is traced.

## Broadcast Direction

C++ sends the joining player's `__getLogin` packet to nearby existing clients
through `Server::sendPacketToLevelArea(player weak_ptr, exclude={self})`.

The C# skeleton has no real multi-session socket graph yet, so this pass returns
the would-be broadcasts as `LevelEntryBroadcast` records instead of sending to
other sessions.

## Joining Player Direction

C++ sends each nearby existing player's `__getLogin` packet directly to the
joining player's file queue. The C# boundary queues those prebuilt packets into
the current `ClientSessionSkeleton` outbound buffer.

## Filtering

Singleplayer levels skip the whole tail.

No map:

- broadcast self props to same-level client players, excluding self
- send same-level other player props to the joining player, excluding self

With map:

- iterate server player list order
- skip self
- skip non-clients
- require the same map object
- if the map is a group map, require matching group
- require `abs(otherMapX - selfMapX) < 2`
- require `abs(otherMapY - selfMapY) < 2`

For GMAP position, `Player::getMapPosition` uses `PLPROP_GMAPLEVELX` and
`PLPROP_GMAPLEVELY`. For big maps it uses the level map coordinates.

## C# Status

Implemented:

- `GetLoginPropertySet.All`
- `PlayerPropertySerializer.SerializeOtherPlayerPropsPayload`
- `PlayerPropertySerializer.BuildOtherPlayerPropsPacket`
- `LevelEntryPlayerSyncPayload`
- `NearbyLevelPlayerSnapshot`
- `LevelEntryBroadcast`
- no-map and map/group/distance filtering from explicit snapshots
- `SessionLifecycle.LevelEntryPlayerPropsSynchronized`

Not implemented:

- live server player list iteration/storage
- real other-session socket sends
- external-player `m_isExternal` suffix
- exact group-map cloning and lifecycle
- production current-level/player-list ownership
