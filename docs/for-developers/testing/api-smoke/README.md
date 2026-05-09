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

## Sub-collection consumers

- `private-game/` ← scenari implementati in #902 (SG1)
- `kb/` ← scenari implementati in #903 (SG2)
- `agents/` ← scenari implementati in #904 (SG3)
- `sessions/` ← scenari implementati in #905 (SG4)
