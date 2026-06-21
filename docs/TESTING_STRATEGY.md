# Testing Strategy

Priority test types:

- Golden byte tests for `CString` raw and Graal encodings.
- Packet ID assertions sourced from `IEnums.h`.
- Encryption generation tests sourced from `CEncryption.cpp`.
- Packet framing tests for newline, raw-data transitions, and bundles.
- Login/session state tests sourced from `Player.cpp` and `PlayerLogin.cpp`.
- File queue tests sourced from `CFileQueue.cpp`.
- Account/settings persistence tests sourced from `Account.cpp`, `CSettings.cpp`, and `Server.cpp`.
- Gameplay formula tests added only after each system is recovered.

Current tests:

- `Preagonal.GServer.Protocol.Tests`: 22 tests covering packet IDs, player type bits, Graal/raw integer encoding, encryption gen behavior, framing, bundle reading, signature and disconnect packet bytes.
- `Preagonal.GServer.Network.Tests`: 2 tests covering initial awaiting-login state and confirmed client prelude generation selection.
- `Preagonal.GServer.Core.Tests`: 1 test recording recovered `gs2lib` commit.
- `Preagonal.GServer.Scripting.Tests`: 1 test guarding that scripting remains intentionally unimplemented.

Every future porting task should add a red-green compatibility test before production code.
