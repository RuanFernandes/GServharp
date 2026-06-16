# Level Filesystem Loading Spec

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/FileSystem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/FileSystem.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`

## FileSystem Indexing

`FileSystem::addDir(dir, wildcard, forceRecursive)` normalizes path separators,
appends a path separator when missing, then indexes files under
`serverPath + dir + wildcard`.

The file map key is only the filename, not the relative path:

```cpp
m_fileList[file] = CString(path) << file;
```

`find(file)` is exact and case-sensitive because it calls `m_fileList.find`.
`findi(file)` scans the map and uses case-insensitive `comparei`. Normal
`Level::loadLevel` uses `find`, not `findi`.

`load(file)` resolves through `find(file)` and returns an empty `CString` when
the file is not indexed. `getModTime(file)` also resolves through `find(file)`
and returns `0` when missing or when `stat` fails.

## Directory Setup

When `nofoldersconfig=true`, C++ calls `Server::loadAllFolders` and indexes:

```txt
world
sharefolder entries, comma-separated
```

When `nofoldersconfig=false`, `Server::loadFolderConfig` reads
`config/foldersconfig.txt`. Each non-empty, non-comment line is parsed as:

```txt
type path-or-wildcard
```

The path is normalized, prefixed with `world/`, split into directory and
wildcard, added to the type-specific filesystem, and always also added to
`FS_ALL`.

## Level Loading Selection

`Level::loadLevel` dispatches by exact lowercase extension strings:

```txt
.nw    -> loadNW
.graal -> loadGraal
.zelda -> loadZelda
other  -> detectLevelType
```

`detectLevelType` reads the first eight bytes and accepts:

```txt
GLEVNW01 -> NW
GR-V1.03, GR-V1.02, GR-V1.01 -> Graal
Z3-V1.04, Z3-V1.03 -> Zelda
```

The C# boundary currently implements filesystem-backed loading only for `.nw`
snapshots. Known `.graal` and `.zelda` formats are detected and reported as
unsupported instead of being guessed.

## C# Status

Implemented:

- `IndexedServerFileSystem` read-only file index.
- `ServerFileSystemKind` for the confirmed `FS_ALL`, `FS_FILE`, `FS_LEVEL`,
  `FS_HEAD`, `FS_BODY`, `FS_SWORD`, and `FS_SHIELD` buckets.
- `ServerResourceFileSystems.LoadAllFolders(...)` for source-confirmed
  `nofoldersconfig=true` world/sharefolder indexing into `FS_ALL`.
- `ServerResourceFileSystems.LoadFolderConfig(...)` for source-confirmed
  `config/foldersconfig.txt` parsing, type mapping, `world/` prefixing, and
  `FS_ALL` mirroring.
- exact `Find` lookup by filename with ordinal case-sensitive keys.
- `FindInsensitive` and `FileExistsAs` helpers matching source-confirmed
  `findi`/`fileExistsAs` semantics for future callers.
- `Load`, `GetModTime`, `GetFileSize`, and `Resync` read-only boundaries.
- `NwLevelFileLoader` for indexed `.nw` files.
- `LoadedNwLevel.ToModernStaticPayload(...)` for board/layers/links/signs/chests.
- `ModernLevelPayload.FromNwStatic(...)` integration into the existing
  `SendLevelBoundary`.

Not implemented:

- production default server path discovery.
- `loadAbsolute` mutation from `Level::findLevel`.
- `.graal` and `.zelda` parsing.
- level cache insertion/map attachment from `Level::findLevel`.
- filesystem writes, `setModTime`, and RC file-browser mutation behavior.

See `docs/spec/FILESYSTEM_RESOURCE_LOADING_SPEC.md` for the dedicated filesystem
resource loading spec.
