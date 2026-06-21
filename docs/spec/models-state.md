# Core Models And State Specification

Confirmed model surfaces:

- `Server`: settings, logs, socket manager, server-list connection, player maps, level maps, NPC lists, weapon/class lists, server flags, translations, update packages.
- `Player`: account data, socket, receive buffer, encryption, file queue, player ID, type, version, level/map, cached levels, external players, flags, weapons, packet count, raw-data state.
- `Account`: text-file account persistence and permissions; exact field map still requires dedicated recovery.
- `Level`: board, links, signs, chests, baddies, NPCs, items, maps, singleplayer/group behaviors.
- `NPC`/`Weapon`: scripting and packet construction are heavily tied to V8/GS2 and version-specific client behavior.

Current C# state:

- `Preagonal.GServer.Core` only records source references.
- `Preagonal.GServer.Protocol` implements confirmed low-level protocol primitives.
- `Preagonal.GServer.Network` contains only a session skeleton matching the initial `PLTYPE_AWAIT` to parsed-login-prelude transition.
- `Preagonal.GServer.Game`, `Preagonal.GServer.Persistence`, `Preagonal.GServer.Admin`, and `Preagonal.GServer.Scripting` are boundary projects only.

Do not add gameplay state until the matching C++ subsystem has been documented and tested.
