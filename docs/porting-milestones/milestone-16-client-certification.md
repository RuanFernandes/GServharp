# Client Certification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Certify that the C# server is a 100% faithful client-compatible replacement for the original C++ server from login through gameplay and admin surfaces.
**Architecture:** Add external harnesses and diagnostics without changing production protocol behavior; keep compatibility captures separate from implementation code.
**Tech Stack:** C#/.NET, xUnit/integration tests, original C++ server, closed-source client, packet capture tooling.

---

## Source Of Truth

- Entire `ai_resources/GServer-CPP-ORIGINAL/` tree.
- Entire `external/gs2lib/` recovered dependency.
- All specs under `docs/spec/`.
- All implemented C# projects under `src/` and tests under `tests/`.

## Required Work

- [x] Build or document how to run the original C++ server with the same test content used by the C# server.
- [x] Create `docs/spec/CLIENT_CERTIFICATION_SPEC.md`.
- [x] Add packet capture harnesses that compare C++ and C# byte sequences for identical client actions.
- [ ] Add integration tests for login, warp, movement, chat, file transfer, combat, inventory, NPCs, RC/NC, and shutdown.
- [x] Add a compatibility matrix in `docs/spec/CLIENT_COMPATIBILITY_MATRIX.md`.
- [ ] Fix any mismatches by returning to the relevant earlier milestone source files.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [ ] Run manual closed-source client certification and document exact client version/date used.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Certify client compatibility harness`.

## Compatibility Constraints

- Do not accept “close enough” packet differences.
- Do not hide mismatches behind client-specific special cases unless C++ has the same branch.
- Do not mark the port complete until mismatches are either fixed or proven non-client-facing.

## Definition Of Done

- The closed-source client can complete all supported flows against the C# server.
- Packet captures show no unexplained client-facing differences from the original C++ server.
- The compatibility matrix records every certified area and remaining risk.
