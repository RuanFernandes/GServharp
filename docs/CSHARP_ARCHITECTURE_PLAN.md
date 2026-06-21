# C# Architecture Plan

Solution projects:

- `Preagonal.GServer`: executable host.
- `Preagonal.GServer.Core`: shared source references, common constants, future clocks/config abstractions.
- `Preagonal.GServer.Protocol`: packet IDs, Graal codecs, encryption/compression/framing, packet abstractions.
- `Preagonal.GServer.Network`: socket/session lifecycle and send/receive queues.
- `Preagonal.GServer.Game`: player/world/level/gameplay domain model.
- `Preagonal.GServer.Scripting`: GS2/V8-compatible scripting boundary and future runtime.
- `Preagonal.GServer.Persistence`: account/settings/filesystem persistence.
- `Preagonal.GServer.Admin`: RC/NC/admin/server-list command surfaces.

Dependency direction:

- Higher-level projects may depend on lower-level projects.
- `Preagonal.GServer.Protocol` must stay independent of gameplay/persistence.
- `Preagonal.GServer.Network` may depend on protocol and core, but not gameplay until session handoff is explicit.
- Scripting should depend on game abstractions only through stable compatibility interfaces.

Current implementation status:

- Only `Core`, `Protocol`, `Network`, and `Scripting` have minimal source-confirmed code.
- `Game`, `Persistence`, and `Admin` are empty boundaries with marker classes.
