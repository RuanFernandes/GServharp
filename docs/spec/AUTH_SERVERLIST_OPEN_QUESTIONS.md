# Auth And Server-List Open Questions

- Exact production account validation remains blocked on a full `Account::loadAccount` and account-file compatibility pass.
- The exact list-server authentication authority is external; local C# models
  it through request/response boundaries until live/list-server behavior is
  captured.
- `Player::sendLogin` success enters account props, `Server::playerLoggedIn`, world/level warp, file sending, RC/NC flows, and optional scripting events. It is intentionally not implemented here.
- The exact zlib socket bytes for gen2 list-server `CFileQueue` flush are now
  fixture-confirmed. Real list-server integration still needs the production
  connection lifecycle and auth service boundary.
- The production auth boundary can now queue `SVO_VERIACC2` through an
  `IProductionServerListGateway`, and the response handler can consume
  `SVI_VERIACC2` success/rejection responses for pending sessions. The actual
  zlib-framed live list-server receive loop is still not ported.
- `ServerList::connectServer` registration packet bodies, local IP discovery,
  and queue codec transition timing are implemented behind an interface. A
  concrete remote list-server TCP client is still blocked.
- The banned-account rejection message applies `guntokenize().replaceAll("\n", "\r")`; exact tokenization fixtures are not implemented yet.
