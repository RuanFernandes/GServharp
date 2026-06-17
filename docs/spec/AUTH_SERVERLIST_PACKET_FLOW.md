# Auth And Server-List Packet Flow

## Login Auth Request

```txt
client login packet
  -> Player::msgPLI_LOGIN
  -> capacity / ip ban / allowed version / server-list connected checks
  -> ServerList::sendLoginPacketForPlayer
  -> SVO_VERIACC2 queued to list server
```

`SVO_VERIACC2` uses Graal-packed lengths and IDs, not raw length prefixes inside the packet body.

## Login Auth Response

```txt
list-server SVI_VERIACC2
  -> account, id, type, message
  -> overwrite player account name
  -> if message != SUCCESS: PLO_DISCMESSAGE, load-only, disconnect
  -> if message == SUCCESS: Player::sendLogin()
```

The C# production response handler now implements the byte parse, pending
session lookup, account-name overwrite, rejection packet, and success state
transition. It marks the session `ServerListAuthAcceptedPreWorld`; the separate
account/login continuation remains responsible for running the confirmed
beginning of `Player::sendLogin`. A concrete live list-server TCP receive loop
is still blocked.

## Server-List Registration Packet Flow

```txt
connectServer success
  -> clear file queue buffers
  -> codec gen1
  -> SVO_REGISTERV3 + APP_VERSION, send immediately
  -> codec gen2
  -> SVO_SERVERHQPASS + hq_password
  -> SVO_NEWSERVER + length-prefixed server info fields
  -> SVO_SERVERHQLEVEL + (0 if onlystaff else hq_level default 1)
  -> SVO_SENDTEXT "Listserver,settings,allowedversions,{gtokenized versions}"
  -> SVO_SETPLYR
  -> SVO_PLYRADD for each current player
```

The C# protocol project now has byte-level body builders for these confirmed
packets. `ServerListLifecycle` now sequences these builders through a
socket-boundary interface, but the concrete remote list-server TCP client and
live response loop remain blocked.

## Queue Behavior

`ServerList::sendPacket` appends a newline before passing the packet into
`CFileQueue::addPacket`. The list-server socket uses `ENCRYPT_GEN_2` after
registration, so `CFileQueue::sendCompress` zlib-compresses queued bytes and
prefixes a raw big-endian short length when flushed. The zlib socket flush
format is now fixture-confirmed in `docs/spec/CFILEQUEUE_FIXTURE_HARNESS.md`;
real list-server socket connection/reconnect lifecycle remains a separate
milestone.
