# Graal Level Format Specification

Status: source-confirmed parser implemented for the static `.graal` file
payload boundary. Runtime side effects remain blocked.

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/level/Level.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelLink.cpp`
- `external/gs2lib/src/CString.cpp`

## File Selection And Header

`Level::loadLevel` calls `loadGraal` for literal `.graal` extension names.
`detectLevelType` also dispatches to `loadGraal` for these eight-byte headers:

- `GR-V1.01`
- `GR-V1.02`
- `GR-V1.03`

`loadGraal` itself additionally accepts:

- `GR-V1.00`

Version mapping in C++:

```txt
GR-V1.00 -> v = 0
GR-V1.01 -> v = 1
GR-V1.02 -> v = 2
GR-V1.03 -> v = 3
```

Unknown versions return false.

## Name And Filesystem Behavior

`loadGraal` selects `FS_LEVEL` unless `nofoldersconfig` is true. It sets:

```txt
m_actualLevelName = m_levelName = pLevelName
m_fileName = fileSystem->find(pLevelName)
m_modTime = fileSystem->getModTime(pLevelName)
```

Unlike `.nw`, it does not strip directory components with `getFilename`.

## Tile RLE

After the eight-byte header, the tile stream fills layer 0 until `4096` tiles
are written or file data runs out.

Bit width:

- `GR-V1.00`: 12-bit codes
- `GR-V1.01+`: 13-bit codes

Codes are read least-significant-bit first:

1. Accumulate bytes into `buffer += byte << read`.
2. Extract `buffer & mask`.
3. Shift `buffer >>= bits`.

Masks:

- 12-bit: tile mask `0x0fff`, control bit `0x0800`
- 13-bit: tile mask `0x1fff`, control bit `0x1000`

Control codes:

- `count = code & 0xff`
- `code & 0x100` enables double-repeat mode

Regular repeat:

```txt
{control count N}{tile T} -> T repeated N times
```

Double repeat:

```txt
{control 0x100 | N}{tile A}{tile B} -> A,B repeated N times
```

The double-repeat C++ loop stops while `boardIndex < 64 * 64 - 1`, preserving
the original off-by-one guard.

## Section Order

After tiles, sections are parsed in this exact order:

1. Links
2. Baddies
3. NPCs
4. Chests, only when `v > 0`
5. Signs

The parser does not have section tags. Empty lines or `#` sentinel lines end
several sections.

## Links

Each link line is read with `readString("\n")` until empty line or `#`.
The line is tokenized by spaces.

The level name begins at token 0. If the line has more than seven tokens, the
leading extra tokens are joined back into the target level name, matching
`LevelLink::parseLinkStr`.

`loadGraal` only calls `addLink` if `fileSystem->find(level)` is non-empty.
The C# pure parser therefore takes an explicit `linkTargetExists` callback and
skips links when the callback is absent or returns false.

## Baddies

Baddies are raw bytes, not Graal-packed chars:

```txt
{signed char x}{signed char y}{signed char type}{verses until newline}
```

The sentinel is:

```txt
0xff 0xff 0xff
```

When the sentinel appears, C++ reads one newline-terminated string for empty
verses and exits the baddy section.

Verse text is split on `\`. The C# parser preserves x, y, type, and verse
strings only; baddy ids, props, AI, drops, and timers remain runtime blockers.

## NPCs

NPC lines are read until empty line or `#`.

Line shape:

```txt
{GCHAR x}{GCHAR y}{image until #}{code rest}
```

The C++ loader replaces byte `0xa7` (`§`) in the code with `\n`, then creates a
runtime NPC via `m_server->addNPC(...)`.

The C# parser preserves image, x, y, and converted code text only. It does not
create or execute NPC runtime objects.

## Chests

Chest lines exist only for `GR-V1.01+`. Lines are read until empty line or `#`.

Line shape:

```txt
{GCHAR x}{GCHAR y}{GCHAR item}{GCHAR signIndex}
```

The item byte is cast directly to `LevelItemType`; unlike `.nw`, there is no
item-name lookup.

For `GR-V1.00`, the chest section is skipped entirely. Any bytes at that point
are parsed by the following sign section, exactly as the C++ section order
implies.

## Signs

Sign lines are read until an empty line:

```txt
{GCHAR x}{GCHAR y}{text rest}
```

The C++ passes `encoded = true` to `addSign`, so the parser preserves the
encoded text payload. Client packet sign-code translation remains outside this
parser milestone.

## C# Status

Implemented:

- `GraalLevelParser`
- version validation for `GR-V1.00` through `GR-V1.03`
- 12-bit and 13-bit LSB-first tile decoding
- regular and double-repeat RLE
- static link, baddy, NPC, chest, and sign payload preservation
- `GR-V1.00` chest-section skip behavior
- tests in `GraalLevelParserTests`

Blocked:

- Production filesystem/runtime loader wiring for `.graal`
- NPC runtime creation and script execution
- baddy ids, props, AI, drops, and timers
- sign text client translation beyond preserving encoded text
- write/save behavior
