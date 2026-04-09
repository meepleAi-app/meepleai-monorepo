# MeepleDev — Fast Dev Loop con Mock/Real Toggle

**Date**: 2026-04-09
**Status**: Design (v2, post spec-panel review)
**Author**: Brainstorming session
**Reviewed by**: Expert panel (Wiegers, Adzic, Fowler, Nygard, Crispin, Hightower) via `/sc:spec-panel`
**Related**: `docs/superpowers/specs/2026-03-18-integration-hardening-e5base-design.md`

## Changelog

- **v1 (initial)**: Vision, 2-layer architecture, 3 fasi
- **v2 (post-review)**: Fix P0 (success criteria testabili, ISP split, RequestInspector limits, X-Dev-Token security tests); Fix P1 (NFR espliciti, fidelity contract, DispatchProxy generico, scenario switch protocol, GWT scenarios, matrice integration, operational hardening)

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

### 1.3 Success Criteria (funzionali, misurabili)

Ogni success criterion specifica: **baseline ambiente**, **procedura di misurazione**, **tolleranza statistica**.

**SC-1 — Fast start ≤ 5s (FE-only mode)**
- **Ambiente**: GitHub Actions `ubuntu-latest` (runner standard 4 vCPU / 16GB), Node 20 LTS, pnpm 10, cache `node_modules/` primata (`pnpm install` già completato)
- **Procedura**: da cartella `infra/`, eseguire:
  ```bash
  make dev:fast &
  PID=$!
  while ! curl -s -o /dev/null http://localhost:3000/; do sleep 0.1; done
  END=$(date +%s.%N)
  # misura END - start_of_make
  ```
- **Criterio**: `time_to_first_200 ≤ 5.0s` nel **p95 di 10 run consecutivi**
- **Test**: `.github/workflows/dev-tools-perf.yml` (nuovo)

**SC-2 — HMR frontend ≤ 1s**
- **Ambiente**: stesso di SC-1, con `make dev:fast` già avviato
- **Procedura**: `touch apps/web/src/app/page.tsx` seguito da WebSocket listen su `/_next/webpack-hmr` fino a ricezione evento `built`
- **Criterio**: `time_to_built ≤ 1.0s` nel p95 di 10 run

**SC-3 — Hot reload backend ≤ 5s (con `dotnet watch`)**
- **Ambiente**: GitHub Actions `ubuntu-latest`, .NET 9 SDK, `make dev:fast-api` avviato e stabile (watch in stato "Waiting for changes")
- **Procedura**: `touch` a un file `.cs` triviale (es. aggiunta commento); poll `GET http://localhost:8080/health` fino a ricezione 200 con header `X-Build-Id` cambiato
- **Criterio**: `time_to_new_build_id ≤ 5.0s` nel p95 di 10 run
- **Nota**: non copre rebuild strutturali (nuove classi); solo "hot reload" nativo .NET

**SC-4 — Toggle runtime change (fase 2)**
- **Ambiente**: `make dev:fast-api` avviato + Dev Panel aperto
- **Procedura**: `PATCH /dev/toggles { "llm": false }` seguito da una request chat; verificare header response
- **Criterio**: zero restart (PID invariato); header `X-Meeple-Mock` della prima request post-PATCH riflette il nuovo stato; latenza PATCH → next effective request ≤ 50ms

**SC-5 — Onboarding dev ≤ 10 min**
- **Ambiente**: VM ubuntu fresca, zero cache, solo git + Node 20 + pnpm installati
- **Procedura**:
  ```bash
  git clone <repo> && cd meepleai-monorepo-frontend
  pnpm install
  cp infra/.env.dev.local.example infra/.env.dev.local
  cd infra && make dev:fast
  # attendere 200 su localhost:3000
  ```
- **Criterio**: tempo totale ≤ 10 minuti p50 (network-dependent → no p95)

### 1.4 Non-Functional Requirements

**NFR-PERF-1 — MSW overhead**
Overhead di MSW intercept su una request intercettata ≤ 5ms p95 rispetto a una passthrough. Misurato via `performance.now()` pre/post-fetch nel browser, 100 samples.

**NFR-PERF-2 — Switchable wrapper overhead**
Overhead di `MockAwareProxy<T>` ≤ 1µs p95 per invocation rispetto a call diretta. Misurato via BenchmarkDotNet.

**NFR-SEC-1 — No mock types in Release DLL**
Reflection su `Api.dll` built in Release: zero tipi nel namespace `Api.DevTools.*`. Verificato da `NoMockInRelease.cs`.

**NFR-SEC-2 — No dev token in logs**
Ricerca regex `MEEPLE_DEV_TOKEN|X-Dev-Token` in tutti i log writer (Serilog sinks, Console, OpenTelemetry) → zero match. Verificato da `DevTokenNotLoggedTests.cs`.

**NFR-SEC-3 — Timing-safe token comparison**
Il confronto di `X-Dev-Token` usa `CryptographicOperations.FixedTimeEquals`. Verificato da code review + grep CI.

**NFR-SIZE-1 — Bundle size invariant**
`pnpm build` in config prod produce JS bundle ≤ baseline + 2KB. Baseline aggiornata in PR dedicate. Verificato da `bundle-size.test.ts`.

**NFR-DX-1 — Env file schema migration**
Ogni `make dev:fast` run esegue `scripts/dev-env-check.sh` che confronta chiavi di `.env.dev.local.example` vs `.env.dev.local`; missing keys → warning rosso + diff + istruzioni fix. Non bloccante.

**NFR-OBS-1 — Backend crash detection**
`scripts/dev-fast.sh` monitora `dotnet watch`; 3 restart in 60s → kill + banner rosso. Frontend: polling `/health` ogni 10s; 5 fail consecutivi → DevBadge "Backend unreachable".

### 1.5 Mock Fidelity Contract

Ogni mock deve garantire fedeltà minima al servizio reale:

| Servizio | Fedeltà minima richiesta |
|---|---|
| **LLM** | Contratto SSE identico (event types, `data:` delimiter, `[DONE]` terminator). Contenuto deterministico da `scenario.chatHistory` o generato proceduralmente da `prompt.length` |
| **Embedding** | `float[768]` normalizzato, stabile per `hash(model + text)` |
| **Reranker** | Contratto score array identico; scoring basato su `Jaccard(query_tokens, doc_tokens)` |
| **SmolDocling** | JSON identico al reale; contenuto da `scenario.documents[id]` o 5 pagine default generate |
| **Unstructured** | JSON identico; stesso contenuto di SmolDocling, formato differente |
| **BGG** | XML+JSON identico per 10 giochi seed (`scenario.bggGames`); `404` deterministico per ID fuori seed |
| **S3** | Upload: ritorna `blobKey` deterministico (`MOCK-<hash>`); Download: legge da `Dictionary<string, byte[]>` in-memory; pre-signed URL: ritorna `http://localhost/mock-s3/<key>` |
| **n8n** | Webhook POST ritorna `202 { "executionId": "MOCK-<uuid>" }`, no-op lato server |

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

### Flow D.prime — Scenario switch protocol (per evitare stato inconsistente)

Switch tra scenari deve essere atomico rispetto alle richieste in volo. Protocollo:

```
1. UI click "switch to admin-busy"
2. ScenarioStore.beginSwitch('admin-busy')
   └─ flag isSwitching = true
3. Tutti gli handler MSW che leggono lo store:
   └─ if (isSwitching) return 503 { "error": "scenario-switching" }
4. queryClient.cancelQueries({ type: 'active' })
   └─ aborta tutte le in-flight
5. EventSource registry → close() su tutte le SSE aperte
   └─ reconnect automatico dei componenti dopo switch
6. ScenarioStore.load('admin-busy')
   └─ fetch JSON + Ajv validation + commit store
7. ScenarioStore.endSwitch()
   └─ isSwitching = false
8. queryClient.invalidateQueries()
   └─ refetch automatico post-switch
9. UI overlay "Loading scenario..." rimosso
```

**Durata totale**: target ≤ 200ms end-to-end. Misurato via E2E test.
**Fallback**: se `endSwitch()` non viene chiamato entro 2s (es. crash in mezzo), timeout → rollback a scenario precedente + toast rosso.

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

### 3.Z — Executable scenarios (Given/When/Then)

I seguenti scenari diventano direttamente E2E test Playwright (`apps/web/e2e/dev-loop/`). Sono il criterio di accettazione formale per le feature della fase 1.

```gherkin
Feature: Fast start minimale
  Scenario: Nuovo contributor primo run
    Given git clone fresco
    And pnpm install completato
    And infra/.env.dev.local copiato dal template senza modifiche
    When il dev esegue `make dev:fast` nella cartella infra
    Then entro 5 secondi curl localhost:3000 ritorna 200
    And il DOM contiene l'elemento [data-testid="dev-badge"]
    And il DevBadge mostra "MOCK · scenario: small-library"

Feature: MSW group toggle via env var
  Scenario: Disabilitare il gruppo 'games' via .env
    Given .env.dev.local contiene "NEXT_PUBLIC_MSW_DISABLE=games"
    And `make dev:fast` è in esecuzione
    When il browser esegue fetch("/api/v1/games")
    Then la response non ha header "X-Meeple-Mock: msw:games"
    And (se backend down) la response ha status 502 o network error
    And (se backend up) la response ha status 200 da backend reale

  Scenario: Abilitare solo 'auth' e 'games'
    Given .env.dev.local contiene "NEXT_PUBLIC_MSW_ENABLE=auth,games"
    When il browser esegue fetch("/api/v1/chat")
    Then la response non ha header "X-Meeple-Mock"
    When il browser esegue fetch("/api/v1/games")
    Then la response ha header "X-Meeple-Mock: msw:games"

Feature: Role switch via query string
  Scenario: Visitare admin page come User
    Given `make dev:fast` avviato con DEV_AS_ROLE=Admin
    When il browser visita /admin?dev-role=User
    Then la pagina risponde con redirect a / o status 403
    When il browser visita /admin?dev-role=Admin
    Then la pagina risponde con status 200
    And il DOM contiene navigation elements admin

Feature: Stateful CRUD in mock mode
  Scenario: Crea game persiste finché non si fa refresh
    Given mock mode attivo con scenario 'small-library'
    And la lista giochi mostra 3 giochi
    When il dev crea un nuovo game "Test Game" via UI
    Then POST /api/v1/games ritorna 201 con id prefixato "MOCK-"
    When il dev naviga a /games
    Then la lista contiene "Test Game" (totale 4)
    When il dev fa F5 del browser
    Then la lista mostra di nuovo 3 giochi (scenario reset)

Feature: Backend mock toggle — chat
  Scenario: Chat con LLM mockato
    Given `make dev:fast-api` avviato
    And .env.dev.local contiene "MOCK_LLM=true"
    When il dev apre /chat e invia "Hello"
    Then la response SSE contiene dati deterministici da scenario.chatHistory
    And la response ha header "X-Meeple-Mock: backend-di:llm"
    And la chiamata a OpenRouter non viene mai effettuata (zero outbound)

Feature: Runtime toggle switch (fase 2)
  Scenario: Disattivare LLM mock runtime
    Given `make dev:fast-api` avviato
    And MOCK_LLM=true e `OPENROUTER_API_KEY` valida
    And Dev Panel aperto
    When il dev clicca toggle "LLM" da ON a OFF nel panel
    Then PATCH /dev/toggles ritorna 204
    And il prossimo POST /api/v1/chat colpisce OpenRouter reale
    And la response ha header "X-Meeple-Mock: backend-di:" (senza llm)

Feature: Isolation guarantee (prod safety)
  Scenario: Release build non include mock code
    Given repo al commit corrente
    When si esegue `dotnet build -c Release`
    Then il DLL Api.dll non contiene tipi con namespace "Api.DevTools"
    And la tabella symbol non contiene "MockLlmService"
    And GET /dev/toggles ritorna 404 in Release config

  Scenario: Stash-and-build passa
    Given repo al commit corrente
    When si rimuove apps/web/src/dev-tools e apps/api/src/Api/DevTools
    And si esegue pnpm build e dotnet build -c Release
    Then entrambi i build ritornano exit code 0
    And smoke test GET / ritorna 200
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

### Toggle state — 3 interfacce segregate (ISP)

Separazione in tre interfacce piccole per rispettare il Single Responsibility Principle. I consumers dipendono solo da ciò che serve:

```csharp
// apps/api/src/Api/DevTools/IMockToggle*.cs
namespace Api.DevTools;

// Read-only — iniettata nei MockAwareProxy
public interface IMockToggleReader
{
    bool IsMocked(string serviceName);
    IReadOnlyDictionary<string, bool> GetAll();
}

// Write-only — iniettata negli endpoint /dev/toggles
public interface IMockToggleWriter
{
    void Set(string serviceName, bool mocked);
}

// Event-only — iniettata nei listener (future)
public interface IMockToggleEvents
{
    event EventHandler<MockToggleChangedEventArgs> ToggleChanged;
}

// Singola classe implementa tutte e tre
public sealed class MockToggleStateProvider
    : IMockToggleReader, IMockToggleWriter, IMockToggleEvents
{
    private readonly ConcurrentDictionary<string, bool> _state;
    public event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;
    // ... bootstrap da env, thread-safe read/write
}
```

### Dispatch wrapper generico

Invece di 8 wrapper duplicati (`SwitchableLlmService`, `SwitchableEmbeddingService`, ...), un singolo proxy generico basato su `System.Reflection.DispatchProxy`:

```csharp
public sealed class MockAwareProxy<TService> : DispatchProxy where TService : class
{
    private TService _real = default!;
    private TService _mock = default!;
    private IMockToggleReader _toggles = default!;
    private string _serviceName = default!;

    public static TService Create(TService real, TService mock, IMockToggleReader toggles, string serviceName)
    {
        var proxy = Create<TService, MockAwareProxy<TService>>();
        var impl = (MockAwareProxy<TService>)(object)proxy!;
        impl._real = real;
        impl._mock = mock;
        impl._toggles = toggles;
        impl._serviceName = serviceName;
        return proxy!;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var target = _toggles.IsMocked(_serviceName) ? _mock : _real;
        return method?.Invoke(target, args);
    }
}
```

Registrazione uniforme via extension method:

```csharp
public static IServiceCollection AddMockAwareService<TService, TReal, TMock>(
    this IServiceCollection services, string serviceName)
    where TService : class
    where TReal : class, TService
    where TMock : class, TService
{
    services.AddSingleton<TReal>();
    services.AddSingleton<TMock>();
    services.AddSingleton<TService>(sp => MockAwareProxy<TService>.Create(
        sp.GetRequiredService<TReal>(),
        sp.GetRequiredService<TMock>(),
        sp.GetRequiredService<IMockToggleReader>(),
        serviceName));
    return services;
}
```

Uso in `AddMeepleDevTools()`:

```csharp
if (env.IsDevelopment())
{
    services.AddSingleton<MockToggleStateProvider>();
    services.AddSingleton<IMockToggleReader>(sp => sp.GetRequiredService<MockToggleStateProvider>());
    services.AddSingleton<IMockToggleWriter>(sp => sp.GetRequiredService<MockToggleStateProvider>());
    services.AddSingleton<IMockToggleEvents>(sp => sp.GetRequiredService<MockToggleStateProvider>());

    services.AddMockAwareService<ILlmService, RealLlmService, MockLlmService>("llm");
    services.AddMockAwareService<IEmbeddingService, RealEmbeddingService, MockEmbeddingService>("embedding");
    services.AddMockAwareService<IRerankerService, RealRerankerService, MockRerankerService>("reranker");
    services.AddMockAwareService<ISmolDoclingService, RealSmolDoclingService, MockSmolDoclingService>("smoldocling");
    services.AddMockAwareService<IUnstructuredService, RealUnstructuredService, MockUnstructuredService>("unstructured");
    services.AddMockAwareService<IBggApiService, RealBggApiService, MockBggApiService>("bgg");
    services.AddMockAwareService<IBlobStorageService, S3BlobStorageService, MockBlobStorageService>("s3");
    services.AddMockAwareService<IN8nWebhookService, RealN8nWebhookService, MockN8nWebhookService>("n8n");
}
```

**Trade-off**: `DispatchProxy` usa reflection → ~100ns overhead per invocation. Per servizi non hot-path (chiamati N volte per request, dove N < 10) è accettabile rispetto al valore di zero duplicazione. Se in futuro un servizio diventa hot-path, si può introdurre un wrapper specializzato per quello senza cambiare il pattern generale.

**NFR-PERF-2** copre la misurazione di questo overhead.

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
- `DevToolsEndpointsTests.cs` — security test exhaustive:
  - `PATCH /dev/toggles` senza header `X-Dev-Token` → **404** (non 401)
  - Con token errato → **404**
  - Con token corretto ma env `Production` → **404** (endpoint non registrato)
  - Con token corretto ma env `Staging` → **404**
  - Con token corretto in env `Development` → **204**
  - **Timing attack test**: 1000 confronti con token differente di 1 byte devono avere deviation ≤ 10% rispetto a confronto con token uguale (conferma `CryptographicOperations.FixedTimeEquals` in uso — NFR-SEC-3)
  - **Log leak test**: dopo 100 request con token valido, grep dei log writer (`TestLoggerProvider`) per pattern `X-Dev-Token` → zero match (NFR-SEC-2)

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
- `scenario-switch.spec.ts` — cambio scenario durante request in-flight, verifica cancel + refetch
- `degradation.spec.ts` — simula MSW worker fail (inject reject), verifica passthrough + app alive

### Matrice integrazione FE × BE

Oltre ai test isolati, copertura della matrice di configurazione attraverso test integration in `tests/Api.Tests/Integration/DevTools/DevLoopMatrixTests.cs` (backend-side) e `apps/web/__tests__/integration/dev-tools/matrixTest.ts` (frontend-side, con MSW + mock fetch):

| MSW state | BE Mock state | Scenario | Atteso |
|---|---|---|---|
| all-on | n/a (BE down) | `small-library` | Tutti gli endpoint intercettati da MSW; network log zero chiamate uscenti |
| all-off | LLM=mock | `small-library` | Chat passa al backend → `MockLlmService`; response ha `X-Meeple-Mock: backend-di:llm` |
| partial (only `games` on) | S3=mock | `small-library` | `GET /games` ha `X-Meeple-Mock: msw:games`; `POST /pdf/upload` ha `X-Meeple-Mock: backend-di:s3` |
| all-off | all-off | n/a | Full real stack (con Testcontainers per Ollama/S3 mock) — questo è lo standard integration test già esistente |
| all-on | irrelevant | `empty` | Lista giochi vuota, crea game, verifica comparsa, F5, verifica reset |

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

#### Milestone 1.4 — Make targets + CI guard + operational hardening (~1 giorno)
- [ ] `scripts/dev-fast.sh` (orchestratore), `scripts/dev-fast-down.sh` (cleanup)
- [ ] `scripts/dev-env-check.sh` — diff `.env.dev.local.example` vs `.env.dev.local` → warning su chiavi mancanti (NFR-DX-1)
- [ ] `scripts/dev-backend-watchdog.sh` — monitora output `dotnet watch`; se ≥ 3 restart in 60s → kill + banner rosso + log (NFR-OBS-1)
- [ ] Health check polling in `DevBadge`: fetch `/health` ogni 10s (solo se DEV_BACKEND=true); 5 fail consecutivi → badge rosso "Backend unreachable"
- [ ] Makefile targets `dev:fast`, `dev:fast-api`, `dev:fast-full`, `dev:fast-down`
- [ ] Workflow `.github/workflows/dev-tools-isolation.yml`
- [ ] Workflow `.github/workflows/dev-tools-perf.yml` — misura SC-1, SC-2, SC-3 (p95 di 10 run)
- [ ] Smoke test end-to-end: `make dev:fast && curl / && make dev:fast-down`
- [ ] Bundle size snapshot test (NFR-SIZE-1)

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
- [ ] `RequestInspector` nel panel con vincoli espliciti:
  - **Ring buffer fisso a 50 entry** (`const REQUEST_LOG_CAPACITY = 50`)
  - **Solo metadata**: `{ url, method, status, durationMs, isMock, timestamp, mockSource }`
  - **Mai il body** (né request né response) — privacy e memory cap
  - **Reset** on page load (no persistence)
  - **StrictMode-safe**: hook con `useRef` per evitare doppio-mount registration
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

| Fase | Giorni | Note |
|---|---|---|
| Fase 0 — Preparazione | 0.5 | |
| Fase 1 — Infrastruttura statica | 5.5 | +1g per operational hardening (N-2, H-1) |
| Fase 2 — Dev Panel runtime | 5 | |
| Fase 3 — Polish | 1.5 | |
| **Totale** | **~12.5 giorni** | |

**Stime riviste post spec-panel review**: l'aggiunta di NFR (sezione 1.4), scenari GWT (sezione 3.Z), operational hardening (milestone 1.4), e test di sicurezza (sezione 6) ha aggiunto ~1 giornata al totale. Il trade-off è: scope più grande ma qualità e safety garantite.

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
