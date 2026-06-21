# Warp SendLevel Runtime Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish source-confirmed `Player::warp`, `Player::setLevel`, `Player::sendLevel`, and old-client level-send behavior while stopping before unported gameplay runtimes.
**Architecture:** `Preagonal.GServer.Game` owns warp/level state; `Preagonal.GServer.Protocol` owns packet bytes; `Preagonal.GServer.Network` only flushes ordered output.
**Tech Stack:** C#/.NET, xUnit golden packet sequences, C++ player/level sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Map.cpp` if present.
- `external/gs2lib/include/IEnums.h`
- Existing docs: `docs/spec/WARP_WORLD_ENTRY_SPEC.md`, `docs/spec/SENDLEVEL_SPEC.md`, `docs/spec/SENDLEVEL_TAIL_SPEC.md`, `docs/spec/SENDLEVEL_DYNAMIC_PACKETS_SPEC.md`.

## Required Work

- [x] Re-trace the exact point where `warp` begins runtime level/map behavior.
- [x] Update `docs/spec/WARP_WORLD_ENTRY_SPEC.md` and `docs/spec/SENDLEVEL_PACKET_FLOW.md`.
- [x] Add failing tests for missing level fallback, unstick/previous-level behavior, GMAP branch, old client branch, and packet order.
- [x] Implement source-confirmed `warp` and `setLevel` state changes.
- [x] Implement `sendLevel141` only if packet bytes and branches are fully confirmed.
- [x] Guard NPC/baddy/script execution branches behind documented blocked interfaces.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement warp sendlevel runtime boundary`.

## Compatibility Constraints

- Do not create fake map runtime ownership.
- Do not invent default level names, positions, or modtimes.
- Preserve packet order exactly.

## Definition Of Done

- Login can progress through source-confirmed warp/sendLevel bytes without entering unported gameplay.
- Old-client behavior is either implemented with fixtures or explicitly blocked.

## Completion Notes

- Implemented source-confirmed `warp` orchestration through same-level X/Y update,
  target `setLevel`, previous-level fallback, and unstick fallback.
- Preserved C++'s unusual `warp` return value: fallback can reach a level while
  the returned value still reflects the original target failure.
- `sendLevel141` is traced and documented in `docs/spec/SENDLEVEL_PACKET_FLOW.md`,
  but remains blocked because exact old-client fixtures for board changes,
  first-level state, serverside links/signs, NPCs, baddies, and player prop
  synchronization are not yet captured.
