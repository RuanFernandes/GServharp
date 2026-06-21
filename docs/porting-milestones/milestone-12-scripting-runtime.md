# Scripting Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover exact scripting dependencies and port the GS2/V8-compatible scripting lifecycle and API boundary without inventing script semantics.
**Architecture:** `Preagonal.GServer.Scripting` owns compiler/runtime adapters; `Preagonal.GServer.Game` exposes compatibility interfaces; gameplay calls into scripts only through source-confirmed hooks.
**Tech Stack:** C#/.NET, xUnit, recovered compiler/runtime source, original C++ scripting sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/` if present.
- C++ NPC/server scripting files and CMake dependency declarations.
- Recovered exact `gs2compiler` source when available.
- C++ V8 binding files and build settings.
- Existing docs: `docs/research/scripting-system.md`, `docs/KNOWN_BLOCKERS.md`.

## Required Work

- [x] Re-inspect CMake/submodule/package references for exact `gs2compiler` URL and commit.
- [x] Recover the exact dependency outside `ai_resources/`, or document the failed recovery evidence.
- [x] Update `docs/spec/SCRIPTING_RUNTIME_SPEC.md` and `docs/research/scripting-system.md`.
- [x] Add tests that assert scripting remains blocked until exact dependency behavior is known.
- [ ] After dependency recovery, add tests for script parse/load errors, event hooks, exposed APIs, and lifecycle order.
- [x] Implement only source-confirmed compiler/runtime adapter behavior.
- [x] Keep gameplay effects blocked until both script API and affected gameplay system are confirmed.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Recover scripting runtime boundary`.

## Compatibility Constraints

- Do not replace GS2/V8 semantics with a different scripting language behavior.
- Do not invent script-visible APIs.
- Do not execute scripts until lifecycle and sandbox/error behavior are confirmed.

## Definition Of Done

- Exact scripting dependency status is known.
- Source-confirmed scripting lifecycle/API behavior is documented and guarded by tests.
