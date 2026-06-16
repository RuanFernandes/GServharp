# Level Runtime Lookup And Map Ownership Spec

Status: source-confirmed documentation pass for Phase 5. This document covers
the original C++ runtime boundary around `Level::findLevel`, server level cache
ownership, filesystem lookup, map attachment, and load failure behavior. It does
not implement or redefine gameplay/runtime level simulation.

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Level.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Map.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Map.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/FileSystem.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/FileSystem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- Selected call sites in `NPC.cpp`, `Player.cpp`, and `PlayerRC.cpp`.

## Authoritative C++ Entry Points

`Level::findLevel(const CString& pLevelName, bool loadAbsolute = false)` is the
central cache lookup and lazy level load entry point.

`Server::getLevel(const std::string& pLevel)` is only a thin wrapper around
`Level::findLevel(pLevel)`, so it uses the default `loadAbsolute = false`.

`Player::warp`, `Player::setLevel`, `msgPLI_ADJACENTLEVEL`, NPC warp/reset code,
and RC reload handling call `Level::findLevel` directly. Several call sites pass
`m_server` or `server` as the second argument even though the signature expects a
`bool`. In C++, this non-null pointer coerces to `true`; the C# port must model
that call-site intent as `loadAbsolute = true` for equivalent paths, not as a
server dependency parameter.

Confirmed direct C++ call-site behavior:

- `Server::getLevel(pLevel)` calls `Level::findLevel(pLevel)` with
  `loadAbsolute = false`.
- `Player::warp`, `Player::setLevel`, `msgPLI_ADJACENTLEVEL`, NPC reset/warp,
  and RC level reload paths pass `m_server`/`server` as the second argument,
  which means `loadAbsolute = true` in the compiled C++ behavior.
- A commented-out old GMAP NPC loading block also shows the same historical
  `server` second argument, but commented code is not runtime behavior.

## Level Cache Behavior

The server owns a single process-wide `std::vector<std::shared_ptr<Level>>`
returned by `Server::getLevelList()`. `Level::findLevel` always searches this
vector before doing filesystem work.

Lookup is linear and case-insensitive:

1. Convert requested `pLevelName` to lowercase into local `levelName`.
2. Iterate `m_server->getLevelList()`.
3. Compare `it->getLevelName().toLower()` against `levelName`.
4. Return the first matching cached level.

Important compatibility details:

- Duplicate level names are not resolved. The C++ TODO explicitly notes that a
  duplicate will break on the first occurrence.
- Cache matching uses `getLevelName()`, not `m_actualLevelName` or full resolved
  path.
- No cache entry is appended if loading fails.
- `Level::createLevel` also appends a new level to the same vector, but it does
  not run filesystem lookup.
- `Level::clone` loads a new `Level` from the same `m_levelName`, but by itself
  does not append to the global server cache.

## Filesystem Lookup Selection

The effective filesystem depends on the `nofoldersconfig` setting:

- When `nofoldersconfig` is false or absent, level/map loads use typed buckets:
  `FS_LEVEL` for level files and GMAP files, `FS_FILE` for bigmap `.txt` maps.
- When `nofoldersconfig` is true, loads use the global filesystem returned by
  `getFileSystem()`.

`FileSystem::find(file)` is exact-name lookup in `m_fileList`; it does not do
case-insensitive fallback. `FileSystem::findi(file)` is the case-insensitive
variant, but `Level::findLevel`, `loadNW`, `loadGraal`, `loadZelda`,
`detectLevelType`, `Map::loadGMap`, and `Map::loadBigMap` use `find`.

`Server::loadAllFolders` clears every filesystem bucket, adds `world`, and then
adds each comma-separated `sharefolder` entry to the global bucket. With folder
config enabled, `Server::loadFolderConfig` clears all buckets, reads
`config/foldersconfig.txt`, adds each typed wildcard under `world/` to its
matching bucket, and also adds every configured directory to the global bucket.

## `loadAbsolute` Behavior

When `Level::findLevel` misses the cache and `loadAbsolute` is true:

1. Select the same effective level filesystem described above.
2. Call `fileSystem->find(pLevelName).trim().length()`.
3. If the result is empty, call `fileSystem->addFile(pLevelName)`.
4. Then call `fileSystem->addDir(getPath(pLevelName), "*", true)`.

This is a filesystem-index mutation step, not a direct file read. The following
level load still goes through `level->loadLevel(pLevelName)`. The C# port should
preserve this quirk for warp/RC/NPC paths that map to `loadAbsolute = true`.

Compatibility risks:

- If `pLevelName` has no path component, `getPath(pLevelName)` behavior must be
  confirmed before implementing production mutation.
- `addFile` stores the file by filename under the server path after normalizing
  path separators.
- `addDir` can recurse when `forceRecursive` is true or when `nofoldersconfig`
  is enabled.

## Level Load Dispatch

`Level::loadLevel` chooses the parser from the requested file extension:

- `.nw` calls `loadNW`.
- `.graal` calls `loadGraal`.
- `.zelda` calls `loadZelda`.
- Any other extension calls `detectLevelType`.

`detectLevelType` reads the first eight bytes via the effective level
filesystem and dispatches by signature:

- `GLEVNW01` => `loadNW`
- `GR-V1.03`, `GR-V1.02`, `GR-V1.01` => `loadGraal`
- `Z3-V1.04`, `Z3-V1.03` => `loadZelda`
- Anything else => load failure

The existing C# format-detection code already documents and tests this
signature boundary. This spec adds the runtime cache and filesystem behavior
around that parser dispatch.

## Per-Format Name And Failure Behavior

`.nw` path behavior:

- `loadNW` sets `m_actualLevelName = m_levelName = getFilename(pLevelName)`.
- `m_fileName = fileSystem->find(m_actualLevelName)`.
- `m_modTime = fileSystem->getModTime(m_actualLevelName)`.
- It reads tokenized file data from `m_fileName`.
- Empty file data returns false.

`.graal` and `.zelda` path behavior:

- `loadGraal` and `loadZelda` set
  `m_actualLevelName = m_levelName = pLevelName`.
- They resolve and stat exactly `pLevelName`.
- Missing or unreadable file data returns false.
- `loadZelda` re-dispatches to `loadGraal` when the first two file-version
  characters are `GR`, preserving the old client `.zelda`/`.graal` save quirk.

Load failure behavior from `Level::findLevel`:

- If `level->loadLevel(pLevelName)` returns false, `findLevel` returns null.
- The failed level is not added to `m_levelList`.
- Map attachment does not run.
- Call sites decide the client-visible result. For example, `Player::setLevel`
  sends `PLO_WARPFAILED` with the requested level name and returns false.

## Map Ownership And Attachment

The server owns maps in `m_server->getMapList()`.

`Level::findLevel` attaches a newly loaded level to the first loaded map that
claims it:

1. Iterate `m_server->getMapList()` in vector order.
2. Call `map->isLevelOnMap(levelName.text(), mx, my)` where `levelName` is the
   lowercased originally requested level name, not necessarily
   `level->getLevelName().toLower()`.
3. On first match, call `level->setMap(map, mx, my)` and stop searching.
4. Push the level into `m_levelList`.

This order matters. If multiple maps contain the same level, the first map in
`m_mapList` wins.

`Server::loadMaps` clears `m_mapList` and reloads configured maps from:

- `settings["gmaps"]`
- `settings["maps"]`
- `settings["groupmaps"]`

After reloading maps, `Server::loadMaps` refreshes map relationships for every
already-cached level:

1. Iterate cached levels in `m_levelList`.
2. Iterate maps in the new `m_mapList` order.
3. Match using `level->getLevelName().toLower().text()`.
4. On first match, call `level->setMap(map, mx, my)`.
5. If no map matches, clear the level map reference with `level->setMap({})`.

`Player::warp` also copies the destination level map weak reference into the
player's `m_pmap` before `setLevel` attempts to enter the level. Failed warp
paths may restore the previous level map or use the configured unstick level map.

## Map File Behavior

`Map::loadBigMap`:

- Uses `FS_FILE` when folder config is enabled, otherwise global filesystem.
- Resolves the requested map name with exact `find`.
- Returns false if the map file cannot be resolved.
- Parses non-empty trimmed rows.
- Uses `guntokenize().tokenize("\n", true)` to split row entries.
- Computes width as the largest row size after excluding trailing empty entries.
- Stores non-empty level names lowercased in both the flattened level list and
  `m_levels`.

`Map::loadGMap`:

- Uses `FS_LEVEL` when folder config is enabled, otherwise global filesystem.
- Resolves the requested GMAP name with exact `find`.
- Returns false if the GMAP file cannot be resolved.
- Parses `WIDTH`, `HEIGHT`, `LEVELNAMES`, `MAPIMG`, `MINIMAPIMG`,
  `NOAUTOMAPPING`, `LOADFULLMAP`, and `LOADATSTART`.
- `LEVELNAMES` entries are lowercased and stored by coordinate.
- `LOADFULLMAP` enables full preload.
- `LOADATSTART` disables full preload and records lowercased preload names.

`Map::loadMapLevels` preloads levels after server startup map loading:

- If `LOADFULLMAP` was set, every non-empty level in the map is loaded through
  `m_server->getLevel(levelName)`.
- Otherwise, if `LOADATSTART` entries exist, each preload level is loaded through
  `m_server->getLevel(level)`.
- Both paths assert that `getLevel` succeeds in the C++ code.

The pure C# map parser now exposes the confirmed metadata and preload selection
without invoking production level loading. See `LEVEL_MAP_FORMAT_SPEC.md`.

## C# Port Mapping

Implemented source-confirmed cache boundary:

- `RuntimeLevelCache`
- `RuntimeLevelMapBinding`
- `RuntimeLevel.SetMap(...)`
- `RuntimeLevel.MapX`
- `RuntimeLevel.MapY`

The C# cache keeps server-owned list semantics instead of dictionary semantics.
`FindOrLoad` returns the first cached case-insensitive match, appends only after
successful loader return, and does not append failed loads. `CreateLevel`
appends directly, matching `Level::createLevel`.

For `loadAbsolute`, the cache exposes the confirmed sequencing but keeps the
filesystem mutation behind callbacks: if the requested name is not already
indexed, the caller can perform the C++ `addFile`/`addDir` equivalent before the
level loader runs. Exact write-capable filesystem mutation remains blocked.

Map attachment is source-confirmed for the safe metadata boundary:

- new loads attach to the first matching map using the lowercased requested
  level name
- map replacement remaps every cached level by `level.LevelName.ToLower()`
- missing map matches clear the level map and reset coordinates to zero

Recommended structure for future production wiring:

- Keep a server-owned level cache service with vector/list semantics, preserving
  first-match behavior and case-insensitive cache lookup.
- Keep filesystem indexing separate from direct file IO so `loadAbsolute`
  mutation can be reproduced.
- Model the legacy second-argument call-site behavior explicitly with a named
  `loadAbsolute` parameter. Do not expose a misleading server-parameter overload.
- Keep map ownership as weak/session-safe references where possible, but preserve
  first-map-wins order and reload remapping semantics.
- Keep parser dispatch separate from cache lookup: lookup/cache should not know
  gameplay contents of `.nw`, `.graal`, or `.zelda`.

## Confirmed Blockers

- Production filesystem-backed `Level::findLevel` wiring is not implemented yet.
  The safe in-memory cache/lookup/map-remap boundary exists in
  `RuntimeLevelCache`.
- Write/delete filesystem mutation remains blocked beyond documented
  `loadAbsolute` index mutation callback sequencing.
- `.graal`, `.zelda`, BIGMAP, and GMAP pure parsers exist, but production
  settings/filesystem-driven load integration remains blocked.
- Horse, baddy, NPC runtime construction, scripting hooks, and map runtime
  gameplay remain blocked.
- Exact `getPath(pLevelName)` edge behavior for pathless `loadAbsolute` names
  should be fixture-tested before enabling write-capable filesystem mutation.
