# Player::sendLogin Pre-World Specification

Authoritative sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `external/gs2lib/include/IEnums.h`

## Scope

This milestone covers the beginning of `Player::sendLogin()` after `ServerList::msgSVI_VERIACC2` has returned `SUCCESS` and before real world/level entry begins.

The C# implementation stops at `SessionLifecycle.ReadyForWorldEntry`, which maps to the point immediately before:

```cpp
m_server->playerLoggedIn(shared_from_this());
```

That call is not implemented yet because it starts server-list presence, optional NPC scripting hooks, and the later `sendLoginClient()`/`sendLoginRC()` branches.

## Account Load Boundary

C++ begins with:

```cpp
loadAccount(m_accountName, (isRC() || isNC() ? true : false));
```

`Account.cpp` loads `{account}.txt`, falling back to `accounts/defaultaccount.txt` when the account file is missing. Confirmed fields used by the pre-world checks:

- `BANNED` -> `m_isBanned`
- `BANREASON` -> `m_banReason`
- `LOCALRIGHTS` -> permission flags including `PLPERM_MODIFYSTAFFACCOUNT = 0x04000`
- `IPRANGE` -> `m_adminIp`
- staff/admin state through account rights helpers
- guest state through `getGuest()`

The C# code uses `PlayerSendLoginAccount` as a source-confirmed snapshot. It does not implement production account file loading yet.

## Confirmed Rejection Cases

Order is significant:

1. Banned account:
   - Condition: `m_isBanned && !hasRight(PLPERM_MODIFYSTAFFACCOUNT)`
   - Packet: `PLO_DISCMESSAGE`
   - Message: `You have been banned.  Reason: {banReason.guntokenize().replaceAll("\n", "\r")}`

2. RC/NC rights:
   - Condition: `(isRC() || isNC()) && (!isStaff() || !isAdminIp())`
   - Packet: `PLO_DISCMESSAGE`
   - Message: `You do not have RC rights.`

3. Staff-only client server:
   - Condition: `isClient() && getSettings().getBool("onlystaff", false) && !isStaff()`
   - Packet: `PLO_DISCMESSAGE`
   - Message: `This server is currently restricted to staff only.`

4. Client admin IP mismatch:
   - Condition: `isClient() && !isAdminIp() && IPRANGE does not contain "0.0.0.0"`
   - Packet: `PLO_DISCMESSAGE`
   - Message: `Your IP doesn't match one of the allowed IPs for this account.`

## Confirmed Early Success Packets

After the checks above, C++ sends:

```cpp
sendPacket(CString() >> (char)PLO_SIGNATURE >> (char)73);
```

For normal clients, C++ later sends:

```cpp
sendPacket(CString() >> (char)PLO_UNKNOWN168);
```

`Player::sendPacket` appends `\n` when the queued packet does not already end in newline. Therefore the queued bytes for the normal client pre-world continuation are:

```txt
PLO_SIGNATURE: [57, 105, 10]
PLO_UNKNOWN168: [200, 10]
```

`PLO_UNKNOWN168 = 168` is confirmed in `external/gs2lib/include/IEnums.h`; `CString::writeGChar` writes `value + 32`, producing byte `200`.

## Duplicate Session Handling

For non-guest accounts only, C++ searches the current player list after the early packets above.

It compares:

- account names case-insensitively
- different player id
- same client family:
  - client family `0`: `PLTYPE_ANYCLIENT`
  - client family `1`: `PLTYPE_ANYRC`
  - client family `2`: everything else

If the duplicate session's last data age is greater than 30 seconds, C++ sends the existing session:

```txt
Someone else has logged into your account.
```

and disconnects that existing session. The current login continues.

If the duplicate session is active within 30 seconds, C++ sends the current login:

```txt
Account is already in use.
```

and returns false. Because the duplicate check occurs after `PLO_SIGNATURE` and client `PLO_UNKNOWN168`, those packets have already been queued for normal clients before the rejection packet.

## Login-Server Name Branch

C++ also contains:

```cpp
if (m_server->getName().find("login") != CString::npos)
{
    sendPacket(CString() >> (char)PLO_FULLSTOP);
    sendPacket(CString() >> (char)PLO_GHOSTICON >> (char)1);
}
```

`PLO_GHOSTICON = 174` is confirmed in `IEnums.h`. `PLO_FULLSTOP` is referenced in `PlayerLogin.cpp` but not present in the C++ tree or recovered `IEnums.h`; only `PLO_FULLSTOP2 = 177` exists. For this recovered source set, the branch is permanently blocked: do not map `PLO_FULLSTOP` to `PLO_FULLSTOP2` without recovering the original missing enum/source or proving the exact byte from a running original server capture.

The C# boundary now exposes this as `LoginServerFullStopBlocked` when the server
name contains `login`, and deliberately emits neither guessed full-stop bytes
nor the following `PLO_GHOSTICON` packet for that unresolved branch.

## Stop Point

The C# milestone stops before:

```cpp
m_server->playerLoggedIn(shared_from_this());
```

Everything after that point is blocked for a later world-entry milestone:

- `Server::playerLoggedIn`
- optional NPC server `playerLogin` scripting event
- `sendLoginClient`
- `sendLoginRC`
- `sendLoginNC`
- player props, level warp, status icons, files, and gameplay state
