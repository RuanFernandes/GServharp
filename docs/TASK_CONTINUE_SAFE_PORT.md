Read `docs/AGENTS.md`, `docs/COMPATIBILITY_RULES.md`, `docs/SERVER_SPEC.md`, `docs/PORTING_PLAN.md`, `docs/KNOWN_BLOCKERS.md`, and all docs under `docs/`.

Continue the C#/.NET 1:1 port using only source-confirmed behavior.

Source of truth:

```txt
ai_resources/GServer-CPP-ORIGINAL/
external/gs2lib/
```

Do not modify anything inside `ai_resources/`.

Do not invent behavior.

If something is unclear, document it as unknown and continue with the next safe task.

Current status:

* Dev-only local TCP shell exists behind explicit `--dev-only-local`.
* Shell integrates source-confirmed outbound `GraalFileQueue.FlushSocket`.
* Login -> dev auth -> account/level -> sendLevel boundary works in dev-only path.
* Continuous TCP loop exists.
* Parser for already-decoded `PLI_PLAYERPROPS` exists.
* RuntimePlayer mutation exists for confirmed movement props.
* Forwarding builder for decoded movement props exists.
* Current blocker: post-login inbound frames from the closed client are still encrypted/compressed/framed and must be decoded before `PLI_PLAYERPROPS` parser can handle them.
* Tests are green.

Goal of this run:

Recover and implement the source-confirmed inbound post-login decrypt/decompress/framing pipeline so the dev server can decode real client packets after login.

---

# Milestone 1: Trace inbound post-login decode path

Deeply trace the original inbound packet path:

* `Player::doMain`
* `Player::decryptPacket`
* `Player::parsePacket`
* `Player::processPackets` if present
* any socket receive/read loop
* any rawdata handling
* `CEncryption`
* `CString`
* `CSocket`
* relevant packet handlers under `server/src/player/packets/`

Focus on:

* how inbound length-prefixed frames are read
* when decryptPacket is called
* which encryption generation is used after login
* how gen2/gen3/gen4/gen5 inbound decrypt differs
* how compression flags are detected
* how zlib/bzip2 decompression is selected
* how PLI_BUNDLE is handled inbound
* how PLI_RAWDATA affects the next packet
* how newline-delimited inner packets are split
* how malformed encrypted/compressed packets behave
* how unsupported/unknown packets behave

Document everything before implementing.

---

# Milestone 2: Create gs2lib inbound fixture harness

Extend or create a C++ fixture harness under:

```txt
tools/gs2lib-fixtures/
```

Use `external/gs2lib` without modifying it.

Capture exact inbound decode behavior for deterministic cases:

* gen5 short encrypted packet from client -> decoded inner packet
* gen5 zlib compressed packet -> decoded inner packet
* gen2/gen3 if source-confirmed for inbound
* rawdata next-packet behavior if safe
* bundle packet behavior if safe
* malformed packet behavior where deterministic

If C++ does not expose a direct inbound helper, create the smallest harness around the same functions used by `Player::decryptPacket`.

Do not invent fixture bytes.

If harness cannot cover a branch, document why.

---

# Milestone 3: Implement C# inbound decoder only where byte-confirmed

Add a C# inbound pipeline that converts socket frame payloads into decoded inner packets.

Allowed implementation:

* Inbound frame decoder
* decrypt/decompress helpers for confirmed generations
* newline splitter after decrypt/decompress
* bundle splitter if confirmed
* rawdata mode if confirmed
* explicit blocked exceptions for unsupported bzip2/gen branches
* tests against C++ harness fixtures

Not allowed:

* Do not approximate bzip2.
* Do not guess compression flags.
* Do not silently ignore malformed packets unless C++ confirms.
* Do not consume state incorrectly on unsupported branches.
* Do not bypass encryption just to make dev server work.

---

# Milestone 4: Integrate inbound decoder into dev-only TCP shell

If the inbound decoder is confirmed, connect it to the dev-only shell.

Focus on:

* after login, decode real client frames before dispatch
* feed decoded `PLI_PLAYERPROPS` to existing parser
* keep unsupported packets logged/blocked explicitly
* preserve generation/key state
* preserve rawdata/bundle state if confirmed
* flush outbound responses after each decoded packet if needed

Allowed:

* Add tests with fake transports.
* Add manual run docs.
* Add debug logging of decoded packet IDs.

Not allowed:

* Do not implement unknown packet handlers.
* Do not fake movement if packet decoding is unsupported.
* Do not hide unsupported packet IDs.

---

# Milestone 5: Minimal movement manual test readiness

If inbound decoding is integrated, update docs for a limited manual test.

Focus on:

* run command
* required tiny `.nw`
* expected client behavior
* expected server logs
* limitations
* how to identify unsupported packets
* whether movement props should now be decoded

Do not claim gameplay readiness.

---

# Milestone 6: Docs/tests/report

Create/update docs:

```txt
docs/spec/INBOUND_PACKET_DECODE_SPEC.md
docs/spec/MOVEMENT_PLAYER_PROPS_SPEC.md
docs/spec/TCP_SESSION_PIPELINE_SPEC.md
docs/spec/RUN_LOCAL_DEV_SERVER.md
docs/spec/CFILEQUEUE_FIXTURE_HARNESS.md
docs/spec/GOLDEN_FIXTURES.md
docs/spec/docs/KNOWN_BLOCKERS.md
docs/KNOWN_BLOCKERS.md
```

Run:

```bash
dotnet build GServerSharp.sln
dotnet test GServerSharp.sln
```

If a C++ fixture harness is used, build/run it or document why it cannot run.

At the end, report:

* What was completed
* Which inbound generations/branches are now supported
* Which fixture bytes were captured
* Whether real post-login client movement frames can now be decoded
* Which C# files/tests were added or modified
* Which docs were updated
* Whether `ai_resources/` stayed untouched
* Build/test results
* Whether manual client test is now recommended
* Exact run command and expected limitations
* Safest next step

Continue as far as safely possible. Do not stop after one small task if another safe task can be done safely.
