# Scripting Runtime Boundary Spec

## Source Files

Source of truth:

- `ai_resources/GServer-CPP-ORIGINAL/.gitmodules`
- `ai_resources/GServer-CPP-ORIGINAL/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/server/CMakeLists.txt`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/scripting/SourceCode.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/scripting/GS2ScriptManager.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/GS2ScriptManager.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/scripting/ScriptClass.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/ScriptClass.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/scripting/ScriptEngine.h`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/scripting/ScriptEngine.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/NPC.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Weapon.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/Server.cpp`

Recovered supporting dependency:

- `external/gs2compiler/`
- URL: `https://github.com/xtjoeytx/gs2-parser.git`
- recovered commit: `4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9`

The fresh C++ snapshot proves the repository URL through `.gitmodules`, but it
does not prove the exact original submodule commit. The recovered commit must
therefore remain a supporting reference until the original gitlink is recovered
from a fuller source checkout or historical commit.

## `gs2compiler` Gitlink Recovery Result

Recovery pass on 2026-06-16:

- `.gitmodules` proves the submodule path `dependencies/gs2compiler`.
- `.gitmodules` proves the URL
  `https://github.com/xtjoeytx/gs2-parser.git`.
- top-level CMake proves the server expects
  `add_subdirectory(${PROJECT_SOURCE_DIR}/dependencies/gs2compiler
  EXCLUDE_FROM_ALL)`.
- `server/CMakeLists.txt` proves the target includes, depends on, and links
  `gs2compiler`.
- the extracted `ai_resources/GServer-CPP-ORIGINAL/dependencies/gs2compiler`
  directory contains no usable source checkout in this workspace snapshot.
- the workspace git index does not expose a `160000` submodule gitlink entry for
  `ai_resources/GServer-CPP-ORIGINAL/dependencies/gs2compiler`, because
  `ai_resources` is a reference snapshot inside this port repository rather
  than the original repository metadata.

Conclusion: the exact original `dependencies/gs2compiler` gitlink commit cannot
be recovered from the current source snapshot. The cloned
`external/gs2compiler` commit
`4fa0a26ca75ac5238fe34a1d90ef9a459b02c2f9` is useful for understanding API
shape and intent, but it is not canonical for bytecode compatibility until a
full original checkout, upstream commit, release archive, or build artifact
proves the historical submodule pointer.

## Build Facts

`V8NPCSERVER` defaults to `OFF` in top-level CMake. When enabled:

- `-DV8NPCSERVER` is defined
- V8 sources/wrappers are included
- `ScriptEngine`, `ScriptAction`, `ScriptExecutionContext`, and V8 binding files
  are compiled
- `bin/servers/default/bootstrap.js` is embedded

`gs2compiler` is always added by top-level CMake:

```txt
add_subdirectory(${PROJECT_SOURCE_DIR}/dependencies/gs2compiler EXCLUDE_FROM_ALL)
```

The server target includes and links it:

```txt
target_include_directories(${TARGET_NAME} PUBLIC ${GS2COMPILER_INCLUDE_DIRECTORY})
add_dependencies(${TARGET_NAME} gs2compiler)
target_link_libraries(${TARGET_NAME} gs2compiler)
```

## SourceCode Classification

`SourceCode.h` is source-confirmed and safe to model without executing scripts.

When `V8NPCSERVER` is enabled:

- code before the first `//#CLIENTSIDE` marker is server-side
- code from `//#CLIENTSIDE` onward is client-side
- when no marker exists, all source is server-side

When `V8NPCSERVER` is not enabled:

- all source is client-side

Client-side GS1/GS2 split:

- if `gs2default == false`, separator is `//#GS2`
  - before separator => client GS1
  - from separator onward => client GS2
  - no separator => all client GS1
- if `gs2default == true`, separator is `//#GS1`
  - before separator => client GS2
  - from separator onward => client GS1
  - no separator => all client GS2

The C# `SourceCodeSlices` implements only this classification behavior.

## GS2 Compiler Boundary

Recovered `GS2Context` behavior:

- `compile(script)` clears previous errors, parses to AST, then visits AST to
  produce bytecode
- if parsing/root statement fails and no error exists, it adds `"malformed input"`
- `compile(script, type, name, saveToDisk)` calls `compile(script)` and, on
  success, wraps bytecode with `CreateHeader`
- `CreateHeader` writes:
  - GSHORT header-section length
  - `scriptType`
  - comma
  - `scriptName`
  - comma
  - `saveToDisk ? '1' : '0'`
  - comma
  - ten random `GraalByte(rand() % 0xFF)` bytes
  - raw bytecode

Because the exact original submodule commit is not proven and native loading/
error behavior is not ported, C# does not invoke `gs2compiler` yet.

`BlockedGs2CompilerAdapter` intentionally throws until this is implemented.

## GS2ScriptManager Boundary

`GS2ScriptManager.cpp` confirms:

- `THREADPOOL_WORKERS = 0`
- `compileScript` first checks a cache keyed by exact script string
- async compilation is effectively disabled
- `syncCompileJob` compiles synchronously through one `_context`
- result is inserted into cache before the callback is invoked
- `runQueue` is present for async mode but has no queued work when workers are 0

C# does not implement a real manager yet; the behavior above is documented for a
future native/compiler adapter milestone.

## Script Lifecycle Boundary

Confirmed lifecycle entry points under `V8NPCSERVER`:

- `Server::init` initializes `m_scriptEngine`
- `Server::doMain` calls `m_scriptEngine.runScripts(currentTimer)`
- player cleanup queues `npc.playerlogout`
- `Server::playerLoggedIn` queues `npc.playerlogin`
- `NPC::execute` queues `npc.created` after successful script execution
- `Weapon::updateWeapon` queues `weapon.created` when server-side code exists
- `NPC::setTimeout` registers/unregisters timer callbacks
- `NPC::doTimedEvents` queues `npc.timeout`
- `NPC::queueNpcTrigger` queues `npc.trigger`
- `Weapon::queueWeaponAction` queues `weapon.serverside`

The exact V8 object model, binding API surface, scheduling semantics, exception
formatting, and sandbox behavior are not ported.

## V8 Binding Source Inventory

The recovered C++ source registers V8 constructors and global helpers from the
following implementation files. This inventory identifies the authoritative
source locations for a future function-by-function binding port; it is not
permission to approximate or replace the API.

| File | Confirmed responsibility |
| --- | --- |
| `V8FunctionsImpl.cpp` | global template helpers, including `print`, `server`, and `global` setup. |
| `V8EnvironmentImpl.cpp` | `environment` constructor and environment callbacks such as `reportException`, `setCallBack`, and `setNpcEvents`. |
| `V8ServerImpl.cpp` | `server` and `server.flags` constructors; HTTP helpers, level/NPC/player lookup, string persistence, shoot params, logging, RC/NC sends, time/server flag helpers. |
| `V8PlayerImpl.cpp` | `player`, `player.attr`, `player.colors`, and `player.flags` constructors; player properties, weapon helpers, flags, colors, position, account/name/state accessors. |
| `V8NPCImpl.cpp` | `npc`, `npc.attr`, `npc.colors`, `npc.flags`, and `npc.save` constructors; NPC properties, flags/save data, timeout/timer registration, trigger/action registration. |
| `V8LevelImpl.cpp` | `level`, `level.tiles`, `level.links`, `level.signs`, `level.chests`, and `level.npcs` constructors; level save/search/shoot/explosion/NPC/tile/link/sign/chest collections. |
| `V8LevelLinkImpl.cpp` | `LevelLink` wrapper constructor and link field accessors. |
| `V8LevelSignImpl.cpp` | `LevelSign` wrapper constructor and sign field accessors. |
| `V8LevelChestImpl.cpp` | `LevelChest` wrapper constructor and chest field accessors. |
| `V8WeaponImpl.cpp` | `weapon` constructor and weapon wrapper accessors. |
| `V8ScriptEnv.cpp` / `V8ScriptEnv.h` | V8 isolate/context/global template management, constructor registry, `TryCatch` parsing, and last-error storage. |

Confirmed constructor registrations found through `env->setConstructor(...)`:

```txt
environment
server
server.flags
player
player.attr
player.colors
player.flags
npc
npc.attr
npc.colors
npc.flags
npc.save
level
level.tiles
level.links
level.signs
level.chests
level.npcs
LevelLink wrapper
LevelSign wrapper
LevelChest wrapper
Weapon wrapper
```

The next scripting implementation slice must extract each getter, setter, method
name, argument count/type check, thrown error string, return value, side effect,
and ownership/lifetime rule directly from these files before enabling any
runtime execution.

## Scheduling And Error Handling Details

`ScriptExecutionContext::runExecution` moves queued actions into a local vector,
clears the queue before execution so scripts can enqueue follow-up actions, then
invokes each action in order. Failed invokes call
`ScriptEngine::reportScriptException(m_scriptEngine->getScriptError())`.

`ScriptEngine::runScripts` first runs timers, then, inside one V8 function scope,
iterates queued NPCs and weapons:

- NPCs returning `PendingEvents` stay queued.
- NPCs returning `Delete` are collected and deleted after the iteration.
- weapons run and then the weapon update queue is cleared.
- deleted callbacks are only freed once `!func->isReferenced()`.

`executeNpc` wraps the user server-side source into a function object, caches by
exact wrapped code, invokes with exception catching, and reports NPC exceptions
with `levelName,x,y` plus optional NPC name. `executeWeapon` follows the same
compile/cache/invoke shape but reports the script error directly.

## C# Status

Implemented:

- dependency status constants
- `SourceCodeSlices` classification
- explicit blocked compiler/runtime adapters
- `ScriptVisibleApiCatalog`, listing recovered V8 binding groups as explicitly
  unimplemented until each API is ported from C++
- tests that verify runtime execution remains blocked

Not implemented:

- native gs2compiler invocation
- bytecode header generation
- compiler diagnostics mapping
- V8 runtime
- script object wrappers
- script-visible APIs
- event queue/scheduler
- gameplay effects from scripts

## Required Next Evidence Before Runtime Work

Before implementing real script compilation/execution:

- recover the exact original `dependencies/gs2compiler` gitlink commit from an
  external source that still preserves the submodule pointer
- decide how C# will load or host the exact compiler behavior
- produce golden compile fixtures from the original C++/compiler pair
- map V8 bindings function by function from `V8*Impl.cpp`
- capture lifecycle order fixtures for common events such as `npc.created`,
  `npc.playerlogin`, `npc.timeout`, and `weapon.created`
