# Domain Invariants — Agent Builder (SP7 US-33)

**Data**: 2026-07-15
**Origine**: spec-panel [#1889](https://github.com/meepleAi-app/meepleai-monorepo/issues/1889) (audit §3, 8 invarianti candidati) → risolti via [#2964](https://github.com/meepleAi-app/meepleai-monorepo/issues/2964) direzione "allinea a backend (MVP)".
**Decisione governante**: [ADR-085](../../for-claude/architecture/adr/adr-085-agent-builder-admin-backend-alignment.md)
**Scope**: bounded context `KnowledgeBase` — aggregato `AgentDefinition`

> Questo doc consolida le ambiguità AB-1..AB-8 del brief agent-builder in invarianti **allineati al backend `AgentDefinition` esistente** (admin-only). NON introduce lo scope user-facing `/editor/*` del brief (differito — vedi ADR-085 § Consequences).

---

## Backend di riferimento (verificato)

| Elemento | Realtà |
|---|---|
| Aggregato | `AgentDefinition` (non `AgentProposal`) — `KnowledgeBase/Domain/Entities/AgentDefinition.cs` |
| Route | `/api/v1/admin/agent-definitions*` — `RequireAdminSessionFilter` (admin-only) |
| Status enum | `{Draft=0, Testing=1, Published=2}` |
| Config persistiti | `Model`, `MaxTokens`, `Temperature` + `Prompts`(JSONB), `Tools`, `Strategy`, `KbCardIds`, `GameId?`, `TypologySlug`({arbitro,game-master,chat}), `ChatLanguage`, `IsActive`, soft-delete |
| Testing | `PlaygroundChatCommand` (SSE) + `PlaygroundTestScenario` (scenari salvati) |
| Assenti | `ConfidenceThreshold`, `Tone` picker, version-history/rollback |

---

## Invarianti

### I-AB-1 — Lifecycle a 3 stati, publish via Testing
Il lifecycle è **`Draft → Testing → Published`** (enum backend). Transizioni legali:
- `StartTesting()`: Draft/Testing → Testing. **Blocca** da Published ("Unpublish first").
- `Publish()`: **blocca Draft→Published diretto** ("Move to Testing first") → l'agente deve passare da Testing. Setta `IsActive = true`.
- `Unpublish()`: Published → Draft (`IsActive = false`).

**`Archived` NON è un 4° status di lifecycle.** È il soft-delete (`SoftDelete()/Restore()`), oggi **non esposto via REST** (il `DELETE` è hard-delete). Nella UI "Archiviati" = filtro sui soft-deleted (richiede esporre SoftDelete via REST — futuro), non un badge accanto a Draft/Testing/Published.

### I-AB-2 — Nessun confidence-threshold per-agente (MVP)
Il backend **non persiste** un `ConfidenceThreshold` per agente. La confidence renderizzata a runtime (`RagValidation/ConfidenceValidationService`) è un **output** della generazione, non una soglia configurabile per agente. **MVP**: nessun gate/slider confidence nel builder. Differito come estensione backend; quando aggiunto, definire semantica deterministica (tier-di-disclaimer XOR refuse-gate).

### I-AB-3 — Cardinalità Agent↔Game + surfacing inline
`AgentDefinition.GameId` è **opzionale (0..1)**: un agente è legato ad al più 1 game. Un game può avere **N** agenti. Surfacing inline (mockup H, `/library/games/[id]/agent`): **solo agenti `Published` + `IsActive`** sono esposti all'utente; gli agenti `Draft`/`Testing` non compaiono mai inline. Regola di selezione quando >1 Published per game: **default = più recentemente pubblicato** (rivedibile se emerge un "default agent" designato).

### I-AB-4 — Ownership & visibilità: admin-only (MVP)
**MVP**: la costruzione agenti è **admin-only** (`RequireAdminSessionFilter`). Nessuna ownership per-utente. Gli agenti `Published` sono visibili a **tutti gli utenti** del game linkato via chat inline (H). Costruzione/edit/publish = admin/dogfood. La ownership per-utente + route user-facing `/editor/*` è **estensione futura** (ADR-085), non MVP.

### I-AB-5 — Publish gate = passaggio da Testing
Il publish richiede il passaggio da `Testing` (guard backend `Publish()`). **Nessun** "Pubblica subito" da Draft. MVP: **nessun gate di qualità aggiuntivo** (KB non-vuoto NON è richiesto — 0-KB ammesso, l'agente userà i soli prompt). Il flusso canonico è **Crea (Draft) → testa (playground) → pubblica**.

### I-AB-6 — Nessun versioning/rollback (MVP)
Il backend **non ha** version history né rollback. `UpdateAgentDefinitionCommand` muta l'agente **in place**. **MVP**: niente "Last 5 versions"/diff/ripristino (mockup G della feature version-diff = fuori MVP). Differito come estensione backend.

### I-AB-7 — Linking KB
Gli agenti linkano KB via `KbCardIds` (lista Guid). MVP: il link non impone un precondition di readiness a livello di dominio; la gestione di un KB non-ready/failed è **best-effort a runtime** (l'agente ignora i chunk non disponibili). Se in futuro si vuole vietare il link di KB non-`Ready`, va aggiunta una validazione esplicita.

### I-AB-8 — Catalogo eventi + terminologia
Gli agenti sono **RAG su KB**: usare **"indicizzazione"**, mai **"training"** (correggere il copy notifiche I/J `agent training` → `agent indexing/ready`). Eventi lifecycle candidati a notifica: **publish**, **KB index-complete** (sul KB linkato), **invocation-milestone** (backend traccia `InvocationCount`/`LastInvokedAt`). MVP: definire in fase impl quali emettono notifica; il "daily summary" del brief è opzionale.

---

## Note di mapping (brief ↔ backend)

- **"tone preset" (E-Step 3)** → analogo parziale: `TypologySlug` ∈ {arbitro, game-master, chat} + `ChatLanguage`. NON il picker a 5 toni + confidence-slider del brief.
- **"AgentProposal"** → `AgentDefinition`.
- **status "Archived"** → soft-delete (non lifecycle).
- **"Testing"** → status persistito reale (`Testing=1`), entrato via `StartTesting()`.

---

## Riferimenti
- ADR-085 — decisione align-to-backend
- Audit `docs/for-developers/audits/2026-07-15-sp7-spec-panel-invariant-diff.md` §2-§3
- Brief `admin-mockups/briefs/SP7-game-night-agent-builder.md` (sez. D–H — corretta)
- Backend: `AgentDefinition.cs`, `AdminAgentDefinitionEndpoints.cs`, `AgentPlaygroundEndpoints.cs`
