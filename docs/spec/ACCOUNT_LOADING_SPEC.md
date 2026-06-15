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
controls subsequent logins and persistence. It is not implemented in C# yet.

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

## C# Boundary

The C# implementation now includes a pure `AccountFileParser` for confirmed
`GRACC001` content. It intentionally does not resolve account paths, fall back
to `defaultaccount.txt`, save newly created accounts, or perform guest RNG.

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

## Blockers

- Exact `FileSystem::findi`, path canonicalization, and account filesystem
  refresh behavior still need a dedicated persistence pass.
- Guest account randomization uses `srand(time(0))` and connected-player checks.
- Production account loading still needs default-account fallback and
  save-on-first-load side effects.
