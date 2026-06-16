# Porting Plan

Port in compatibility-first phases:

1. Protocol foundation: complete packet catalog, all `CString` codecs, encryption/compression, packet bundle/raw/newline behavior, file queue behavior.
2. Session lifecycle: exact login parsing, version handling, encryption key negotiation, disconnect/error responses, no gameplay.
3. Account/config persistence: text file schemas, defaults, server options, staff/admin IP checks.
4. World/resource loading: levels, maps, files, packages, default assets, update packages.
5. Player model and props: every property ID, login props, RC props, forwarding flags.
6. Source-confirmed gameplay systems only: movement, levels, items, weapons, baddies, combat, chests, signs, links, and only other systems with explicit C++ handlers. Do not add generic genre systems that are absent from the recovered C++ source.
7. RC/NC/admin/server-list: file browser, account editing, NPC control, list-server packets.
8. Scripting: GS2 compiler behavior, V8 bindings/events, NPC lifecycle.
9. Compatibility harness: golden byte fixtures from C++ and live-client captures.

For each phase:

- Inspect C++ first.
- Recover exact dependency source if needed.
- Document source paths and behavior.
- Keep absent systems absent; do not add generic feature backlog items that are
  not source-derived.
- If a backlog item is found to be non-source-derived, mark it as removed from
  scope and continue with the next source-confirmed item.
- Write failing compatibility tests.
- Implement minimal C# behavior.
- Verify with `dotnet build` and `dotnet test`.
