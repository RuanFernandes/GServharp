# Timing Save Loop Production Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port production server loop timing, autosave, shutdown, websocket/TLS branches, logging, signal handling, and operational hardening.
**Architecture:** `Preagonal.GServer` owns host lifecycle; `Preagonal.GServer.Core` owns clocks/logging abstractions; network transports remain source-compatible wrappers.
**Tech Stack:** C#/.NET, xUnit fake-clock tests, C++ server/socket sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/main.cpp`
- `external/gs2lib/src/CSocket.cpp`
- `external/gs2lib/include/CSocket.h`
- C++ websocket branches gated by `WOLFSSL_ENABLED`.
- Existing docs: `docs/research/startup-runtime.md`, `docs/spec/TCP_SESSION_PIPELINE_SPEC.md`, `docs/KNOWN_BLOCKERS.md`.

## Required Work

- [x] Re-trace `Server::doMain()` timing branches, periodic jobs, save intervals, list-server heartbeats, and shutdown behavior.
- [x] Update `docs/spec/TIMING_SAVE_LOOP_SPEC.md`.
- [x] Add fake-clock tests for every confirmed periodic branch.
- [x] Implement source-confirmed autosave, heartbeat, cleanup, idle timeout, and shutdown order.
- [x] Recover websocket/TLS behavior only from C++/gs2lib; keep it blocked if dependencies or byte behavior are unclear.
- [ ] Add production logging messages only where they do not change client-facing behavior.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement timing save loop hardening`.

## Compatibility Constraints

- Preserve timer intervals and ordering.
- Do not change save timing or disconnect timing.
- Do not implement websocket behavior from assumptions.

## Definition Of Done

- Production loop behavior is source-confirmed, testable with fake clocks, and ready for long-running operation.
