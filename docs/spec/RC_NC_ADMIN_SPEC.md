# RC/NC/Admin/Server-List Boundary Spec

## Source Files

Source of truth:

- `external/gs2lib/include/IEnums.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Account.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerRC.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerNC.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`

No Rust/Python behavior is canonical for this milestone.

## Confirmed Admin Rights

`Account.h` defines the RC/admin rights as bit flags. `Account::hasRight(mask)`
is a direct bitwise check:

```cpp
return (m_adminRights & pRight) ? true : false;
```

Confirmed flags now represented in C#:

```txt
PLPERM_WARPTO             0x00001
PLPERM_DISCONNECT         0x00010
PLPERM_SETRIGHTS          0x00400
PLPERM_MODIFYSTAFFACCOUNT 0x04000
PLPERM_NPCCONTROL         0x80000
PLPERM_ANYRIGHT           0xFFFFFF
```

The full flag table is captured in `GServ.Admin.AdminRight`.

## RC/NC Login Gate

`Player::sendLogin` loads RC and NC accounts with the staff-load path:

```cpp
loadAccount(m_accountName, (isRC() || isNC() ? true : false));
```

For RC/NC users, C++ then rejects the login unless both conditions are true:

- account is staff
- source IP matches the configured admin IP check

Confirmed rejection packet text:

```txt
PLO_DISCMESSAGE "You do not have RC rights."
```

The C# milestone implements only the decision boundary in
`GServ.Admin.ControlLoginGate`; it does not perform production account loading
or socket disconnects.

## Confirmed RC Packet IDs

`IEnums.h` confirms the following client-to-server RC packet ids:

```txt
PLI_RC_SERVEROPTIONSGET    51
PLI_RC_SERVEROPTIONSSET    52
PLI_RC_FOLDERCONFIGGET     53
PLI_RC_FOLDERCONFIGSET     54
PLI_RC_RESPAWNSET          55
PLI_RC_HORSELIFESET        56
PLI_RC_APINCREMENTSET      57
PLI_RC_BADDYRESPAWNSET     58
PLI_RC_PLAYERPROPSGET      59
PLI_RC_PLAYERPROPSSET      60
PLI_RC_DISCONNECTPLAYER    61
PLI_RC_UPDATELEVELS        62
PLI_RC_ADMINMESSAGE        63
PLI_RC_PRIVADMINMESSAGE    64
PLI_RC_LISTRCS             65
PLI_RC_DISCONNECTRC        66
PLI_RC_APPLYREASON         67
PLI_RC_SERVERFLAGSGET      68
PLI_RC_SERVERFLAGSSET      69
PLI_RC_ACCOUNTADD          70
PLI_RC_ACCOUNTDEL          71
PLI_RC_ACCOUNTLISTGET      72
PLI_RC_PLAYERPROPSGET2     73
PLI_RC_PLAYERPROPSGET3     74
PLI_RC_PLAYERPROPSRESET    75
PLI_RC_PLAYERPROPSSET2     76
PLI_RC_ACCOUNTGET          77
PLI_RC_ACCOUNTSET          78
PLI_RC_CHAT                79
PLI_RC_WARPPLAYER          82
PLI_RC_PLAYERRIGHTSGET     83
PLI_RC_PLAYERRIGHTSSET     84
PLI_RC_PLAYERCOMMENTSGET   85
PLI_RC_PLAYERCOMMENTSSET   86
PLI_RC_PLAYERBANGET        87
PLI_RC_PLAYERBANSET        88
PLI_RC_FILEBROWSER_START   89
PLI_RC_FILEBROWSER_CD      90
PLI_RC_FILEBROWSER_END     91
PLI_RC_FILEBROWSER_DOWN    92
PLI_RC_FILEBROWSER_UP      93
PLI_RC_FILEBROWSER_MOVE    96
PLI_RC_FILEBROWSER_DELETE  97
PLI_RC_FILEBROWSER_RENAME  98
PLI_RC_LARGEFILESTART      155
PLI_RC_LARGEFILEEND        156
PLI_RC_FOLDERDELETE        160
```

Confirmed server-to-client RC packet ids:

```txt
PLO_RC_CHAT                74
PLO_RC_PLAYERRIGHTSGET     62
PLO_RC_FILEBROWSER_DIRLIST 65
PLO_RC_FILEBROWSER_DIR     66
PLO_RC_FILEBROWSER_MESSAGE 67
PLO_RC_MAXUPLOADFILESIZE   103
```

## Confirmed NC Packet IDs

Confirmed client-to-server NC packet ids:

```txt
PLI_NC_NPCGET       103
PLI_NC_NPCSCRIPTGET 106
PLI_NC_WEAPONLISTGET 115
PLI_NC_WEAPONGET    116
PLI_NC_LEVELLISTGET 150
```

Confirmed server-to-client NC packet ids:

```txt
PLO_NPCSERVERADDR   79
PLO_NC_NPCATTRIBUTES 157
PLO_NC_NPCSCRIPT    160
PLO_NC_WEAPONLISTGET 167
PLO_NC_WEAPONGET    192
```

## Confirmed Packet Construction

All ids are written through `CString >> (char)packetId`, which maps to Graal
GCHAR encoding (`packetId + 32`, clamped by the recovered writer behavior).
`Player::sendPacket` appends a newline before queue flush.

Implemented and tested builders:

- `PLO_RC_CHAT + message + "\n"`
- `PLO_RC_MAXUPLOADFILESIZE + GINT5(bytes) + "\n"`
- `PLO_RC_FILEBROWSER_MESSAGE + message + "\n"`
- `PLO_RC_FILEBROWSER_DIRLIST + tokenized folder list + "\n"`
- `PLO_RC_FILEBROWSER_DIR + GCHAR(folder length) + folder + file entries + "\n"`
- `PLO_NC_WEAPONLISTGET + repeated GCHAR(name length) + name + "\n"`
- `PLO_NC_WEAPONGET + GCHAR(name length) + name + GCHAR(image length) + image + script + "\n"`
- `PLO_NPCSERVERADDR + GSHORT(npcServerId) + "ip,port" + "\n"`

`PlayerNC.cpp::msgPLI_NC_WEAPONGET` replaces every script newline with byte
`0xA7` before sending `PLO_NC_WEAPONGET`. The C# builder uses Latin-1 encoding
for that script tail to preserve the exact byte.

## RC File Browser Boundary

`PlayerRC.cpp::msgPLI_RC_FILEBROWSER_START` confirms:

- empty folder rights list sends `PLO_RC_FILEBROWSER_MESSAGE "You don't have any folders configured."`
- folder list is sent through `PLO_RC_FILEBROWSER_DIRLIST`
- first start sends `PLO_RC_FILEBROWSER_MESSAGE "Welcome to the File Browser."`
- directory payload begins with `GCHAR(currentFolder.Length) + currentFolder`
- each file entry is prefixed in the outer payload by literal space byte `0x20`
- each nested file entry contains:
  - `GCHAR(fileName.Length)`
  - file name
  - `GCHAR(rights.Length)`
  - rights
  - `GINT5(size)`
  - `GINT5(modifiedTime)`

Folder-right line parsing is source-confirmed:

- empty line defaults to rights `r`, folder `*`, wildcard `*`
- rights are lowercased
- backslashes are normalized to `/`
- if a configured path does not end in `/`, the final path segment becomes the
  wildcard and the parent path remains the folder key

Protected file download rejection is source-confirmed for protected entries
such as `config/adminconfig.txt` when the player lacks
`PLPERM_MODIFYSTAFFACCOUNT`:

```txt
PLO_RC_FILEBROWSER_MESSAGE "Insufficient rights to download/view <path>"
```

Production file browsing, directory creation, upload, delete, move, rename, and
disk writes remain blocked.

## Server-List Operational Packets

`ServerList.cpp` confirms:

- `sendText(data)` sends `SVO_SENDTEXT + data`
- `sendTextForPlayer(player, data)` sends `SVO_REQUESTLIST + GSHORT(playerId) + data`
- `msgSVI_PING` replies with `SVO_PING`

These packet bodies are now represented in `ServerListAuthPackets`.

## Blocked

- Production RC login session lifecycle and live socket disconnect behavior.
- Production account loading and admin IP validation wiring.
- RC server-options/folder-options mutation and reload side effects.
- RC file browser disk mutation, upload/download queue integration, and large-file upload handling.
- `PLI_RC_PLAYERRIGHTSSET` mutation and self-protection edge cases.
- NC NPC/class/weapon mutation, script compile/execution, and NPC server runtime hooks.
- Exact `CString::gtokenize()` behavior for complex RC/NC list payloads outside already covered safe cases.
