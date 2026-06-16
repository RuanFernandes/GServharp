# Movement And Player Props Boundary

Authoritative sources to trace next:

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerProps.cpp`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Player.h`
- `external/gs2lib/include/IEnums.h`

## Confirmed Entry Points

`Player::createFunctions` maps:

```txt
PLI_PLAYERPROPS = 2 -> Player::msgPLI_PLAYERPROPS
```

`msgPLI_PLAYERPROPS` calls:

```cpp
setProps(pPacket, PLSETPROPS_SETBYPLAYER | PLSETPROPS_FORWARD);
```

Confirmed property IDs from `Account.h` and `PlayerProps.cpp` include:

```txt
PLPROP_GANI = 10
PLPROP_X = 15
PLPROP_Y = 16
PLPROP_Z = 45
PLPROP_SPRITE = 17
PLPROP_CURLEVEL = 20
PLPROP_X2 = 78
PLPROP_Y2 = 79
PLPROP_Z2 = 80
```

For legacy movement fields:

- `PLPROP_X` reads one `GUChar`, stores `m_x = value * 8`, clears paused
  status, updates movement timestamps, marks movement updated, enables touch
  testing, and prepares a forwarded `PLPROP_X2`.
- `PLPROP_Y` mirrors `X` for `m_y`.
- `PLPROP_Z` reads one `GUChar`, stores `(value - 50) * 8`, clears paused
  status, updates movement timestamps, enables touch testing, and prepares
  `PLPROP_Z2`.
- `PLPROP_SPRITE` reads one `GUChar`; non-V8 builds also enable touch testing.
- `PLPROP_CURLEVEL` reads a length-prefixed level name and, in non-V8 builds,
  assigns `m_levelName` without performing a full warp there.
- `PLPROP_GANI` reads a length-prefixed gani string for modern clients. The
  special value `"spin"` emits `PLO_HITOBJECTS` packets around the player.
- `PLPROP_X2`, `PLPROP_Y2`, and `PLPROP_Z2` each read one `GUShort`; the low
  bit is the sign flag and the remaining bits are the pixel coordinate shifted
  right by one. C++ mirrors each precise prop to the legacy prop for older
  clients.

Forwarding behavior confirmed from the end of `setProps`:

- `__sendLocal[propId]` controls whether a changed prop is added to
  `levelBuff`.
- Legacy movement props (`X/Y/Z`) also add their precise counterpart
  (`X2/Y2/Z2`) to `levelBuff2`.
- Precise movement props (`X2/Y2/Z2`) also add their legacy counterpart
  (`X/Y/Z`) to `levelBuff2`.
- The final packet is `PLO_OTHERPLPROPS + GSHORT(playerId) + buffers`.
- If the sending client version is `>= CLVER_2_3`, C++ writes `levelBuff2`
  before `levelBuff`; otherwise it writes `levelBuff` before `levelBuff2`.
- C++ forwards through `Server::sendPacketToLevelArea(..., exclude={self})`.

## Current Status

Implemented C# boundary:

- `IncomingPlayerPropsParser` parses confirmed decoded `PLI_PLAYERPROPS` bodies
  for `X`, `Y`, `Z`, `Sprite`, `CurrentLevel`, `Gani`, `X2`, `Y2`, and `Z2`.
- The parser stops at the first unconfirmed property, matching the C++
  `default: return` shape in `setProps`, and exposes the unsupported property
  id instead of guessing its size.
- `RuntimePlayerPropsApplier.ApplyConfirmed` mutates only local runtime state:
  pixel X/Y/Z, sprite, current level name, gani, and movement/touch flags.
- `IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket` builds the
  confirmed `PLO_OTHERPLPROPS` movement forwarding bytes for the supported
  subset, including legacy/precise mirror props and sender-version ordering.
- The dev-only TCP shell accepts decoded `PLI_PLAYERPROPS` frames after the
  login/level boundary and applies the local mutation. It still does not
  decrypt real post-login client frames or send live multi-session movement
  broadcasts.

## Required Recovery Before Implementation

Still blocked:

- inbound gen4/gen5 decrypt/decompress for real post-login client frames in the
  dev shell
- full `Player::setProps` implementation for all property ids
- `PLPROP_GANI == "spin"` hit-object side effects
- `PLPROP_STATUS`, carry NPC, chat, touch/link/chest/NPC/combat side effects
- real `Server::sendPacketToLevelArea` socket forwarding to other sessions
- invalid-packet counter behavior, which is subtle because C++ returns from the
  `default` case before the later `sentInvalid` block runs
- anti-cheat, clipping, and link traversal behavior

Do not implement movement, anti-cheat, link traversal, or live forwarding until
the exact C++ behavior is documented and tested.
