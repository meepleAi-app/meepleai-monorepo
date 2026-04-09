# MeepleDev — Fast Dev Loop con Mock/Real Toggle

**Date**: 2026-04-09
**Status**: Design
**Author**: Brainstorming session
**Related**: `docs/superpowers/specs/2026-03-18-integration-hardening-e5base-design.md`

---

## 1. Vision & Scope

### One-liner

Un sistema a due leve indipendenti che permette a un developer di vedere una pagina in ≤5 secondi dal clone, con la capacità di attivare progressivamente servizi reali quando servono integrazione o testing end-to-end.

### Le due leve

**Leva 1 — Frontend MSW granulare**
Intercetta le chiamate HTTP nel browser prima ancora che raggiungano il backend. Permette di lavorare con backend spento. Controllo per gruppo (handler file) con drill-down runtime per singolo endpoint.

**Leva 2 — Backend DI mock wrapper**
Quando il backend è vivo, decorator wrapper attorno ai servizi esterni possono dispatchare runtime tra implementazione reale o mock. Permette di testare la logica reale degli use case MediatR senza dipendere da Ollama, OpenRouter, Cloudflare R2, BGG, n8n, ecc.

Le due leve sono **indipendenti**: un dev può usare solo MSW (frontend-only, zero backend), solo mock BE (backend vivo con dipendenze finte), o entrambi.

### Goal quantitativi (success criteria)

| Metrica | Target |
|---|---|
| `make dev:fast` → primo paint browser | ≤ 5 secondi |
| Cambio riga TSX → HMR visibile | ≤ 1 secondo |
| Cambio riga C# (con `dotnet watch`) → endpoint ricompilato | ≤ 5 secondi |
| Cambio toggle mock/real runtime (fase 2) | 0 restart, next request usa nuovo stato |
| Codice mock nel bundle di produzione | 0 bytes (verificato in CI) |
| Codice mock eseguibile in runtime di produzione | 0 paths (verificato in CI) |

### Non-goal

- Replicare al 100% il comportamento dei servizi AI reali (i mock sono plausibili, non fedeli)
- Sostituire Postgres o Redis con mock — restano veri o assenti
- Fornire un framework riutilizzabile per altri progetti — è una capability interna
- Offrire parità 1:1 con produzione (non è uno staging, è un dev loop)
- Sostituire `make dev`, `make integration`, `make staging` — `dev:fast` è additivo

### Personas

- **Frontend Dev** — caso d'uso primario: itera su componenti/pagine, vuole vedere il browser aggiornarsi, non vuole avviare Docker
- **Full-stack Dev** — itera su BE+FE, ha backend locale con `dotnet watch`, mocka i servizi AI per velocità
- **Nuovo dev (onboarding)** — clona il repo, `pnpm install`, `make dev:fast`, vede subito l'app
- **Demo/screenshot** — apre in mock mode con uno seed scenario, registra video/screenshot deterministici

---

## 2. Architettura

### Diagramma di alto livello

```
┌─────────────────────────────────────────────────────────────────┐
│                         BROWSER (dev mode)                      │
│  ┌───────────────┐  fetch()  ┌──────────────────┐               │
│  │ React pages   ├──────────▶│ MSW Service      │               │
│  │ React Query   │           │ Worker           │               │
│  └───────────────┘           │                  │               │
│         │                    │  Dev Panel ◀─────┼── toggle UI   │
│         │                    │  Badge           │               │
│         │                    └────────┬─────────┘               │
│         │                      intercept?                       │
│         │                    ┌────┴────┐                        │
│         │               YES  │         │  NO                    │
│         │         ┌──────────▼───┐  ┌──▼──────────────────────┐ │
│         │         │ Mock handler │  │ passthrough → Next.js   │ │
│         │         │ + Scenario   │  │ proxy /api/[...path]    │ │
│         │         │ store (JS)   │  └──┬──────────────────────┘ │
│         │         └──────┬───────┘     │                        │
│         └◀───── response ┘             │                        │
└─────────────────────────────────────────┼────────────────────────┘
                                          │
                                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  .NET API (dotnet watch, locale)                │
│  ┌─────────────────┐   MediatR    ┌─────────────────────────┐   │
│  │  Endpoint       ├─────────────▶│  CommandHandler         │   │
│  └─────────────────┘              └─────────┬───────────────┘   │
│                                             │                    │
│                                       uses services              │
│                                             │                    │
│  ┌──────────────────────────────────────────▼────────────────┐  │
│  │         DI Container with Mock-Aware Wrappers             │  │
│  │                                                           │  │
│  │  ILlmService → SwitchableLlmService                       │  │
│  │                  ├─ RealLlmService (OpenRouter HTTP)      │  │
│  │                  └─ MockLlmService (scenario JSON)        │  │
│  │                                                           │  │
│  │  IEmbeddingService → SwitchableEmbeddingService           │  │
│  │                  ├─ RealEmbeddingService (Ollama)         │  │
│  │                  └─ MockEmbeddingService (hash→vector)    │  │
│  │                                                           │  │
│  │  [... stesso pattern per 8 servizi totali]                │  │
│  │                                                           │  │
│  │  Tutti leggono da: IMockToggleState (singleton)           │  │
│  │    ├─ Fase 1: popolato da env var all'avvio (frozen)      │  │
│  │    └─ Fase 2: mutabile via PATCH /dev/toggles             │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│  Postgres ✓ reale  |  Redis ✓ reale (opzionale)                │
└─────────────────────────────────────────────────────────────────┘
```

### Nuovi componenti

#### Frontend — `apps/web/src/dev-tools/`

| Componente | Responsabilità |
|---|---|
| `MockControlCore` | Store Zustand + API per leggere/scrivere toggle FE (MSW groups, role, scenario) |
| `MswHandlerRegistry` | Refactor minimo degli attuali handler per raggrupparli in moduli toggle-abili con drill-down per endpoint |
| `ScenarioStore` | Carica scenari da `docs/superpowers/fixtures/scenarios/*.json`, mantiene store in-memory stateful |
| `MockAuthStore` | Gestisce sessione finta con role switching via env + query string + (fase 2) Dev Panel |
| `DevBadge` | Componente React flottante, bottom-right, mostra stato dei toggle sempre visibile |
| `DevPanel` (fase 2) | Overlay React con sezioni: toggles, scenari, auth, recent requests |
| `devPanelBootstrap` | Guard montaggio solo in `NODE_ENV === 'development'` + `NEXT_PUBLIC_MOCK_MODE === 'true'`, dynamic import |

#### Backend — `apps/api/src/Api/DevTools/`

Cartella gated da `#if DEBUG` + `env.IsDevelopment()` + `<Compile Remove>` in Release.

| Componente | Responsabilità |
|---|---|
| `IMockToggleState` | Singleton con `service_name → bool` map + event su change |
| `MockToggleStateProvider` | Implementazione, legge da env var all'avvio |
| `Switchable{Service}` | 8 decorator wrapper (LLM, Embedding, Reranker, SmolDocling, Unstructured, BGG, S3, N8n) |
| `Mock{Service}` | 8 implementazioni mock deterministiche |
| `MockHeaderMiddleware` | Aggiunge header `X-Meeple-Mock: backend-di:<lista>` alle response |
| `DevToolsEndpoints` (fase 2) | `GET /dev/toggles`, `PATCH /dev/toggles`, `POST /dev/reset-scenarios`. Protetti da `X-Dev-Token` |

#### Shared — `docs/superpowers/fixtures/`

- `scenarios/empty.json`, `scenarios/small-library.json`, `scenarios/admin-busy.json`, ...
- `schema/scenario.schema.json` — JSON Schema validato in CI
- `validate-scenarios.ts` — script CI con Ajv, cross-check con schemi OpenAPI generati

#### Infra — `infra/`

- `Makefile`: nuovi target `dev:fast`, `dev:fast-api`, `dev:fast-full`, `dev:fast-down`
- `scripts/dev-fast.sh`: orchestratore che legge `.env.dev.local` e boots solo ciò che serve
- `scripts/dev-fast-down.sh`: cleanup con PID tracking in `.dev-fast.pids`

### Principi di isolamento

Ogni componente nuovo rispetta:
- **Single responsibility**: es. `MockAuthStore` fa *solo* role switching
- **Dipende da interfacce**: `MockControlCore` espone `getToggles() / setToggle(name, value)`; il Dev Panel lo usa senza sapere se dietro c'è env var o localStorage
- **Testabile in isolamento**: store, wrapper DI, registry unit-testabili senza browser o API
- **Tree-shakable**: import solo via `import()` dinamico o guard su `NODE_ENV`

---

## 3. Data Flow

### Flow A — Boot di `make dev:fast` (default minimo)

```
1. Dev digita `make dev:fast` in infra/
2. Script dev-fast.sh legge .env.dev.local
   ├─ DEV_BACKEND=false (default) → skip .NET
   ├─ DEV_POSTGRES=false (default) → skip Docker
   └─ DEV_REDIS=false (default) → skip Docker
3. cd apps/web && NEXT_PUBLIC_MOCK_MODE=true pnpm dev
4. Next.js Turbopack parte → ready in ~3s
5. Browser apre localhost:3000
6. MockProvider monta → MSW worker.start()
7. ScenarioStore carica default scenario dal JSON
8. DevBadge appare (bottom-right, arancione)
9. Prima richiesta fetch() → intercettata da MSW → response mock
   Totale first paint: ~5s ✓
```

### Flow B — Boot con backend locale

```
1. Dev mette DEV_BACKEND=true in .env.dev.local
2. `make dev:fast`
3. Script avvia in parallelo:
   ├─ docker compose up -d postgres
   ├─ (cd apps/api/src/Api && dotnet watch run) & → terminal dedicato
   └─ (cd apps/web && NEXT_PUBLIC_MOCK_MODE=false pnpm dev) &
4. dotnet watch carica → API :8080 pronta in ~20-30s
5. MockToggleStateProvider legge env:
   └─ MOCK_LLM=true, MOCK_EMBEDDING=true, MOCK_BGG=true, ...
6. DI Container configura wrapper: SwitchableLlmService in stato "mock"
7. Frontend parte SENZA MSW (MOCK_MODE=false)
8. Prima richiesta → proxy → backend reale → MediatR → handler
9. Handler chiama ILlmService → SwitchableLlmService
   → legge toggle → dispatcha a MockLlmService → risposta da scenario
10. Response ha header X-Meeple-Mock: backend-di:llm,embedding,bgg
11. DevBadge mostra 3 mock attivi (letto via GET /dev/toggles)
    Totale first paint: ~35s
```

### Flow C — Richiesta HTTP (MSW decide: mock o passthrough)

```
Page → React Query → httpClient.get('/api/v1/games')
         │
         ▼
   fetch('/api/v1/games')
         │
         ▼
  ┌──────────────┐
  │ MSW worker   │
  └──────┬───────┘
         │
         ▼
  MswHandlerRegistry.shouldIntercept('/api/v1/games', 'GET')
         │
         ├─ 1. Controlla gruppo 'games' in MockControlCore.toggles
         │    ├─ enabled → procedi
         │    └─ disabled → passthrough
         │
         ├─ 2. Controlla override fine-granularity
         │    (Map<endpointKey, boolean> da Dev Panel runtime)
         │
         └─ 3. Decisione
              │
    ┌─────────┴──────────┐
    │                    │
    ▼                    ▼
[Mock handler]     [Passthrough]
 ├─ legge          ├─ fetch reale
 │  ScenarioStore  │  via Next.js proxy
 ├─ applica        │  /api/[...path]
 │  MockAuthStore  ▼
 │  (role filter) Backend .NET
 ├─ genera        (può avere suoi
 │  response       mock BE attivi)
 ├─ header:
 │  X-Meeple-Mock: msw:games
 └─ ritorna
```

### Flow D — Stateful CRUD

```
User click "Crea game" in mock mode
  → POST /api/v1/games { name: "Wingspan" }
  → MSW handler 'games.createGame'
      ├─ ScenarioStore.addGame({...dati, id: 'MOCK-uuid', createdAt: now})
      └─ return 201 + gameDto

User naviga a /games
  → GET /api/v1/games
  → MSW handler 'games.listGames'
      └─ return ScenarioStore.snapshot().games   // include il nuovo ✓

User F5
  → MSW worker re-init
  → ScenarioStore.resetToScenario(currentScenarioName)
  → stato torna al seed (nuovo game perso, by design)

User apre Dev Panel → "Switch scenario: admin-busy" (fase 2)
  → ScenarioStore.loadScenario('admin-busy')
  → queryClient.invalidateQueries() → UI refetch
```

### Flow E — Toggle change runtime (fase 2)

```
Dev clicca in Dev Panel: "LLM: ON → OFF"
  │
  ├─ FE: MockControlCore.setToggle('llm', false)
  │    ├─ (se BE up) → PATCH /dev/toggles { llm: false }
  │    │              + X-Dev-Token: <dev-local-secret>
  │    └─ emit 'toggle-changed' event
  │
  ├─ BE: DevToolsEndpoints riceve PATCH
  │    ├─ IMockToggleState.Set("llm", false)
  │    └─ emette domain event ToggleChanged
  │
  ├─ FE: DevBadge si aggiorna istantaneamente
  │
  └─ Next chat request → SwitchableLlmService legge
     toggle → dispatcha a RealLlmService → chiamata OpenRouter
     reale. Nessun restart.
```

---

## 4. Contratti

### `.env.dev.local` (nuovo, gitignored, template `.env.dev.local.example`)

```bash
# ================================================================
# MeepleDev — Fast dev loop configuration
# Attivo SOLO con MOCK_MODE=true o ASPNETCORE_ENVIRONMENT=Development
# ================================================================

# -- Leva 1: cosa avviare con `make dev:fast` -------------------
DEV_BACKEND=false              # true → dotnet watch locale
DEV_POSTGRES=false             # true → docker compose postgres
DEV_REDIS=false                # true → docker compose redis

# -- Leva 2: MSW frontend ---------------------------------------
NEXT_PUBLIC_MOCK_MODE=true
NEXT_PUBLIC_MSW_ENABLE=auth,games,chat,library,admin
NEXT_PUBLIC_MSW_DISABLE=
NEXT_PUBLIC_DEV_SCENARIO=small-library
NEXT_PUBLIC_DEV_AS_ROLE=Admin

# -- Leva 3: Backend DI mock wrappers (solo se DEV_BACKEND=true)
MOCK_LLM=true
MOCK_EMBEDDING=true
MOCK_RERANKER=true
MOCK_SMOLDOCLING=true
MOCK_UNSTRUCTURED=true
MOCK_BGG=true
MOCK_S3=true
MOCK_N8N=true

# -- Dev Panel secret (fase 2) -----------------------------------
MEEPLE_DEV_TOKEN=change-me-local-only
```

### Precedenze di lettura

1. **Query string** (`?dev-role=X`) — runtime, solo per ruolo
2. **Dev Panel override runtime** (fase 2) — sessione browser, non tocca file
3. **`.env.dev.local`** — default statico
4. **Hardcoded fallback** — tutto disabilitato / sessione anonima

### Scenario JSON (`docs/superpowers/fixtures/scenarios/*.json`)

```json
{
  "$schema": "../schema/scenario.schema.json",
  "name": "small-library",
  "description": "Utente base con 3 giochi e 5 sessioni",
  "auth": {
    "currentUser": {
      "id": "MOCK-00000000-0000-0000-0000-000000000001",
      "email": "dev@meeple.local",
      "displayName": "Dev User",
      "role": "User"
    },
    "availableUsers": [
      { "id": "...", "email": "admin@meeple.local", "role": "Admin" },
      { "id": "...", "email": "editor@meeple.local", "role": "Editor" }
    ]
  },
  "games": [
    {
      "id": "MOCK-11111111-...",
      "title": "Wingspan",
      "publisher": "Stonemaier",
      "averageRating": 8.2,
      "bggId": 266192
    }
  ],
  "sessions": [],
  "library": { "ownedGameIds": [], "wishlistGameIds": [] },
  "chatHistory": []
}
```

- Schema strict validato in CI via `validate-scenarios.ts` con Ajv
- Forma allineata agli schemi OpenAPI generati da `pnpm generate:api`. Il cross-check gira (a) al boot del ScenarioStore in dev (log di warning su mismatch) e (b) nel workflow CI `dev-tools-isolation.yml` come step aggiuntivo (errore bloccante)
- ID convenzione: prefix `MOCK-` per essere riconoscibili nei log

### `IMockToggleState` (C#)

```csharp
// apps/api/src/Api/DevTools/IMockToggleState.cs
namespace Api.DevTools;

public interface IMockToggleState
{
    bool IsMocked(string serviceName);
    IReadOnlyDictionary<string, bool> GetAll();
    void Set(string serviceName, bool mocked);
    event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;
}
```

Registration (in `Program.cs`, gated):
```csharp
if (env.IsDevelopment())
{
    services.AddMeepleDevTools();
    // Internamente:
    // services.AddSingleton<IMockToggleState, MockToggleStateProvider>();
    // services.Decorate<ILlmService, SwitchableLlmService>();
    // ... 8 wrapper
}
```

Il pattern `Decorate<>` usa `Scrutor` se già presente in repo; altrimenti factory manuale. La scelta è tactical e va verificata in fase plan (non blocca il design).

### Dev Panel API (fase 2)

| Endpoint | Body | Risposta | Auth |
|---|---|---|---|
| `GET /dev/toggles` | — | `{ "llm": true, "embedding": false, ... }` | `X-Dev-Token` |
| `PATCH /dev/toggles` | `{ "llm": false }` | `204` | `X-Dev-Token` |
| `GET /dev/scenarios` | — | `[{ "name": "...", "description": "..." }]` | `X-Dev-Token` |
| `POST /dev/reset` | — | `204` | `X-Dev-Token` |

- Endpoint registrati **solo se** `env.IsDevelopment()`
- `X-Dev-Token` mancante/invalido → **404** (non 401) per non annunciare la presenza dell'endpoint
- `MEEPLE_DEV_TOKEN` in `.env.dev.local`, mai in `infra/secrets/`

### Response headers (dev mode)

```
X-Meeple-Mock: msw:games,chat                    (MSW intercept)
X-Meeple-Mock: backend-di:llm,embedding,s3       (mock BE attivi)
X-Meeple-Scenario: small-library                 (scenario corrente)
```

### Makefile targets

```makefile
dev\:fast:              # Legge .env.dev.local, boot minimo
dev\:fast-api:          # + dotnet watch locale + Postgres
dev\:fast-full:         # + Redis, tutti i mock BE disattivati
dev\:fast-down:         # Stop + cleanup PID
```

---

## 5. Error Handling & Safety

### Failure modes

| Failure | Rilevato da | Comportamento |
|---|---|---|
| MSW worker fallisce lo start | `worker.start()` reject | Toast "MSW failed to start — using real backend"; app continua in passthrough |
| Scenario JSON corrotto/invalid | `ScenarioStore.load()` + Ajv | Fallback a scenario `empty` hardcoded; banner rosso nel DevBadge |
| Scenario referenzia ID inesistente | Cross-check al load | Warning + scenario parziale (degradato > fail) |
| `MEEPLE_DEV_TOKEN` mancante fase 2 | `DevToolsEndpoints` startup | Endpoint non registrati → 404 esterno; warning a startup |
| Toggle per mock non implementato | `MockToggleStateProvider.Set(...)` | `InvalidOperationException` con lista valida — crash esplicito in dev è desiderato |
| Scenario switch con dati incompatibili | `ScenarioStore.loadScenario()` | Invalida React Query, reset store FE; flash "Loading scenario..." ~100ms |
| Dev Panel in prod per sbaglio | Build guard + runtime check | Chunk assente dal bundle; guard runtime bypassa |
| `dotnet watch` crash loop | script bash parent | PID in `.dev-fast.pids`; log `dotnet-watch.log`; non killa MSW |
| Concurrent toggle update | `MockToggleStateProvider` | `ConcurrentDictionary<string, bool>` + `Interlocked`; eventual consistency OK in dev |
| Header `X-Dev-Token` leak nei log | Middleware logging | Lista header sensibili esclusa dal logging; audit in test |

### Tree-shake e dead code elimination

**Frontend**:
```typescript
// apps/web/src/app/layout.tsx
if (
  process.env.NODE_ENV === 'development' &&
  process.env.NEXT_PUBLIC_MOCK_MODE === 'true'
) {
  const { installDevTools } = await import('@/dev-tools');
  installDevTools();
}
```
- `process.env.NODE_ENV` inlined da Next.js → blocco dead code in prod → eliminato
- Bundle size test in CI asserta `<= baseline + 2KB`
- Grep CI: nessun import statico di `@/dev-tools` da file non-dev

**Backend**:
```xml
<!-- apps/api/src/Api/Api.csproj -->
<ItemGroup Condition="'$(Configuration)' == 'Release'">
  <Compile Remove="DevTools/**/*.cs" />
</ItemGroup>
```
- In Release la cartella `DevTools/` non viene compilata
- `Program.cs`:
  ```csharp
  #if DEBUG
  if (env.IsDevelopment())
  {
      services.AddMeepleDevTools();
  }
  #endif
  ```
- Nota sull'interazione dei due guard: `#if DEBUG` è **compile-time** → in Release build il blocco sparisce anche se a runtime l'ambiente fosse `Development`. `env.IsDevelopment()` è **runtime** → in Debug build impedisce l'attivazione se l'ambiente è `Production`/`Staging`. I due guard sono complementari per design: uno copre il caso "Release in ambiente dev" (mock escluso), l'altro il caso "Debug in ambiente prod" (mock escluso). Per abilitare MeepleDev servono **entrambi**: Debug build + Development env.
- CI check: integration test che load DLL Release e asserta assenza tipi `Api.DevTools.*`

### Invariant: "mock code NEVER in production"

Enforcement multi-livello:
1. **Compile-time BE**: `<Compile Remove>` in Release
2. **Bundle-time FE**: dead code elimination + test di bundle size
3. **Runtime BE**: `env.IsDevelopment()` check prima di registrare wrapper/endpoint
4. **Runtime FE**: check `NODE_ENV` + `NEXT_PUBLIC_MOCK_MODE` prima del mount
5. **CI guard**: workflow "Dev Tools Isolation" (sezione 6) formalizza lo "stash and build"
6. **E2E prod guard**: test E2E in prod config che asserta:
   - Assenza header `X-Meeple-Mock`
   - Assenza `DevBadge` nel DOM
   - `404` su `GET /dev/toggles`

### Degradazione elegante

**Filosofia**: il codice dev è **additive**. Disabilitandolo tutto, l'app deve comportarsi esattamente come prima. Nessun path critico prod deve dipendere da `dev-tools/`. Il CI guard (sezione 6) formalizza questo invariant.

---

## 6. Testing Strategy

### Unit tests

**Frontend (`apps/web/__tests__/dev-tools/`, Vitest)**
- `mockControlCore.test.ts` — get/set, precedenze query > panel > env, event emission
- `scenarioStore.test.ts` — load valido, fallback su invalid, CRUD in-memory, reset
- `mockAuthStore.test.ts` — role switch, query string wins, guest fallback
- `mswHandlerRegistry.test.ts` — `shouldIntercept` con gruppi/override
- `scenarioValidator.test.ts` — Ajv validation su tutti i JSON in `fixtures/scenarios/`

**Backend (`tests/Api.Tests/DevTools/`, xUnit + Moq)**
- `MockToggleStateProviderTests.cs` — concurrent reads/writes, event emission, env read
- `SwitchableLlmServiceTests.cs` — dispatch corretto, no state leak
- Un test per ogni `Switchable{Service}` (8 totali, `[Theory]` dove possibile)
- `MockLlmServiceTests.cs` — determinismo, SSE stream format, contratto `ILlmService`
- Analoghi per gli altri 7 mock services
- `DevToolsEndpointsTests.cs` — 404 senza token, 200 con token, 404 assoluto in Production

### Integration tests

**Backend (`tests/Api.Tests/Integration/DevTools/`, Testcontainers)**
- `DevLoopFullFlowTest.cs` — API con `MOCK_LLM=true`, `MOCK_S3=true`; chat endpoint reale; verifica headers
- `ToggleRuntimeChangeTest.cs` — PATCH toggle, next request usa nuovo path
- `NoMockInRelease.cs` — reflection su DLL Release, asserta assenza tipi `Api.DevTools.*`

**Frontend (`apps/web/__tests__/integration/dev-tools/`, Vitest + MSW)**
- `mswGroupToggleTest.ts` — toggle gruppo, passthrough, re-enable
- `scenarioSwitchTest.ts` — crea entity in A, switch a B, invalidazione

### E2E tests (Playwright)

**`apps/web/e2e/dev-loop/`** — suite taggata `@dev-loop`, non in CI prod
- `fast-start.spec.ts` — boot, first paint, DevBadge visibile, scenario caricato
- `role-switch.spec.ts` — `?dev-role=Admin` → /admin OK; `?dev-role=User` → 403/redirect
- `stateful-crud.spec.ts` — crea, lista, F5, reset
- `dev-panel-runtime.spec.ts` (fase 2) — apre panel, toggle off chat, verifica passthrough

### Meta-test: Dev Tools Isolation (CI guard critico)

**`.github/workflows/dev-tools-isolation.yml`**

```yaml
name: Dev Tools Isolation Check
on: [pull_request]
jobs:
  stash-and-build:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - name: Backup and remove dev-tools
        run: |
          tar -czf /tmp/dev-tools-backup.tgz \
            apps/web/src/dev-tools \
            apps/api/src/Api/DevTools \
            docs/superpowers/fixtures/scenarios \
            2>/dev/null || true
          rm -rf apps/web/src/dev-tools
          rm -rf apps/api/src/Api/DevTools
      - name: Build FE Release (must succeed)
        run: cd apps/web && pnpm install && pnpm build
      - name: Build BE Release (must succeed)
        run: cd apps/api/src/Api && dotnet build -c Release
      - name: Run smoke tests
        run: cd apps/web && pnpm test --run __tests__/smoke
      - name: Restore
        run: tar -xzf /tmp/dev-tools-backup.tgz
```

Se fallisce → un import di prod dipende da `dev-tools/` → errore bloccante PR.

**Nota**: tar vs `git stash` è una scelta tactical del plan. tar è più esplicito e indipendente da git state; stash è più "git-native" ma richiede file tracked. Decisione in fase plan.

### Bundle size snapshot

**`apps/web/__tests__/bundle-size.test.ts`**
- `pnpm build` con `NODE_ENV=production`, `NEXT_PUBLIC_MOCK_MODE=false`
- Asserta: JS bundle ≤ baseline + 2KB
- Baseline aggiornata in PR dedicate

### Compatibilità con test esistenti

- I 930+ test BE usano `Moq<IService>` direttamente → non passano dai `Switchable` → invariati
- I test FE esistenti usano MSW con `setupServer` per suite → handler invariati; solo `browser.ts` (worker) toccato
- CI pipeline esistente invariata; il workflow di isolation è **additivo**

### Convenzioni per contributori

- `apps/api/src/Api/DevTools/README.md` — template "come aggiungere un nuovo mock service"
- `apps/web/src/dev-tools/README.md` — template "come aggiungere un handler MSW / scenario"

---

## 7. Implementation Phases

### Fase 0 — Preparazione (~0.5 giorni)

- [ ] `.gitignore` aggiornato con `.env.dev.local`, `.dev-fast.pids`, `dev-fast.log`
- [ ] `.env.dev.local.example` con tutti i toggle documentati
- [ ] `docs/superpowers/fixtures/scenarios/` + `schema/scenario.schema.json`
- [ ] 1 scenario seed minimale (`empty.json`)
- [ ] Scheletro `apps/web/src/dev-tools/README.md` e `apps/api/src/Api/DevTools/README.md`
- [ ] `<Compile Remove>` condizionale in `Api.csproj`

**Deliverable**: PR #1 — "chore(dev): scaffold MeepleDev folders and env template"

### Fase 1 — Infrastruttura statica (~4.5 giorni)

**Obiettivo**: `make dev:fast` funziona. Env var controlla i toggle. Nessuna UI, nessun runtime switching.

#### Milestone 1.1 — FE MSW granulare + scenari (~2 giorni)
- [ ] `MockControlCore` (Zustand, read-only fase 1)
- [ ] Refactor `MswHandlerRegistry` con `NEXT_PUBLIC_MSW_ENABLE/DISABLE`
- [ ] `ScenarioStore` con load + stateful CRUD + Ajv
- [ ] `MockAuthStore` con env + query string
- [ ] 3 scenari seed: `empty`, `small-library`, `admin-busy`
- [ ] `DevBadge` versione statica
- [ ] Unit tests

**Deliverable**: PR #2 — `make dev:fast` parte in 5s con mock tutto-in-FE

#### Milestone 1.2 — BE IMockToggleState read-only + 3 wrapper (~1.5 giorni)
- [ ] `IMockToggleState` + `MockToggleStateProvider` (frozen after startup)
- [ ] Lettura env all'avvio
- [ ] `SwitchableLlmService`, `SwitchableEmbeddingService`, `SwitchableS3BlobStorageService`
- [ ] `MockLlmService` con SSE streaming replay da scenario
- [ ] `MockEmbeddingService`, `MockS3BlobStorageService`
- [ ] `MockHeaderMiddleware`
- [ ] `dotnet watch` documentato in README dev-tools
- [ ] Unit + integration tests

**Deliverable**: PR #3 — backend locale con 3 servizi mockabili via env

#### Milestone 1.3a — Altri 3 mock BE (~0.75 giorni)
- [ ] `MockRerankerService`, `MockBggService`, `MockN8nService` + wrapper
- [ ] Unit tests

**Deliverable**: PR #4 — 6 servizi mockabili

#### Milestone 1.3b — PDF pipeline mocks (~0.75 giorni)
- [ ] `MockSmolDoclingService`, `MockUnstructuredService` + wrapper
- [ ] Unit tests + integration test upload PDF end-to-end in mock mode

**Deliverable**: PR #5 — 8 servizi mockabili completi (PDF flow funzionante)

#### Milestone 1.4 — Make targets + CI guard (~0.5 giorni)
- [ ] `scripts/dev-fast.sh`, `scripts/dev-fast-down.sh`
- [ ] Makefile targets `dev:fast`, `dev:fast-api`, `dev:fast-full`, `dev:fast-down`
- [ ] Workflow `.github/workflows/dev-tools-isolation.yml`
- [ ] Smoke test end-to-end
- [ ] Bundle size snapshot test

**Deliverable**: PR #6 — CLI completa + CI guard

**Checkpoint fase 1**: FE in ≤5s, BE optional in ~30s, toggle via `.env.dev.local`, zero regressioni prod, isolation check verde.

### Fase 2 — Dev Panel runtime (~5 giorni)

**Obiettivo**: toggle runtime senza restart. Dev Panel in-browser.

#### Milestone 2.1 — BE endpoint `/dev/*` (~1 giorno)
- [ ] `IMockToggleState` mutabile thread-safe
- [ ] `DevToolsEndpoints.cs` con `GET/PATCH /dev/toggles`, `POST /dev/reset`
- [ ] `X-Dev-Token` middleware
- [ ] Integration tests

**Deliverable**: PR #7 — backend runtime toggles via HTTP

#### Milestone 2.2 — Dev Panel React (~2.5 giorni)
- [ ] `DevPanel` componente con sezioni: Toggles, Auth, Scenarios, Recent requests
- [ ] Shortcut `Ctrl+Shift+M`, query param `?devpanel=1`
- [ ] Persistence stato in `sessionStorage`
- [ ] Styling minimale, dark mode
- [ ] Component + integration tests

**Deliverable**: PR #8 — Dev Panel funzionante

#### Milestone 2.3 — Badge interattivo + request inspector (~1 giorno)
- [ ] `DevBadge` upgrade con counter e click→panel
- [ ] `RequestInspector` nel panel (ultimi 50, filtrabile)
- [ ] Hook globale su `fetch` per metriche (solo dev)

**Deliverable**: PR #9 — badge interattivo + inspector

#### Milestone 2.4 — Scenario e auth switch live (~0.5 giorni)
- [ ] `invalidateQueries()` automatico su scenario switch
- [ ] Role switch runtime senza F5
- [ ] E2E tests fase 2

**Deliverable**: PR #10 — scenario e auth live

**Checkpoint fase 2**: Dev Panel aperto con un click, zero restart, scenario switch istantaneo, request inspector.

### Fase 3 — Polish e adozione (~1.5 giorni, non bloccante)

- [ ] 2-3 scenari seed più ricchi basati su casi d'uso reali
- [ ] Video demo / GIF per README
- [ ] Update `CLAUDE.md` con sezione "Fast dev loop"
- [ ] Update `docs/development/README.md`
- [ ] `make dev:fast` come default consigliato per nuovi dev

### Stima totale

| Fase | Giorni |
|---|---|
| Fase 0 — Preparazione | 0.5 |
| Fase 1 — Infrastruttura statica | 4.5 |
| Fase 2 — Dev Panel runtime | 5 |
| Fase 3 — Polish | 1.5 |
| **Totale** | **~11.5 giorni** |

### Valore incrementale

Ogni milestone della fase 1 è già uno sblocco:
- **Dopo 1.1**: `pnpm dev:mock` diventa granulare per gruppo
- **Dopo 1.2**: backend locale con chat+embedding+S3 mockati (chat demo senza Ollama)
- **Dopo 1.3a**: + reranker, BGG lookup, n8n stub
- **Dopo 1.3b**: + upload PDF funzionante senza SmolDocling/Unstructured
- **Dopo 1.4**: CLI + safety net

Il lavoro può fermarsi a fine fase 1 e consegnare comunque un salto enorme nel DX.

---

## Appendix: Open tactical decisions (per fase plan)

1. **tar vs `git stash`** nel workflow di isolation check
2. **`Scrutor`** per `Decorate<>` vs factory pattern manuale (verificare se già presente)
3. **Shape esatta del `MockLlmService` SSE stream** (replay da scenario vs generazione procedurale)
4. **Persistence del `sessionStorage` del Dev Panel** (cosa salvare esattamente)
5. **Naming interno**: "MeepleDev" è solo nome di lavoro; può essere rinominato
