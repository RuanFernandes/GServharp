# Post-Login Packet Dispatch Spec

## Source Files

- `ai_resources/GServer-CPP-ORIGINAL/server/src/player/Player.cpp`
  - `Player::createFunctions`
  - `Player::parsePacket`
  - `Player::msgPLI_NULL`
  - `Player::msgPLI_PLAYERPROPS`
- `ai_resources/GServer-CPP-ORIGINAL/server/include/Player.h`
- `external/gs2lib/include/IEnums.h`

## Confirmed C++ Behavior

After socket-frame decode, `Player::parsePacket` loops over decoded packet bytes:

1. If `m_nextIsRaw` is set, read exactly `m_rawPacketSize` bytes and clear the
   flag. Clients and RC versions above `RCVER_1_1` strip one trailing newline
   from that raw payload when present.
2. Otherwise, read a newline-delimited packet.
3. For inbound encryption gen3, decrypt the extracted packet before reading the
   packet id.
4. Read the packet id with `readGUChar`.
5. Dispatch through the global `TPLFunc` table initialized by
   `Player::createFunctions`.
6. If the selected handler returns false, `parsePacket` returns false and the
   socket manager unregister/delete path begins.

The default `TPLFunc` entry is `Player::msgPLI_NULL`.

`msgPLI_NULL` behavior is source-confirmed:

- reset the packet read cursor
- print/log the unknown packet id and raw bytes
- increment `m_invalidPackets`
- if `m_invalidPackets > 5`, send `PLO_DISCMESSAGE` with
  `Disconnected for sending invalid packets.` and return false
- otherwise return true

## Implemented C# Boundary

`PostLoginPacketDispatcher` is a production-safe decoded-packet
dispatcher below the socket frame decoder and above gameplay/runtime handlers.

Implemented:

- `PLI_PLAYERPROPS` dispatch for the already-confirmed movement/player-prop
  subset through `IncomingPlayerPropsParser` and
  `RuntimePlayerPropsApplier.ApplyConfirmed`.
- Parsed player props whose byte encoding is confirmed but whose C++ runtime
  side effects are not ported yet are guarded at the production dispatcher
  boundary. Confirmed earlier props in the same packet are applied in packet
  order, then the dispatcher returns a blocked result instead of crashing or
  pretending the later side-effect branch is implemented. This currently covers
  cases such as `PLPROP_NICKNAME`, whose C++ branch enters word filtering,
  `setNick`, global forwarding, and self echo, `PLPROP_CARRYNPC`, whose C++
  branch enters NPC ownership, duplicate carry checks, NPC deletion packets,
  and local/global forwarding, and `PLPROP_STATUS`, whose C++ branch enters
  death/revive/drop/leader behavior.
- Assigned-but-unimplemented C++ `TPLFunc` ids return a blocked result and do
  not increment the invalid-packet counter.
- Unassigned ids follow the source-confirmed `msgPLI_NULL` invalid-packet
  counter.
- The sixth unassigned packet returns a stop result with exact
  `PLO_DISCMESSAGE "Disconnected for sending invalid packets."` bytes.

This dispatcher intentionally accepts already-decoded inner packet bytes. It
does not perform socket-frame buffering, encryption, compression, newline
splitting, or `PLI_RAWDATA` state transitions. Those remain in
`SocketReceiveBuffer`, `InboundPacketDecoder`, and
`ClientPacketStreamFramer`.

`PostLoginFrameHandler` wires the dispatcher to the production TCP
skeleton's `IClientSocketFrameHandler` interface for already-authenticated
post-login sessions:

- decodes one socket frame with `InboundPacketDecoder`
- logs any source-compatible decode warnings
- splits decoded bytes with stateful `ClientPacketStreamFramer`
- dispatches each inner packet through `PostLoginPacketDispatcher`
- logs each dispatch result with the dispatch status
- continues after handled and non-fatal invalid packets
- stops without outbound bytes for assigned-but-unimplemented blocked packets
- stops with the exact C++ invalid-packet disconnect bytes when
  `msgPLI_NULL` exceeds five invalid packets

## Blocked / Not Implemented

- Production auth/login wiring into the TCP listener.
- Production multi-session socket manager.
- A production session factory that creates `PostLoginFrameHandler`
  only after real login/auth/world-entry prerequisites have been satisfied.
- Dispatch for assigned gameplay/admin packets other than the confirmed
  `PLI_PLAYERPROPS` subset.
- `PLI_RAWDATA` dispatch side effect (`m_nextIsRaw`/`m_rawPacketSize`) in the
  production dispatcher. The packet framing helper already models the decoded
  raw-data transition, but production session state is not wired yet.
- Websocket branches.
- Live movement forwarding, touch tests, level links, NPC events, combat, and
  any other gameplay side effects.

## Test Coverage

Confirmed tests cover:

- decoded `PLI_PLAYERPROPS` applying the supported movement subset
- decoded `PLI_PLAYERPROPS` blocking parsed-but-unported runtime side-effect
  props such as `PLPROP_NICKNAME`, `PLPROP_CARRYNPC`, and `PLPROP_STATUS`
  without incrementing the invalid-packet counter and without losing
  already-applied confirmed preceding props
- assigned-but-unimplemented packets returning blocked status without counting
  as invalid
- unassigned packets following the `msgPLI_NULL` counter
- sixth unassigned packet returning exact invalid-packet disconnect bytes
- post-login frame handler decode/framing/dispatch integration
- production TCP loopback dispatch and disconnect-byte writes through the
  handler boundary
