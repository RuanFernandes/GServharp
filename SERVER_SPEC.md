# Server Specification

The future server must be a 100% client-compatible C# replacement for `ai_resources/GServer-CPP-ORIGINAL/`.

Authoritative behavior comes from:

- C++ server source under `ai_resources/GServer-CPP-ORIGINAL/`.
- Recovered `gs2lib` under `external/gs2lib/` at commit `63b1ae96491c188905b50c6b61c8532c601a2122`.

The original server is `GS2Emu 3.0.9`, a Graal Online `v1.411` to `v6.037` compatible server. It starts from `server/src/main.cpp`, creates a `Server`, initializes settings/logs/sockets/world state, then runs `Server::operator()` until shutdown.

Externally visible source-confirmed compatibility areas:

- Exact packet IDs, field order, Graal integer encodings, raw big-endian encodings, newline/raw framing, encryption/compression, file queue ordering, login/session behavior, disconnect messages, RC/NC/server-list packets, level/map/file loading, account persistence, timing, source-confirmed gameplay, and scripting.
- Strange or bug-like C++ behavior must be preserved unless proven safe to diverge.
- Systems not implemented by the recovered C++ source are not compatibility
  gaps. They are out of scope for the faithful port. Do not add built-in shops,
  trades, parties, quests, missions, social systems, or other generic gameplay
  features unless future recovered original C++ source or exact dependency
  source proves a client-facing C++ path. Packet captures may validate
  source-confirmed behavior but must not create new feature scope by
  themselves.
- Any existing plan text that lists a non-source-derived feature must be
  interpreted as historical noise and corrected before implementation.

Implemented in this phase:

- C# solution/project foundation.
- Confirmed packet constants for the first protocol layer.
- Graal binary read/write primitives.
- Confirmed encryption generation primitives.
- Packet framing helpers for newline, raw-data transition, and bundles.
- Login prelude parsing skeleton.
- Outbound signature/disconnect packet construction.

Not implemented:

- Gameplay, full login success, database validation, account persistence, world spawning, RC/NC/admin behavior, server-list behavior, file queue compression flush, and scripting runtime.
