# NW Level Format Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelLink.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelSign.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelBaddy.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/LevelTiles.h`
- `external/gs2lib/src/CString.cpp`

## File Loading Boundary

`Level::loadNW` loads the file with `CString::loadToken(m_fileName, "\n", true)`.
An empty loaded vector causes `loadNW` to return false. The first loaded line is
stored as `m_fileVersion`; the parser still iterates over all lines, including
the version line, but it is ignored because it does not match any known line
family.

Each parse pass tokenizes the current line with `CString::tokenize()` using the
default separator `" "` and `keepEmpty=false`. This maps to `strtok`, so repeated
spaces do not produce empty tokens.

Unknown line families are ignored.

## BOARD

Shape:

```txt
BOARD x y width layer tileData
```

Accepted only when token count is exactly 6.

Confirmed checks:

- `x` and `y` must be in `0..64`
- `width` must be greater than 0
- `x + width` must not exceed 64
- `tileData.length >= width * 2`

Each tile consumes two characters:

```txt
tile = (getBase64Position(left) << 6) + getBase64Position(top)
```

The base64 table is source-confirmed:

- `A..Z` -> `0..25`
- `a..z` -> `26..51`
- `0..9` -> `52..61`
- `+` -> `62`
- `/` -> `63`
- all other characters -> `0`

The C++ level tile storage defaults to zero for `new Level()`. `getBoardPacket`
later writes the `short[4096]` memory directly. For the original x86/x64 target,
the C# packet builder emits raw little-endian tile bytes.

## LINK

Shape:

```txt
LINK targetLevel x y width height newX newY
```

Accepted only when token count is at least 8. If the link has more than seven
fields after `LINK`, the extra leading tokens are joined back into the target
level name before coordinates are parsed.

`loadNW` only calls `addLink` if `fileSystem->find(level)` is non-empty. The C#
pure parser therefore requires an explicit `linkTargetExists` callback and skips
links when no callback is supplied.

## SIGN

Shape:

```txt
SIGN x y
...text lines...
SIGNEND
```

Accepted only when token count is exactly 3. C++ appends each body line plus
`\n` until `SIGNEND` or EOF, then constructs `LevelSign(x, y, text)`.

The C# parser preserves unformatted sign text with the same trailing newline
behavior. Client packet sign-code encoding is not implemented in this parser
milestone.

## NPC

Shape:

```txt
NPC image x y
...script lines...
NPCEND
```

Accepted when token count is at least 4. If the header has more than 4 tokens,
the extra middle tokens are joined into the image name; the last two tokens are
parsed as `x` and `y`.

C++ appends each script line plus `\n` until `NPCEND` or EOF, then calls
`m_server->addNPC(...)`. The C# parser only preserves image, position, and
script payload; it does not create or execute NPC runtime objects.

## BADDY

Shape:

```txt
BADDY x y type
...verse lines...
BADDYEND
```

Accepted only when token count is exactly 4. C++ calls `addBaddy`, then stores
each verse line until `BADDYEND` or EOF. The C# parser preserves x, y, type, and
verse strings only. Baddy AI, generated ids, property packets, drops, and
runtime timers remain out of scope.

## CHEST

Shape:

```txt
CHEST x y itemName signIndex
```

Accepted by C++ only when token count is exactly 5 and
`LevelItem::getItemId(itemName)` is not `INVALID`. The C# parser now accepts
only source-confirmed item names from `LevelItem.cpp`.

## C# Status

Implemented:

- `NwLevelParser`
- `NwLevelSnapshot`
- `NwLevelLink`
- `NwLevelSign`
- `NwLevelNpc`
- `NwLevelBaddy`
- `NwLevelChest`
- `LevelItemType`
- `LevelItemCatalog`
- `NwLevelPacketBuilder.BuildBoardPacket`
- `NwLevelPacketBuilder.BuildLayerPacket`
- `NwLevelPacketBuilder.BuildLinksPacket`
- `NwLevelPacketBuilder.BuildSignsPacket`
- `NwLevelPacketBuilder.BuildChestPacket`

Not implemented:

- NPC runtime creation or packet props
- baddy runtime ids/props/AI
- filesystem `find`, `findi`, `nofoldersconfig`, and mod-time integration
- `.zelda` parsing
- production `.graal`/`.zelda` filesystem/runtime wiring
