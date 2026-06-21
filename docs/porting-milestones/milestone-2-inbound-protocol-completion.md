# Inbound Protocol Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete confirmed inbound packet decoding before gameplay dispatch: raw data, bundles, encrypted/compressed generations, malformed packets, and session routing.
**Architecture:** Keep byte-level codecs in `Preagonal.GServer.Protocol`; session integration belongs in `Preagonal.GServer.Network`; gameplay dispatch remains blocked behind interfaces.
**Tech Stack:** C#/.NET, xUnit golden fixtures, C++ `Player` packet parsing, `gs2lib` codecs.

---

## Source Of Truth

- `ai_resources/GServer-CPP-ORIGINAL/server/src/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/PlayerPacket.cpp` if present.
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Player.h`
- `external/gs2lib/src/CFileQueue.cpp`
- `external/gs2lib/src/CEncryption.cpp`
- `external/gs2lib/include/IEnums.h`
- Existing docs: `docs/spec/INBOUND_PACKET_DECODE_SPEC.md`, `docs/spec/TCP_SESSION_PIPELINE_SPEC.md`, `docs/spec/CFILEQUEUE_FLUSH_SPEC.md`.

## Required Work

- [x] Re-trace `Player::parsePacket`, `Player::doMain`, encryption generation selection, `PLI_RAWDATA`, and bundle handling.
- [x] Update `docs/spec/INBOUND_PACKET_DECODE_SPEC.md` with exact source references and remaining unsupported branches.
- [x] Generate or extend gs2lib fixture harness tests for any newly confirmed inbound frame branch.
- [x] Add failing xUnit tests for confirmed gen4 bzip2, gen5 bzip2, raw-data transition, bundle expansion, and malformed packet behavior when exact bytes are available.
- [x] Implement only branches with source-confirmed byte layout.
- [x] Keep unsupported branches throwing documented compatibility exceptions instead of guessing.
- [x] Integrate confirmed branches into the TCP dev shell without adding gameplay dispatch.
- [x] Run `dotnet build GServerSharp.sln`.
- [x] Run `dotnet test GServerSharp.sln`.
- [x] Confirm `git status --short ai_resources` is empty.
- [x] Commit with message `Complete confirmed inbound protocol decoding`.

## Compatibility Constraints

- Preserve newline and raw length transitions exactly.
- Preserve compressed payload thresholds and length encodings exactly.
- Do not invent bzip2 framing if fixture/source proof is incomplete.

## Definition Of Done

- Every source-confirmed inbound generation and framing path has tests.
- Every blocked branch has a documented reason and a guard test.

## Completion Notes

- No new bzip2 fixture was generated because gen4/gen5 bzip2 byte output is
  still blocked pending exact compression fixture proof.
- No inbound bundle expansion was implemented because `PLI_BUNDLE` is defined in
  `IEnums.h`, but this C++ snapshot does not assign `TPLFunc[PLI_BUNDLE]`; the
  existing length-prefix utility remains tested only as a packet utility.
