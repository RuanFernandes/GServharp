# Production Startup Spec

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `external/gs2lib/src/CSettings.cpp`
- `external/gs2lib/include/CSettings.h`

`CSettings` is not implemented inside the original server tree. It is provided by the recovered `gs2lib` dependency and is canonical for settings parsing behavior.

## Startup Server Selection

`main.cpp` selects the server directory in this order:

1. Command-line or environment override.
2. Non-empty `startupserver.txt` in the GServer home path.
3. Exactly one directory under `servers/`.
4. Failure with `ERR_SETTINGS`.

When an override server is present, startup does not inspect `startupserver.txt` or `servers/`.

`startupserver.txt` is loaded as a raw `CString` and accepted when it is not empty. The C++ code does not trim the loaded value before assigning `overrideServer`.

## Command-Line And Environment Rules

`parseArgs` uses environment variables only when `USE_ENV` exists. In that mode, command-line arguments are ignored.

Confirmed command-line options:

- `-h`, `--help`: print help and abort startup.
- `-s`, `--server`: set override server.
- `-p`, `--port`: set override port only if an override server is already set.
- `--localip`: set local IP only if an override server is already set.
- `--serverip`: set list-server delivered IP only if an override server is already set.
- `--interface`: set bind interface only if an override server is already set.
- `--staff`: set staff account only if an override server is already set.
- `--name`: set server display name only if an override server is already set.

Confirmed environment variables when `USE_ENV` exists:

- `SERVER`
- `PORT`
- `LOCALIP`
- `SERVERIP`
- `INTERFACE`
- `STAFFACCOUNT`
- `SERVERNAME`

All non-`SERVER` environment overrides are ignored when `SERVER` is missing or empty.

## Server Path And Logs

`Server::Server` builds the server path as:

```txt
<homePath>/servers/<serverName>/
```

The constructor also derives log filenames under:

```txt
servers/<serverName>/logs/npclog.txt
servers/<serverName>/logs/rclog.txt
servers/<serverName>/logs/serverlog.txt
servers/<serverName>/logs/scriptlog.txt
```

`scriptlog.txt` is gated by `V8NPCSERVER`.

## Config Load Order

`Server::loadConfigFiles` performs this order:

1. `loadSettings`
2. `loadAdminSettings`
3. `loadAllowedVersions`
4. folder config enabled/disabled log
5. `loadFileSystem`
6. `loadServerFlags`
7. `loadServerMessage`
8. `loadIPBans`
9. `loadWeapons`
10. `loadClasses`
11. `loadMaps`
12. `loadNpcs` when `V8NPCSERVER`
13. `loadMapLevels`
14. `loadTranslations`
15. `loadWordFilter`

The C# milestone implements only the safe startup/settings boundary. Later milestones must port the runtime loaders in their own source-confirmed passes.

## Confirmed Config Files

- `config/serveroptions.txt`: loaded by `CSettings` with separator `=`.
- `config/adminconfig.txt`: loaded by `CSettings` with separator `=`.
- `config/allowedversions.txt`: loaded as raw text, comments removed, `\r`, tabs, and spaces removed, then split by newline.
- `config/foldersconfig.txt`: used by folder config when `nofoldersconfig` is false.
- `serverflags.txt`: loaded from server root and split by newline with carriage returns removed.
- `config/servermessage.html`: loaded, carriage returns removed, newlines replaced with spaces.
- `config/ipbans.txt`: split by newline with carriage returns removed.
- `config/rules.txt`: loaded into the word filter.

## CSettings Parsing Rules

Confirmed from `external/gs2lib/src/CSettings.cpp` and `external/gs2lib/include/CSettings.h`:

- Carriage returns are removed before parsing.
- Lines are split on `\n`.
- A final blank line is removed from the internal line list.
- A line whose first character is `#` is ignored.
- Blank lines and lines without the separator are ignored.
- Keys are lowercased before trimming.
- Key and value are trimmed.
- If a value contains additional separator characters, the remaining tokens are rejoined using the separator.
- Inline comments are handled by `CKey`: value text before `#` is trimmed, and text from the trimmed value length onward is stored as raw comment data for saving.
- Duplicate keys append `,<value>` when `fromRC` is false.
- Duplicate keys replace the value when `fromRC` is true.
- `getBool` returns true only for exact value `true` or `1`; case is not normalized.
- `getBool` default is true.
- `getFloat` default is `1.00`.
- `getInt` default is `1`.
- `getStr` default is an empty string.
- Missing files leave `isOpened()` false.

## Implemented C# Boundary

- `Preagonal.GServer.Persistence.Gs2Settings` implements the source-confirmed parsing and typed accessors above.
- `Preagonal.GServer.Persistence.ServerStartupCommandLine` implements confirmed CLI/environment override rules.
- `Preagonal.GServer.Persistence.ServerStartupResolver` implements override, `startupserver.txt`, and single-directory selection.
- `Preagonal.GServer.Persistence.ServerStartupLoader` loads `config/serveroptions.txt` and `config/adminconfig.txt` after a server root resolves.
- `src/Server/Program.cs` now reports production startup resolution and then stops before unported production runtime.

## Blocked Runtime

The C# production host intentionally does not start sockets, list-server auth, account login, file system runtime loading, or gameplay yet. Those are blocked until later milestones port `Server::init`, `ServerList`, account persistence, file system loading, and gameplay runtime behavior.
