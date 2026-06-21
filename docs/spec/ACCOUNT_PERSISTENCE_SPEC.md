# Account Persistence Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`

Supporting source:

- `external/gs2lib/include/CString.h` for string/numeric conversion behavior where
  already recovered.

## Save Entry Point

`Account::saveAccount()` is the authoritative account file writer. It has one
early failure case:

```cpp
if (m_isLoadOnly)
    return false;
```

After that, it builds the full account text in memory and attempts a disk write.
If `save(...)` fails, the C++ server logs an RC message but still returns
`true`. The C# boundary preserves this by reporting both the C++ return value and
the actual write result.

## Filename Selection

The writer asks the accounts filesystem for a case-preserved existing filename:

```cpp
accountFileName = m_filesystem->fileExistsAs(m_accountName + ".txt");
```

If no existing case-preserved name is found, it writes to:

```txt
<accountName>.txt
```

The final disk path is:

```txt
<serverPath>/accounts/<accountFileName>
```

When `loadAccount` created an account from `defaultaccount.txt`, the C++ server
also calls:

```cpp
accfs->addFile(CString() << "accounts/" << pAccount << ".txt");
```

The C# `AccountSaveService.SaveCreatedDefaultAccount` exposes this
source-confirmed side effect explicitly.

## Save Format

The file starts with `GRACC001\r\n`. Every emitted line uses `\r\n`, matching the
C++ `CString() << ... << "\r\n"` construction.

Core fields are written in this exact order:

```txt
GRACC001
NAME
NICK
COMMUNITYNAME
LEVEL
X
Y
Z
MAXHP
HP
RUPEES
ANI
ARROWS
BOMBS
GLOVEP
SHIELDP
SWORDP
BOWP
BOW
HEAD
BODY
SWORD
SHIELD
COLORS
SPRITE
STATUS
MP
AP
APCOUNTER
ONSECS
IP
LANGUAGE
KILLS
DEATHS
RATING
DEVIATION
LASTSPARTIME
ATTR1..ATTR30, only when non-empty
CHEST, in vector order
WEAPON, in vector order
FLAG, in `std::unordered_map` iteration order
blank line
BANNED
BANREASON
BANLENGTH
COMMENTS
EMAIL
LOCALRIGHTS
IPRANGE
LOADONLY
FOLDERRIGHT, in vector order
LASTFOLDER
```

`COMMUNITYNAME` is written as `m_accountName`, not the stored community-name
member. This oddity is source-confirmed and must remain compatible.

Flags are written as either:

```txt
FLAG name=value
FLAG name
```

depending on whether the value is empty. The original container is
`std::unordered_map`, so global flag order must not be treated as stable unless a
future compatibility test proves the exact compiled/runtime behavior.

## Implemented C# Boundary

`Preagonal.GServer.Persistence` now contains:

- `AccountFileParser`, a pure `GRACC001` parser for confirmed fields and C++
  defaults.
- `AccountLoadService`, a source-confirmed load/default-account resolver.
- `AccountFileSerializer`, a pure save-format serializer for confirmed
  `Account::saveAccount` field order.
- `AccountSaveService`, a filesystem side-effect boundary for case-preserved
  filename selection, disk write attempts, and default-account add-file
  signaling.

`Preagonal.GServer.Network` now contains `AccountLoginBoundary`, which loads the
server-list-approved account, maps confirmed account fields into the existing
`PlayerSendLoginAccount` snapshot, applies the default-account save/add-file
side effect when requested by `AccountLoadService`, and then runs the
source-confirmed pre-world `Player::sendLogin` continuation checks. Guest login
can proceed only when an explicit `IGuestIdentitySelector` supplies the
source-shaped `pc:` candidate stream; otherwise guest identity generation remains
blocked instead of faked.

Compatibility tests cover:

- exact CRLF output for a controlled representative account
- fixed save field order
- `LOADONLY` returning false before serialization/writes
- case-preserved account filename selection
- C++ true return value when the disk write fails after serialization
- default-account created-file `accounts/<account>.txt` add-file signaling
- production account-login wiring from `AccountLoadService` into
  `PlayerSendLoginContinuation`
- `LOCALRIGHTS` mapping to `PLPERM_MODIFYSTAFFACCOUNT`
- staff-list and `IPRANGE` wildcard mapping for pre-world login checks
- deterministic guest identity candidate truncation and active-name collision
  skipping

## Remaining Unknowns

- Exact `CString(float)` formatting for unusual float values still needs a
  golden harness against the recovered dependency or original binary behavior.
  Current tests lock simple values whose text is unambiguous.
- `std::unordered_map` flag iteration order is runtime/container-dependent and
  should not be asserted as a protocol-stable order.
- Guest random account identity generation remains blocked only for the exact
  C `rand()`/`time(0)` candidate stream. The candidate-to-account transform and
  active-player uniqueness rule are implemented behind an explicit selector.
