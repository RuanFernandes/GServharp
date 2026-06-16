# Inventory Shops Social Quests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port remaining durable gameplay systems: inventory, items, shops, trade, party, guild, chat/social, quests/missions, and related persistence.
**Architecture:** Domain rules live in `GServ.Game`; persistence formats in `GServ.Persistence`; network packet builders in `GServ.Protocol`.
**Tech Stack:** C#/.NET, xUnit state/packet/persistence tests, C++ gameplay and account sources.

---

## Source Of Truth

- C++ inventory/item/shop/trade/party/guild/chat/quest/mission source files.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `external/gs2lib/include/IEnums.h`
- Existing docs under `docs/spec/` for protocol, models, account, and level runtime.

## Required Work

- [x] Locate and catalog every source file for these systems.
- [x] Split `docs/spec/INVENTORY_SHOPS_SOCIAL_QUESTS_SPEC.md` into sub-sections with exact C++ references.
- [x] Add compatibility tests per system before implementation.
- [x] Implement inventory/item add/remove/use/equip behavior only where source-confirmed.
- [ ] Implement shop/trade/party/guild/chat/quest behavior only after packet and persistence formats are confirmed.
- [ ] Preserve account save/load side effects and ordering.
- [x] Keep unclear systems blocked with explicit docs and guard tests.
- [x] Run `dotnet build GServharp.sln`.
- [x] Run `dotnet test GServharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement inventory shops social quests systems`.

## Compatibility Constraints

- Do not drop unclear account fields.
- Do not redesign social or quest rules.
- Do not infer missing packet behavior from Rust/Python.

## Definition Of Done

- Every durable gameplay system with source-confirmed behavior is implemented, tested, and documented.
