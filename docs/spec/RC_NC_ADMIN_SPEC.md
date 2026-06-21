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
PLPERM_WARPTOPLAYER       0x00002
PLPERM_SUMMON             0x00004
PLPERM_UPDATELEVEL        0x00008
PLPERM_DISCONNECT         0x00010
PLPERM_VIEWATTRIBUTES     0x00020
PLPERM_SETATTRIBUTES      0x00040
PLPERM_SETSELFATTRIBUTES  0x00080
PLPERM_RESETATTRIBUTES    0x00100
PLPERM_ADMINMSG           0x00200
PLPERM_SETRIGHTS          0x00400
PLPERM_BAN                0x00800
PLPERM_SETCOMMENTS        0x01000
PLPERM_INVISIBLE          0x02000
PLPERM_MODIFYSTAFFACCOUNT 0x04000
PLPERM_SETSERVERFLAGS     0x08000
PLPERM_SETSERVEROPTIONS   0x10000
PLPERM_SETFOLDEROPTIONS   0x20000
PLPERM_SETFOLDERRIGHTS    0x40000
PLPERM_NPCCONTROL         0x80000
PLPERM_ANYRIGHT           0xFFFFFF
```

The full flag table is captured in `Preagonal.GServer.Admin.AdminRight`.

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
`Preagonal.GServer.Admin.ControlLoginGate`; it does not perform production account loading
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
PLI_RC_UNKNOWN162          162
```

Confirmed server-to-client RC packet ids:

```txt
PLO_RC_ADMINMESSAGE        35
PLO_RC_ACCOUNTADD          50
PLO_RC_ACCOUNTSTATUS       51
PLO_RC_ACCOUNTNAME         52
PLO_RC_ACCOUNTDEL          53
PLO_RC_ACCOUNTPROPS        54
PLO_RC_ACCOUNTPROPSGET     57
PLO_RC_ACCOUNTCHANGE       58
PLO_RC_PLAYERPROPSCHANGE   59
PLO_RC_SERVERFLAGSGET      61
PLO_RC_PLAYERRIGHTSGET     62
PLO_RC_PLAYERCOMMENTSGET   63
PLO_RC_PLAYERBANGET        64
PLO_RC_FILEBROWSER_DIRLIST 65
PLO_RC_FILEBROWSER_DIR     66
PLO_RC_FILEBROWSER_MESSAGE 67
PLO_RC_ACCOUNTLISTGET      70
PLO_RC_PLAYERPROPS         71
PLO_RC_PLAYERPROPSGET      72
PLO_RC_ACCOUNTGET          73
PLO_RC_CHAT                74
PLO_RC_SERVEROPTIONSGET    76
PLO_RC_FOLDERCONFIGGET     77
PLO_RC_MAXUPLOADFILESIZE   103
```

## Confirmed NC Packet IDs

Confirmed client-to-server NC packet ids:

```txt
PLI_NC_NPCGET       103
PLI_NC_NPCDELETE    104
PLI_NC_NPCRESET     105
PLI_NC_NPCSCRIPTGET 106
PLI_NC_NPCWARP      107
PLI_NC_NPCFLAGSGET  108
PLI_NC_NPCSCRIPTSET 109
PLI_NC_NPCFLAGSSET  110
PLI_NC_NPCADD       111
PLI_NC_CLASSEDIT    112
PLI_NC_CLASSADD     113
PLI_NC_LOCALNPCSGET 114
PLI_NC_WEAPONLISTGET 115
PLI_NC_WEAPONGET    116
PLI_NC_WEAPONADD    117
PLI_NC_WEAPONDELETE 118
PLI_NC_CLASSDELETE  119
PLI_NC_LEVELLISTGET 150
PLI_NC_LEVELLISTSET 151
```

`Player.cpp::createFunctions` binds every listed NC packet above except
`PLI_NC_LEVELLISTSET`. `PLI_NC_LEVELLISTSET` is enum-confirmed in
`IEnums.h`, but no handler assignment is present in the recovered C++ source.

Confirmed server-to-client NC packet ids:

```txt
PLO_NPCSERVERADDR   79
PLO_NC_LEVELLIST    80
PLO_NC_NPCATTRIBUTES 157
PLO_NC_NPCADD       158
PLO_NC_NPCDELETE    159
PLO_NC_NPCSCRIPT    160
PLO_NC_NPCFLAGS     161
PLO_NC_CLASSGET     162
PLO_NC_CLASSADD     163
PLO_NC_LEVELDUMP    164
PLO_NC_WEAPONLISTGET 167
PLO_NC_CLASSDELETE  188
PLO_NC_WEAPONGET    192
```

## Confirmed RC Handler/Rights/Response Catalog

`Player.cpp::createFunctions` assigns these packet ids to the listed
`PlayerRC.cpp` handlers. Deprecated handlers return `true` without sending a
packet unless noted. Account names read from RC packets are path-sanitized by
removing everything through the last `/` or `\` before filesystem/account
lookup.

| Packet | Handler | Rights and rejection | Confirmed response packets |
| --- | --- | --- | --- |
| `PLI_RC_SERVEROPTIONSGET` 51 | `msgPLI_RC_SERVEROPTIONSGET` | Client type is logged as hack and receives no packet. | `PLO_RC_SERVEROPTIONSGET << settings.getSettings().gtokenize()`. |
| `PLI_RC_SERVEROPTIONSSET` 52 | `msgPLI_RC_SERVEROPTIONSSET` | Requires non-client and `PLPERM_SETSERVEROPTIONS`; denial sends `PLO_RC_CHAT "Server: <account> is not authorized to change the server options."`. Without `PLPERM_MODIFYSTAFFACCOUNT`, admin-only keys in `__admin` are restored from current settings before save. | Broadcasts `PLO_RC_CHAT "<account> has updated the server options."` to RCs; may resend NC address to RCs with NPC control. |
| `PLI_RC_FOLDERCONFIGGET` 53 | `msgPLI_RC_FOLDERCONFIGGET` | Client type is logged as hack and receives no packet. | `PLO_RC_FOLDERCONFIGGET << foldersconfig.txt.gtokenize()`. |
| `PLI_RC_FOLDERCONFIGSET` 54 | `msgPLI_RC_FOLDERCONFIGSET` | Requires non-client and `PLPERM_SETFOLDEROPTIONS`; denial sends `PLO_RC_CHAT "Server: <account> is not authorized to change the folder config."`. | Saves tokenized folder config, reloads file system, broadcasts `PLO_RC_CHAT "<account> updated the folder config."`. |
| 55-59, 65-67 | deprecated RC handlers | No rights checks. | Return `true`, send no packet. |
| `PLI_RC_PLAYERPROPSSET` 60 | `msgPLI_RC_PLAYERPROPSSET` | Target is online player id. Requires non-client and `PLPERM_SETATTRIBUTES` for others or `PLPERM_SETSELFATTRIBUTES` for self; denial sends `PLO_RC_CHAT "Server: <account> is not authorized to set the properties of <target>"`. | Calls `setPropsRC`, saves account, broadcasts `PLO_RC_CHAT "<account> set the attributes of player <target>"`. |
| `PLI_RC_DISCONNECTPLAYER` 61 | `msgPLI_RC_DISCONNECTPLAYER` | Requires non-client and `PLPERM_DISCONNECT`; denial sends `PLO_RC_CHAT "Server: <account> is not authorized to disconnect players."`. | Broadcasts `PLO_RC_CHAT "<account> disconnected <target>"`, sends target `PLO_DISCMESSAGE "One of the server administrators, <account>, has disconnected you..."`, then deletes target player. |
| `PLI_RC_UPDATELEVELS` 62 | `msgPLI_RC_UPDATELEVELS` | Requires non-client and `PLPERM_UPDATELEVEL`; denial sends `PLO_RC_CHAT "Server: <account> is not authorized to update levels."`. | Reloads each listed level; no success packet. |
| `PLI_RC_ADMINMESSAGE` 63 | `msgPLI_RC_ADMINMESSAGE` | Requires non-client and `PLPERM_ADMINMSG`; denial sends `PLO_RC_CHAT "Server: You are not authorized to send an admin message."`. | Broadcasts `PLO_RC_ADMINMESSAGE "Admin <account>:\xA7<message>"` to all except sender. |
| `PLI_RC_PRIVADMINMESSAGE` 64 | `msgPLI_RC_PRIVADMINMESSAGE` | Same as admin message. | Sends target `PLO_RC_ADMINMESSAGE "Admin <account>:\xA7<message>"`. |
| `PLI_RC_SERVERFLAGSGET` 68 | `msgPLI_RC_SERVERFLAGSGET` | Client type is logged as hack and receives no packet. | `PLO_RC_SERVERFLAGSGET >> GSHORT(count)` then repeated `GCHAR(length) + "name=value"`. |
| `PLI_RC_SERVERFLAGSSET` 69 | `msgPLI_RC_SERVERFLAGSSET` | Requires non-client and `PLPERM_SETSERVERFLAGS`; denial sends `PLO_RC_CHAT "Server: You are not authorized to set the server flags."`. | Replaces server flags, forwards `PLO_FLAGSET`/`PLO_FLAGDEL` to clients, broadcasts `PLO_RC_CHAT "<account> has updated the server flags."`. |
| `PLI_RC_ACCOUNTADD` 70 | `msgPLI_RC_ACCOUNTADD` | Requires non-client and `PLPERM_MODIFYSTAFFACCOUNT`; denial sends `PLO_RC_CHAT "Server: You are not authorized to create new accounts."`. | Creates/saves account fields and broadcasts `PLO_RC_CHAT "<account> has created a new account: <acc>"`. |
| `PLI_RC_ACCOUNTDEL` 71 | `msgPLI_RC_ACCOUNTDEL` | Requires non-client and `PLPERM_MODIFYSTAFFACCOUNT`; denial sends `PLO_RC_CHAT "Server: You are not authorized to delete accounts."`. Default account denial sends `PLO_RC_CHAT "Server: You are not allowed to delete the default account."`. | Removes account file and broadcasts `PLO_RC_CHAT "<account> has deleted the account: <acc>"`. |
| `PLI_RC_ACCOUNTLISTGET` 72 | `msgPLI_RC_ACCOUNTLISTGET` | Client type is logged as hack and receives no packet. | `PLO_RC_ACCOUNTLISTGET` followed by repeated `GCHAR(account.Length) + account` for matches. |
| `PLI_RC_PLAYERPROPSGET2` 73 | `msgPLI_RC_PLAYERPROPSGET2` | Requires non-client and `PLPERM_VIEWATTRIBUTES`; denial sends `PLO_RC_CHAT "Server: You are not authorized to view player props."`. | `PLO_RC_PLAYERPROPSGET >> GSHORT(playerId) << getPropsRC()`. |
| `PLI_RC_PLAYERPROPSGET3` 74 | `msgPLI_RC_PLAYERPROPSGET3` | Same as `PLAYERPROPSGET2`, but target is account name and may load offline account. | Same `PLO_RC_PLAYERPROPSGET` body. |
| `PLI_RC_PLAYERPROPSRESET` 75 | `msgPLI_RC_PLAYERPROPSRESET` | Requires non-client and `PLPERM_RESETATTRIBUTES`; denial sends `PLO_RC_CHAT "Server: You are not authorized to reset accounts.\n"`. | Resets account while preserving RC fields; if online, sends target `PLO_DISCMESSAGE "Your account was reset by <account>"`; broadcasts `PLO_RC_CHAT "<account> has reset the attributes of account: <acc>"`. |
| `PLI_RC_PLAYERPROPSSET2` 76 | `msgPLI_RC_PLAYERPROPSSET2` | Same set-attributes/self-attributes split as packet 60. Additional defaultaccount denial without `PLPERM_MODIFYSTAFFACCOUNT`: `PLO_RC_CHAT "Server: You are not authorized to modify the default account."`. | Calls `setPropsRC`, saves, broadcasts set-attributes chat. |
| `PLI_RC_ACCOUNTGET` 77 | `msgPLI_RC_ACCOUNTGET` | Client type is logged as hack and receives no packet. | `PLO_RC_ACCOUNTGET` with account, zero password length, email, banned, loadOnly, zero admin-level, world `"main"`, ban length, ban reason. |
| `PLI_RC_ACCOUNTSET` 78 | `msgPLI_RC_ACCOUNTSET` | Requires non-client and `PLPERM_MODIFYSTAFFACCOUNT`; denial sends `PLO_RC_CHAT "Server: You are not authorized to edit accounts.\n"`. Ban fields only change when sender also has `PLPERM_BAN`. | Saves account, reloads online RC copy, optionally sends banned player `PLO_DISCMESSAGE "<account> has banned you.  Reason: <reason>"`, broadcasts `PLO_RC_CHAT "<account> has modified the account: <acc>"`. |
| `PLI_RC_CHAT` 79 | `msgPLI_RC_CHAT` | Client type logged as hack and receives no packet. NC type returns early. | Non-command text sends to RC chat as `<nick>: <message>`. Confirmed slash commands include `/help`, `/version`, `/credits`, `/open*`, `/reset`, reload/refresh commands, updatelevel commands, server uptime, and `/find`; each uses `PLO_RC_CHAT`, `PLO_SERVERTEXT`, or delegates to the corresponding RC handler. |
| `PLI_RC_WARPPLAYER` 82 | `msgPLI_RC_WARPPLAYER` | Requires non-client and `PLPERM_WARPTOPLAYER`; denial sends `PLO_RC_CHAT "Server: You are not authorized to warp players.\n"`. | Warps target to `GCHAR x/2`, `GCHAR y/2`, level string; no success packet. |
| `PLI_RC_PLAYERRIGHTSGET` 83 | `msgPLI_RC_PLAYERRIGHTSGET` | Requires non-client and either self account or `PLPERM_SETRIGHTS`; denial sends `PLO_RC_CHAT "Server: You are not authorized to view that player's rights."`. | `PLO_RC_PLAYERRIGHTSGET` with account, `GINT5` rights, admin IP, and gtokenized folder rights length/body. |
| `PLI_RC_PLAYERRIGHTSSET` 84 | `msgPLI_RC_PLAYERRIGHTSSET` | Requires non-client and `PLPERM_SETRIGHTS`; denial sends `PLO_RC_CHAT "Server: You are not authorized to set player rights."`. Sender without `PLPERM_MODIFYSTAFFACCOUNT` cannot grant rights they do not have. Sender cannot remove own `PLPERM_MODIFYSTAFFACCOUNT` or `PLPERM_SETRIGHTS`. Invalid folder rights containing `:`, `..`, or ` /*` are dropped. | Saves account, reloads online RC/NPCSERVER, disconnects NC if NPC control was removed or sends NC address if added, refreshes file browser if open, broadcasts `PLO_RC_CHAT "<account> has set the rights of <acc>"`. |
| `PLI_RC_PLAYERCOMMENTSGET` 85 | `msgPLI_RC_PLAYERCOMMENTSGET` | Client type logged as hack and receives no packet. | `PLO_RC_PLAYERCOMMENTSGET >> GCHAR(acc.Length) << acc << comments`. |
| `PLI_RC_PLAYERCOMMENTSSET` 86 | `msgPLI_RC_PLAYERCOMMENTSSET` | Requires non-client and `PLPERM_SETCOMMENTS`; denial sends `PLO_RC_CHAT "Server: You are not authorized to set player comments."`. | Saves comments, reloads online RC, broadcasts `PLO_RC_CHAT "<account> has set the comments of <acc>"`. |
| `PLI_RC_PLAYERBANGET` 87 | `msgPLI_RC_PLAYERBANGET` | Client type logged as hack and receives no packet. | `PLO_RC_PLAYERBANGET >> GCHAR(acc.Length) << acc >> GCHAR(banned?1:0) << banReason`. |
| `PLI_RC_PLAYERBANSET` 88 | `msgPLI_RC_PLAYERBANSET` | Requires non-client and `PLPERM_BAN`; denial sends `PLO_RC_CHAT "Server: You are not authorized to set player bans."`. | Saves ban fields, reloads online RC, optionally sends online player `PLO_DISCMESSAGE "<account> has banned you.  Reason: <reason>"`, broadcasts `PLO_RC_CHAT "<account> has set the ban of <acc>"`. |
| `PLI_RC_FILEBROWSER_START` 89 | `msgPLI_RC_FILEBROWSER_START` | Client type logged as hack and receives no packet. Empty folder rights list returns without packet. | Sends `PLO_RC_FILEBROWSER_DIRLIST`, optional first `PLO_RC_FILEBROWSER_MESSAGE "Welcome to the File Browser."`, then `PLO_RC_FILEBROWSER_DIR`. |
| `PLI_RC_FILEBROWSER_CD` 90 | `msgPLI_RC_FILEBROWSER_CD` | Client type returns without packet. Unknown folder returns without packet. | Creates missing folder path, sends `PLO_RC_FILEBROWSER_MESSAGE "Folder changed to <folder>"`, then `PLO_RC_FILEBROWSER_DIR`. |
| `PLI_RC_FILEBROWSER_END` 91 | `msgPLI_RC_FILEBROWSER_END` | Client type returns without packet. | Clears FTP flag; no packet. |
| `PLI_RC_FILEBROWSER_DOWN` 92 | `msgPLI_RC_FILEBROWSER_DOWN` | Client type logged as hack and receives no packet. Protected files require `PLPERM_MODIFYSTAFFACCOUNT`; denial sends `PLO_RC_FILEBROWSER_MESSAGE "Insufficient rights to download/view <path>"`. | Calls `sendFile`, then sends `PLO_RC_FILEBROWSER_MESSAGE "Downloaded file <file>"`. |
| `PLI_RC_FILEBROWSER_UP` 93 | `msgPLI_RC_FILEBROWSER_UP` | Client type logged as hack. Important-file uploads require either `PLPERM_MODIFYSTAFFACCOUNT` or the file-specific right in `__importantFileRights`; denial typo is source-confirmed: `PLO_RC_FILEBROWSER_MESSAGE "Insufficent rights to upload <path>"`. | Normal uploads save file, send `PLO_RC_FILEBROWSER_MESSAGE "Uploaded file <file>"`, and call `updateFile`; large-file chunks append in memory. |
| `PLI_RC_FILEBROWSER_MOVE` 96 | `msgPLI_RC_FILEBROWSER_MOVE` | Client type logged as hack. Moving important files is rejected with `PLO_RC_FILEBROWSER_MESSAGE "Not allowed to move file <source>"`. | Copies file to destination then removes source; no success packet. |
| `PLI_RC_FILEBROWSER_DELETE` 97 | `msgPLI_RC_FILEBROWSER_DELETE` | Client type logged as hack. Important-file deletion rejected with `PLO_RC_FILEBROWSER_MESSAGE "Not allowed to delete file <path>"`. | Deletes file, sends `PLO_RC_FILEBROWSER_MESSAGE "Deleted file <file>"`. |
| `PLI_RC_FILEBROWSER_RENAME` 98 | `msgPLI_RC_FILEBROWSER_RENAME` | Client type logged as hack. Important source rejected with `PLO_RC_FILEBROWSER_MESSAGE "Not allowed to rename/overwrite file <src> or <dst>"`. | Renames file, temporarily closes/reopens logs if renaming `logs/rclog.txt` or `logs/serverlog.txt`, sends `PLO_RC_FILEBROWSER_MESSAGE "Renamed file <src> to <dst>"`. |
| `PLI_RC_LARGEFILESTART` 155 | `msgPLI_RC_LARGEFILESTART` | Client type logged as hack and receives no packet. | Initializes in-memory large-file buffer; no packet. |
| `PLI_RC_LARGEFILEEND` 156 | `msgPLI_RC_LARGEFILEEND` | Client type logged as hack and receives no packet. | Saves buffered data, calls `updateFile`, sends `PLO_RC_FILEBROWSER_MESSAGE "Uploaded large file <file>"`. |
| `PLI_RC_FOLDERDELETE` 160 | `msgPLI_RC_FOLDERDELETE` | Client type logged as hack and receives no packet. | On `rmdir` failure sends `PLO_RC_FILEBROWSER_MESSAGE "Error removing <folder>.  Folder may not exist or may not be empty."`; on success sends `PLO_RC_FILEBROWSER_MESSAGE "Folder <folder> has been removed.\n"` and refreshes file browser. |

## Confirmed NC Handler/Response Catalog

The recovered source compiles NC handlers under `#ifdef V8NPCSERVER`.
Authorization is generally `isNC()` for direct NC packets. Unauthorized calls
log a hack message and return `false`; they do not send a denial packet. The
separate `sendNCAddr()` path is RC-only and requires `PLPERM_NPCCONTROL`.

| Packet | Handler | Confirmed behavior |
| --- | --- | --- |
| `PLI_NC_NPCGET` 103 | `msgPLI_NC_NPCGET` | Empty payload is accepted as ping/no-op. With `GINT4 npcId`, sends `PLO_NC_NPCATTRIBUTES << npc.getVariableDump().gtokenize()` if found. |
| `PLI_NC_NPCDELETE` 104 | `msgPLI_NC_NPCDELETE` | Deletes DBNPC only; on success broadcasts `PLO_NC_NPCDELETE >> GINT4(npcId)` to NCs and sends log text to NC chat. |
| `PLI_NC_NPCRESET` 105 | `msgPLI_NC_NPCRESET` | Resets DBNPC script and sends log text to NC; no structured packet. |
| `PLI_NC_NPCSCRIPTGET` 106 | `msgPLI_NC_NPCSCRIPTGET` | Sends `PLO_NC_NPCSCRIPT >> GINT4(npcId) << script.gtokenize()`. |
| `PLI_NC_NPCWARP` 107 | `msgPLI_NC_NPCWARP` | Reads `GINT4 id`, `GCHAR x*2`, `GCHAR y*2`, level string; warps NPC to `int(x*16)`, `int(y*16)` if level exists; no packet. |
| `PLI_NC_NPCFLAGSGET` 108 | `msgPLI_NC_NPCFLAGSGET` | Sends `PLO_NC_NPCFLAGS >> GINT4(npcId) << "flag=value\n"...gtokenize()`. |
| `PLI_NC_NPCSCRIPTSET` 109 | `msgPLI_NC_NPCSCRIPTSET` | Reads `GINT4 id` and `GSTRING script`, untokenizes, saves script, sends log text to NC. Source TODO says permission validation is missing. |
| `PLI_NC_NPCFLAGSSET` 110 | `msgPLI_NC_NPCFLAGSSET` | Reads `GINT4 id` and untokenized flag list, replaces NPC flags, saves NPC, logs added/deleted flag details, sends update text to NC. |
| `PLI_NC_NPCADD` 111 | `msgPLI_NC_NPCADD` | Reads gtokenized lines `name,id,type,scripter,level,x,y`; empty name no-ops, missing level sends NC text `"Error adding database npc: Level does not exist"`. On success broadcasts `PLO_NC_NPCADD >> GINT4(id) << npcProps`, saves NPC, logs to NC. |
| `PLI_NC_CLASSEDIT` 112 | `msgPLI_NC_CLASSEDIT` | Sends `PLO_NC_CLASSGET >> GCHAR(name.Length) << name << classCode.gtokenize()` if class exists. |
| `PLI_NC_CLASSADD` 113 | `msgPLI_NC_CLASSADD` | Reads `GCHAR name length`, name, untokenized script; updates/adds class, updates player weapons, broadcasts `PLO_NC_CLASSADD << className` only when newly added, sends log text. |
| `PLI_NC_LOCALNPCSGET` 114 | `msgPLI_NC_LOCALNPCSGET` | Sends `PLO_NC_LEVELDUMP << npcDump.gtokenize()` with level variable dump and each local NPC dump. Empty level no-ops. |
| `PLI_NC_WEAPONLISTGET` 115 | `msgPLI_NC_WEAPONLISTGET` | Sends `PLO_NC_WEAPONLISTGET` followed by repeated non-default `GCHAR(name.Length) + name`. |
| `PLI_NC_WEAPONGET` 116 | `msgPLI_NC_WEAPONGET` | Newline in script is replaced by `0xA7`. For `version < NCVER_2_1`, sends legacy `PLO_NPCWEAPONADD` body; otherwise sends `PLO_NC_WEAPONGET >> GCHAR(name.Length) << name >> GCHAR(image.Length) << image << script`. Missing/default weapon broadcasts `PLO_RC_CHAT "<account> prob: weapon <weapon> doesn't exist"` to NCs. |
| `PLI_NC_WEAPONADD` 117 | `msgPLI_NC_WEAPONADD` | Reads weapon name/image/code, replaces `0xA7` with newline, updates non-default existing weapon or adds new weapon via `NC_AddWeapon`, updates player weapons on edit, sends log text when added/updated. |
| `PLI_NC_WEAPONDELETE` 118 | `msgPLI_NC_WEAPONDELETE` | Calls `NC_DelWeapon`; sends NC log text `"Weapon <weapon> deleted by <account>"` or `"<account> prob: weapon <weapon> doesn't exist"`. |
| `PLI_NC_CLASSDELETE` 119 | `msgPLI_NC_CLASSDELETE` | On success broadcasts `PLO_NC_CLASSDELETE << className`; sends NC log text for success or `"error: <class> does not exist on this server!\n"`. |
| `PLI_NC_LEVELLISTGET` 150 | `msgPLI_NC_LEVELLISTGET` | Sends `PLO_NC_LEVELLIST << "<actualLevelName>\n"...gtokenize()`. |
| `PLI_NC_LEVELLISTSET` 151 | none assigned | Enum-confirmed but no recovered `TPLFunc` handler assignment; behavior is blocked. |
| `PLI_NPCSERVERQUERY` 94 | `msgPLI_NPCSERVERQUERY` | Outside NC enum block but NC-related. Reads `GSHORT pid` and message; if message is `"location"`, calls `sendNCAddr()`. |
| `sendNCAddr()` | helper | RC-only and requires `PLPERM_NPCCONTROL`. Sends `PLO_NPCSERVERADDR >> GSHORT(npcServerId) << "ip,port"`. If `ns_ip=auto`, uses list-server IP, but replaces it with the account IP for localhost setups. |

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

- empty folder rights list returns without sending any packet
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
