# Filesystem Resource Loading Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/include/FileSystem.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/FileSystem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`

## Filesystem Buckets

The C++ server owns seven filesystem buckets:

```txt
FS_ALL    = 0
FS_FILE   = 1
FS_LEVEL  = 2
FS_HEAD   = 3
FS_BODY   = 4
FS_SWORD  = 5
FS_SHIELD = 6
```

`Server::getFileSystemByType` maps folder config type names
case-insensitively using this exact list:

```txt
all, file, level, head, body, sword, shield
```

Unknown types are not added to a typed bucket, but the parsed directory/wildcard
is still added to `FS_ALL` because `Server::loadFolderConfig` unconditionally
calls `m_filesystem[0].addDir(dir, wildcard)` after the typed add attempt.

## FileSystem::addDir

Confirmed behavior:

- `addDir(dir, wildcard="*", forceRecursive=false)` returns immediately if the
  injected server pointer is null.
- Directory separators are normalized to the platform separator.
- If `dir` lacks a trailing slash/backslash, one is appended before separator
  normalization.
- The directory list stores `<serverPath><dir><wildcard>`.
- If the same directory-list entry already exists, C++ calls `resync()`.
- Otherwise it indexes the directory immediately.
- Recursive indexing is enabled when `forceRecursive` is true or the server
  setting `nofoldersconfig` is true.
- For recursive child directories, C++ calls `addDir(newDir, "*", true)`, so
  child directories use wildcard `*` rather than the parent wildcard.

The file map key is only the filename, not the relative path:

```cpp
m_fileList[file] = CString(path) << file;
```

This means later files with the same filename overwrite earlier indexed paths in
the same map.

## Lookup Semantics

Confirmed methods:

- `find(file)`: exact case-sensitive map lookup; returns empty string when
  missing.
- `findi(file)`: linear case-insensitive scan; returns the matched full path or
  empty string.
- `fileExistsAs(file)`: linear case-insensitive scan; returns the case-preserved
  indexed filename or empty string.
- `load(file)`: resolves through `find`; returns empty string when missing.
- `getModTime(file)`: resolves through `find`; returns `0` when missing or when
  `stat` fails.
- `getFileSize(file)`: resolves through `find`; returns `0` when missing or when
  `stat` fails.
- `resync()`: clears the file map and reloads each stored directory entry.

`getDirByExtension(extension)` scans directory-list entries and returns the
first directory path whose wildcard extension matches `extension`.

## Server::loadAllFolders

When `nofoldersconfig=true`, C++ clears all filesystem buckets and indexes only
`FS_ALL`:

```txt
world
sharefolder entries, comma-separated and trimmed
```

Because `nofoldersconfig` is true, these directories are indexed recursively.
Typed buckets such as `FS_LEVEL` remain empty in this mode.

## Server::loadFolderConfig

When `nofoldersconfig=false`, C++ clears all buckets and reads:

```txt
config/foldersconfig.txt
```

Each line is parsed as follows:

1. Remove everything from the first `#` onward.
2. Trim the line.
3. Skip empty lines.
4. Read the first space-delimited token as `type`.
5. Read the rest as `config`.
6. Trim both values.
7. Normalize path separators in `config`.
8. Split `config` at the final path separator.
9. Prefix the directory portion with `world/`.
10. Use the remainder as the wildcard.
11. Add the directory/wildcard to the typed bucket if `type` is known.
12. Always add the directory/wildcard to `FS_ALL`.

Examples:

```txt
level levels/*.nw -> FS_LEVEL and FS_ALL add dir world/levels/, wildcard *.nw
file images/*.png -> FS_FILE and FS_ALL add dir world/images/, wildcard *.png
LEVEL levels/*.nw -> accepted as level because type matching is case-insensitive
unknown levels/*.txt -> only FS_ALL is indexed
```

## Level Loading Integration

Existing C# `.nw` loading now uses the source-confirmed indexed filesystem
semantics:

- exact filename lookup
- missing file returns `LevelLoadStatus.Missing`
- extension/header format selection
- `.nw` static payload parsing
- `.graal`/`.zelda` detection as unsupported rather than guessed

The local-debug local diagnostic shell now builds its level index through
`ServerResourceFileSystems.LoadAllFolders(...)`, matching the C++ no-folder-config
directory setup more closely while still remaining an explicitly fake-auth
diagnostic shell.

## Implemented C# Boundary

`Preagonal.GServer.Game` now contains:

- `IndexedServerFileSystem`
- `ServerFileSystemKind`
- `ServerResourceFileSystems.LoadAllFolders`
- `ServerResourceFileSystems.LoadFolderConfig`
- `NwLevelFileLoader`

Tests cover:

- exact vs case-insensitive lookup helpers
- recursive child-directory wildcard `*`
- folder config comments/trimming
- typed bucket mapping
- `FS_ALL` mirroring
- case-insensitive filesystem type names
- no-folder-config world/sharefolder indexing into `FS_ALL`
- missing files and `.nw` loading through the indexed filesystem

## Remaining Unknowns

- `.graal`, `.zelda`, `.gmap`, package, and resource parsers remain blocked
  until dedicated source-confirmed fixtures are captured.
- Full production `Level::findLevel` cache ownership and map attachment remain
  blocked.
- `FileSystem::setModTime`, write/delete mutation behavior, RC file browser
  semantics, and package transfer remain future milestones.
- Exact `CString::match` wildcard edge cases beyond `*` and `?` should be
  fixture-tested before broader pattern use.
