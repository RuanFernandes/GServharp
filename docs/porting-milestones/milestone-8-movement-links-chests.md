# Movement Links Chests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port source-confirmed movement property updates, touch/link traversal, signs, and chest-opening boundary behavior.
**Architecture:** Movement and level interaction rules live in `Preagonal.GServer.Game`; packets remain in `Preagonal.GServer.Protocol`; persistence writes stay behind interfaces.
**Tech Stack:** C#/.NET, xUnit, C++ `PlayerProps`, `Player`, and `Level` sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Level.cpp`
- C++ link/sign/chest parsing and serialization code.
- Existing docs: `docs/spec/MOVEMENT_PLAYER_PROPS_SPEC.md`, `docs/spec/LEVEL_LINKS_SIGNS_SPEC.md`, `docs/spec/LEVEL_ITEM_CHEST_SPEC.md`.

## Required Work

- [x] Re-trace `Player::setProps` side effects for movement-related properties.
- [x] Update `docs/spec/MOVEMENT_LINKS_CHESTS_SPEC.md`.
- [x] Add tests for X/Y/Z/X2/Y2/Z2, level-name changes, link touch bounds, sign text translation, chest item names, and blocked side effects.
- [x] Implement source-confirmed property mutation and forwarding behavior.
- [x] Implement source-confirmed client-triggered link warp packet parsing
  (`PLI_LEVELWARP`/`PLI_LEVELWARPMOD`) and keep automatic movement-to-link warp
  blocked until a direct C++ player call path is proven.
- [x] Implement chest/sign packet responses only where bytes and state changes are confirmed.
- [x] Keep combat/NPC/script triggers blocked unless explicitly confirmed in this milestone.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement movement links chests boundary`.

## Compatibility Constraints

- Preserve weird or permissive C++ movement behavior.
- Do not add validation the C++ server does not perform.
- Do not invent chest persistence or item reward semantics.

## Definition Of Done

- Confirmed movement and simple level interaction behavior is source-compatible and tested.
