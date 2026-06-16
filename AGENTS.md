# AGENTS.md

## Project Context

This project is a clean C#/.NET port/reimplementation of an original open-source C++ game server.

The game client is closed-source. Only the server source is available.

Because of that, the new C# implementation must be a **100% faithful 1:1 compatible replacement** for the original C++ server.

The repository contains the original C++ source under:

```txt
ai_resources/GServer-CPP-ORIGINAL/
```

This C++ server is the **only source of truth**.

## Target Language

The future server implementation will be written in **C#/.NET**.

The C++ source may be messy, legacy, inconsistent, or hard to understand. The C# port should be organized professionally, with:

* Clean project structure
* Clear module boundaries
* Strong typing
* Maintainable services
* Tests from the beginning
* Good documentation
* Idiomatic C#/.NET design

However, clean architecture is only allowed internally.

Client-facing behavior must remain identical to the original C++ server.

## Non-Negotiable Compatibility Rule

This project is not a loose rewrite, redesign, modernization, or “similar behavior” implementation.

This project is a **100% faithful 1:1 port** of the original C++ server.

The closed-source client must not be able to tell the difference between the original C++ server and the new C# server.

Every external behavior that exists in the original C++ server must match the
original C++ server exactly.

This includes, but is not limited to:

* Network protocol
* Packet IDs/opcodes
* Packet structure
* Packet field order
* Packet field sizes
* Byte order
* String encoding
* Integer encoding
* Serialization/deserialization
* Encryption
* Compression
* Checksums, if any
* Login/session behavior
* Disconnect/error behavior
* File/resource transfer behavior
* Server list behavior
* Timing behavior
* Game loop behavior
* Movement behavior
* Combat behavior
* Skill behavior
* Item/inventory behavior
* NPC/mob behavior
* Quest/mission behavior, only if present in the original C++ source
* Map/level behavior
* Shop/trade behavior, only if present in the original C++ source
* Party/guild/social behavior, only if present in the original C++ source
* Admin/RC/NC behavior
* Database/persistence behavior
* Constants
* Flags
* Magic values
* Limits
* Edge cases
* Bug-compatible behavior required by the client

Do not remove, simplify, approximate, reinterpret, rename, rebalance, or redesign any client-facing behavior unless explicitly instructed.

Absence is also compatibility behavior. If a gameplay or service system is not
present in the original C++ source, do not add it as a C# built-in and do not
keep it as a feature-parity backlog item. Document the absence and leave it out
of scope unless future recovered C++ source or byte-level capture proof shows
that the original server exposed that behavior.

If C++ behavior looks strange, ugly, duplicated, outdated, or bug-like, preserve it unless there is explicit proof that changing it is safe.

Compatibility wins over clean design.

## Source Priority

There is only one authoritative source:

```txt
ai_resources/GServer-CPP-ORIGINAL/
```

Do not rely on older ports, outdated rewrites, assumptions, memory from previous attempts, or guessed behavior.

If external dependencies are referenced by the C++ build system, recover the exact dependency source/version/commit when possible and document it.

Recovered dependencies should be placed outside `ai_resources/`, preferably under:

```txt
external/
```

Treat `ai_resources/` as read-only.

## Reference Directory Rules

Do not modify files inside:

```txt
ai_resources/
```

Do not:

* Refactor the original C++ source
* Rewrite the original C++ source
* Move files out of `ai_resources/`
* Delete files from `ai_resources/`
* Patch the original source to make analysis easier
* Invent missing behavior
* Use assumptions from older versions
* Implement behavior before it is confirmed

Do:

* Read the C++ source carefully
* Recover external dependencies when needed
* Document source paths, classes, functions, constants, and line references
* Preserve packet compatibility
* Preserve timing compatibility
* Preserve gameplay compatibility
* Preserve persistence compatibility
* Preserve edge cases
* Add tests for confirmed behavior
* Mark unclear behavior as unknown instead of guessing

## Work Process

Before implementing any C# feature, system, or module:

1. Inspect the relevant C++ code first.
2. Identify the exact behavior that must be preserved.
3. Recover any missing dependency needed to understand that behavior.
4. Document the relevant C++ files/classes/functions.
5. Implement only behavior that is confirmed.
6. Add compatibility tests whenever possible.
7. Do not invent or “fill in” missing behavior.
8. If blocked, document the blocker and continue with safe work elsewhere.

## Commit Rule

At the end of each completed task, automatically commit all pending repository
changes unless the user explicitly says not to commit.

Before committing:

* Run the relevant verification commands for the task whenever feasible.
* Confirm that `ai_resources/` remains unmodified unless the user explicitly
  instructed otherwise.
* Use a concise commit message that describes the completed work.
* Include all pending changes with `git add .`, because the user expects the
  finished workspace state to be committed automatically.

## Documentation Expectations

Maintain documentation under `docs/`.

When analyzing a module, document:

* Relevant C++ files
* Relevant external dependency files, if any
* Classes/functions involved
* Data structures involved
* Constants/enums/flags involved
* Client-facing behavior
* Timing behavior
* Packet behavior, if any
* Persistence behavior, if any
* Edge cases
* Unknowns/blockers
* Compatibility risks
* Suggested tests

Documentation should be clear enough that a future implementation task can follow it without rediscovering everything.

## Implementation Expectations

The C# implementation should be professional and idiomatic, but behavior must remain compatible with the original C++ server.

Prioritize:

1. Correct C++ behavior
2. Closed-source client compatibility
3. Protocol compatibility
4. Complete feature parity
5. Testability
6. Maintainable architecture
7. Clean C# design

When correctness and clean design conflict, correctness wins.

Do not blindly translate line by line unless necessary.

Do not modernize client-facing behavior.

Do not rebalance gameplay.

Do not simplify protocol behavior.

Do not remove old behavior just because it looks unused.

## Protocol Rules

For anything related to networking, packets, sockets, binary data, serialization, encryption, compression, checksums, login, session handling, or file transfer:

* Use the C++ server and its recovered dependencies as source of truth.
* Preserve byte order.
* Preserve integer sizes.
* Preserve signed/unsigned behavior.
* Preserve field order.
* Preserve field offsets.
* Preserve packet IDs.
* Preserve flags.
* Preserve padding.
* Preserve string encoding.
* Preserve newline/raw-data behavior.
* Preserve compression/encryption behavior.
* Preserve disconnect/error behavior.

Packet compatibility is critical.

Do not implement packet IDs, encoding, encryption, compression, or login/session behavior unless directly confirmed from C++ or recovered dependency source.

## Scripting Rules

If the C++ server has a scripting system, analyze and document it before implementing any C# scripting runtime.

Preserve:

* Script loading behavior
* Script lifecycle
* Script hooks/events
* Script-visible APIs
* Script object behavior
* Script error behavior
* Script reload behavior
* Script timing behavior
* Script interaction with players, NPCs, levels, items, and persistence

Do not replace the scripting system with a different behavior unless a compatibility layer is designed and documented.

## Testing Strategy

Add tests from the beginning.

Prioritize:

* Binary codec tests
* Packet framing tests
* Packet ID tests
* Encryption/compression tests
* Login/session tests
* Golden byte fixtures
* Timing tests
* Persistence format tests
* Gameplay formula tests
* Guard tests for blocked/unknown behavior

Whenever possible, tests should lock behavior proven from the C++ source.

## Definition of Done

A module is only considered complete when:

* Relevant C++ behavior is identified
* External dependencies were recovered or documented as blockers
* Client-facing behavior is preserved
* Constants/enums/flags/magic values are confirmed
* Tests or verification steps exist where possible
* Unknowns are documented
* No files inside `ai_resources/` were modified

## Important Reminder

The goal is not to create a new server inspired by the original.

The goal is to create a fully compatible C# replacement server that behaves exactly like the original C++ server from the closed-source client’s perspective.

A cleaner architecture is allowed only when it does not affect compatibility.

If behavior is unclear, investigate the C++ source and dependencies.

If it is still unclear, document it as unknown and do not invent it.

The port is only successful if the original closed-source client can connect to the C# server and use every feature exactly as it would with the original C++ server.
