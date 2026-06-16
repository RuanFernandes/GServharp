# Player Props Packet Flow

## Login Property Flow

```txt
Player::sendLoginClient
  -> sendProps(__sendLogin)
  -> for each true __sendLogin property id in ascending order
       append GCHAR property id
       append getProp(property id)
  -> send PLO_PLAYERPROPS + payload
  -> Player::sendPacket appends "\n"
```

## Confirmed Fixture

Input:

```txt
properties: PLPROP_MAXPOWER, PLPROP_CURPOWER, PLPROP_ACCOUNTNAME
maxPower=3
hitpoints=4.0
accountName="pc:Ruan"
```

Payload:

```txt
[33, 35,
 34, 40,
 66, 39, 112, 99, 58, 82, 117, 97, 110]
```

Meaning:

- `33`: `GCHAR PLPROP_MAXPOWER`
- `35`: `GCHAR 3`
- `34`: `GCHAR PLPROP_CURPOWER`
- `40`: `GCHAR 8`, because hitpoints are multiplied by two
- `66`: `GCHAR PLPROP_ACCOUNTNAME`
- `39`: `GCHAR length 7`
- remaining bytes are raw ASCII `pc:Ruan`

Wrapped packet with newline:

```txt
[41, 33, 35, 34, 40, 66, 39, 112, 99, 58, 82, 117, 97, 110, 10]
```

`41` is `GCHAR PLO_PLAYERPROPS`.

## `__sendLogin` Integration

`SendLoginPropertySet.All` now represents the exact C++ `__sendLogin` true
entries in emitted order. `SendLoginPropertySet.ForClient(preClient21: true)`
matches `Player::sendProps` with `pCount = 37`.

For clients older than `CLVER_2_1`, `PLPROP_GANI` keeps property id `10` but
uses the old `PLPROP_BOWGIF` payload:

```txt
empty bow image: GCHAR bowPower
non-empty bow image: GCHAR(10 + bowImage.length) + bowImage bytes
```

The pre-warp login boundary applies the same old-client cutoff even if the
caller supplies the modern full table.

The serializer supports the confirmed `__sendLogin` IDs. Production use still
requires real account/player values rather than invented defaults.

## Pre-Warp Login Integration

The pre-warp login boundary now calls `PlayerPropertySerializer.SerializeConfirmedLoginSubset` instead of accepting arbitrary login prop bytes for the tested path.

Full production `__sendLogin` emission still requires real account/default-account values, but the source-confirmed old-client `PLPROP_GANI`/`PLPROP_BOWGIF` branch is implemented for login serialization and incoming player-prop parse/mutation/forwarding.
