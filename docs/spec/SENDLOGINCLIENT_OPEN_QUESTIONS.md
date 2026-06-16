# sendLoginClient Open Questions

- Full `sendProps(__sendLogin)` needs a dedicated player property pass before C# can compute the login prop payload from account/player fields.
- The exact account/default-account values used by `getProp` are still blocked on production account-file loading.
- Old-version map workaround now routes confirmed BIGMAP entries through the
  existing file-transfer boundary. Production still needs the real live map
  repository to supply the same `m_server->getMapList()` order as C++.
- `flaghack_ip` immediate `PLO_FLAGSET gr.ip=<ip>` is traced, but full
  implementation remains blocked on the subsequent `std::unordered_map`
  iteration order for the duplicate `gr.ip` flag emission.
- Weapon/class/protected-weapon packet emission order is implemented when the
  boundary is given already-built source-confirmed packet bytes. Production
  remains blocked on live server weapon lookup, default weapon conversion
  through `msgPLI_WEAPONADD`, script/bytecode compilation, `time(0)` class
  packets, and the concrete `getClassList()` iteration order.
- The zlib-fix NPC weapon branch embeds `_zlibFix`; this needs a version-specific golden fixture before implementation.
- `warp(m_levelName, getX(), getY())` begins real level/map runtime behavior and must be traced separately.
- `PLO_UNKNOWN190` in C++ maps to `PLO_SERVERLISTCONNECTED = 190` in recovered `IEnums.h`; docs should keep both names visible for source traceability.
