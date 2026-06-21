# Production Auth Serverlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace fake local auth with source-confirmed production auth/list-server login behavior up to the account/world boundary.
**Architecture:** Put list-server protocol builders in `Preagonal.GServer.Protocol`, list-server sockets in `Preagonal.GServer.Network` or `Preagonal.GServer.Admin`, and auth decisions behind interfaces until persistence is confirmed.
**Tech Stack:** C#/.NET, xUnit, C++ server-list and player-login sources.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/ServerList.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `external/gs2lib/include/IEnums.h`
- Existing docs: `docs/spec/AUTH_SERVERLIST_SPEC.md`, `docs/spec/AUTH_SERVERLIST_PACKET_FLOW.md`, `docs/spec/LOGIN_SESSION_SPEC.md`.

## Required Work

- [x] Re-trace list-server connection, registration, account verification, version enforcement, and rejection paths.
- [x] Update `docs/spec/AUTH_SERVERLIST_SPEC.md` and `docs/spec/AUTH_SERVERLIST_PACKET_FLOW.md`.
- [x] Add packet builders/tests for confirmed `SVO_*`, `SVI_*`, login reject, and disconnect messages.
- [x] Add production auth interface names that preserve C++ concepts in docs.
- [x] Remove or isolate fake dev auth from production code paths.
- [x] Implement confirmed version checks from `allowedversions.txt` and list-server responses.
- [x] Add tests for invalid version, rejected account, banned response, and allowed auth only where exact response packets are confirmed.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Implement production auth serverlist boundary`.

## Compatibility Constraints

- Do not invent database/account validation.
- Do not treat test fakes as production behavior.
- Do not proceed into world entry except through confirmed session state transitions.

## Definition Of Done

- Production auth no longer depends on fake local accept logic.
- Confirmed rejection and auth-boundary packets have golden tests.

## Completion Notes

- Fake server-list success remains only in the local-debug TCP shell and is still
  gated by `EnableLocalDebugAuth=true`.
- Real account/password authority is still external to the game server and is
  represented by the production gateway interface.
- Real list-server socket lifecycle is intentionally left for a later milestone.
