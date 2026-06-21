# Rc Nc Admin Serverlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port source-confirmed RC, NC, admin command, rights, file browser, and server-list operational behavior.
**Architecture:** `Preagonal.GServer.Admin` owns RC/NC/admin services; protocol builders stay in `Preagonal.GServer.Protocol`; persistence/file access stays behind confirmed interfaces.
**Tech Stack:** C#/.NET, xUnit, C++ admin and server-list sources.

---

## Source Of Truth

- C++ `PlayerRC` source/header files.
- C++ `PlayerNC` source/header files.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- C++ admin/rights/settings files.
- Existing docs: `docs/spec/admin-rc-nc-serverlist.md`.

## Required Work

- [x] Locate and re-read all RC/NC/admin source files.
- [x] Update `docs/spec/RC_NC_ADMIN_SPEC.md` with packet IDs, permissions, rights files, and command flows.
- [x] Add tests for rights parsing, RC login rejection/success boundary, NC packet construction, file browser packets, and server-list side effects where confirmed.
- [x] Implement source-confirmed RC/NC session states and packet builders.
- [x] Keep scripting execution, NPC editing execution, and unsafe filesystem writes blocked until exact behavior is confirmed.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement rc nc admin boundary`.

## Compatibility Constraints

- Preserve staff/admin IP mismatch behavior.
- Preserve rights flags and packet order exactly.
- Do not merge RC/NC behavior with normal player behavior unless C++ does.

## Definition Of Done

- Confirmed RC/NC/admin login and packet boundaries are represented and tested.
