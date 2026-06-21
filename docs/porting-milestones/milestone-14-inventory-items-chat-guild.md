# Source-Confirmed Inventory, Items, Chat, And Guild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port only durable gameplay/chat/guild behavior that is directly
confirmed in the original C++ server.
**Architecture:** Domain rules live in `Preagonal.GServer.Game`; persistence formats in
`Preagonal.GServer.Persistence`; network packet builders in `Preagonal.GServer.Protocol`.
**Tech Stack:** C#/.NET, xUnit state/packet/persistence tests, C++ gameplay and
account sources.

---

## Source Of Truth

- C++ inventory/item/guild/chat/profile source files.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Account.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/level/LevelItem.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/TriggerCommandHandlers.cpp`
- `external/gs2lib/include/IEnums.h`
- Existing docs under `docs/spec/` for protocol, models, account, and level runtime.

## Required Work

- [x] Locate and catalog source-confirmed item/chat/guild files.
- [x] Document confirmed behavior in `docs/spec/INVENTORY_ITEMS_CHAT_GUILD_SPEC.md`.
- [x] Add compatibility tests for source-confirmed item behavior before implementation.
- [x] Implement inventory/item add/remove/use/equip behavior only where source-confirmed.
- [ ] Implement only the guild/chat/profile behavior that has concrete C++
  handlers and confirmed packet/persistence formats.
- [ ] Preserve account save/load side effects and ordering where those systems touch account state.
- [x] Keep behavior absent when no C++ source path exists.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.

## Removed From Scope Unless Source Is Recovered

Built-in shop, trade, party, quest, and mission systems are not part of this
port backlog because the recovered C++ source does not contain confirmed core
runtime paths for them. Do not add tasks, code, tests, or matrix rows for these
categories unless future recovered original C++ source or exact dependency
source proves that the original C++ server exposed a concrete client-facing
path.

Do not add generic service categories to fill gaps. If a behavior is not
implemented by the recovered C++ source, it is not part of the C# port backlog.

## Compatibility Constraints

- Do not drop unclear account fields.
- Do not invent rules for absent systems.
- Do not infer missing packet behavior from Rust/Python.

## Definition Of Done

Every durable gameplay/chat/guild system with source-confirmed C++ behavior is
implemented, tested, and documented. Systems absent from the C++ source remain
absent from the C# port.
