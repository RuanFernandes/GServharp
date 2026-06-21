# Run Local Development Server

## Current Status

A minimal diagnostic local development server shell now exists. It is
explicitly local-debug and is not production-compatible.

The current C# codebase has protocol/session/account/level-boundary components,
and now has read-only filesystem-backed `.nw` loading into the static
`sendLevel` boundary. The shell can accept a TCP client, read length-prefixed
frames in sequence, run the confirmed login/account/world-entry boundaries with
local-debug auth, load a `.nw` file, send confirmed pre-runtime level packets
through `CFileQueue.FlushSocket`, decode confirmed post-login inbound
uncompressed/zlib frames, accept `PLI_PLAYERPROPS` for the confirmed
movement/property subset, report parsed-but-unported player-prop side effects
with explicit `PLPROP_*` logs, and stop before runtime world simulation.

## Run Command

Prepare a root folder with `world/start.nw`, then run:

```bash
dotnet run --project src/Server/Server.csproj -- --local-debug --dev-root <root> --dev-level start.nw --port 14900
```

The shell logs a warning on startup. Without `--local-debug`, it does not
enable the fake auth path.

Production auth code now has source-confirmed `SVO_VERIACC2` request and
`SVI_VERIACC2` response boundaries, but this diagnostic command still does not
connect to a real list server. The fake success used here is only reachable
through `--local-debug` / `EnableLocalDebugAuth=true`.

Expected limitations:

- accepts one client at a time
- uses local-debug local auth, not the production list server
- writes socket-framed queued bytes through confirmed `CFileQueue.FlushSocket`
  paths
- sends the diagnostic `.nw` level's full static board payload through
  `PLO_RAWDATA` and the confirmed Gen5 bzip2 socket-flush branch
- applies only decoded `PLI_PLAYERPROPS` local state for confirmed safe
  movement/player-property updates
- stops clearly on source-confirmed parsed props whose runtime side effects are
  not ported yet, such as nickname, carried NPC, status death/revive, or GMAP
  level switching
- decodes confirmed inbound gen4 bzip2 and gen5 uncompressed/zlib/bzip2
  post-login frames using the login key
- stops before touch/link traversal, NPCs, scripts, file transfer, combat, and
  live world runtime
- stops clearly on unsupported post-login frames before gameplay/runtime packet
  dispatch

The protocol project now has source-confirmed socket flush primitives for
gen1/gen6 passthrough, gen2/gen3 zlib, gen5 uncompressed payloads up to 55
bytes, gen5 zlib payloads through `0x2000` bytes, gen5 bzip2 payloads above
`0x2000` bytes, and gen4 bzip2 payloads.

## Manual Closed-Client Status

A synthetic/manual TCP diagnostic is possible. A tiny closed-source game client
connection test may now reach the first socket-framed login/level boundary if
the client sends the same supported Client3 login prelude.

Recommended tiny level fixture:

```txt
<root>/world/start.nw
GLEVNW01
```

Run:

```bash
dotnet run --project src/Server/Server.csproj -- --local-debug --dev-root <root> --dev-level start.nw --port 14900
```

A meaningful playable session is still not expected because production
auth/server-list behavior, live movement forwarding, NPCs, scripts, file
transfer, and live world runtime are not implemented.
