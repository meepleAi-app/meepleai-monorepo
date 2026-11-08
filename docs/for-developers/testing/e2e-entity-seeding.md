# E2E Entity Seeding Infra (Issue #1928 Task B)

> **Status**: Shipped 2026-06-06. References: [spec consolidato](../../superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md) DEC-B-1..8.

## 1. API Reference

### TypeScript factory (`apps/web/e2e/_helpers/seedEntities.ts`)

```typescript
import { test, expect } from '@playwright/test';
import { seedAuthSession } from './_helpers/seedAuthSession';
import {
  seedGameNight,
  seedSession,
  seedPlayer,
  cleanupTestEntities,
  newTestRunId,
} from './_helpers/seedEntities';

test.describe('My data-driven flow', () => {
  let testRunId: string;

  test.beforeEach(async ({ page }) => {
    testRunId = newTestRunId(test.info().testId);
    await seedAuthSession(page, { role: 'admin' });
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) await cleanupTestEntities(page, { testRunId });
  });

  test('seeds GN with player + guest', async ({ page }) => {
    const gn = await seedGameNight(page, {
      testRunId,
      status: 'Published',
      ownerEmail: 'host@e2e.test',
    });
    await seedPlayer(page, {
      testRunId,
      gameNightId: gn.gameNightId,
      role: 'player',
    });
    await seedPlayer(page, {
      testRunId,
      gameNightId: gn.gameNightId,
      role: 'guest',
      displayName: 'E2E Guest',
    });
    // ... navigate + assert UI
  });
});
```

### Admin endpoints (BE)

| Endpoint | Command | Response |
|---|---|---|
| `POST /api/v1/admin/test/seed/game-night` | `SeedTestGameNightCommand` | `{ gameNightId, ownerId, testRunId }` |
| `POST /api/v1/admin/test/seed/session` | `SeedTestSessionCommand` | `{ sessionId, gameNightId, isLive, testRunId }` |
| `POST /api/v1/admin/test/seed/player` | `SeedTestPlayerCommand` | `{ playerId, gameNightId, role, isGuest, testRunId }` |
| `POST /api/v1/admin/test/seed/library-game` | `SeedTestLibraryGameCommand` | `{ gameId, libraryEntryId, ownerId, testRunId }` |
| `POST /api/v1/admin/test/seed/user-game-session` | `SeedTestUserGameSessionCommand` | `{ sessionId, userLibraryEntryId, testRunId }` |
| `POST /api/v1/admin/test/seed/cleanup` | `CleanupTestEntitiesCommand` | `{ testRunId, deletedGameNights, deletedSessions, deletedInvitations, deletedRsvps, deletedUsers, deletedLibraryEntries, deletedSharedGames, deletedUserGameSessions, durationMs }` |

### 🔴 Chi CREA un utente e chi lo RIUSA (#4054)

I due endpoint che accettano `ownerEmail` lo trattano in modo **opposto**, e il nome del campo non
lo dice: letto dall'esterno suggerisce «l'email di un owner esistente», e per uno dei due è falso.

| Endpoint | Semantica di `ownerEmail` | Email già presente |
|---|---|---|
| `/seed/game-night` | **CREA** un utente host nuovo (stampato col `testRunId` della chiamata) | **409** `conflict`, messaggio che nomina `ownerEmail` |
| `/seed/library-game` | **lookup-or-create**: riusa l'utente se esiste, altrimenti lo crea | 200, `ownerId` è quello preesistente |

Conseguenza pratica, perché non è un caso di laboratorio: le tre spec `cross-asse-journey-*`
seminano lo stesso `ANNA_PERSONA.email` (`anna.host@meepleai.test`). Journey #1 passa da
`/seed/game-night` (crea), journey #2 e #3 da `/seed/library-game` (riusa e quindi crea per primo
chi arriva prima). In locale `playwright.config.ts` ha `fullyParallel: true` e `workers: 2`, quindi
l'ordine fra spec non è garantito; e un `afterEach` che non arriva a chiamare `/seed/cleanup`
lascia quella riga nel database per **tutte** le run successive. In entrambi i casi la chiamata a
`/seed/game-night` fallisce. Prima di #4054 con un `internal_server_error` e `message: "An
unexpected error occurred"`: `ApiExceptionHandlerMiddleware` non riconosce `DbUpdateException`
(tratta a parte solo la sottoclasse `DbUpdateConcurrencyException`) e la manda nel fallback, dove
il corpo porta al massimo `ex.StackTrace` — e solo in Development. Il nome del vincolo vive in
`InnerException.Message`, quindi **non arrivava al chiamante per nessuna via**: non era solo il
DETAIL di Postgres («Key (email)=(…) already exists») a mancare. Oggi: un 409 che nomina il campo.

**Regola per chi scrive una spec nuova**: se ti serve un host, derivalo dal `testRunId`
(`host-${testRunId}@e2e.test`) invece di usare un indirizzo fisso condiviso. Se ti serve
*quell'utente* perché un'altra chiamata lo ha già creato, usa `/seed/library-game` (che riusa) o
l'`ownerId` che ti ha restituito.

### Violazioni di vincolo: un 4xx che nomina il vincolo, non un 500 (#4054)

Oltre alle due guardie che nominano il campo (`ownerEmail` su `/seed/game-night`, `userId` su
`/seed/player`, dove `(event_id, user_id)` è unico), **tutti** i seeder persistono via
`TestSeedPersistence.SaveSeedAsync`, che traduce gli SQLSTATE Postgres attribuibili al chiamante:

| SQLSTATE | Esito HTTP | Messaggio |
|---|---|---|
| `23505` unique_violation | 409 `conflict` | nomina vincolo e tabella (es. `IX_users_Email` su `users`) |
| `23503` foreign_key_violation | 400 `bad_request` | nomina la FK, invita a seminare prima il padre |

Ogni altro guasto resta un 500, com'è giusto.

⚠️ **Un 409 non è sempre colpa di chi chiama**, e il messaggio non lo afferma: su
`/seed/session` con `isLive: true` il `session_code` sono 6 cifre esadecimali prese da un Guid
dentro l'handler (16^6 valori, collisione di compleanno attesa intorno alle 4.800 righe vive), e
nessun campo della richiesta lo controlla. Leggi il **nome del vincolo** prima di cercare l'errore
nel tuo payload: è l'unica parte che Postgres non redige.

Il gate `TestSeedPersistenceArchitectureTests` (`Category=Unit`) blocca una `SaveChangesAsync`
diretta nel bounded context `Testing`: è la differenza di sette caratteri che riapre la classe
senza che nessun test del nuovo seeder se ne accorga.

## 2. Opt A Architectural Rationale

**DEC-B-1**: admin endpoint MediatR canonical (vs direct DB or gRPC). Coerente con `AdminCatalogSeedEndpoints.cs` pattern + CQRS rule.

**DEC-B-8**: explicit `TestRunId` column su 5 persistence entities (vs shadow property). Production-proven, no EF Core 9 + Npgsql gotcha shadow-property-null-after-save.

## 3. Triple Gate Defense-in-Depth (DEC-B-4)

```
┌─ Component 1/3: STARTUP fail-fast ────────────────────────┐
│  Program.cs throws InvalidOperationException if both:     │
│    ASPNETCORE_ENVIRONMENT=Production                      │
│    E2E_SEEDING_ENABLED=true                               │
│  Prevents accidental prod deployment with flag enabled.   │
└────────────────────────────────────────────────────────────┘
┌─ Component 2/3: Conditional endpoint registration ────────┐
│  Program.cs registers /api/v1/admin/test/seed/* ONLY if:  │
│    !IsProduction && E2E_SEEDING_ENABLED=true              │
│  Returns 404 if either condition is false.                │
└────────────────────────────────────────────────────────────┘
┌─ Component 3/3: RequireAdminSessionFilter ────────────────┐
│  Group-level filter on endpoint group:                    │
│    Unauthenticated → 401                                  │
│    Authenticated non-admin → 403                          │
│    Admin session → endpoint executes                      │
└────────────────────────────────────────────────────────────┘
```

## 4. testRunId Convention (DEC-B-5)

Format: `e2e-{playwrightTestId}-{epochMs}`

Regex validation: `^e2e-[a-zA-Z0-9]{8,32}-\d{13}$`

All factory functions REQUIRE `testRunId` (TypeScript `required`). Validators enforce format server-side.

## 5. CI Ops Runbook

### Enable for E2E CI job

```yaml
# .github/workflows/<workflow>.yml
jobs:
  e2e-tests:
    env:
      ASPNETCORE_ENVIRONMENT: 'Testing'
      E2E_SEEDING_ENABLED: 'true'
      PLAYWRIGHT_AUTH_BYPASS: 'true'
```

### Local development

```bash
# Terminal 1: BE
cd apps/api/src/Api
ASPNETCORE_ENVIRONMENT=Development E2E_SEEDING_ENABLED=true dotnet run

# Terminal 2: FE
cd apps/web && pnpm dev

# Terminal 3: Playwright
cd apps/web && pnpm exec playwright test
```

### Env failure recovery

**Symptom**: App refuses to start with `InvalidOperationException`:
```
E2E_SEEDING_ENABLED=true is FORBIDDEN in Production environment.
```

**Cause**: Deployment misconfigured `E2E_SEEDING_ENABLED=true` in production.

**Resolution**:
1. Verify `ASPNETCORE_ENVIRONMENT` value (Production)
2. Remove `E2E_SEEDING_ENABLED` from deployment config (Kubernetes ConfigMap, App Service Settings, etc.)
3. Restart app

The flag MUST be set ONLY in CI E2E job, NEVER in deployment runtime.

## References

- Spec consolidato: [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../../superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md)
- Plan Task B: [`docs/superpowers/plans/2026-06-05-asse-d-p4-task-b-be-seeding-infra.md`](../../superpowers/plans/2026-06-05-asse-d-p4-task-b-be-seeding-infra.md)
- Issue: [#1928](https://github.com/meepleAi-app/meepleai-monorepo/issues/1928)
- BE pattern reference: [`AdminCatalogSeedEndpoints.cs`](../../../apps/api/src/Api/Routing/Admin/AdminCatalogSeedEndpoints.cs)
- FE auth seeding companion: [`seedAuthSession.ts`](../../../apps/web/e2e/_helpers/seedAuthSession.ts)
