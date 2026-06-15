# C# Structure Mapping

This document maps the initial C#/.NET foundation to the original C++ server. It is intentionally structural only; gameplay systems are not implemented yet.

## Source Files Used

Primary C++ sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`

Supporting docs:

- `docs/original-cpp-server-technical-spec.md`
- `docs/csharp-dotnet-porting-plan.md`

Rust/Python references were not needed for this implementation step.

## Project Layout

- `src/GServ`: process entrypoint and host composition. This maps to C++ `main.cpp` and the top-level server startup loop.
- `src/GServ.Core`: shared compatibility constants, typed IDs, server generation, cached config defaults, and time helpers. This maps to C++ `Server.h`, `Server.cpp`, and shared primitive model concerns.
- `src/GServ.Protocol`: binary protocol utilities and packet abstractions. This maps to C++ `CString` `readG*`/`writeG*` usage and `Player::sendPacket`.
- `src/GServ.Network`: listener/session skeletons. This maps to C++ `Server::onRecv`, `PlayerLogin`, and `Player` subclasses.
- `src/GServ.Persistence.FlatFiles`: reserved for `FlatFileAccountLoader` and `FlatFileNPCLoader`.
- `src/GServ.Game`: reserved for player/world/gameplay systems.
- `src/GServ.Admin`: reserved for RC, NC, and server-list integration.
- `tests/GServ.Protocol.Tests`: compatibility tests for binary primitives, packet framing, and constants.

## Compatibility Notes

Packet numeric IDs are now source-confirmed from recovered `external/gs2lib/include/IEnums.h`. The port exposes `PacketIdSourceStatus.NumericPacketIdsRecovered = true` and has started with protocol-critical packet IDs only; add the rest module by module or generate a complete enum mirror directly from `IEnums.h`.

`GraalBinaryReader` and `GraalBinaryWriter` now map to recovered `external/gs2lib/src/CString.cpp` behavior for GChar, GShort, GInt, GInt4, GUInt5, and raw byte-preserving strings.

`GraalEncryption` maps to recovered `external/gs2lib/src/CEncryption.cpp` behavior for generation constants, iterator reset/update, gen 3 insertion/removal, and gen 4/5 XOR limits. It is intentionally not wired into login/session flow yet.

`PacketFramer.FrameForSend` preserves the C++ `Player::sendPacket` newline rule: empty packets are not sent, packets get a newline unless disabled, and existing newlines are not duplicated.

`ServerCompatibilityOptions.Default` mirrors `ExternalServerCachedSettings` defaults that influence client-visible behavior.

## Next Mapping Targets

1. Capture or generate golden C++ bytes for login and file-queue flows.
2. Implement startup directory discovery from `main.cpp`.
3. Implement login packet parsing from `PlayerLogin` and `PlayerClient::handleLogin`.
4. Port `CFileQueue` compression/send behavior behind fixtures.
