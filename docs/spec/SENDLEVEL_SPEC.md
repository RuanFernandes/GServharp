# sendLevel Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `external/gs2lib/include/IEnums.h`
- `external/gs2lib/include/CString.h`

## Entry From setLevel

After `Player::setLevel` resolves the level, optionally sends
`PLO_PLAYERWARP`/`PLO_PLAYERWARP2`, and updates `m_levelName`, it calls:

```cpp
if (m_versionId >= CLVER_2_1)
    succeed = sendLevel(newLevel, modTime, false);
else
    succeed = sendLevel141(newLevel, modTime, false);
```

The current C# milestone implements the modern `sendLevel` path for
`CLVER_2_1+` through nearby player property synchronization, then stops before
live runtime simulation.

Minimal runtime ownership is now available in `Preagonal.GServer.Game` to feed the
visibility-selection portion of this boundary. It does not replace the explicit
packet DTOs used by `SendLevelBoundary`; those DTOs remain the safe way to queue
only already-confirmed bytes.

## Modern sendLevel Order

`Player::sendLevel(pLevel, modTime, fromAdjacent)`:

1. Returns `false` when `pLevel == nullptr`.
2. Sends `PLO_LEVELNAME + pLevel->getLevelName()` immediately.
3. Reads `l_time = getCachedLevelModTime(pLevel.get())`.
4. If `modTime == -1`, replaces it with `pLevel->getModTime()`.
5. If `l_time == 0`:
   - If `modTime != pLevel->getModTime()`:
     - send `PLO_RAWDATA + GINT(1 + 64 * 64 * 2 + 1)`
     - send `pLevel->getBoardPacket()`
     - for each non-zero layer, send `PLO_RAWDATA + GINT(layer.length())`
       followed by `pLevel->getLayerPacket(layerId)`
   - send `PLO_LEVELMODTIME + GINT5(pLevel->getModTime())`
   - send `pLevel->getLinksPacket()`
   - send `pLevel->getSignsPacket(this)`
6. If `!fromAdjacent`, runtime packets begin:
   - board changes
   - chests
   - horses
   - baddies
7. Then GMAP correction, ghost icon, leadership, new world time, active level,
   NPC packets, nearby player props, and forwarding begin.

## Board And Layer Packets

`Level::getBoardPacket()` returns:

```txt
PLO_BOARDPACKET as GCHAR
4096 raw short tiles as direct `short[4096]` memory
"\n"
```

The raw-data header length sent before it is hardcoded to:

```txt
1 + (64 * 64 * 2) + 1 = 8194
```

`Level::getLayerPacket(layer)` returns:

```txt
PLO_BOARDLAYER as GCHAR
raw layer byte
raw 0
raw 0
raw 64
raw 64
4096 raw short tiles
"\n"
```

The raw-data header for layers uses `layer.length()`.

The C# `NwLevelPacketBuilder` now builds board/layer packets from parsed `.nw`
snapshots. Because C++ writes tile memory directly instead of using a portable
integer writer, C# emits raw little-endian tile bytes for the original x86/x64
server target.

## C# Boundary

Implemented:

- `ModernLevelPayload`
- `LevelLayerPayload`
- `LevelBoardChangePayload`
- `LevelChestPayload`
- `LevelHorsePayload`
- `LevelBaddyPayload`
- `LevelRuntimeContinuationPayload`
- `LevelEntryPlayerSyncPayload`
- `NearbyLevelPlayerSnapshot`
- `LevelEntryBroadcast`
- `SendLevelRequest`
- `SendLevelBoundary.BeginModern`
- `SessionLifecycle.LevelPayloadSent`
- `SessionLifecycle.DynamicLevelPayloadSent`
- `SessionLifecycle.LevelRuntimePacketsSent`
- `SessionLifecycle.LevelEntryPlayerPropsSynchronized`

The C# boundary queues:

- `PLO_LEVELNAME`
- optional raw board/layer payloads using pre-serialized bytes
- `PLO_LEVELMODTIME`
- pre-serialized links packet bytes
- pre-serialized signs packet bytes
- source-confirmed board-change packets
- source-confirmed chest packets
- horse packets using pre-serialized `LevelHorse::getHorseStr()` bytes
- baddy packets using pre-serialized `LevelBaddy::getProps(...)` bytes
- optional GMAP correction `PLO_LEVELNAME`
- `PLO_GHOSTICON + GCHAR(0)`
- conditional `PLO_ISLEADER`
- `PLO_NEWWORLDTIME + GINT4(server.getNWTime())`
- conditional `PLO_SETACTIVELEVEL`
- optional pre-serialized NPC packet bytes
- nearby player visibility sync using pre-serialized `PLO_OTHERPLPROPS` packets
- parsed `.nw` board/layer snapshots can feed the existing raw board/layer
  payload slots
- parsed `.nw` links/signs/chests can feed the existing links/signs/chest
  payload slots
- indexed filesystem `.nw` files can now flow through
  `NwLevelFileLoader.TryLoad(...)`, `LoadedNwLevel.ToModernStaticPayload(...)`,
  and `ModernLevelPayload.FromNwStatic(...)` into this boundary

The boundary stops after nearby player prop synchronization and before live
movement/gameplay simulation. Runtime data that C# cannot yet compute safely is
accepted only as already serialized/snapshotted input.

Not implemented:

- old `sendLevel141`
- `getCachedLevelModTime`
- production `Level::findLevel` cache/map ownership
- production `foldersconfig.txt` setup
- production horse and baddy runtime state
- production NPC packet construction
- production multi-session socket forwarding
