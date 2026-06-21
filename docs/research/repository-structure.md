# Repository Structure Research

Source of truth: `ai_resources/GServer-CPP-ORIGINAL/`.

Recovered dependency references:

- `external/gs2lib/` from `https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git`, commit `63b1ae96491c188905b50c6b61c8532c601a2122`.
- `external/gs2compiler/` from `https://github.com/xtjoeytx/gs2-parser.git`, recovered current commit `4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9`; the fresh C++ snapshot proves the URL but not the original submodule commit.

Important C++ paths:

- `CMakeLists.txt`: project/version/options/dependency declarations.
- `server/CMakeLists.txt`: server target layout and optional V8 scripting sources.
- `server/src/main.cpp`: process startup, command/env overrides, server selection.
- `server/src/Server.cpp`, `server/include/Server.h`: main server lifecycle, socket manager, world state, persistence.
- `server/src/player/*.cpp`, `server/include/Player.h`: session lifecycle, packet dispatch, login, RC/NC/client behavior.
- `server/src/level/*.cpp`, `server/include/level/*.h`: level, map, tiles, baddies, links, signs, chests.
- `server/src/scripting/*`, `server/include/scripting/*`: GS2/V8 scripting boundary.
- `external/gs2lib/include/IEnums.h`, `CString.h`, `CEncryption.h`, `CFileQueue.h`, `CSocket.h`: protocol-critical library surface.

The C# foundation mirrors this split with professional internal boundaries: `Preagonal.GServer.Core`, `Preagonal.GServer.Protocol`, `Preagonal.GServer.Network`, `Preagonal.GServer.Game`, `Preagonal.GServer.Scripting`, `Preagonal.GServer.Persistence`, `Preagonal.GServer.Admin`, and `Preagonal.GServer`.
