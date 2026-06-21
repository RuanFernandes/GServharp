# Scripting System Research

The C++ server has two scripting-related paths:

- Always-built sources: `GS2ScriptManager.cpp` and `ScriptClass.cpp`.
- Optional `V8NPCSERVER` sources: `ScriptEngine.cpp`, `ScriptAction`, `ScriptExecutionContext`, `ScriptFactory`, V8 wrappers, and many `V8*Impl.cpp` bindings.

Build facts:

- `V8NPCSERVER` defaults `OFF`.
- When enabled, CMake links V8, `cpp-httplib`, OpenSSL, optional zstd, and generates `EmbeddedBootstrapScript.h` from `bin/servers/default/bootstrap.js`.
- `gs2compiler` is required by the server target and exposed through `GS2COMPILER_INCLUDE_DIRECTORY`.
- `.gitmodules` declares `dependencies/gs2compiler` as `https://github.com/xtjoeytx/gs2-parser.git`.
- `external/gs2compiler` is recovered at commit `4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9`, but the fresh C++ snapshot does not prove this was the exact original submodule pointer.

Behavior confirmed but not implemented:

- Server queues script actions for NPC/player events such as `npc.playerlogin` and `npc.playerlogout` under `V8NPCSERVER`.
- NC/database NPC behavior is gated by `V8NPCSERVER` in many files.
- README documents special NPC commands: `join somefile;`, `singleplayer`, and trigger hacks such as `gr.addweapon`, `gr.addguildmember`, `gr.setgroup`, `gr.appendfile`, `gr.rcchat`, `gr.es`, `gr.attr1` through `gr.attr30`, and `gr.updatelevel`.
- `SourceCode.h` classifies source into server-side/client-side and GS1/GS2 slices using `//#CLIENTSIDE`, `//#GS2`, `//#GS1`, the `V8NPCSERVER` build flag, and the `gs2default` setting.
- `GS2ScriptManager` has `THREADPOOL_WORKERS = 0`, compiles synchronously, caches results by exact script source string, inserts the result before invoking the callback, and leaves async queue processing effectively unused.
- Recovered `GS2Context::CreateHeader` prepends a GSHORT header length, `type,name,saveFlag,`, ten random Graal bytes, then raw bytecode.

Current C# status:

- `Preagonal.GServer.Scripting` is a boundary only.
- `ScriptingRuntimeStatus.IsRuntimeImplemented` is intentionally `false`.
- `SourceCodeSlices` implements only source classification.
- `BlockedGs2CompilerAdapter` and `BlockedScriptRuntime` intentionally reject compile/execute calls.
- No real scripting runtime, compiler invocation, event queue, or bindings are implemented yet.
