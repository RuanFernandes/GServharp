# Local Dev Auth Warning

The local development shell uses an explicit `DevOnly` authentication shortcut.
It is not original C++ server-list behavior and must never be treated as
production-compatible authentication.

Authoritative production sources:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/ServerList.cpp`
- `external/gs2lib/include/IEnums.h`

Production C++ sends credentials to the list server with `SVO_VERIACC2` and
continues only after receiving `SVI_VERIACC2 SUCCESS`. The dev shell instead
requires `--dev-only-local` / `EnableDevOnlyAuth=true` and locally injects a
`SUCCESS` response so confirmed post-auth boundaries can be exercised.

This shortcut exists only to test already-confirmed packet/session/level
boundaries locally. It does not validate passwords, account ownership,
server-list connection state, bans, subscriptions, list-server side effects,
buddy/profile behavior, or any production account authority.

