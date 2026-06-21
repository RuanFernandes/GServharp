# Client Certification Spec

## Purpose

This document defines the compatibility certification process for proving that
the C# server is a 1:1 client-compatible replacement for the original C++
server.

Certification is not complete until the same closed-source client version runs
the same scripted/manual actions against both servers and all client-facing
packet bytes either match exactly or have a documented C++-proven reason for
any difference.

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/`
- `external/gs2lib/`
- all specs under `docs/spec/`
- implemented C# boundaries under `src/`
- tests under `tests/`

Rust and Python ports are not certification sources.

## Original C++ Build/Run Notes

Confirmed from `ai_resources/GServer-CPP-ORIGINAL/README.md` and CMake:

- Project: `GS2Emu VERSION 3.0.9`
- Claimed compatibility: Graal Online `v1.411` to `v6.037`
- Top-level CMake options:
  - `STATIC=ON` by default
  - `V8NPCSERVER=OFF` by default
  - `TESTS=ON` by default
  - `UPNP=OFF` by default
  - `WOLFSSL=ON` appears in Docker build commands
- Required source dependencies:
  - `dependencies/gs2lib`
  - `dependencies/gs2compiler`
- Docker build path exists for Linux GNU:
  - `ai_resources/GServer-CPP-ORIGINAL/docker/GServer-x86_64-linux-gnu.dockerfile`
  - CMake command in that Dockerfile uses:
    - `-DSTATIC=ON`
    - `-DV8NPCSERVER=${NPCSERVER}`
    - `-DWOLFSSL=ON`
    - `-DUPNP=OFF`

The original C++ source in `ai_resources/` remains read-only. A runnable C++
certification copy should be created outside `ai_resources/`.

## Certification Harness

The C# codebase now includes a passive diagnostic harness:

- `Preagonal.GServer.Network.ClientCaptureStep`
- `Preagonal.GServer.Network.ClientCaptureFlow`
- `Preagonal.GServer.Network.ClientCertificationHarness`

The harness compares expected C++ capture bytes against actual C# capture bytes
step-by-step. It certifies only exact byte equality.

Mismatch kinds:

- `None`
- `ByteMismatch`
- `LengthMismatch`
- `LabelMismatch`
- `MissingStep`

For byte mismatches, the harness reports:

- step label
- first mismatch offset
- expected byte
- actual byte
- expected length
- actual length

This harness does not change production protocol behavior.

## Required Capture Method

For each certification case:

1. Start the original C++ server with a clean, versioned test content root.
2. Connect the closed-source client.
3. Perform one deterministic action.
4. Capture the complete server-to-client and client-to-server byte sequence at
   the socket boundary.
5. Reset state.
6. Start the C# server with equivalent content/config.
7. Repeat the same client action using the same client binary/version.
8. Compare captures with `ClientCertificationHarness`.
9. If bytes differ, return to the relevant C++ source/spec and either:
   - fix the C# behavior, or
   - document a C++-proven non-client-facing reason.

Do not normalize, trim, sort, decrypt, decompress, or reinterpret bytes in the
certification comparison unless a separate diagnostic view is explicitly marked
as non-authoritative. The authoritative comparison is raw client-facing bytes.

## Minimum Certification Cases

The following cases are required before claiming full client compatibility:

- login prelude and encryption negotiation
- rejected login
- valid login through world entry
- allowed-version rejection
- duplicate account rejection
- account banned/staff/admin-IP branches
- initial property serialization
- warp to existing level
- missing-level fallback
- level board/resource transfer
- movement/player-prop forwarding
- chat and private message flows
- file wanted/cache/update flows
- combat packet forwarding and confirmed gameplay effects
- inventory/item pickup/drop/weapon flows
- NPC visibility and script-triggered client packets
- baddy/mob runtime packets
- source-confirmed guild/chat/profile flows
- RC login and common admin packet families
- NC login and common NPC/class/weapon packet families
- shutdown/disconnect/timeout behavior

## Current Status

Certification is not complete.

What exists:

- C# passive byte comparison harness.
- Unit tests proving exact-equality, byte mismatch, length mismatch, label
  mismatch through flow order, and missing-step behavior.
- Documentation for how to run future certification.

What is blocked:

- A runnable original C++ server outside `ai_resources/` with matched test
  content.
- A closed-source client binary/version selected for certification.
- Raw packet captures for the C++ baseline.
- Equivalent raw packet captures from the C# server.
- Full gameplay/admin runtime parity.

## Manual Client Certification Log

No manual closed-source client certification was run during this milestone.

Required log format for future runs:

```txt
Date:
Client binary:
Client version string:
Client checksum/hash:
C++ server commit/source snapshot:
C# server commit:
Test content root:
Capture tool:
Scenario:
Result:
Notes:
```

## Compatibility Rule

Do not mark a matrix row as certified unless:

- the same closed-source client action was captured against both servers;
- raw client-facing byte sequences were compared;
- all mismatches were fixed or proven non-client-facing from C++ source.
