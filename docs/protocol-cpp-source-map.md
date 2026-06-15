# Protocol C++ Source Map

This map records where protocol behavior is defined in the original C++ checkout. The C++ source remains authoritative; Rust and Python were not needed for this pass.

## Authoritative Files Present

- `ai_resources/GServer-CPP-ORIGINAL/server/include/network/IPacketHandler.h`
  - Packet bundle extraction.
  - Login packet special-case parsing.
  - Newline-delimited packet parsing.
  - Raw-data follow-up packet handling.
  - Encryption/compression generation dispatch.
  - Input packet logging name list under `FOR_INPUT_PACKETS` when `PACKETLOGGING` is enabled.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - Outbound `Player::sendPacket` newline behavior.
  - `Player::sendFile` raw-data/file framing behavior.
  - Shared player packet handlers and unknown packet behavior.
  - Output packet logging name list under `FOR_OUTPUT_PACKETS` when `PACKETLOGGING` is enabled.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
  - Temporary login-session first-byte dispatch.
  - Session object swap into client, RC, NC, or NPC-server handlers.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerClient.cpp`
  - Game-client login parsing.
  - Client encryption generation selection.
  - Initial login packet send sequence after server-list verification.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerRC.cpp`
  - RC login parsing.
  - RC encryption generation selection.
  - RC packet handler table.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerNC.cpp`
  - NC login parsing.
  - NC packet handler table.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/npcserver/PlayerNPCServer.cpp`
  - NPC-server session stub behavior in this checkout.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/packets/PlayerClientPackets.cpp`
  - Game-client packet parsing and many server-to-client packet constructions.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/packets/PlayerRCPackets.cpp`
  - RC packet parsing and admin/file-browser packet constructions.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/packets/PlayerNCPackets.cpp`
  - NC packet parsing and NPC/class/weapon packet constructions.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
  - Server-list packet parsing, registration, login verification forwarding, and server-list file-transfer handling.

## Recovered External Authoritative Files

- `external/gs2lib/include/IEnums.h`
  - Required for numeric values of `PLI_*`, `PLO_*`, `PLTYPE_*`, `SVI_*`, `SVO_*`, `CLVER_*`, `RCVER_*`, `NCVER_*`, compression constants, permission/status/flag constants, and other protocol enums.
- `external/gs2lib/include/CString.h`
  - Required for exact implementation of `readShort`, `readG*`, `writeG*`, `readString`, tokenization, raw byte writing, compression helpers, CRC helpers, and string encoding.
- `external/gs2lib/include/CEncryption.h`
  - Required for exact encryption generation algorithms, key reset behavior, and compression/encryption limit behavior.
- `external/gs2lib/include/CFileQueue.h`
  - Required for outbound queue ordering, compression, bundle length prefix serialization, send flushing, and codec selection.
- `external/gs2lib/include/CSocket.h`
  - Required for socket buffering, websocket flags, and actual send/receive semantics.

The matching implementation files are under `external/gs2lib/src/`.

## Compatibility Impact

Packet names, handler membership, and numeric opcode values are confirmed from C++ call sites plus recovered `IEnums.h`. The C# port must not assign numeric packet IDs from Rust/Python or from name guesses.

The C# implementation may safely encode documented primitive helpers when they are directly evidenced by recovered `CString.cpp`, `CEncryption.cpp`, and `CFileQueue.cpp`. Integration behavior still needs packet fixtures before wiring production login.
