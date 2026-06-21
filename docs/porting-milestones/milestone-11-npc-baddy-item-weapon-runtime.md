# Npc Baddy Item Weapon Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port level runtime entities excluding the full scripting VM: NPC containers, baddies, level items, horses, weapons/classes, and related packet serialization.
**Architecture:** Runtime models live in `Preagonal.GServer.Game`; scripts remain inert until milestone 12; packet serialization stays byte-for-byte in `Preagonal.GServer.Protocol`.
**Tech Stack:** C#/.NET, xUnit, C++ level/entity sources.

---

## Source Of Truth

- C++ `NPC`, `LevelBaddy`, `LevelItem`, `Weapon`, `Class`, `Level`, and related source/header files.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `external/gs2lib/include/IEnums.h`
- Existing docs: `docs/spec/SENDLEVEL_DYNAMIC_PACKETS_SPEC.md`, `docs/spec/LEVEL_RUNTIME_OWNERSHIP_SPEC.md`.

## Required Work

- [x] Locate and re-read all entity runtime source files.
- [x] Update `docs/spec/ENTITY_RUNTIME_SPEC.md`.
- [x] Add tests for source-confirmed entity ID allocation, spawn order, serialization bytes, deletion bytes, and level ownership.
- [x] Implement inert NPC/baddy/item/weapon runtime state only where C++ behavior is confirmed.
- [x] Implement packet builders for confirmed add/remove/update packets.
- [x] Keep script execution, AI behavior, combat effects, and inventory rewards blocked unless proven in this milestone.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement npc baddy item weapon runtime boundary`.

## Compatibility Constraints

- Preserve source order and IDs.
- Preserve opaque payload bytes where runtime meaning remains unclear.
- Do not replace scripts with invented callbacks.

## Definition Of Done

- Static/runtime entity packets visible during level entry are source-compatible and tested.
