# Porting Milestones

These milestone specs are the execution queue for finishing the C#/.NET 1:1 port.

Use them one at a time. A good command to give the agent is:

```txt
Execute docs/porting-milestones/milestone-1-production-settings-startup.md
```

Rules for every milestone:

- Read `docs/AGENTS.md`, `docs/COMPATIBILITY_RULES.md`, `docs/SERVER_SPEC.md`, `docs/PORTING_PLAN.md`, `docs/KNOWN_BLOCKERS.md`, and the milestone file first.
- Use `ai_resources/GServer-CPP-ORIGINAL/` and `external/gs2lib/` as source of truth.
- Never modify `ai_resources/`.
- Do not invent behavior.
- If behavior is unclear, document it as blocked and continue with the next safe item in the same milestone.
- Write compatibility tests before implementation where possible.
- Run `dotnet build GServerSharp.sln` and `dotnet test GServerSharp.sln`.
- Confirm `ai_resources/` is untouched.
- Commit all pending changes at the end.

## Sequence

1. `milestone-1-production-settings-startup.md`: replace dev-only startup assumptions with source-confirmed production settings/startup scaffolding.
2. `milestone-2-inbound-protocol-completion.md`: complete confirmed inbound packet/framing gaps before gameplay dispatch.
3. `milestone-3-production-auth-serverlist.md`: replace fake auth boundary with source-confirmed list-server/auth protocol behavior.
4. `milestone-4-account-persistence.md`: port account/default-account/guest persistence semantics.
5. `milestone-5-filesystem-resource-loading.md`: port production folders, file indexing, level/resource lookup.
6. `milestone-6-warp-sendlevel-runtime-boundary.md`: finish source-confirmed `warp`, `setLevel`, `sendLevel`, and old-client level send boundaries.
7. `milestone-7-live-world-session-forwarding.md`: implement live player registry, level membership, visibility, and packet forwarding.
8. `milestone-8-movement-links-chests.md`: port movement property side effects, links, signs, and chest boundary.
9. `milestone-9-file-transfer-cache.md`: port client file/resource transfer and cache/update behavior.
10. `milestone-10-rc-nc-admin-serverlist.md`: port RC, NC, admin, and server-list surfaces.
11. `milestone-11-npc-baddy-item-weapon-runtime.md`: port level runtime entities that are not the scripting VM itself.
12. `milestone-12-scripting-runtime.md`: recover exact scripting dependencies and port the GS2/V8-compatible scripting boundary.
13. `milestone-13-combat-player-gameplay.md`: port combat, damage, death, hearts, AP, spar, and player gameplay rules.
14. `milestone-14-inventory-items-chat-guild.md`: port only inventory/item, chat/profile, and guild behavior that has concrete recovered C++ handlers, packet paths, persistence paths, or runtime rules.
15. `milestone-15-timing-save-loop-production-hardening.md`: port server timing, autosave, shutdown, websocket, and production hardening behavior.
16. `milestone-16-client-certification.md`: build the compatibility harness and certify against the closed-source client.

Do not skip earlier milestones unless the milestone itself says it is independent. The port only counts as complete when milestone 16 passes and the original client cannot distinguish the C# server from the C++ server.
