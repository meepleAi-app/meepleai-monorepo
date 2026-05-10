# API Smoke Tests (Bruno collection)

Bruno collection git-friendly per smoke test API CRUD, parte di EPIC #906.

## Setup locally

1. Install Bruno desktop: <https://www.usebruno.com/downloads>
2. Open the collection:
   - Bruno → Open Collection → seleziona `tests/api-smoke/bruno-collection/`
3. Select environment: `local` (default) o `staging`
4. Click Run.

## Setup CLI (per CI o headless)

```bash
# Pin version
cat tests/api-smoke/.bruno-version  # → 2.15.1

# Run all 4 sub-collections against local env
# (richiede `make dev-core` running)
tests/api-smoke/run-smoke.sh --env local

# Run only private-game collection against staging
tests/api-smoke/run-smoke.sh --env staging --collection private-game

# Windows
.\tests\api-smoke\run-smoke.ps1 -Env local
.\tests\api-smoke\run-smoke.ps1 -Env staging -Collection private-game
```

## CI policy

Il workflow `.github/workflows/api-smoke.yml` gira **solo su PR `main-dev → main-staging`**
(no su PR feature → main-dev). Vedi commit `674f3c355` (issue #897) per la rationale
CI policy 2026-05-09.

**Soft-launch attivo**: `continue-on-error: true` per le prime 1-2 settimane mentre
l'infrastruttura stabilizza (issue #910 DoD). Rimuovere dopo che 30 scenari Bruno
(#902-905) sono verdi in CI per 2 settimane consecutive.

### Note operative su CI

- **Windows runner usa staging API**: se staging è degradato, il job Windows fallisce
  (mascherato da `continue-on-error`). I failure Windows sono canary di
  staging-health, non regressioni di codice.
- **Test passwords in YAML**: `SmokeUser1!!` e `SmokeAaron1!!` sono hardcoded nel
  workflow. Sono per container CI ephemeri / utenti `IsDemoAccount=true` — bassa
  sensibilità accettabile per smoke test.

### Distinct dal nightly E2E

Questo workflow è **distinto** da `e2e-smoke-real-backend.yml`:

| Aspetto | `api-smoke.yml` | `e2e-smoke-real-backend.yml` |
| --- | --- | --- |
| Trigger | PR `main-dev → main-staging` | Cron nightly + manual |
| Tooling | Bruno CLI (HTTP) | Playwright (browser) |
| Scope | API contracts CRUD 4 domini | E2E user journeys via UI |
| Persona | `smoke-aaron@meepleai.test` | `smoke-user@meepleai.test` |
| Failure action | continue-on-error | Open P0 issue |

I 4 issue P0 nightly recenti (#884, #877, #832, #821) NON impattano
`api-smoke.yml` perché la pipeline è separata. Audit nightly E2E vs
api-smoke completato in T7 del plan
`docs/superpowers/plans/2026-05-09-sg0-foundation-implementation.md`.

**Audit 2026-05-09**: confermato no overlap (vedi commento su #884). I 4 P0 nightly OPEN (#884, #877, #832, #821) non bloccano l'attivazione di api-smoke.yml.

## Persona override mechanism

⚠️ **NON usare** Aaron `badsworm@alice.it` nei smoke test API. Aaron è **superadmin**
nel DB reale — `TierEnforcementService.GetLimitsAsync` lo risolve come
`TierLimits.Unlimited`, bypassando le tier-quota che vogliamo testare.

✅ **Usare** `smoke-aaron@meepleai.test` (creato da `tests/fixtures/smoke-test-users.sql`):

| Campo | Valore |
| --- | --- |
| Email | `smoke-aaron@meepleai.test` |
| Password | `SmokeAaron1!!` |
| Role | `user` (NON admin/editor/superadmin) |
| Tier | `free` |
| IsContributor | `false` (per evitare premium override) |
| EmailVerified | `true` (skip grace period) |
| IsDemoAccount | `true` |
| PasswordHash format | PBKDF2 v1.{iterations}.{salt}.{hash} |

### Quote free-tier validate dai test

| Risorsa | Limite free | Sub-issue |
| --- | --- | --- |
| privateGame | 3 | SG1 #902 |
| Agent slots | 1 | SG1, SG3 #904 |
| ChatSession | 5 | SG4 #905 |
| RAPTOR rebuild | tier-locked (free → 403) | SG2 #903 |

### Discrepanza con seed Aaron reale

`docker-compose.dev.yml` + `INITIAL_ADMIN_EMAIL` seed l'admin di sistema
(`smoke-user@meepleai.test`, NON Aaron `badsworm`). La fixture SQL
`tests/fixtures/smoke-test-users.sql` aggiunge invece `smoke-aaron@meepleai.test`
come secondo utente, idempotente (`ON CONFLICT ("Email") DO NOTHING`).

## Naming disambiguation (preview ADR-054)

I 4 sub-collection mappano a 4 entità con naming ambiguo:

| Sub-collection | Entità target | Note |
| --- | --- | --- |
| `private-game/` | `PrivateGame` | UserLibrary BC |
| `kb/` | KB di game | KnowledgeBase BC |
| `agents/` | `AgentDefinition` | KnowledgeBase BC |
| `sessions/` | GameSession, ChatSession, ChatThread | Dettagli sotto |

### Tabella disambiguazione `sessions/`

| Concept | Backend entity | Endpoint | Cosa rappresenta |
| --- | --- | --- | --- |
| Play session | `GameSession` | `/api/v1/game-sessions` | Una partita |
| Chat history | `ChatSession` | `/api/v1/chat/sessions` | Storia messaggi |
| RAG thread | `ChatThread` | `/api/v1/chat/threads` | Thread RAG |
| Game night | `GameNightEvent` | `/api/v1/game-nights` | Evento RSVP |

ADR-054 (post-MVP) proporrà di rinominare gli endpoint a
`play-session/chat-thread/chat-history/game-night` per eliminare l'ambiguità.
Out of scope per #906 (EPIC).

### Naming-confusion guard (since SG4)

POST `/chat/sessions/{id}/rename` include un **400 INVALID_ENTITY_TYPE hint** difensivo: se il UUID fornito appartiene a un `ChatThread` (che ha il proprio endpoint di rename), l'API risponde con:

```json
{
  "error": "INVALID_ENTITY_TYPE",
  "hint": "The provided UUID belongs to a ChatThread (use POST /chat/threads/{id}/title) not a ChatSession"
}
```

Questo guard è **solo sull'endpoint rename** per evitare rumore su ogni GET — è una disambiguazione opportunistica quando l'utente sta facendo un errore chiaro (rename su entità sbagliata).

ADR-054 (post-MVP) rinominerà gli endpoint a `play-session/chat-thread/chat-history/game-night` per eliminare l'overload di naming in modo strutturale.

## SG2 — KB lifecycle (since 2026-05-10)

### KB management endpoints

- `POST /games/{gameId:guid}/kb/reindex` — synchronous re-embed all chunks of all PDFs (returns 202 + jobId + status="completed"; future enhancement: true async background job).
- `POST /games/{gameId:guid}/kb/raptor/rebuild` — rebuild RAPTOR summary tree, **tier-gated**: free-tier → 403 with `{ "error": "TIER_FEATURE_LOCKED", "feature": "RaptorRebuild" }`.

### Cascade-delete (existing, verified by SG2)

`DELETE /pdf/{pdfId}` triggers DB-level cascade for `raptor_summaries.pdf_document_id` (foreign key with `ON DELETE CASCADE`). Verified by integration test `DeletePdf_CascadeDeletesOrphanRaptorSummaries`.

### Schema finding (SG2 spec adjustment)

The original SG2 spec referenced `game_entity_relations.source_document_id` for orphan cleanup. **The actual schema does NOT have this FK** — `GameEntityRelationEntity` has only a `GameId` FK (no PDF reference). No orphan issue exists for that table. Cleanup migration was not needed.

### Idempotent reindex (SG2 implementation detail)

The reindex endpoint is intentionally idempotent for unknown or empty gameIds. If no PDFs are found for the given gameId, the handler returns 202 with `pdfCount=0` rather than 404. A dedicated game-existence guard is a carry-forward improvement (see #903).

### Sub-collection consumer

- `kb/` ← scenari implementati in #903 (SG2) — 6 .bru: login + preflight + reindex (happy path) + raptor tier-gated (403) + cascade verify (smoke) + read PDFs + idempotent unknown gameId

## SG3 — Agent CRUD lifecycle (since 2026-05-10)

### Endpoints (5 nuovi user-facing)

- `DELETE /api/v1/agents/{agentId:guid}` — soft-delete con cascade ChatThread.CloseThread()
- `POST /api/v1/agents/{agentId:guid}/restore` — un-soft-delete (thread restano Closed per spec)
- `POST /api/v1/agents/{agentId:guid}/start-testing` — Draft → Testing
- `POST /api/v1/agents/{agentId:guid}/publish` — Testing → Published
- `POST /api/v1/agents/{agentId:guid}/unpublish` — Published → Draft

### Cascade strategy (cross-aggregate)

`SoftDeleteUserAgentCommandHandler` orchestra cross-aggregate cleanup nell'application layer (DDD: domain entities NON reach across):

1. Carica agent (HasQueryFilter esclude soft-deleted)
2. Guard: `IsSystemDefined=true` → `SystemAgentProtectedException` → 403
3. `agent.SoftDelete()` (sets IsDeleted=true, raises domain event)
4. Trova `ChatThread` attivi via `IChatThreadRepository.FindActiveByAgentIdAsync(agentId)`
5. `thread.CloseThread()` su ognuno
6. Persist via `IUnitOfWork.SaveChangesAsync` (ChatThreadRepository usa Unit-of-Work pattern, non auto-save)

### Tier quota gate

`CreateUserAgentCommandHandler` pre-check: se user ha già `MaxAgentSlots` agenti attivi (free-tier=1) → `TierQuotaExceededException` → 402 con `error="AGENT_SLOT_QUOTA_EXCEEDED"`.

### System-agent guard

`AgentDefinition.IsSystemDefined` (true per agenti seedati: arbitro, game-master, chat) blocca DELETE → 403 `SYSTEM_AGENT_PROTECTED`.

## Sub-collection consumers

- `private-game/` ← scenari implementati in #902 (SG1)
- `kb/` ← scenari implementati in #903 (SG2) — 6 .bru: reindex, raptor (tier-gated), cascade verify, list PDFs, idempotent unknown gameId
- `agents/` ← scenari implementati in #904 (SG3) — 10 .bru: login + preflight + create user-agent + lifecycle (start-testing/publish/unpublish) + soft-delete cascade + restore + builder filters + tier quota
- `sessions/` ← scenari implementati in #905 (SG4) — 4 sub-folder: game-session/ chat-session/ chat-thread/ naming-disambiguation/
