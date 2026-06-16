# sendLoginClient Packet Flow

Authoritative source: `ai_resources/GServer-CPP-ORIGINAL/server/src/player/PlayerLogin.cpp`.

## Confirmed Order Before Warp

For a normal client path with no old-version map workaround, no login flags, no
weapons/classes/protected weapons, and no zlib-fix branch, the pre-warp order
is:

```txt
sendProps(__sendLogin)
optional old-version BIGMAP file sends through msgPLI_WANTFILE
PLO_CLEARWEAPONS
optional immediate flaghack_ip PLO_FLAGSET "gr.ip=<remote-ip>"
player flags as PLO_FLAGSET
server flags as PLO_FLAGSET
PLO_NPCWEAPONDEL "Bomb"
PLO_NPCWEAPONDEL "Bow"
player weapon packets in m_weaponList order
protected weapon auto-add packets for missing protectedweapons entries
class packets from getClassList() when version >= CLVER_4_0211
PLO_SERVERLISTCONNECTED / C++ symbol PLO_UNKNOWN190
STOP before warp(m_levelName, getX(), getY())
```

`sendProps(__sendLogin)` builds:

```txt
PLO_PLAYERPROPS + encoded property payload
```

`Player::sendPacket` appends `\n` to each packet unless already present.

## Implemented C# Boundary

`PostLoginWorldEntryBoundary.BeginClient` implements the source-confirmed packet wrappers and ordering above using `PlayerPropertySerializer.SerializeConfirmedLoginSubset` for the tested property payload and ordered flag lists.

It now accepts optional `PostLoginClientOptions` for the old-version map-file
workaround. When the parsed login version is exactly `CLVER_2_31` or
`CLVER_1_411`, it iterates the supplied map list and calls the confirmed
`FileTransferBoundary.HandleWantFile` only for `BIGMAP` entries. This preserves
the C++ position of the side effect immediately after `sendProps(__sendLogin)`
and before `PLO_CLEARWEAPONS`. GMAP entries are skipped.

`PostLoginClientOptions` also accepts source-confirmed login weapon/class packet
snapshots:

- `PlayerWeapons` are queued in supplied order after `PLO_NPCWEAPONDEL "Bow"`.
  This maps to the C++ `for (auto& weaponName: m_weaponList)` loop and queues
  only already-built `Weapon::getWeaponPacket(...)` bytes.
- `ProtectedWeaponNames` are evaluated in supplied setting-token order. Entries
  already present in `PlayerWeapons` are skipped, matching the C++ erase step
  before `addWeapon`.
- `ProtectedWeaponPackets` supplies the already-confirmed packet bytes for
  protected names that are missing from the player list.
- `OrderedClassPackets` are queued only when the parsed client version is
  `CLVER_4_0211` or newer, matching the C++ class branch.

This is a boundary implementation, not a complete production weapon/class
loader. It deliberately does not invent missing weapon lookups, default weapon
conversion, script compilation, bytecode headers, or unordered class-list
iteration order.

It marks the session:

```txt
ReadyForWorldEntry -> ReadyForLevelWarp
```

`ReadyForLevelWarp` means the port has reached the exact point before the C++ `warp(...)` call.

## Confirmed Packet IDs

- `PLO_PLAYERPROPS = 9`
- `PLO_FLAGSET = 28`
- `PLO_NPCWEAPONDEL = 34`
- `PLO_DEFAULTWEAPON = 43`
- `PLO_SERVERLISTCONNECTED = 190`; C++ source calls this `PLO_UNKNOWN190`
- `PLO_CLEARWEAPONS = 194`
- `PLO_LOADSCRIPT = 197`; C++ source calls this `PLO_UNKNOWN197` in
  `ScriptClass::getClassPacket`

## Old-Version Map Workaround

Source:

```cpp
if (m_versionId == CLVER_2_31 || m_versionId == CLVER_1_411)
{
    for (const auto& map: m_server->getMapList())
    {
        if (map->getType() == MapType::BIGMAP)
            msgPLI_WANTFILE(CString() << map->getMapName());
    }
}
```

`msgPLI_WANTFILE` reads the filename and calls `sendFile`. For clients older
than `CLVER_2_1`, the filename gets `.gif` appended when it has no extension.
For `CLVER_2_31`, the file transfer path includes mod time, raw-data wrapping,
and the existing `PLO_FILE` chunk behavior implemented by `FileTransferBoundary`.

## `flaghack_ip`

Source:

```cpp
if (settings.getBool("flaghack_ip", false) == true)
    this->setFlag("gr.ip", this->m_accountIpStr, true);
```

`Player::setFlag(..., sendToPlayer=true)` first mutates the account flag map
through `Account::setFlag`, then immediately sends `PLO_FLAGSET gr.ip=<ip>` to
the player. `sendLoginClient` then iterates `m_flagList` and sends all flags.
Because `m_flagList` is a C++ `std::unordered_map`, the second occurrence of
`gr.ip` has no portable stable order relative to other player flags unless a
future capture proves the concrete compiled/runtime order. The C# port does not
invent that ordering yet.

## Weapon, Protected Weapon, And Class Branches

Source:

```cpp
for (auto& weaponName: m_weaponList)
{
    auto weapon = m_server->getWeapon(weaponName.toString());
    if (weapon == nullptr)
    {
        if (auto itemType = LevelItem::getItemId(weaponName.toString()); itemType != LevelItemType::INVALID)
        {
            CString defWeapPacket = CString() >> (char)PLI_WEAPONADD >> (char)0 >> (char)LevelItem::getItemTypeId(itemType);
            defWeapPacket.readGChar();
            msgPLI_WEAPONADD(defWeapPacket);
            continue;
        }
        continue;
    }
    sendPacket(weapon->getWeaponPacket(m_versionId));
}
```

`Weapon::getWeaponPacket` confirms:

- default weapons send `PLO_DEFAULTWEAPON + GCHAR(default item id)`
- non-default weapons begin with `PLO_NPCWEAPONADD`, `GCHAR(name length)`,
  name bytes, `GCHAR(NPCPROP_IMAGE)`, `GCHAR(image length)`, image bytes
- GS1 script payload uses `GCHAR(NPCPROP_SCRIPT)`, `GSHORT(script length)`,
  script bytes unless a newer/bytecode branch returns earlier

Protected weapons are read from the `protectedweapons` setting with
`gCommaStrTokens()`, remove names already present in `m_weaponList`, then call
`addWeapon(name)` for each remaining token. `addWeapon` mutates `m_weaponList`
and sends `weapon->getWeaponPacket(m_versionId)` only when the weapon exists
and was not already present.

Class packets are sent only for `m_versionId >= CLVER_4_0211`.
`ScriptClass::getClassPacket` emits a `PLO_LOADSCRIPT`/C++ `PLO_UNKNOWN197`
line when bytecode exists, using the bytecode header followed by `,` and a
tokenized `time(0)` value. Empty class bytecode returns an empty packet.

## Deferred Branches Before Warp

These are traced but not implemented yet:

- spar deviation recalculation mutates account/player state
- `flaghack_ip` full duplicate flag emission order after unordered-map mutation
- production weapon list lookup/default conversion through `msgPLI_WEAPONADD`
- production protected weapon lookup/mutation when packet bytes are not supplied
- production class-list ordering and dynamic bytecode/time packet generation
- zlib-fix NPC weapon for client versions 2.21 through 2.31

The C# boundary only emits packets whose bytes are directly confirmed. Full `__sendLogin` is still blocked, so callers provide the explicit confirmed property IDs to serialize.
