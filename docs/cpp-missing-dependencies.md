# C++ Dependency Recovery Inventory

Update: the external `gs2lib` dependency has now been recovered under `external/gs2lib/` at commit `63b1ae96491c188905b50c6b61c8532c601a2122`. This document preserves the original missing-dependency investigation and call-site inventory. See `docs/gs2lib-recovery.md` for the successful recovery result.

This recovery pass searched the full workspace for the protocol-critical C++ headers/classes that block a faithful C# implementation.

## Result

At the time of the recovery pass, none of the following files existed in the original server checkout under `ai_resources/GServer-CPP-ORIGINAL/`:

- `IEnums.h`
- `CString.h`
- `CEncryption.h`
- `CFileQueue.h`
- `CSocket.h`

The absence was checked with a full repository filename search. The visible C++ code compiles these includes as angle-bracket includes, not local quoted includes, so they are expected to come from an external include path.

## Expected Source

The original C++ CMake configuration identifies the likely source:

- `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt` declares `FetchContent_Declare(gs2lib ...)`.
- It fetches `https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git` at commit `63b1ae96491c188905b50c6b61c8532c601a2122`.
- It links `gs2lib` and adds `target_include_directories(... "${gs2lib_SOURCE_DIR}/include")`.

Therefore the missing headers were part of the external `gs2lib` dependency, not the server source tree itself.

The top-level CMake also sets:

- `CMAKE_PREFIX_PATH` to `dependencies`
- `FETCHCONTENT_BASE_DIR` to `dependencies/fc`
- a `.gitignore` rule for `dependencies/fc/`

The dependency source would normally be fetched or cached under `dependencies/fc`, but that directory is intentionally absent from this checkout.

## Dependency Status

| Dependency | Found? | Expected source | Compatibility impact |
| --- | --- | --- | --- |
| `IEnums.h` | Yes, recovered in `external/gs2lib/include` | `gs2lib/include` | Unblocks numeric packet IDs, player type masks, status flags, and protocol enums found in `IEnums.h`. |
| `CString.h` | Yes, recovered in `external/gs2lib/include` | `gs2lib/include` | Unblocks exact string storage, integer codecs, raw bundle length byte order, tokenization, compression helper semantics, CRC/hash helpers, and byte/string conversion details. |
| `CEncryption.h` | Yes, recovered in `external/gs2lib/include` | `gs2lib/include` | Unblocks encryption algorithm, generation constants, key reset behavior, and compression-type-dependent decrypt limits. |
| `CFileQueue.h` | Yes, recovered in `external/gs2lib/include` | `gs2lib/include` | Unblocks outbound bundle construction, queue flushing, compression selection, codec application, and socket send details. |
| `CSocket.h` | Yes, recovered in `external/gs2lib/include` | `gs2lib/include` | Unblocks exact socket state enum values, socket manager semantics, buffering behavior, websocket field behavior, and platform socket lifecycle. |

## Build Metadata Reviewed

- `ai_resources/GServer-CPP-ORIGINAL/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/Catch_tests/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/vcpkg.json`
- `ai_resources/GServer-CPP-ORIGINAL/CMakePresets.json`
- `ai_resources/GServer-CPP-ORIGINAL/.gitignore`
- `ai_resources/GServer-CPP-ORIGINAL/cpp.hint`

`vcpkg.json` supplies third-party packages such as zlib, bzip2, antlr4, wolfssl, and catch2, but it does not define the missing Graal-specific headers. Those are project-specific and tied to `gs2lib`.

## Supporting References

Rust and Python were searched only for context. They contain protocol constants and codec implementations, but those are outdated supporting references and must not be used to assign canonical numeric packet IDs or encryption behavior.

The Rust README claims complete codecs and packet types. The Python README claims `ENCRYPT_GEN_5` support. These claims are useful for locating concepts, but not for resolving C++ compatibility.
