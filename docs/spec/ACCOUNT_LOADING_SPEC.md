# Account Loading Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`

## File Discovery

`Account::loadAccount(pAccount, ignoreNickname)` begins by setting
`m_accountName = pAccount`. It searches the accounts filesystem for
`pAccount + ".txt"` using `findi`, which is case-insensitive in the server file
system layer. If no account file is found, it loads:

```txt
accounts/defaultaccount.txt
```

and marks `loadedFromDefault = true`.

The loaded file must be tokenized by newline and have first trimmed line
`GRACC001`; otherwise `loadAccount` returns `false`.

## Fields Parsed Before Login Props

The following fields directly feed login/player property serialization or
pre-world checks:

```txt
NICK, COMMUNITYNAME, LEVEL, X, Y, Z, MAXHP, HP, RUPEES, ANI, ARROWS, BOMBS,
GLOVEP, SHIELDP, SWORDP, BOWP, BOW, HEAD, BODY, SWORD, SHIELD, COLORS, SPRITE,
STATUS, MP, AP, APCOUNTER, ONSECS, IP, LANGUAGE, KILLS, DEATHS, RATING,
DEVIATION, LASTSPARTIME, ATTR1..ATTR30, WEAPON, CHEST, BANNED, BANREASON,
BANLENGTH, COMMENTS, EMAIL, LOCALRIGHTS, IPRANGE, LOADONLY, FOLDERRIGHT,
LASTFOLDER
```

`NICK` is truncated to 223 bytes/characters through `val.subString(0, 223)`
unless `ignoreNickname` is true. `COLORS` parses up to five comma-separated
numeric values. `ATTR1..ATTR30` map to the 30 gani attribute slots used by
`__attrPackets`.

`LANGUAGE` is set to `"English"` if the parsed value is empty.

## Default Account Behavior

When the account file is missing and `defaultaccount.txt` loaded successfully:

1. `startlevel`, `startx`, and `starty` settings override the loaded default
   account level/position when present.
2. If the account is not load-only, the C++ server immediately saves a new
   account file and adds `accounts/<pAccount>.txt` to the accounts filesystem.

This write-on-first-login behavior is client-visible indirectly because it
controls subsequent logins and persistence. The C# persistence boundary now
exposes the source-confirmed save/add-file side effect through
`AccountSaveService.SaveCreatedDefaultAccount`; production login code still needs
to decide when to invoke it from the account repository pipeline.

## Guest Behavior

For `pAccount.toLower() == "guest"`:

1. `m_isLoadOnly = true`
2. `m_isGuest = true`
3. A random `pc:` community account number is generated until no connected
   player has that name.
4. The code then sets `m_accountName = m_communityName` and
   `m_communityName = "guest"`.

This depends on server player-list state and RNG timing, so production behavior
remains blocked until the account/session repository boundary is implemented.

## Save Format

`saveAccount` writes `GRACC001\r\n`, then key/value lines in a fixed order.
It refuses to save when `m_isLoadOnly` is true. Attribute, chest, weapon, flag,
folder-right, and account-setting sections are appended after core character
fields.

See `docs/spec/ACCOUNT_PERSISTENCE_SPEC.md` for the full source-confirmed save
order. The important compatibility details are:

- `COMMUNITYNAME` is saved as the current account name.
- Non-empty `ATTR1..ATTR30` values are emitted in numeric order.
- Chests, weapons, and folder rights are emitted in vector/list order.
- Flags come from C++ `std::unordered_map` iteration and must not be assumed to
  have a globally stable order.
- `saveAccount` returns `false` only for load-only accounts; after a write
  attempt, it returns `true` even if the disk write failed and merely logs the
  failure.

## C# Boundary

The C# implementation now includes:

- a pure `AccountFileParser` for confirmed `GRACC001` content
- an `AccountLoadService` boundary that mirrors the confirmed first part of
  `Account::loadAccount`
- an `AccountFileSerializer` for source-confirmed `Account::saveAccount` text
  output
- an `AccountSaveService` boundary for case-preserved filename selection,
  source-confirmed write attempts, and default-account add-file signaling
- `IAccountFileSystem` and `IAccountLoadSettings` abstractions so production
  path lookup/settings can be added without inventing account persistence

Confirmed parser behavior:

- Reject empty files or files whose first trimmed line is not `GRACC001`.
- Preserve C++ member defaults from `Character.h` and `Account.h`.
- Trim each line before splitting the section from the value at the first space.
- Preserve case-sensitive section matching.
- Apply `NICK` 223-char truncation unless `ignoreNickname` is true.
- Apply image/gani truncation: head 123, body/sword/shield/gani 223.
- Store X/Y/Z as pixel coordinates by `floatValue * 16` truncated to `int16`.
- Clip `MAXHP`, `SHIELDP`, and `SWORDP` using confirmed settings defaults.
- Parse `FLAG name=value`; when `cropflags=true`, crop value to
  `223 - 1 - name.length`.
- Set non-guest `COMMUNITYNAME` to the account name after parsing, matching the
  C++ override near the end of `loadAccount`.

Confirmed resolver behavior:

- Look up `pAccount + ".txt"` through a case-insensitive account filesystem
  method matching `FileSystem::findi`.
- If lookup fails, read `<serverPath>/accounts/defaultaccount.txt` and mark the
  result as loaded from default.
- Reject missing/unreadable/malformed resolved content with no save/add
  side-effect request.
- When loaded from default, apply `startlevel`, `startx`, and `starty` settings
  only when each key exists, matching the guarded C++ checks.
- Convert `startx`/`starty` to internal pixel coordinates through the same
  `float * 16` truncation boundary used by `setX`/`setY`.
- When loaded from default and the parsed account is not `LOADONLY`, return a
  source-confirmed request to save the newly created account and add
  `accounts/<pAccount>.txt` to the account filesystem.
- Real disk writes are represented by `AccountSaveService` and tested behind
  `IAccountPersistenceFileSystem`. `ProductionAccountLoginBoundary` now invokes
  the source-confirmed default-account save/add-file side effect before running
  the pre-world `Player::sendLogin` continuation checks.
- For `guest`, force `IsLoadOnly = true` and mark guest identity generation as
  required. The random `pc:` name selection itself remains blocked on the
  connected-player repository and C++ RNG timing behavior.

## Blockers

- Exact production filesystem scan/resync behavior is still not implemented.
  The C# boundary models only the confirmed `findi` contract needed by account
  loading.
- Guest account randomization uses `srand(time(0))`, `(rand() * rand()) %
  9999999`, six-character truncation, and connected-player uniqueness checks.
  C# must not invent this until the player repository and RNG compatibility
  boundary are designed.
- Production account loading and saving now expose and wire the default-account
  save/add-file side effect into the account-login boundary. A complete live
  server still needs concrete filesystem/session host integration before this is
  exercised by a real socket listener.
- Exact `CString(float)` formatting for unusual float values remains open. Tests
  currently lock unambiguous values such as `30`, `30.5`, and `4.5`.
