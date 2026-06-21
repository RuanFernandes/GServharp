# Filesystem Resource Loading Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port production folder configuration, file indexing, resource lookup, and static level file loading semantics.
**Architecture:** Keep filesystem/index services in `Preagonal.GServer.Persistence`, static level models in `Preagonal.GServer.Game`, and packet serialization in `Preagonal.GServer.Protocol`.
**Tech Stack:** C#/.NET, xUnit, C++ filesystem and level sources.

---

## Source Of Truth

- C++ `FileSystem` source/header files under `ai_resources/GServer-CPP-ORIGINAL/server/`.
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Level.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Level.h`
- C++ map/GMAP files and resource loading helpers.
- Existing docs: `docs/spec/LEVEL_FILESYSTEM_LOADING_SPEC.md`, `docs/spec/LEVEL_FILE_FORMAT_SPEC.md`, `docs/spec/LEVEL_NW_FORMAT_SPEC.md`, `docs/spec/LEVEL_RESOURCE_SPEC.md`.

## Required Work

- [x] Re-trace `foldersconfig.txt`, level folders, file folders, cache rules, path normalization, and lookup order.
- [x] Update `docs/spec/FILESYSTEM_RESOURCE_LOADING_SPEC.md`.
- [x] Add tests for production folder parsing, lookup priority, missing files, extension/signature detection, and `.nw` loading.
- [x] Implement `.graal`, `.zelda`, `.gmap`, and package/resource parsing only after source-confirmed tests exist.
- [x] Keep runtime NPC/baddy execution blocked; preserve static payload bytes when behavior is not yet ported.
- [x] Wire production filesystem into the existing dev sendLevel boundary where safe.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement production filesystem resource loading`.

## Compatibility Constraints

- Preserve path case behavior, slash handling, lookup order, file modification time use, and missing-resource behavior.
- Do not normalize paths in a way the C++ server did not.

## Definition Of Done

- Production resource lookup matches confirmed C++ behavior.
- Static level file parsing has tests for every confirmed format.

## Completion Notes

- Added source-confirmed `FS_*` bucket setup and folder config parsing.
- Existing `.nw` loading remains the only implemented static level parser.
- `.graal`, `.zelda`, `.gmap`, package/resource transfer, and runtime NPC/baddy
  behavior remain blocked until dedicated fixtures are recovered.
