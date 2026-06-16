# Client Compatibility Matrix

Status values:

- `Certified`: byte captures match the original C++ server for the stated
  closed-source client version.
- `Harness Ready`: C# has a comparator or local fixture support, but no live
  C++/client capture has been run.
- `Partially Implemented`: source-confirmed C# boundary exists, but surrounding
  runtime behavior is incomplete.
- `Blocked`: certification cannot run without more source recovery, runtime
  implementation, content, or client capture.

No row is currently `Certified`.

| Area | Current Status | C# Coverage | Certification Blocker |
| --- | --- | --- | --- |
| Packet capture comparison harness | Harness Ready | Exact step/flow byte comparison with mismatch offsets | Needs real C++ and C# capture inputs |
| Login prelude | Partially Implemented | Source-confirmed parser/tests | Needs live closed-client capture against C++ and C# |
| Encryption negotiation | Partially Implemented | Confirmed primitive coverage through existing protocol tests | Needs full session capture, gen4/bzip2/websocket branches blocked |
| Rejected login | Partially Implemented | Golden packet tests for known rejection messages | Needs C++ baseline capture with selected client version |
| Valid login through pre-world auth | Partially Implemented | Boundaries through server-list/auth and `ReadyForWorldEntry` | Real server-list auth and production account validation not wired |
| `Player::sendLogin` pre-world continuation | Partially Implemented | Signature/unknown168/rejection boundaries | Full account/default loading and all branch captures pending |
| `Server::playerLoggedIn` / `sendLoginClient` beginning | Partially Implemented | Pre-warp packet order through `ReadyForLevelWarp` | Real world entry/runtime still incomplete |
| Player property serialization | Partially Implemented | Login prop order/constants and explicit serializer subset | Runtime/default property values and old-client branches pending |
| Warp/world entry | Partially Implemented | Existing/missing/same-level/unstick packet boundaries | Full level runtime and old `sendLevel141` fixtures pending |
| Level board/resource transfer | Partially Implemented | `.nw` static loading and selected dynamic packet boundaries | `.graal`/`.zelda`/`.gmap`, bzip2 socket frames, runtime state pending |
| Movement/player-prop forwarding | Partially Implemented | Confirmed inbound subset and forwarding packet builders | Full `setProps`, NPC/combat side effects, invalid-update behavior pending |
| Chat/private messages/profile | Blocked | No full certified runtime | Needs C++ trace/captures and production session routing |
| File wanted/cache/update transfer | Partially Implemented | Confirmed cache/checksum/chunk packet boundaries | Uploads, package lifecycle, bzip2 frames, live client captures pending |
| Combat | Partially Implemented | Selected packet builders/resource clamps/status transitions | Full hit/gameplay simulation and script/NPC side effects pending |
| Inventory/items/weapons | Partially Implemented | Selected pickup/drop/weapon side effects | Durable inventory/runtime behavior pending |
| NPC runtime | Blocked | Inert entity packet boundaries only | Script VM, NPC full props, bytecode, runtime events pending |
| Baddy/mob runtime | Partially Implemented | Default containers/selected props | AI/combat/drop/respawn/timers pending |
| Source-confirmed guild/chat/profile paths | Blocked | Some group-map visibility, chat/PM/profile handlers, and guild verification boundaries | Full filesystem/runtime mutation and chat/guild packet flows pending |
| RC admin login and packets | Partially Implemented | Rights constants, gate decisions, selected packets | Production RC sockets and mutation families pending |
| NC login and packets | Partially Implemented | Selected packet IDs/builders | Production NC sockets, NPC/class/weapon mutation, script execution pending |
| Timing/save loop | Harness Ready | Fake-clock tests for source-confirmed periodic gates | Concrete runtime service wiring and live long-run captures pending |
| Shutdown/disconnect/timeout | Partially Implemented | Timed disconnect boundaries and cleanup order docs | Real production host loop and socket lifecycle captures pending |
| Websocket/WolfSSL | Blocked | Handshake behavior documented | Frame wrapping/unwrapping and TLS integration pending |

## Next Certification Inputs Needed

1. A runnable original C++ server checkout outside `ai_resources/`.
2. Matching test content/config for both servers.
3. Closed-source client binary and exact version string/date.
4. Raw socket capture tooling selected and documented.
5. Scenario scripts or manual runbook that perform deterministic actions.

## Matrix Update Rule

When a row becomes certified, record:

- client version and binary hash;
- C++ server source snapshot;
- C# commit;
- capture file paths/checksums;
- exact scenario;
- comparison result.

Rows are intentionally limited to source-confirmed C++ behavior paths. Generic
server/game feature categories that do not exist in the recovered C++ source
must not be added to this matrix.
