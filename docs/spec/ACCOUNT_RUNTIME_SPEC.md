# Account Runtime Completion Spec

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
  - `Account::reset`
  - `Account::loadAccount`
  - `Account::saveAccount`
  - `Account::setFlag`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::cleanup`
  - `Player::doTimedEvents`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
  - `Server::getPlayer(account, type)`

Rust/Python sources were not used as authority for this pass.

## Load Runtime Behavior

`Account::loadAccount(pAccount, ignoreNickname)` starts by assigning
`m_accountName = pAccount`, then searches the accounts filesystem with
`findi(pAccount + ".txt")`. If not found, it reads
`<serverPath>/accounts/defaultaccount.txt` and marks the load as default-backed.

The file must begin with trimmed `GRACC001`. On missing, unreadable, or malformed
content, C++ returns `false`.

The parser clears gani attributes, chests, flags, weapons, private-message
server list, and folder-right staging before applying fields. Confirmed parsed
fields are:

```txt
NAME ignored
NICK truncated to 223 unless ignoreNickname=true
COMMUNITYNAME parsed, then overridden after parse
LEVEL
X/Y/Z converted through setX/setY/setZ as float * 16 into int16
MAXHP clipped by min(heartlimit, 20)
HP clipped to 0..MAXHP
RUPEES
ANI truncated to 223
ARROWS/BOMBS/GLOVEP raw integer assignment into byte-sized fields
SHIELDP clipped by shieldlimit
SWORDP clipped by swordlimit, with negative lower bound only when healswords=true
BOWP/BOW
HEAD truncated to 123
BODY/SWORD/SHIELD truncated to 223
COLORS first five comma-separated values
SPRITE/STATUS/MP/AP/APCOUNTER/ONSECS/IP/LANGUAGE/KILLS/DEATHS
RATING/DEVIATION/LASTSPARTIME
FLAG
ATTR1..ATTR30
WEAPON/CHEST in file order
BANNED/BANREASON/BANLENGTH/COMMENTS/EMAIL/LOCALRIGHTS/IPRANGE/LOADONLY
FOLDERRIGHT/LASTFOLDER
```

`LANGUAGE` becomes `"English"` when the parsed value is empty. `IP` only assigns
when the current account IP is zero.

## Community Name And Guest Behavior

After parsing, C++ special-cases `pAccount.toLower() == "guest"`:

1. Set `m_isLoadOnly = true`.
2. Set `m_isGuest = true`.
3. Seed C `rand()` with `time(0)`.
4. Loop:
   - `v = (rand() * rand()) % 9999999`
   - candidate account is `"pc:" + CString(v).subString(0, 6)`
   - accept the candidate only when `Server::getPlayer(candidate, PLTYPE_ANYPLAYER)` returns null.
5. Set `m_accountName = m_communityName`.
6. Set `m_communityName = "guest"`.

`Server::getPlayer(account, type)` compares account names case-insensitively.

The C# port now implements the deterministic candidate-selection boundary with
`CandidateGuestIdentitySelector`: it converts supplied C++-style candidate
integers to `pc:` plus the first six decimal digits, skips active account-name
collisions with case-insensitive comparison, and reports blocked when candidates
are exhausted. The exact production RNG source remains blocked because C++ uses
implementation-dependent C `rand()`, `time(0)`, and signed integer multiplication.

## Default Account Creation

When `defaultaccount.txt` was used:

1. `startlevel` overrides level only if the setting exists.
2. `startx` overrides X only if the setting exists; default fallback for the
   setting read is `30.0`.
3. `starty` overrides Y only if the setting exists; default fallback is `30.5`.
4. If the loaded account is not load-only, C++ immediately calls `saveAccount()`
   and `accfs->addFile("accounts/" + pAccount + ".txt")`.

The C# production account-login boundary invokes the existing
`AccountSaveService.SaveCreatedDefaultAccount` when `AccountLoadService`
requests this source-confirmed side effect.

## Save Runtime Behavior

`Account::saveAccount()` returns `false` before serialization when
`m_isLoadOnly` is true. Otherwise, it builds a full `GRACC001\r\n` file and
attempts to write it. After serialization starts, the C++ return value is `true`
even if the disk write fails; write failure only logs to RC.

Field order is documented in `docs/spec/ACCOUNT_PERSISTENCE_SPEC.md` and locked
by tests. Important runtime ordering details:

- `ATTR1..ATTR30` are emitted in numeric order only when non-empty.
- `CHEST`, `WEAPON`, and `FOLDERRIGHT` are emitted in vector order.
- `FLAG` values come from `std::unordered_map` iteration and must not be
  treated as globally stable.
- `COMMUNITYNAME` is saved as `m_accountName`, not `m_communityName`.
- The writer uses `fileExistsAs(m_accountName + ".txt")` to preserve existing
  filename case.

## Save Timing

Confirmed C++ save entry points:

- `Player::cleanup` sends queued data, then when `m_id >= 0`, server exists, and
  `m_loaded`, it calls `saveAccount()` for clients that are not load-only.
- `Player::doTimedEvents` saves every five minutes only when
  `difftime(currTime, m_lastSave) > 300`, then sets `m_lastSave = currTime` and
  calls `saveAccount()` only for loaded clients that are not load-only.
- RC mutation handlers call `saveAccount()` after source-confirmed account edits;
  those mutation flows remain under RC/NC milestones.

The C# `PlayerTimedEventState` already models the strict `> 300` save gate and
the loaded/not-load-only condition as `PlayerTimedEventAction.SaveAccount`.
Concrete production repository invocation remains blocked until the continuous
host/session loop owns live account instances.

## C# Mapping

Implemented:

- `AccountFileParser` for confirmed `GRACC001` load fields and defaults.
- `AccountLoadService` for case-insensitive lookup, default-account fallback,
  start overrides, default save/add-file request, and guest-load marker.
- `AccountFileSerializer` for confirmed save field order and CRLF format.
- `AccountSaveService` for load-only refusal, case-preserved filename
  selection, write attempt reporting, and default-account add-file signalling.
- `CandidateGuestIdentitySelector` for the deterministic, source-confirmed
  part of guest `pc:` identity selection.
- `ProductionAccountLoginBoundary` wiring for account load/save, default-account
  creation side effects, staff/admin-IP mapping, and optional guest identity
  selection.

Blocked:

- Exact C `rand()`/`time(0)` guest candidate stream.
- Full production filesystem resync interaction around account writes.
- Exact `CString(float)` output for unusual float values.
- Treating `std::unordered_map` flag ordering as stable.
- Real continuous session repository save calls on cleanup/shutdown; timing
  actions are modeled, but the host loop is not fully wired.
