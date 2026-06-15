# Protocol Open Questions

These questions track remaining uncertainty after recovering `gs2lib`.

## Recovered Authoritative Dependencies

Recovery result: `gs2lib` was recovered from `https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git` at commit `63b1ae96491c188905b50c6b61c8532c601a2122`.

The recovered checkout contains:

- `IEnums.h`
- `CString.h`
- `CEncryption.h`
- `CFileQueue.h`
- `CSocket.h`

The missing-header blocker is closed. See `docs/gs2lib-recovery.md`.

## Packet ID Risk

Numeric packet IDs are now confirmed by `external/gs2lib/include/IEnums.h`. The C# port may add packet constants/enums directly from that file as each packet family is implemented.

Open question: should the C# project carry a complete generated enum mirror of all `PLI_*`, `PLO_*`, `SVO_*`, and `SVI_*`, or add scoped enums module by module? Current C# changes add only protocol-critical packet IDs to reduce churn.

## Encoding Risk

`CString.cpp` confirms raw integer byte order, Graal printable integer bounds, `readString`, `readChars`, and buffer semantics. The remaining risk is not the primitive codec itself, but packet-specific use of tokenization helpers such as `gtokenize`, `guntokenize`, comma parsing, and escape/unescape behavior.

## Encryption And Compression Risk

`CEncryption.cpp` confirms generation constants, compression constants, iterator starts, key reset, gen 3 insertion/removal, and gen 4/5 XOR behavior. Remaining risk is integration-level: the C# port still needs fixtures for compressed file queue output and encrypted login/session traffic.

## Login Risk

The login field order is documented, but a working login requires:

- golden byte fixtures for each supported login generation,
- outbound file queue bundling,
- server-list behavior or a faithful local substitute,
- exact disconnect/error text behavior per failed login path.

## Recommended Recovery Tactics

- Build/run the original C++ server with packet logging and capture byte fixtures for:
  - unencrypted generation 1/2 login attempts,
  - simple `PLO_DISCMESSAGE`,
  - `PLO_SIGNATURE`,
  - `PLI_PACKETCOUNT`,
  - `PLO_RAWDATA` plus `PLO_FILE`.
- Compare captured bytes against the recovered C# codec/encryption tests before wiring login.

See also:

- `docs/cpp-missing-dependencies.md`
- `docs/protocol-dependency-call-sites.md`
- `docs/protocol-blockers.md`
