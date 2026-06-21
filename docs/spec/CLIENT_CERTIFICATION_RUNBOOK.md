# Client Certification Runbook

## Purpose

This runbook defines the repeatable process for proving that the C# server is
client-compatible with the original C++ server for a specific closed-source
client binary and scenario.

Certification is evidence-based. A flow is not certified until raw
client-facing bytes from the original C++ server and the C# server have been
captured for the same client action, compared, and recorded.

## Non-Negotiable Rules

- Use `ai_resources/GServer-CPP-ORIGINAL/` and `external/gs2lib/` as the source
  truth for behavior.
- Do not modify `ai_resources/`.
- Do not add scenarios for features that are not present in the recovered C++
  source.
- Do not normalize, sort, decrypt, trim, or reinterpret the authoritative
  capture bytes.
- Do not mark any `docs/spec/CLIENT_COMPATIBILITY_MATRIX.md` row as
  `Certified` without capture evidence.
- If bytes differ, fix the C# port from C++ source evidence or document why the
  difference is proven non-client-facing.

## Required Inputs

Record these before each certification run:

```txt
Date:
Operator:
Client binary path:
Client version string:
Client binary hash:
C++ source snapshot:
C# commit:
Test content root:
C++ settings file:
C# settings file:
Capture tool:
Scenario id:
```

## Environment Setup

1. Create a runnable copy of the original C++ server outside `ai_resources/`.
2. Build that copy from the recovered source/dependency versions.
3. Prepare a minimal deterministic content root shared by both servers.
4. Prepare matched settings/accounts/files for both servers.
5. Disable unrelated background services that could change packet ordering.
6. Choose one closed-source client binary and keep it fixed for the full run.
7. Confirm that the C# working tree is clean or record all local changes.

## Capture Requirements

For each scenario, capture both directions at the socket boundary:

- client to server
- server to client
- connection open/close timing
- packet order
- raw bytes before any diagnostic decoding

The comparison fixture may include decoded labels for human review, but the
authoritative artifact is the raw byte stream.

## Baseline Capture

1. Reset the test content and account state.
2. Start the original C++ server.
3. Start packet capture.
4. Connect with the selected closed-source client.
5. Perform exactly one scenario from the scenario list.
6. Stop packet capture after the expected server response is complete.
7. Save the capture under a path that includes:
   - scenario id
   - client version
   - server name `cpp`
   - date
8. Record the capture checksum.
9. Stop the original C++ server.

## C# Capture

1. Reset the same test content and account state.
2. Start the C# server from the recorded commit.
3. Start packet capture with the same tool/settings.
4. Connect with the same closed-source client binary.
5. Perform the same scenario with the same inputs.
6. Stop packet capture at the equivalent response boundary.
7. Save the capture under a path that includes:
   - scenario id
   - client version
   - server name `csharp`
   - date
8. Record the capture checksum.
9. Stop the C# server.

## Comparison

Use `Preagonal.GServer.Network.ClientCertificationHarness` for step-level byte equality
when captures have been segmented into labeled steps.

For each step, record:

- label
- direction
- expected C++ byte length
- actual C# byte length
- first mismatch offset, if any
- expected byte, if any
- actual byte, if any
- result

Exact byte equality is required for certification. Length mismatch, missing
step, label mismatch, or byte mismatch all fail the scenario until explained
from C++ source and fixed or proven non-client-facing.

## Minimum Scenario Order

Run scenarios in this order so failures are isolated near the smallest boundary:

1. Login prelude and encryption negotiation.
2. Rejected login.
3. Allowed-version rejection.
4. Valid login through pre-world auth.
5. `Player::sendLogin` pre-world continuation.
6. `Server::playerLoggedIn` and `sendLoginClient` through pre-warp packets.
7. Initial player property serialization.
8. Existing-level warp and `sendLevel`.
9. Missing-level fallback.
10. Movement/player-prop forwarding.
11. File wanted/cache/update download flow.
12. Chat and private-message flows that exist in recovered C++.
13. Source-confirmed inventory/item/weapon flows.
14. Source-confirmed combat flows.
15. Source-confirmed NPC/baddy visibility and runtime packets.
16. Source-confirmed RC login and read-only admin packets.
17. Source-confirmed RC mutation packets.
18. Source-confirmed NC packets.
19. Disconnect, timeout, and shutdown behavior.
20. Long-running timing/save-loop behavior.

Do not skip a failed earlier scenario to certify a later dependent scenario.

## Failure Handling

For every mismatch:

1. Preserve both captures.
2. Record the first mismatch offset and surrounding bytes.
3. Trace the relevant C++ source path.
4. Add or update a C# golden/unit/integration test that reproduces the C++
   behavior.
5. Implement the smallest source-confirmed fix.
6. Run `dotnet build GServerSharp.sln`.
7. Run `dotnet test GServerSharp.sln`.
8. Re-capture both servers if timing/state changed, otherwise re-run the
   comparison with the same C++ baseline.
9. Commit the fix and evidence docs.

If the C++ behavior cannot be recovered, mark the scenario blocked and keep the
related matrix row below `Certified`.

## Evidence Log Template

Append one log entry per scenario:

```txt
Scenario id:
Scenario name:
Date:
Client binary:
Client version string:
Client binary hash:
C++ source snapshot:
C# commit:
Content root checksum:
C++ capture path:
C++ capture checksum:
C# capture path:
C# capture checksum:
Comparison tool:
Result:
Mismatch summary:
Follow-up commits:
Matrix row updated:
Notes:
```

## Matrix Update Rule

Only update `docs/spec/CLIENT_COMPATIBILITY_MATRIX.md` to `Certified` when:

- the same closed-source client binary was used for both captures;
- the same scenario inputs were used;
- raw client-facing bytes matched exactly, or every difference has a
  C++-proven non-client-facing explanation;
- capture paths and checksums are recorded;
- the C# commit is recorded;
- the scenario does not depend on an uncertified earlier failed boundary.

## Current Status

As of 2026-06-16, no matrix row is certified. The harness and docs are ready,
but live C++ and C# closed-client captures still need to be collected.
