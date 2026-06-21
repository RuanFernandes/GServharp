# Combat Player Gameplay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port player-facing gameplay rules for hearts, damage, death, AP, bombs/arrows, hit detection, spar, and related packets.
**Architecture:** Rules live in `Preagonal.GServer.Game`; packet bytes in `Preagonal.GServer.Protocol`; persistence writes behind `Preagonal.GServer.Persistence`; scripts called only through milestone-12 confirmed hooks.
**Tech Stack:** C#/.NET, xUnit formula/packet tests, C++ gameplay sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/PlayerProps.cpp`
- C++ weapon/combat/baddy/player gameplay source files.
- `external/gs2lib/include/IEnums.h`
- Existing docs: `docs/spec/models-state.md`, `docs/spec/MOVEMENT_PLAYER_PROPS_SPEC.md`.

## Required Work

- [x] Locate and re-read all combat/player gameplay handlers and packets.
- [x] Update `docs/spec/COMBAT_PLAYER_GAMEPLAY_SPEC.md`.
- [x] Add tests for every confirmed formula, clamp, default, flag, packet, and edge case before implementation.
- [x] Implement health/hearts, hurt/death, AP, bombs/arrows, hit detection, and spar behavior only where C++ is confirmed.
- [x] Wire persistence changes only if account save behavior is already confirmed.
- [x] Keep NPC/script side effects blocked if their source-confirmed dependencies are missing.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement combat player gameplay rules`.

## Compatibility Constraints

- Preserve bug-compatible formulas and clamps.
- Do not rebalance or add anti-cheat validation unless C++ has it.
- Do not invent weapon effects.

## Definition Of Done

- Confirmed player combat/gameplay packets and state changes are tested against C++ behavior.
