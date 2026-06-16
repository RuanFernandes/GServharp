# Run Local Development Server

## Current Status

A minimal diagnostic local development server shell now exists. It is
explicitly dev-only and is not production-compatible.

The current C# codebase has protocol/session/account/level-boundary components,
and now has read-only filesystem-backed `.nw` loading into the static
`sendLevel` boundary. The shell can accept a TCP client, read length-prefixed
frames in sequence, run the confirmed login/account/world-entry boundaries with
dev-only auth, load a `.nw` file, send confirmed pre-runtime level packets
through `CFileQueue.FlushSocket`, decode confirmed post-login inbound
uncompressed/zlib frames, accept `PLI_PLAYERPROPS` for the confirmed
movement/property subset, and stop before runtime world simulation.

## Run Command

Prepare a root folder with `world/start.nw`, then run:

```bash
dotnet run --project src/GServ/GServ.csproj -- --dev-only-local --dev-root <root> --dev-level start.nw --port 14900
```

The shell logs a warning on startup. Without `--dev-only-local`, it does not
enable the fake auth path.

Production auth code now has source-confirmed `SVO_VERIACC2` request and
`SVI_VERIACC2` response boundaries, but this diagnostic command still does not
connect to a real list server. The fake success used here is only reachable
through `--dev-only-local` / `EnableDevOnlyAuth=true`.

Expected limitations:

- accepts one client at a time
- uses dev-only local auth, not the production list server
- writes socket-framed queued bytes through confirmed `CFileQueue.FlushSocket`
  paths
- uses the source-confirmed "level modtime already current" branch during the
  diagnostic `.nw` boundary to avoid blocked bzip2 board payloads
- applies only decoded `PLI_PLAYERPROPS` local state for X/Y/Z, precise
  X2/Y2/Z2, sprite, level name, and gani
- decodes confirmed inbound gen5 uncompressed/zlib post-login frames using the
  login key
- stops before touch/link traversal, NPCs, scripts, file transfer, combat, and
  live world runtime
- stops clearly on unsupported post-login frames before gameplay/runtime packet
  dispatch

The protocol project now has source-confirmed socket flush primitives for
gen1/gen6 passthrough, gen2/gen3 zlib, gen5 uncompressed payloads up to 55
bytes, and gen5 zlib payloads through `0x2000` bytes.

## Manual Closed-Client Status

A synthetic/manual TCP diagnostic is possible. A tiny closed-source game client
connection test may now reach the first socket-framed login/level boundary if
the client sends the same supported Client3 login prelude and can tolerate the
diagnostic "level already current" branch.

Recommended tiny level fixture:

```txt
<root>/world/start.nw
GLEVNW01
```

Run:

```bash
dotnet run --project src/GServ/GServ.csproj -- --dev-only-local --dev-root <root> --dev-level start.nw --port 14900
```

A meaningful playable session is still not expected because full board payload
bzip2 framing, production auth/server-list behavior, inbound bzip2 branches,
live movement forwarding, NPCs, scripts, file transfer, and live world runtime
are not implemented.
