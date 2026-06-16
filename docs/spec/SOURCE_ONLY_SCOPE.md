# Source-Only Port Scope

This project ports the recovered original C++ server. It does not create new
server features to match genre expectations.

## Rule

A behavior is in scope only when one of these recovered sources contains a
concrete client-facing implementation path:

- `ai_resources/GServer-CPP-ORIGINAL/`
- `external/gs2lib/`
- another exact recovered dependency proven from the original C++ build

Packet captures can certify that a source-confirmed behavior was ported
correctly, but captures do not expand the feature scope beyond the recovered
source.

## Absence Is Behavior

If the recovered C++ source does not implement a built-in system, the compatible
C# behavior is to leave that system absent. Do not add built-in shops, trades,
parties, quests, missions, social systems, or other generic gameplay services
unless future recovered original C++ source or an exact dependency proves a
concrete client-facing path.

When a task, milestone, or matrix row is found to be non-source-derived, update
it as removed from scope instead of implementing a replacement.

## Still In Scope

Systems such as chat, guild, profile, items, weapons, baddies, combat, levels,
NPCs, RC/NC, server-list, scripting, and persistence remain in scope only for
the exact handlers, packet paths, persistence formats, and runtime rules present
in the recovered C++ source.
