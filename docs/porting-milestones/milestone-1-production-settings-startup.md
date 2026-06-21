# Production Settings Startup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace local-debug startup assumptions with source-confirmed production startup, settings, and server-root structure scaffolding.
**Architecture:** Keep startup in `Preagonal.GServer`, durable configuration in `Preagonal.GServer.Core`/`Preagonal.GServer.Persistence`, and no protocol/gameplay behavior outside confirmed source paths.
**Tech Stack:** C#/.NET, xUnit, original C++ files under `ai_resources/GServer-CPP-ORIGINAL/`, recovered `external/gs2lib/`.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Server.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/CSettings.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/CSettings.h`
- Any C++ code that reads `serveroptions.txt`, `serverflags.txt`, `foldersconfig.txt`, `adminconfig.txt`, `allowedversions.txt`, `rules.txt`, or startup server files.

## Required Work

- [x] Re-read the source files above and update `docs/spec/PRODUCTION_STARTUP_SPEC.md`.
- [x] Document every settings filename, default, parse rule, section/key behavior, and missing-file behavior confirmed from C++.
- [x] Add tests in `tests/Persistence.Tests` for confirmed settings parsing behavior before implementation.
- [x] Add production settings DTOs and readers only for confirmed fields.
- [x] Add a server-root resolver that keeps the current local-debug mode opt-in and separates it from production startup.
- [x] Add startup diagnostics that clearly state when behavior is blocked rather than silently faking production behavior.
- [x] Update `docs/KNOWN_BLOCKERS.md` and `docs/spec/docs/KNOWN_BLOCKERS.md`.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement production settings startup boundary`.

## Compatibility Constraints

- Do not invent missing config defaults.
- Do not make `--local-debug` production behavior.
- Do not start gameplay or real auth from this milestone unless C++ startup behavior is fully confirmed.

## Definition Of Done

- The C# host can locate and parse source-confirmed production settings files.
- Missing or unclear settings are documented as blocked.
- Existing tests stay green and new settings tests lock C++ behavior.
