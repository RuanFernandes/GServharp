# Protocol Specification

Authoritative sources:

- `external/gs2lib/include/IEnums.h`
- `external/gs2lib/include/CString.h`
- `external/gs2lib/src/CString.cpp`
- `external/gs2lib/include/CEncryption.h`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/include/CFileQueue.h`
- `external/gs2lib/src/CFileQueue.cpp`
- `server/src/player/Player.cpp`
- `server/src/player/PlayerLogin.cpp`

Confirmed packet IDs used in the C# foundation:

| Symbol | Direction | ID |
| --- | --- | ---: |
| `PLI_RAWDATA` | client to server | 50 |
| `PLI_REQUESTTEXT` | client to server | 152 |
| `PLI_SENDTEXT` | client to server | 154 |
| `PLI_SET_ENC_KEY` | client to server | 252 |
| `PLI_BUNDLE` | client to server | 253 |
| `PLO_PLAYERPROPS` | server to client | 9 |
| `PLO_LEVELNAME` | server to client | 6 |
| `PLO_PLAYERWARP` | server to client | 14 |
| `PLO_WARPFAILED` | server to client | 15 |
| `PLO_DISCMESSAGE` | server to client | 16 |
| `PLO_SIGNATURE` | server to client | 25 |
| `PLO_FLAGSET` | server to client | 28 |
| `PLO_NPCWEAPONDEL` | server to client | 34 |
| `PLO_PLAYERWARP2` | server to client | 49 |
| `PLO_LARGEFILESTART` | server to client | 68 |
| `PLO_LARGEFILEEND` | server to client | 69 |
| `PLO_LARGEFILESIZE` | server to client | 84 |
| `PLO_RAWDATA` | server to client | 100 |
| `PLO_BOARDPACKET` | server to client | 101 |
| `PLO_FILE` | server to client | 102 |
| `PLO_UNKNOWN168` | server to client | 168 |
| `PLO_SERVERLISTCONNECTED` / C++ `PLO_UNKNOWN190` | server to client | 190 |
| `PLO_CLEARWEAPONS` | server to client | 194 |
| `PLO_SET_ENC_KEY` | server to client | 252 |
| `PLO_BUNDLE` | server to client | 253 |

Confirmed player type bits:

- `PLTYPE_AWAIT = 0x80000000`
- `PLTYPE_CLIENT = 1 << 0`
- `PLTYPE_RC = 1 << 1`
- `PLTYPE_NPCSERVER = 1 << 2`
- `PLTYPE_NC = 1 << 3`
- `PLTYPE_CLIENT2 = 1 << 4`
- `PLTYPE_CLIENT3 = 1 << 5`
- `PLTYPE_RC2 = 1 << 6`
- `PLTYPE_EXTERNAL = 1 << 7`
- `PLTYPE_WEB = 1 << 8`

Packet byte convention:

- Packet IDs are usually written with `CString::operator>>(char)`, which calls `writeGChar`; wire byte is packet ID plus 32, clamped at 223 before adding 32.
- C++ `CString::operator>>(char/short/int/long long)` writes Graal-packed `GCHAR`/`GSHORT`/`GINT`/`GINT5`.
- Raw C++ `operator<<(char/short/int)` writes non-Graal raw values; raw short/int are big-endian.
- Socket receive frames in `Player::doMain` begin with a raw big-endian short length before compression/decryption and inner packet parsing.
- `PLI_RAWDATA` switches the next inner packet from newline-delimited to exact-byte-count parsing. Clients and RC versions greater than `RCVER_1_1` strip a trailing newline from that raw payload.
