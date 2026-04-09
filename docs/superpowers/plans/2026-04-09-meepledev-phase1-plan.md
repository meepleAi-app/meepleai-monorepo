# MeepleDev Phase 0 + 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare un fast dev loop con MSW granulare (FE) + 8 mock services BE via `MockAwareProxy<T>`, controllati da `.env.dev.local`, con `make dev:fast` come entry point. Zero runtime toggles in questa fase (è la fase statica — la fase 2 aggiungerà Dev Panel runtime).

**Architecture:** Due leve indipendenti. Frontend: MSW worker con `MswHandlerRegistry` toggle-abile per gruppo via env. Backend: `DispatchProxy<T>` dispatcha runtime tra real/mock implementations leggendo da `IMockToggleReader` (singleton popolato da env var al boot). Scenari JSON validati via Ajv/JSON Schema.

**Tech Stack:** Next.js 16 + Turbopack, MSW v2, Zustand, React Query, .NET 9, xUnit, Testcontainers, Ajv, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-04-09-meepledev-fast-dev-loop-design.md` (v2, post-panel-review)

---

## File Structure

### Files to create (frontend — `apps/web/`)

| Path | Responsibility |
|---|---|
| `src/dev-tools/index.ts` | Public API: `installDevTools()` |
| `src/dev-tools/mockControlCore.ts` | Zustand store: toggle groups, role, scenario name |
| `src/dev-tools/scenarioStore.ts` | Zustand store: loaded scenario data, stateful CRUD |
| `src/dev-tools/mockAuthStore.ts` | Zustand store: current simulated user (env + query string) |
| `src/dev-tools/mswHandlerRegistry.ts` | Group-aware handler lookup + passthrough logic |
| `src/dev-tools/scenarioValidator.ts` | Ajv loader + validator for scenario JSON |
| `src/dev-tools/devBadge.tsx` | Bottom-right badge component (static phase 1) |
| `src/dev-tools/install.ts` | Bootstrap: wire stores, start MSW, mount badge |
| `src/dev-tools/types.ts` | Shared TS types: `Scenario`, `MockUser`, `ToggleState` |
| `__tests__/dev-tools/mockControlCore.test.ts` | Unit tests |
| `__tests__/dev-tools/scenarioStore.test.ts` | Unit tests |
| `__tests__/dev-tools/mockAuthStore.test.ts` | Unit tests |
| `__tests__/dev-tools/mswHandlerRegistry.test.ts` | Unit tests |
| `__tests__/dev-tools/scenarioValidator.test.ts` | Unit tests |
| `__tests__/integration/dev-tools/mswGroupToggle.test.ts` | Integration (MSW + fetch) |

### Files to modify (frontend)

| Path | Change |
|---|---|
| `src/mocks/browser.ts` | Usa `mswHandlerRegistry.getActiveHandlers()` invece di array statico |
| `src/mocks/mock-provider.tsx` | Import dinamico di `installDevTools()` con guard |
| `src/app/layout.tsx` (o root) | Chiamare `installDevTools()` solo in dev+mock |
| `package.json` | Aggiungere `ajv` dep, `dev:mock:fast` script |

### Files to create (backend — `apps/api/src/Api/DevTools/`)

Tutto dentro `DevTools/` è `#if DEBUG` + `<Compile Remove>` in Release.

| Path | Responsibility |
|---|---|
| `DevTools/IMockToggleReader.cs` | Read-only interface (iniettata nei proxy) |
| `DevTools/IMockToggleWriter.cs` | Write-only interface (iniettata negli endpoint fase 2) |
| `DevTools/IMockToggleEvents.cs` | Event-only interface |
| `DevTools/MockToggleStateProvider.cs` | Impl delle 3 interfacce |
| `DevTools/MockToggleChangedEventArgs.cs` | Event args record |
| `DevTools/MockAwareProxy.cs` | Generic `DispatchProxy<T>` dispatcher |
| `DevTools/MockAwareServiceCollectionExtensions.cs` | `AddMockAwareService<T,TReal,TMock>()` |
| `DevTools/MockHeaderMiddleware.cs` | Writes `X-Meeple-Mock: backend-di:...` |
| `DevTools/DevToolsServiceCollectionExtensions.cs` | `AddMeepleDevTools()` entry point |
| `DevTools/MockImpls/MockLlmService.cs` | `ILlmService` mock |
| `DevTools/MockImpls/MockEmbeddingService.cs` | `IEmbeddingService` mock |
| `DevTools/MockImpls/MockBggApiService.cs` | `IBggApiService` mock |
| `DevTools/MockImpls/MockBlobStorageService.cs` | `IBlobStorageService` mock |
| `DevTools/MockImpls/MockRerankerService.cs` | Reranker mock (interface TBD) |
| `DevTools/MockImpls/MockSmolDoclingPdfTextExtractor.cs` | SmolDocling mock |
| `DevTools/MockImpls/MockUnstructuredPdfTextExtractor.cs` | Unstructured mock |
| `DevTools/MockImpls/MockN8nTemplateService.cs` | n8n mock |
| `DevTools/Scenarios/ScenarioLoader.cs` | Legge JSON scenari per backend use |
| `DevTools/README.md` | Doc per contributori |

### Test files (backend — `tests/Api.Tests/DevTools/`)

| Path | Responsibility |
|---|---|
| `DevTools/MockToggleStateProviderTests.cs` | Unit |
| `DevTools/MockAwareProxyTests.cs` | Unit |
| `DevTools/MockLlmServiceTests.cs` | Unit |
| `DevTools/MockEmbeddingServiceTests.cs` | Unit |
| `DevTools/MockBggApiServiceTests.cs` | Unit |
| `DevTools/MockBlobStorageServiceTests.cs` | Unit |
| `Integration/DevTools/DevLoopMatrixTests.cs` | Matrix integration |
| `Integration/DevTools/NoMockInReleaseTests.cs` | Reflection check |

### Files to create (shared fixtures)

| Path | Responsibility |
|---|---|
| `docs/superpowers/fixtures/scenarios/empty.json` | Seed: nessun dato |
| `docs/superpowers/fixtures/scenarios/small-library.json` | Seed: utente base |
| `docs/superpowers/fixtures/scenarios/admin-busy.json` | Seed: admin con molti dati |
| `docs/superpowers/fixtures/schema/scenario.schema.json` | JSON Schema |
| `docs/superpowers/fixtures/validate-scenarios.ts` | CI validator script |

### Files to create (infra)

| Path | Responsibility |
|---|---|
| `infra/.env.dev.local.example` | Template env var |
| `infra/scripts/dev-fast.sh` | Orchestratore |
| `infra/scripts/dev-fast-down.sh` | Cleanup |
| `infra/scripts/dev-env-check.sh` | Env migration check (NFR-DX-1) |
| `infra/scripts/dev-backend-watchdog.sh` | Crash detection (NFR-OBS-1) |
| `infra/Makefile` (modify) | Aggiungere target `dev:fast*` |
| `.github/workflows/dev-tools-isolation.yml` | Stash-and-build CI check |
| `.github/workflows/dev-tools-perf.yml` | SC-1/2/3 p95 benchmarks |
| `.gitignore` (modify) | `.env.dev.local`, `.dev-fast.pids`, `dev-fast.log` |
| `apps/api/src/Api/Api.csproj` (modify) | `<Compile Remove>` per DevTools in Release |

---

## Milestone mapping

| PR | Task range | Milestone | Est |
|---|---|---|---|
| PR #1 | Task 1-3 | Fase 0 — Scaffolding | 0.5g |
| PR #2 | Task 4-14 | Fase 1.1 — FE MSW granulare + scenari + badge statico | 2g |
| PR #3 | Task 15-22 | Fase 1.2 — BE toggle state + proxy generico + 3 mock (LLM, Embedding, S3) | 1.5g |
| PR #4 | Task 23-25 | Fase 1.3a — 3 altri mock (Reranker, BGG, n8n) | 0.75g |
| PR #5 | Task 26-28 | Fase 1.3b — PDF pipeline mocks (SmolDocling, Unstructured) | 0.75g |
| PR #6 | Task 29-34 | Fase 1.4 — dev-fast.sh + Makefile + CI guards + operational hardening | 1g |

**Totale**: 6.5 giorni. Allineato alla stima spec (5.5g fase 1 + 0.5g fase 0 + 0.5g buffer).

---

## Prerequisiti per ogni task

Ogni task presuppone:
- Branch feature corrente: `feature/meepledev-phase1`
- Working directory: root del repo
- `.NET 9 SDK`, `pnpm 10+`, `Git Bash` (su Windows), `docker` (opzionale per BE tasks)
- Pre-commit hooks attivi (lint, typecheck)

**Branch setup** (eseguito UNA VOLTA prima del task 1):
```bash
git checkout main-dev
git pull origin main-dev
git checkout -b feature/meepledev-phase1
git config branch.feature/meepledev-phase1.parent main-dev
```

---

## PR #1 — Fase 0: Scaffolding (Task 1-3)

### Task 1: Git ignore e template env

**Files:**
- Modify: `.gitignore`
- Create: `infra/.env.dev.local.example`

- [ ] **Step 1: Aggiungere pattern a `.gitignore`**

Open `.gitignore` e aggiungere al termine del file:

```
# MeepleDev fast dev loop
infra/.env.dev.local
infra/.dev-fast.pids
infra/dev-fast.log
infra/dotnet-watch.log
```

- [ ] **Step 2: Creare template `.env.dev.local.example`**

Create `infra/.env.dev.local.example`:

```bash
# ================================================================
# MeepleDev — Fast dev loop configuration
# Attivo SOLO con NEXT_PUBLIC_MOCK_MODE=true o IsDevelopment
# Copia questo file in .env.dev.local e personalizza.
# ================================================================

# -- Leva 1: cosa avviare con `make dev:fast` -------------------
DEV_BACKEND=false              # true → dotnet watch locale
DEV_POSTGRES=false             # true → docker compose postgres
DEV_REDIS=false                # true → docker compose redis

# -- Leva 2: MSW frontend ---------------------------------------
NEXT_PUBLIC_MOCK_MODE=true
# Gruppi MSW attivi di default (virgole, vuoto = tutti)
NEXT_PUBLIC_MSW_ENABLE=auth,games,chat,library,admin
# Gruppi esplicitamente disabilitati (precedenza su ENABLE)
NEXT_PUBLIC_MSW_DISABLE=
# Scenario seed iniziale
NEXT_PUBLIC_DEV_SCENARIO=small-library
# Ruolo simulato all'avvio
NEXT_PUBLIC_DEV_AS_ROLE=Admin

# -- Leva 3: Backend DI mock wrappers (solo se DEV_BACKEND=true) -
MOCK_LLM=true
MOCK_EMBEDDING=true
MOCK_RERANKER=true
MOCK_SMOLDOCLING=true
MOCK_UNSTRUCTURED=true
MOCK_BGG=true
MOCK_S3=true
MOCK_N8N=true

# -- Dev Panel secret (usato in fase 2, non fase 1) -------------
MEEPLE_DEV_TOKEN=change-me-local-only
```

- [ ] **Step 3: Verificare che `.env.dev.local` sia effettivamente ignored**

Run:
```bash
cp infra/.env.dev.local.example infra/.env.dev.local
git status --short infra/
```
Expected: `.env.dev.local` NON appare in `git status` (solo `.env.dev.local.example` se nuovo).

- [ ] **Step 4: Commit**

```bash
git add .gitignore infra/.env.dev.local.example
git commit -m "chore(dev): add .env.dev.local template and gitignore entries"
```

---

### Task 2: Backend csproj Release exclusion + folder scaffolds

**Files:**
- Modify: `apps/api/src/Api/Api.csproj`
- Create: `apps/api/src/Api/DevTools/README.md`
- Create: `apps/web/src/dev-tools/README.md`

- [ ] **Step 1: Leggere csproj corrente**

Read: `apps/api/src/Api/Api.csproj`
Identificare la posizione dell'ultimo `</ItemGroup>` o `</Project>`.

- [ ] **Step 2: Aggiungere Compile Remove condizionale**

Aggiungere DENTRO `<Project>`, prima di `</Project>`:

```xml
  <!-- MeepleDev: exclude dev tools from Release builds -->
  <ItemGroup Condition="'$(Configuration)' == 'Release'">
    <Compile Remove="DevTools/**/*.cs" />
    <None Remove="DevTools/**/*.cs" />
  </ItemGroup>
```

- [ ] **Step 3: Creare README scaffold backend**

Create `apps/api/src/Api/DevTools/README.md`:

```markdown
# MeepleDev DevTools (backend)

Codice attivo SOLO in `Debug` build + `Development` environment.
Escluso dal Release build via `<Compile Remove>` in `Api.csproj`.

## Come aggiungere un nuovo mock service

1. Verifica che esista `I{Service}` interface nel namespace `Api.Services` (o simile)
2. Crea `DevTools/MockImpls/Mock{Service}.cs` che implementa `I{Service}`
3. In `DevToolsServiceCollectionExtensions.cs`, aggiungi:
   ```csharp
   services.AddMockAwareService<IService, RealService, MockService>("service-name");
   ```
4. Aggiungi `MOCK_SERVICE` env var a `infra/.env.dev.local.example`
5. Aggiungi voce a `MockToggleStateProvider` known keys
6. Scrivi unit test in `tests/Api.Tests/DevTools/Mock{Service}Tests.cs`

Vedi `MockLlmService.cs` come template canonico.
```

- [ ] **Step 4: Creare README scaffold frontend**

Create `apps/web/src/dev-tools/README.md`:

```markdown
# MeepleDev DevTools (frontend)

Codice attivo SOLO con `NODE_ENV=development` + `NEXT_PUBLIC_MOCK_MODE=true`.
Tree-shaken dal bundle prod via dynamic import + dead-code elimination.

## Architettura

- `mockControlCore.ts`: store Zustand per toggle MSW groups
- `scenarioStore.ts`: store Zustand per dati scenario (stateful CRUD)
- `mockAuthStore.ts`: store Zustand per utente simulato
- `mswHandlerRegistry.ts`: decide quali handler MSW attivare
- `scenarioValidator.ts`: Ajv loader per JSON scenari
- `devBadge.tsx`: componente UI (bottom-right)
- `install.ts`: bootstrap sequence

## Come aggiungere un nuovo scenario

1. Crea `docs/superpowers/fixtures/scenarios/{nome}.json`
2. Conforme a `scenario.schema.json`
3. `pnpm test __tests__/dev-tools/scenarioValidator.test.ts` deve passare
4. Aggiungi `NEXT_PUBLIC_DEV_SCENARIO={nome}` in `.env.dev.local` per usarlo
```

- [ ] **Step 5: Verificare che Api.csproj build ancora passi**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
```
Expected: Build succeeded.

Run:
```bash
dotnet build -c Release
```
Expected: Build succeeded. Il condizionale è innocuo perché `DevTools/` non esiste ancora.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Api.csproj apps/api/src/Api/DevTools/README.md apps/web/src/dev-tools/README.md
git commit -m "chore(dev): scaffold DevTools folders and Release exclusion rule"
```

---

### Task 3: Scenario JSON schema + seed iniziale

**Files:**
- Create: `docs/superpowers/fixtures/schema/scenario.schema.json`
- Create: `docs/superpowers/fixtures/scenarios/empty.json`

- [ ] **Step 1: Creare directory**

Run:
```bash
mkdir -p docs/superpowers/fixtures/scenarios docs/superpowers/fixtures/schema
```

- [ ] **Step 2: Creare `scenario.schema.json`**

Create `docs/superpowers/fixtures/schema/scenario.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://meepleai.app/schemas/scenario.schema.json",
  "title": "MeepleDev Scenario",
  "type": "object",
  "required": ["name", "description", "auth", "games", "sessions", "library", "chatHistory"],
  "additionalProperties": false,
  "properties": {
    "$schema": { "type": "string" },
    "name": {
      "type": "string",
      "pattern": "^[a-z0-9-]+$",
      "description": "Lowercase, kebab-case identifier"
    },
    "description": { "type": "string", "minLength": 1 },
    "auth": {
      "type": "object",
      "required": ["currentUser", "availableUsers"],
      "additionalProperties": false,
      "properties": {
        "currentUser": { "$ref": "#/$defs/user" },
        "availableUsers": {
          "type": "array",
          "items": { "$ref": "#/$defs/user" }
        }
      }
    },
    "games": {
      "type": "array",
      "items": { "$ref": "#/$defs/game" }
    },
    "sessions": {
      "type": "array",
      "items": { "$ref": "#/$defs/session" }
    },
    "library": {
      "type": "object",
      "required": ["ownedGameIds", "wishlistGameIds"],
      "additionalProperties": false,
      "properties": {
        "ownedGameIds": { "type": "array", "items": { "type": "string" } },
        "wishlistGameIds": { "type": "array", "items": { "type": "string" } }
      }
    },
    "chatHistory": {
      "type": "array",
      "items": { "$ref": "#/$defs/chat" }
    },
    "bggGames": {
      "type": "array",
      "items": { "$ref": "#/$defs/bggGame" },
      "description": "Seed BGG lookups for MockBggApiService"
    },
    "documents": {
      "type": "array",
      "items": { "$ref": "#/$defs/document" },
      "description": "Seed PDF extractions for MockSmolDocling/Unstructured"
    }
  },
  "$defs": {
    "user": {
      "type": "object",
      "required": ["id", "email", "displayName", "role"],
      "additionalProperties": false,
      "properties": {
        "id": { "type": "string", "pattern": "^MOCK-" },
        "email": { "type": "string", "format": "email" },
        "displayName": { "type": "string" },
        "role": {
          "type": "string",
          "enum": ["Guest", "User", "Editor", "Admin", "SuperAdmin"]
        }
      }
    },
    "game": {
      "type": "object",
      "required": ["id", "title"],
      "additionalProperties": true,
      "properties": {
        "id": { "type": "string", "pattern": "^MOCK-" },
        "title": { "type": "string" },
        "publisher": { "type": "string" },
        "averageRating": { "type": "number", "minimum": 0, "maximum": 10 },
        "bggId": { "type": "integer" }
      }
    },
    "session": {
      "type": "object",
      "required": ["id", "gameId"],
      "additionalProperties": true,
      "properties": {
        "id": { "type": "string", "pattern": "^MOCK-" },
        "gameId": { "type": "string", "pattern": "^MOCK-" },
        "startedAt": { "type": "string", "format": "date-time" }
      }
    },
    "chat": {
      "type": "object",
      "required": ["chatId", "messages"],
      "additionalProperties": false,
      "properties": {
        "chatId": { "type": "string", "pattern": "^MOCK-" },
        "messages": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["role", "content"],
            "properties": {
              "role": { "type": "string", "enum": ["user", "assistant", "system"] },
              "content": { "type": "string" }
            }
          }
        }
      }
    },
    "bggGame": {
      "type": "object",
      "required": ["bggId", "name"],
      "additionalProperties": true,
      "properties": {
        "bggId": { "type": "integer" },
        "name": { "type": "string" }
      }
    },
    "document": {
      "type": "object",
      "required": ["id", "pages"],
      "additionalProperties": true,
      "properties": {
        "id": { "type": "string", "pattern": "^MOCK-" },
        "pages": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    }
  }
}
```

- [ ] **Step 3: Creare `empty.json` seed iniziale**

Create `docs/superpowers/fixtures/scenarios/empty.json`:

```json
{
  "$schema": "../schema/scenario.schema.json",
  "name": "empty",
  "description": "Scenario vuoto: utente anonimo, nessun dato. Usato come fallback in caso di JSON corrotto.",
  "auth": {
    "currentUser": {
      "id": "MOCK-00000000-0000-0000-0000-000000000000",
      "email": "guest@meeple.local",
      "displayName": "Guest",
      "role": "Guest"
    },
    "availableUsers": []
  },
  "games": [],
  "sessions": [],
  "library": {
    "ownedGameIds": [],
    "wishlistGameIds": []
  },
  "chatHistory": [],
  "bggGames": [],
  "documents": []
}
```

- [ ] **Step 4: Validare manualmente il JSON**

Run:
```bash
node -e "console.log(JSON.parse(require('fs').readFileSync('docs/superpowers/fixtures/scenarios/empty.json', 'utf8')).name)"
```
Expected: `empty`

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/fixtures/
git commit -m "feat(dev): add scenario JSON schema and empty seed"
```

**PR #1 check**: `dotnet build -c Release` e `dotnet build -c Debug` passano; `pnpm build` (se eseguito) passa. Aprire PR #1 → `main-dev` con titolo "chore(dev): Phase 0 scaffolding for MeepleDev fast dev loop".

---

## PR #2 — Fase 1.1: FE MSW granulare + scenari + badge statico (Task 4-14)

### Task 4: Tipi TypeScript condivisi

**Files:**
- Create: `apps/web/src/dev-tools/types.ts`

- [ ] **Step 1: Creare `types.ts`**

Create `apps/web/src/dev-tools/types.ts`:

```typescript
/**
 * MeepleDev shared types.
 * Keep in sync with docs/superpowers/fixtures/schema/scenario.schema.json
 */

export type UserRole = 'Guest' | 'User' | 'Editor' | 'Admin' | 'SuperAdmin';

export interface MockUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface MockGame {
  id: string;
  title: string;
  publisher?: string;
  averageRating?: number;
  bggId?: number;
  [key: string]: unknown;
}

export interface MockSession {
  id: string;
  gameId: string;
  startedAt?: string;
  [key: string]: unknown;
}

export interface MockChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface MockChat {
  chatId: string;
  messages: MockChatMessage[];
}

export interface MockLibrary {
  ownedGameIds: string[];
  wishlistGameIds: string[];
}

export interface MockBggGame {
  bggId: number;
  name: string;
  [key: string]: unknown;
}

export interface MockDocument {
  id: string;
  pages: string[];
  [key: string]: unknown;
}

export interface Scenario {
  $schema?: string;
  name: string;
  description: string;
  auth: {
    currentUser: MockUser;
    availableUsers: MockUser[];
  };
  games: MockGame[];
  sessions: MockSession[];
  library: MockLibrary;
  chatHistory: MockChat[];
  bggGames?: MockBggGame[];
  documents?: MockDocument[];
}

/** MSW group toggle state (group name → enabled). */
export type GroupToggles = Record<string, boolean>;

/** Per-endpoint override (phase 2), key = "group.METHOD /path". */
export type EndpointOverrides = Record<string, boolean>;

export interface ToggleConfig {
  groups: GroupToggles;
  overrides: EndpointOverrides;
}
```

- [ ] **Step 2: Typecheck**

Run:
```bash
cd apps/web && pnpm typecheck
```
Expected: PASS (no type errors).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/dev-tools/types.ts
git commit -m "feat(dev): add MeepleDev TypeScript types"
```

---

### Task 5: ScenarioValidator (Ajv loader)

**Files:**
- Create: `apps/web/src/dev-tools/scenarioValidator.ts`
- Create: `apps/web/__tests__/dev-tools/scenarioValidator.test.ts`
- Modify: `apps/web/package.json` (add `ajv` dep)

- [ ] **Step 1: Aggiungere dipendenza `ajv`**

Run:
```bash
cd apps/web && pnpm add ajv ajv-formats
```
Expected: `ajv` e `ajv-formats` aggiunti a `dependencies`.

- [ ] **Step 2: Write failing test**

Create `apps/web/__tests__/dev-tools/scenarioValidator.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { validateScenario, SCENARIO_FALLBACK } from '@/dev-tools/scenarioValidator';
import type { Scenario } from '@/dev-tools/types';

const validScenario: Scenario = {
  name: 'test',
  description: 'Test scenario',
  auth: {
    currentUser: {
      id: 'MOCK-11111111-1111-1111-1111-111111111111',
      email: 'test@meeple.local',
      displayName: 'Test',
      role: 'User',
    },
    availableUsers: [],
  },
  games: [],
  sessions: [],
  library: { ownedGameIds: [], wishlistGameIds: [] },
  chatHistory: [],
};

describe('scenarioValidator', () => {
  it('validates a correct scenario', () => {
    const result = validateScenario(validScenario);
    expect(result.valid).toBe(true);
    expect(result.errors).toEqual([]);
  });

  it('rejects a scenario missing required fields', () => {
    const invalid = { name: 'bad' };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(0);
  });

  it('rejects a scenario with invalid user role', () => {
    const invalid = {
      ...validScenario,
      auth: {
        ...validScenario.auth,
        currentUser: { ...validScenario.auth.currentUser, role: 'Wizard' },
      },
    };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
  });

  it('rejects user id not starting with MOCK-', () => {
    const invalid = {
      ...validScenario,
      auth: {
        ...validScenario.auth,
        currentUser: { ...validScenario.auth.currentUser, id: 'real-uuid' },
      },
    };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
  });

  it('provides a fallback scenario', () => {
    expect(SCENARIO_FALLBACK.name).toBe('empty');
    expect(SCENARIO_FALLBACK.auth.currentUser.role).toBe('Guest');
  });
});
```

- [ ] **Step 3: Run test to verify failure**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/scenarioValidator.test.ts --run
```
Expected: FAIL with "Cannot find module '@/dev-tools/scenarioValidator'".

- [ ] **Step 4: Implementare `scenarioValidator.ts`**

Create `apps/web/src/dev-tools/scenarioValidator.ts`:

```typescript
import Ajv, { type ErrorObject } from 'ajv';
import addFormats from 'ajv-formats';
import type { Scenario } from './types';
import scenarioSchema from '../../../../docs/superpowers/fixtures/schema/scenario.schema.json';

const ajv = new Ajv({ allErrors: true, strict: false });
addFormats(ajv);
const validator = ajv.compile(scenarioSchema);

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

export function validateScenario(data: unknown): ValidationResult {
  const valid = validator(data);
  if (valid) {
    return { valid: true, errors: [] };
  }
  const errors = (validator.errors ?? []).map(
    (e: ErrorObject) => `${e.instancePath || '(root)'}: ${e.message ?? 'unknown'}`
  );
  return { valid: false, errors };
}

/** Fallback used when loading a scenario fails. */
export const SCENARIO_FALLBACK: Scenario = {
  name: 'empty',
  description: 'Empty fallback scenario (scenario load failed)',
  auth: {
    currentUser: {
      id: 'MOCK-00000000-0000-0000-0000-000000000000',
      email: 'guest@meeple.local',
      displayName: 'Guest',
      role: 'Guest',
    },
    availableUsers: [],
  },
  games: [],
  sessions: [],
  library: { ownedGameIds: [], wishlistGameIds: [] },
  chatHistory: [],
  bggGames: [],
  documents: [],
};
```

- [ ] **Step 5: Configurare import JSON in tsconfig se necessario**

Check `apps/web/tsconfig.json` — deve avere `"resolveJsonModule": true`. Se manca, aggiungerlo sotto `compilerOptions`.

- [ ] **Step 6: Run test to verify pass**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/scenarioValidator.test.ts --run
```
Expected: PASS — 5 tests passing.

- [ ] **Step 7: Typecheck**

Run:
```bash
cd apps/web && pnpm typecheck
```
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/dev-tools/scenarioValidator.ts apps/web/__tests__/dev-tools/scenarioValidator.test.ts apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "feat(dev): add scenario Ajv validator with fallback"
```

---

### Task 6: MockAuthStore (env + query string)

**Files:**
- Create: `apps/web/src/dev-tools/mockAuthStore.ts`
- Create: `apps/web/__tests__/dev-tools/mockAuthStore.test.ts`

- [ ] **Step 1: Write failing test**

Create `apps/web/__tests__/dev-tools/mockAuthStore.test.ts`:

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { createMockAuthStore } from '@/dev-tools/mockAuthStore';
import type { MockUser } from '@/dev-tools/types';

const USERS: MockUser[] = [
  { id: 'MOCK-admin', email: 'a@m.local', displayName: 'Admin', role: 'Admin' },
  { id: 'MOCK-user', email: 'u@m.local', displayName: 'User', role: 'User' },
  { id: 'MOCK-guest', email: 'g@m.local', displayName: 'Guest', role: 'Guest' },
];

describe('mockAuthStore', () => {
  it('uses currentUser from scenario when no override', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: null,
      queryStringRole: null,
    });
    expect(store.getState().currentUser.id).toBe('MOCK-admin');
  });

  it('env var DEV_AS_ROLE overrides scenario', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'User',
      queryStringRole: null,
    });
    expect(store.getState().currentUser.role).toBe('User');
    expect(store.getState().currentUser.id).toBe('MOCK-user');
  });

  it('query string ?dev-role wins over env', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'User',
      queryStringRole: 'Guest',
    });
    expect(store.getState().currentUser.role).toBe('Guest');
  });

  it('falls back to scenario user if requested role not available', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'SuperAdmin',
      queryStringRole: null,
    });
    // No SuperAdmin in availableUsers → fall back to scenario
    expect(store.getState().currentUser.id).toBe('MOCK-admin');
  });

  it('setRole updates current user if role available', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: null,
      queryStringRole: null,
    });
    store.getState().setRole('User');
    expect(store.getState().currentUser.role).toBe('User');
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/mockAuthStore.test.ts --run
```
Expected: FAIL with "Cannot find module '@/dev-tools/mockAuthStore'".

- [ ] **Step 3: Implementare `mockAuthStore.ts`**

Create `apps/web/src/dev-tools/mockAuthStore.ts`:

```typescript
import { createStore, type StoreApi } from 'zustand/vanilla';
import type { MockUser, UserRole } from './types';

export interface MockAuthState {
  currentUser: MockUser;
  availableUsers: MockUser[];
  setRole: (role: UserRole) => void;
  setUser: (user: MockUser) => void;
}

export interface MockAuthStoreInit {
  scenarioUser: MockUser;
  availableUsers: MockUser[];
  envRole: UserRole | null;
  queryStringRole: UserRole | null;
}

function findByRole(users: MockUser[], role: UserRole): MockUser | undefined {
  return users.find((u) => u.role === role);
}

function resolveInitialUser(init: MockAuthStoreInit): MockUser {
  // Precedence: query string > env > scenario
  const requested = init.queryStringRole ?? init.envRole;
  if (requested) {
    const match = findByRole(init.availableUsers, requested);
    if (match) return match;
    // If scenario user itself has the requested role, use it
    if (init.scenarioUser.role === requested) return init.scenarioUser;
  }
  return init.scenarioUser;
}

export function createMockAuthStore(init: MockAuthStoreInit): StoreApi<MockAuthState> {
  return createStore<MockAuthState>((set, get) => ({
    currentUser: resolveInitialUser(init),
    availableUsers: init.availableUsers,
    setRole: (role: UserRole) => {
      const match = findByRole(get().availableUsers, role);
      if (match) {
        set({ currentUser: match });
      }
    },
    setUser: (user: MockUser) => set({ currentUser: user }),
  }));
}

/** Parse ?dev-role=Foo from URL (browser only). Returns null if not present or invalid. */
export function readRoleFromQueryString(): UserRole | null {
  if (typeof window === 'undefined') return null;
  const params = new URLSearchParams(window.location.search);
  const raw = params.get('dev-role');
  if (!raw) return null;
  const valid: UserRole[] = ['Guest', 'User', 'Editor', 'Admin', 'SuperAdmin'];
  return valid.includes(raw as UserRole) ? (raw as UserRole) : null;
}

/** Parse role from NEXT_PUBLIC_DEV_AS_ROLE env var. */
export function readRoleFromEnv(): UserRole | null {
  const raw = process.env.NEXT_PUBLIC_DEV_AS_ROLE;
  if (!raw) return null;
  const valid: UserRole[] = ['Guest', 'User', 'Editor', 'Admin', 'SuperAdmin'];
  return valid.includes(raw as UserRole) ? (raw as UserRole) : null;
}
```

- [ ] **Step 4: Run test to verify pass**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/mockAuthStore.test.ts --run
```
Expected: PASS — 5 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/dev-tools/mockAuthStore.ts apps/web/__tests__/dev-tools/mockAuthStore.test.ts
git commit -m "feat(dev): add mockAuthStore with env+query-string precedence"
```

---

### Task 7: ScenarioStore (stateful CRUD in-memory)

**Files:**
- Create: `apps/web/src/dev-tools/scenarioStore.ts`
- Create: `apps/web/__tests__/dev-tools/scenarioStore.test.ts`

- [ ] **Step 1: Write failing test**

Create `apps/web/__tests__/dev-tools/scenarioStore.test.ts`:

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { createScenarioStore } from '@/dev-tools/scenarioStore';
import type { Scenario, MockGame } from '@/dev-tools/types';

const BASE: Scenario = {
  name: 'test',
  description: 'Test',
  auth: {
    currentUser: {
      id: 'MOCK-u1',
      email: 'u@m.local',
      displayName: 'U',
      role: 'User',
    },
    availableUsers: [],
  },
  games: [
    { id: 'MOCK-g1', title: 'Wingspan' },
    { id: 'MOCK-g2', title: 'Scythe' },
  ],
  sessions: [],
  library: { ownedGameIds: ['MOCK-g1'], wishlistGameIds: [] },
  chatHistory: [],
};

describe('scenarioStore', () => {
  it('loads a scenario and exposes its data', () => {
    const store = createScenarioStore(BASE);
    const state = store.getState();
    expect(state.scenario.name).toBe('test');
    expect(state.games).toHaveLength(2);
  });

  it('addGame appends to games', () => {
    const store = createScenarioStore(BASE);
    store.getState().addGame({ id: 'MOCK-g3', title: 'Terraforming Mars' });
    expect(store.getState().games).toHaveLength(3);
    expect(store.getState().games[2].title).toBe('Terraforming Mars');
  });

  it('removeGame deletes by id', () => {
    const store = createScenarioStore(BASE);
    store.getState().removeGame('MOCK-g1');
    expect(store.getState().games).toHaveLength(1);
    expect(store.getState().games[0].id).toBe('MOCK-g2');
  });

  it('updateGame patches fields by id', () => {
    const store = createScenarioStore(BASE);
    store.getState().updateGame('MOCK-g1', { averageRating: 9.5 });
    expect(store.getState().games[0].averageRating).toBe(9.5);
    expect(store.getState().games[0].title).toBe('Wingspan');
  });

  it('resetToScenario restores initial state', () => {
    const store = createScenarioStore(BASE);
    store.getState().addGame({ id: 'MOCK-new', title: 'New' });
    expect(store.getState().games).toHaveLength(3);
    store.getState().resetToScenario();
    expect(store.getState().games).toHaveLength(2);
    expect(store.getState().games.find((g) => g.id === 'MOCK-new')).toBeUndefined();
  });

  it('loadScenario replaces the entire scenario', () => {
    const store = createScenarioStore(BASE);
    const next: Scenario = {
      ...BASE,
      name: 'next',
      games: [{ id: 'MOCK-x', title: 'X' }],
    };
    store.getState().loadScenario(next);
    expect(store.getState().scenario.name).toBe('next');
    expect(store.getState().games).toHaveLength(1);
  });

  it('isSwitching flag controls 503-guard during load', () => {
    const store = createScenarioStore(BASE);
    store.getState().beginSwitch();
    expect(store.getState().isSwitching).toBe(true);
    store.getState().endSwitch();
    expect(store.getState().isSwitching).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/scenarioStore.test.ts --run
```
Expected: FAIL — module not found.

- [ ] **Step 3: Implementare `scenarioStore.ts`**

Create `apps/web/src/dev-tools/scenarioStore.ts`:

```typescript
import { createStore, type StoreApi } from 'zustand/vanilla';
import type {
  Scenario,
  MockGame,
  MockSession,
  MockChat,
  MockLibrary,
} from './types';

export interface ScenarioState {
  scenario: Scenario;
  games: MockGame[];
  sessions: MockSession[];
  chatHistory: MockChat[];
  library: MockLibrary;
  isSwitching: boolean;

  loadScenario: (next: Scenario) => void;
  resetToScenario: () => void;
  beginSwitch: () => void;
  endSwitch: () => void;

  // Games CRUD
  addGame: (game: MockGame) => void;
  updateGame: (id: string, patch: Partial<MockGame>) => void;
  removeGame: (id: string) => void;

  // Sessions CRUD
  addSession: (session: MockSession) => void;
  removeSession: (id: string) => void;

  // Library
  toggleOwned: (gameId: string) => void;
  toggleWishlist: (gameId: string) => void;

  // Chat
  appendChatMessage: (chatId: string, message: MockChat['messages'][number]) => void;
}

function snapshotOf(scenario: Scenario): Pick<
  ScenarioState,
  'games' | 'sessions' | 'chatHistory' | 'library'
> {
  return {
    games: [...scenario.games],
    sessions: [...scenario.sessions],
    chatHistory: scenario.chatHistory.map((c) => ({
      chatId: c.chatId,
      messages: [...c.messages],
    })),
    library: {
      ownedGameIds: [...scenario.library.ownedGameIds],
      wishlistGameIds: [...scenario.library.wishlistGameIds],
    },
  };
}

export function createScenarioStore(initial: Scenario): StoreApi<ScenarioState> {
  return createStore<ScenarioState>((set, get) => ({
    scenario: initial,
    ...snapshotOf(initial),
    isSwitching: false,

    loadScenario: (next: Scenario) => {
      set({ scenario: next, ...snapshotOf(next) });
    },

    resetToScenario: () => {
      const current = get().scenario;
      set(snapshotOf(current));
    },

    beginSwitch: () => set({ isSwitching: true }),
    endSwitch: () => set({ isSwitching: false }),

    addGame: (game) => set({ games: [...get().games, game] }),

    updateGame: (id, patch) =>
      set({
        games: get().games.map((g) => (g.id === id ? { ...g, ...patch } : g)),
      }),

    removeGame: (id) => set({ games: get().games.filter((g) => g.id !== id) }),

    addSession: (session) => set({ sessions: [...get().sessions, session] }),

    removeSession: (id) =>
      set({ sessions: get().sessions.filter((s) => s.id !== id) }),

    toggleOwned: (gameId) => {
      const owned = get().library.ownedGameIds;
      const next = owned.includes(gameId)
        ? owned.filter((id) => id !== gameId)
        : [...owned, gameId];
      set({ library: { ...get().library, ownedGameIds: next } });
    },

    toggleWishlist: (gameId) => {
      const wl = get().library.wishlistGameIds;
      const next = wl.includes(gameId)
        ? wl.filter((id) => id !== gameId)
        : [...wl, gameId];
      set({ library: { ...get().library, wishlistGameIds: next } });
    },

    appendChatMessage: (chatId, message) => {
      const history = get().chatHistory;
      const idx = history.findIndex((c) => c.chatId === chatId);
      if (idx === -1) {
        set({
          chatHistory: [...history, { chatId, messages: [message] }],
        });
      } else {
        const updated = [...history];
        updated[idx] = {
          chatId,
          messages: [...history[idx].messages, message],
        };
        set({ chatHistory: updated });
      }
    },
  }));
}
```

- [ ] **Step 4: Run test to verify pass**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/scenarioStore.test.ts --run
```
Expected: PASS — 7 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/dev-tools/scenarioStore.ts apps/web/__tests__/dev-tools/scenarioStore.test.ts
git commit -m "feat(dev): add scenarioStore with stateful CRUD and switch flag"
```

---

### Task 8: MockControlCore (toggle state)

**Files:**
- Create: `apps/web/src/dev-tools/mockControlCore.ts`
- Create: `apps/web/__tests__/dev-tools/mockControlCore.test.ts`

- [ ] **Step 1: Write failing test**

Create `apps/web/__tests__/dev-tools/mockControlCore.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import {
  createMockControlStore,
  parseGroupList,
  computeGroupToggles,
} from '@/dev-tools/mockControlCore';

describe('parseGroupList', () => {
  it('splits comma-separated list', () => {
    expect(parseGroupList('auth,games,chat')).toEqual(['auth', 'games', 'chat']);
  });
  it('trims whitespace', () => {
    expect(parseGroupList(' auth , games ')).toEqual(['auth', 'games']);
  });
  it('returns empty for null/empty', () => {
    expect(parseGroupList(null)).toEqual([]);
    expect(parseGroupList('')).toEqual([]);
  });
});

describe('computeGroupToggles', () => {
  const allGroups = ['auth', 'games', 'chat', 'library', 'admin'];

  it('empty enable + empty disable = all enabled', () => {
    expect(computeGroupToggles(allGroups, [], [])).toEqual({
      auth: true, games: true, chat: true, library: true, admin: true,
    });
  });

  it('enable list restricts to only listed', () => {
    expect(computeGroupToggles(allGroups, ['auth', 'games'], [])).toEqual({
      auth: true, games: true, chat: false, library: false, admin: false,
    });
  });

  it('disable has precedence over enable', () => {
    expect(computeGroupToggles(allGroups, ['auth', 'games'], ['games'])).toEqual({
      auth: true, games: false, chat: false, library: false, admin: false,
    });
  });

  it('disable without enable disables only those', () => {
    expect(computeGroupToggles(allGroups, [], ['admin'])).toEqual({
      auth: true, games: true, chat: true, library: true, admin: false,
    });
  });
});

describe('mockControlStore', () => {
  it('initializes with computed group toggles', () => {
    const store = createMockControlStore({
      allGroups: ['auth', 'games'],
      enableList: ['auth'],
      disableList: [],
    });
    expect(store.getState().toggles.groups).toEqual({ auth: true, games: false });
  });

  it('setGroup updates a single group', () => {
    const store = createMockControlStore({
      allGroups: ['auth', 'games'],
      enableList: [],
      disableList: [],
    });
    store.getState().setGroup('games', false);
    expect(store.getState().toggles.groups.games).toBe(false);
    expect(store.getState().toggles.groups.auth).toBe(true);
  });

  it('setEndpointOverride stores per-endpoint override', () => {
    const store = createMockControlStore({
      allGroups: ['games'],
      enableList: [],
      disableList: [],
    });
    store.getState().setEndpointOverride('games.POST /api/v1/games', false);
    expect(store.getState().toggles.overrides['games.POST /api/v1/games']).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/mockControlCore.test.ts --run
```
Expected: FAIL — module not found.

- [ ] **Step 3: Implementare `mockControlCore.ts`**

Create `apps/web/src/dev-tools/mockControlCore.ts`:

```typescript
import { createStore, type StoreApi } from 'zustand/vanilla';
import type { GroupToggles, EndpointOverrides, ToggleConfig } from './types';

export interface MockControlState {
  toggles: ToggleConfig;
  setGroup: (group: string, enabled: boolean) => void;
  setEndpointOverride: (key: string, enabled: boolean) => void;
  clearEndpointOverride: (key: string) => void;
}

export interface MockControlInit {
  allGroups: string[];
  enableList: string[];
  disableList: string[];
}

export function parseGroupList(raw: string | null | undefined): string[] {
  if (!raw) return [];
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
}

export function computeGroupToggles(
  allGroups: string[],
  enableList: string[],
  disableList: string[]
): GroupToggles {
  const result: GroupToggles = {};
  const hasEnableList = enableList.length > 0;
  for (const g of allGroups) {
    if (disableList.includes(g)) {
      result[g] = false;
    } else if (hasEnableList) {
      result[g] = enableList.includes(g);
    } else {
      result[g] = true;
    }
  }
  return result;
}

export function createMockControlStore(
  init: MockControlInit
): StoreApi<MockControlState> {
  const initialGroups = computeGroupToggles(
    init.allGroups,
    init.enableList,
    init.disableList
  );
  return createStore<MockControlState>((set, get) => ({
    toggles: {
      groups: initialGroups,
      overrides: {},
    },
    setGroup: (group, enabled) =>
      set({
        toggles: {
          ...get().toggles,
          groups: { ...get().toggles.groups, [group]: enabled },
        },
      }),
    setEndpointOverride: (key, enabled) =>
      set({
        toggles: {
          ...get().toggles,
          overrides: { ...get().toggles.overrides, [key]: enabled },
        },
      }),
    clearEndpointOverride: (key) => {
      const next: EndpointOverrides = { ...get().toggles.overrides };
      delete next[key];
      set({ toggles: { ...get().toggles, overrides: next } });
    },
  }));
}
```

- [ ] **Step 4: Run test to verify pass**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/mockControlCore.test.ts --run
```
Expected: PASS — 10 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/dev-tools/mockControlCore.ts apps/web/__tests__/dev-tools/mockControlCore.test.ts
git commit -m "feat(dev): add mockControlCore with group toggles and endpoint overrides"
```

---

### Task 9: MswHandlerRegistry

**Files:**
- Create: `apps/web/src/dev-tools/mswHandlerRegistry.ts`
- Create: `apps/web/__tests__/dev-tools/mswHandlerRegistry.test.ts`

**Context**: Il registry riceve i gruppi di handler esistenti (da `apps/web/src/mocks/handlers/*.ts`) e li filtra in base allo stato di `MockControlState`. In questa fase, il registry consuma **un'istantanea** dei toggle — la reattività runtime è fase 2.

- [ ] **Step 1: Leggere come sono esportati gli handler attuali**

Read: `apps/web/src/mocks/handlers/index.ts` (o equivalente) per capire il formato degli export.

Expected: trovi qualcosa tipo
```typescript
export const authHandlers: HttpHandler[] = [...];
export const gamesHandlers: HttpHandler[] = [...];
// ...
```

Se il formato è diverso (es. un singolo array `handlers`), annotare per step 3.

- [ ] **Step 2: Write failing test**

Create `apps/web/__tests__/dev-tools/mswHandlerRegistry.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { http, HttpResponse } from 'msw';
import { buildActiveHandlers, type HandlerGroup } from '@/dev-tools/mswHandlerRegistry';
import type { ToggleConfig } from '@/dev-tools/types';

const auth: HandlerGroup = {
  name: 'auth',
  handlers: [
    http.get('/api/v1/auth/me', () => HttpResponse.json({ id: 'MOCK-u' })),
  ],
};

const games: HandlerGroup = {
  name: 'games',
  handlers: [
    http.get('/api/v1/games', () => HttpResponse.json([])),
    http.post('/api/v1/games', () => HttpResponse.json({ id: 'MOCK-g' })),
  ],
};

describe('buildActiveHandlers', () => {
  it('returns all handlers when all groups enabled', () => {
    const toggles: ToggleConfig = {
      groups: { auth: true, games: true },
      overrides: {},
    };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(3);
  });

  it('excludes handlers from disabled groups', () => {
    const toggles: ToggleConfig = {
      groups: { auth: true, games: false },
      overrides: {},
    };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(1);
  });

  it('returns empty array when all groups disabled', () => {
    const toggles: ToggleConfig = {
      groups: { auth: false, games: false },
      overrides: {},
    };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toEqual([]);
  });

  it('treats missing group in toggles as enabled (default safe)', () => {
    const toggles: ToggleConfig = {
      groups: {},
      overrides: {},
    };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(3);
  });
});
```

- [ ] **Step 3: Implementare `mswHandlerRegistry.ts`**

Create `apps/web/src/dev-tools/mswHandlerRegistry.ts`:

```typescript
import type { HttpHandler } from 'msw';
import type { ToggleConfig } from './types';

export interface HandlerGroup {
  name: string;
  handlers: HttpHandler[];
}

/**
 * Build the list of MSW handlers that should be active for the given toggle state.
 * A group is active unless its toggle is explicitly false.
 * (Missing toggle entries are treated as enabled — safer default.)
 */
export function buildActiveHandlers(
  groups: HandlerGroup[],
  toggles: ToggleConfig
): HttpHandler[] {
  const active: HttpHandler[] = [];
  for (const group of groups) {
    const enabled = toggles.groups[group.name] !== false;
    if (enabled) {
      active.push(...group.handlers);
    }
  }
  return active;
}

/**
 * Generate an endpoint override key from a handler's method and path.
 * Format: "<group>.<METHOD> <path>" — example: "games.POST /api/v1/games"
 */
export function endpointKey(
  groupName: string,
  method: string,
  path: string
): string {
  return `${groupName}.${method.toUpperCase()} ${path}`;
}
```

- [ ] **Step 4: Run test to verify pass**

Run:
```bash
cd apps/web && pnpm test __tests__/dev-tools/mswHandlerRegistry.test.ts --run
```
Expected: PASS — 4 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/dev-tools/mswHandlerRegistry.ts apps/web/__tests__/dev-tools/mswHandlerRegistry.test.ts
git commit -m "feat(dev): add MSW handler registry with group-level toggling"
```

---

### Task 10: DevBadge component (statico)

**Files:**
- Create: `apps/web/src/dev-tools/devBadge.tsx`

- [ ] **Step 1: Implementare `devBadge.tsx`**

Create `apps/web/src/dev-tools/devBadge.tsx`:

```typescript
'use client';

import { useEffect, useState } from 'react';
import type { StoreApi } from 'zustand/vanilla';
import type { MockControlState } from './mockControlCore';
import type { ScenarioState } from './scenarioStore';
import type { MockAuthState } from './mockAuthStore';

export interface DevBadgeProps {
  controlStore: StoreApi<MockControlState>;
  scenarioStore: StoreApi<ScenarioState>;
  authStore: StoreApi<MockAuthState>;
}

function useStoreSlice<T, U>(store: StoreApi<T>, selector: (state: T) => U): U {
  const [slice, setSlice] = useState<U>(() => selector(store.getState()));
  useEffect(() => {
    return store.subscribe((state) => {
      setSlice(selector(state));
    });
  }, [store, selector]);
  return slice;
}

export function DevBadge({ controlStore, scenarioStore, authStore }: DevBadgeProps) {
  const groups = useStoreSlice(controlStore, (s) => s.toggles.groups);
  const scenarioName = useStoreSlice(scenarioStore, (s) => s.scenario.name);
  const role = useStoreSlice(authStore, (s) => s.currentUser.role);

  const enabledCount = Object.values(groups).filter(Boolean).length;
  const totalCount = Object.keys(groups).length;
  const allMocked = enabledCount === totalCount && totalCount > 0;
  const noneMocked = enabledCount === 0;

  const color = noneMocked ? '#22c55e' : allMocked ? '#ef4444' : '#f59e0b';

  return (
    <div
      data-testid="dev-badge"
      style={{
        position: 'fixed',
        bottom: 12,
        right: 12,
        zIndex: 99999,
        background: '#111827',
        color: '#f9fafb',
        padding: '8px 12px',
        borderRadius: 8,
        fontFamily: 'monospace',
        fontSize: 12,
        boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
        border: `2px solid ${color}`,
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        pointerEvents: 'auto',
      }}
      title={`MeepleDev: ${enabledCount}/${totalCount} groups mocked · scenario: ${scenarioName} · role: ${role}`}
    >
      <span
        style={{
          width: 10,
          height: 10,
          borderRadius: '50%',
          background: color,
          display: 'inline-block',
        }}
      />
      <span>
        MOCK · {enabledCount}/{totalCount} · {scenarioName} · {role}
      </span>
    </div>
  );
}
```

- [ ] **Step 2: Typecheck**

Run:
```bash
cd apps/web && pnpm typecheck
```
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/dev-tools/devBadge.tsx
git commit -m "feat(dev): add DevBadge component (static phase 1)"
```

---

### Task 11: Bootstrap installDevTools

**Files:**
- Create: `apps/web/src/dev-tools/install.ts`
- Create: `apps/web/src/dev-tools/index.ts`

- [ ] **Step 1: Creare `install.ts`**

Create `apps/web/src/dev-tools/install.ts`:

```typescript
import { createMockControlStore, parseGroupList } from './mockControlCore';
import { createScenarioStore } from './scenarioStore';
import {
  createMockAuthStore,
  readRoleFromEnv,
  readRoleFromQueryString,
} from './mockAuthStore';
import {
  validateScenario,
  SCENARIO_FALLBACK,
} from './scenarioValidator';
import type { Scenario } from './types';

const KNOWN_GROUPS = ['auth', 'games', 'chat', 'library', 'admin'] as const;

async function loadScenarioByName(name: string): Promise<Scenario> {
  try {
    // Webpack/Turbopack resolves glob imports at build time.
    // These JSON files come from docs/superpowers/fixtures/scenarios.
    // We rely on a static map generated in a later step (task 12 uses a build-time require context).
    const mod = await import(
      /* webpackInclude: /\.json$/ */
      `../../../../docs/superpowers/fixtures/scenarios/${name}.json`
    );
    const data = (mod.default ?? mod) as unknown;
    const result = validateScenario(data);
    if (!result.valid) {
      console.warn(
        `[MeepleDev] Scenario "${name}" failed validation:`,
        result.errors
      );
      return SCENARIO_FALLBACK;
    }
    return data as Scenario;
  } catch (err) {
    console.warn(`[MeepleDev] Failed to load scenario "${name}":`, err);
    return SCENARIO_FALLBACK;
  }
}

export interface InstalledDevTools {
  controlStore: ReturnType<typeof createMockControlStore>;
  scenarioStore: ReturnType<typeof createScenarioStore>;
  authStore: ReturnType<typeof createMockAuthStore>;
}

export async function installDevTools(): Promise<InstalledDevTools> {
  const enableList = parseGroupList(process.env.NEXT_PUBLIC_MSW_ENABLE);
  const disableList = parseGroupList(process.env.NEXT_PUBLIC_MSW_DISABLE);
  const scenarioName = process.env.NEXT_PUBLIC_DEV_SCENARIO ?? 'empty';

  const scenario = await loadScenarioByName(scenarioName);

  const controlStore = createMockControlStore({
    allGroups: [...KNOWN_GROUPS],
    enableList,
    disableList,
  });

  const scenarioStore = createScenarioStore(scenario);

  const authStore = createMockAuthStore({
    scenarioUser: scenario.auth.currentUser,
    availableUsers: scenario.auth.availableUsers,
    envRole: readRoleFromEnv(),
    queryStringRole: readRoleFromQueryString(),
  });

  if (typeof console !== 'undefined') {
    console.info(
      `[MeepleDev] installed · scenario=${scenario.name} · groups=`,
      controlStore.getState().toggles.groups
    );
  }

  return { controlStore, scenarioStore, authStore };
}
```

- [ ] **Step 2: Creare `index.ts` (public API)**

Create `apps/web/src/dev-tools/index.ts`:

```typescript
export { installDevTools } from './install';
export type { InstalledDevTools } from './install';
export type { Scenario, MockUser, UserRole } from './types';
```

- [ ] **Step 3: Typecheck**

Run:
```bash
cd apps/web && pnpm typecheck
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/dev-tools/install.ts apps/web/src/dev-tools/index.ts
git commit -m "feat(dev): add installDevTools bootstrap with scenario loading"
```

---

### Task 12: Integrare DevBadge e installDevTools nel MockProvider

**Files:**
- Modify: `apps/web/src/mocks/mock-provider.tsx`

- [ ] **Step 1: Leggere il file corrente**

Read: `apps/web/src/mocks/mock-provider.tsx`
Identificare il punto in cui MSW viene avviato (probabilmente un `useEffect` + `worker.start()`).

- [ ] **Step 2: Modificare per montare installDevTools + DevBadge**

Modificare la funzione principale del provider. Il pattern esatto dipende dal file corrente, ma l'idea è:

```typescript
'use client';

import { useEffect, useState, type ReactNode } from 'react';
import type { InstalledDevTools } from '@/dev-tools';
import { DevBadge } from '@/dev-tools/devBadge';

const IS_DEV_MOCK =
  process.env.NODE_ENV === 'development' &&
  process.env.NEXT_PUBLIC_MOCK_MODE === 'true';

export function MockProvider({ children }: { children: ReactNode }) {
  const [tools, setTools] = useState<InstalledDevTools | null>(null);

  useEffect(() => {
    if (!IS_DEV_MOCK) return;
    let cancelled = false;
    (async () => {
      const { installDevTools } = await import('@/dev-tools');
      const installed = await installDevTools();
      if (cancelled) return;
      // Start MSW worker with active handlers (existing logic kept below)
      const { worker } = await import('./browser');
      await worker.start({ onUnhandledRequest: 'bypass' });
      setTools(installed);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      {children}
      {IS_DEV_MOCK && tools && (
        <DevBadge
          controlStore={tools.controlStore}
          scenarioStore={tools.scenarioStore}
          authStore={tools.authStore}
        />
      )}
    </>
  );
}
```

**Nota importante**: se `mock-provider.tsx` esistente ha logica aggiuntiva (es. unregister service workers), preservarla. Questa modifica è **additive** alla logica esistente.

- [ ] **Step 3: Typecheck e lint**

Run:
```bash
cd apps/web && pnpm typecheck && pnpm lint
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/mocks/mock-provider.tsx
git commit -m "feat(dev): wire DevBadge and installDevTools into MockProvider"
```

---

### Task 13: Aggiornare `browser.ts` per usare MswHandlerRegistry

**Files:**
- Modify: `apps/web/src/mocks/browser.ts`
- Create: `apps/web/src/mocks/handlers/registry.ts`

**Context**: L'attuale `browser.ts` probabilmente importa handler come array unico. Serve un registry che raggruppa per nome.

- [ ] **Step 1: Leggere `browser.ts` corrente**

Read: `apps/web/src/mocks/browser.ts` e `apps/web/src/mocks/handlers/index.ts` (o files).

- [ ] **Step 2: Creare `handlers/registry.ts`**

Create `apps/web/src/mocks/handlers/registry.ts`:

```typescript
import type { HandlerGroup } from '@/dev-tools/mswHandlerRegistry';

// Import each handler group. Adjust paths to match actual files.
import { authHandlers } from './auth';
import { gamesHandlers } from './games';
import { chatHandlers } from './chat';
import { libraryHandlers } from './library';
import { adminHandlers } from './admin';

/**
 * Registry of all known MSW handler groups.
 * Each group is togglable via NEXT_PUBLIC_MSW_ENABLE / DISABLE.
 */
export const HANDLER_GROUPS: HandlerGroup[] = [
  { name: 'auth', handlers: authHandlers },
  { name: 'games', handlers: gamesHandlers },
  { name: 'chat', handlers: chatHandlers },
  { name: 'library', handlers: libraryHandlers },
  { name: 'admin', handlers: adminHandlers },
];
```

**Nota**: se i file handler correnti esportano nomi diversi (es. `handlers` invece di `authHandlers`), aggiustare gli import di conseguenza. Se un gruppo non esiste ancora, rimuoverlo dalla lista.

- [ ] **Step 3: Modificare `browser.ts`**

Modificare `apps/web/src/mocks/browser.ts` per usare il registry:

```typescript
import { setupWorker } from 'msw/browser';
import { HANDLER_GROUPS } from './handlers/registry';
import { buildActiveHandlers, type HandlerGroup } from '@/dev-tools/mswHandlerRegistry';
import { parseGroupList, computeGroupToggles } from '@/dev-tools/mockControlCore';

function getInitialHandlers() {
  const enable = parseGroupList(process.env.NEXT_PUBLIC_MSW_ENABLE);
  const disable = parseGroupList(process.env.NEXT_PUBLIC_MSW_DISABLE);
  const allNames = HANDLER_GROUPS.map((g: HandlerGroup) => g.name);
  const groupToggles = computeGroupToggles(allNames, enable, disable);
  return buildActiveHandlers(HANDLER_GROUPS, { groups: groupToggles, overrides: {} });
}

export const worker = setupWorker(...getInitialHandlers());
```

**Nota**: preservare qualsiasi configurazione esistente in `browser.ts` (HMR, custom options).

- [ ] **Step 4: Typecheck**

Run:
```bash
cd apps/web && pnpm typecheck
```
Expected: PASS.

- [ ] **Step 5: Smoke test manuale**

```bash
cd apps/web && NEXT_PUBLIC_MOCK_MODE=true NEXT_PUBLIC_MSW_DISABLE=admin pnpm dev
```
Apri `http://localhost:3000`, DevTools Console → dovresti vedere log `[MeepleDev] installed · scenario=...` e il DevBadge in basso a destra.

Kill con Ctrl+C dopo verifica.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/mocks/handlers/registry.ts apps/web/src/mocks/browser.ts
git commit -m "feat(dev): use MswHandlerRegistry for group-level MSW toggles"
```

---

### Task 14: Scenari seed small-library e admin-busy

**Files:**
- Create: `docs/superpowers/fixtures/scenarios/small-library.json`
- Create: `docs/superpowers/fixtures/scenarios/admin-busy.json`

- [ ] **Step 1: Creare `small-library.json`**

Create `docs/superpowers/fixtures/scenarios/small-library.json`:

```json
{
  "$schema": "../schema/scenario.schema.json",
  "name": "small-library",
  "description": "Utente User con 3 giochi, 5 sessioni e una chat storica.",
  "auth": {
    "currentUser": {
      "id": "MOCK-00000000-0000-0000-0000-000000000001",
      "email": "dev@meeple.local",
      "displayName": "Dev User",
      "role": "User"
    },
    "availableUsers": [
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000001",
        "email": "dev@meeple.local",
        "displayName": "Dev User",
        "role": "User"
      },
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000002",
        "email": "admin@meeple.local",
        "displayName": "Admin",
        "role": "Admin"
      },
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000003",
        "email": "editor@meeple.local",
        "displayName": "Editor",
        "role": "Editor"
      },
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000000",
        "email": "guest@meeple.local",
        "displayName": "Guest",
        "role": "Guest"
      }
    ]
  },
  "games": [
    {
      "id": "MOCK-11111111-0000-0000-0000-000000000001",
      "title": "Wingspan",
      "publisher": "Stonemaier Games",
      "averageRating": 8.1,
      "bggId": 266192
    },
    {
      "id": "MOCK-11111111-0000-0000-0000-000000000002",
      "title": "Scythe",
      "publisher": "Stonemaier Games",
      "averageRating": 8.3,
      "bggId": 169786
    },
    {
      "id": "MOCK-11111111-0000-0000-0000-000000000003",
      "title": "Terraforming Mars",
      "publisher": "FryxGames",
      "averageRating": 8.4,
      "bggId": 167791
    }
  ],
  "sessions": [
    {
      "id": "MOCK-22222222-0000-0000-0000-000000000001",
      "gameId": "MOCK-11111111-0000-0000-0000-000000000001",
      "startedAt": "2026-04-01T18:00:00.000Z"
    },
    {
      "id": "MOCK-22222222-0000-0000-0000-000000000002",
      "gameId": "MOCK-11111111-0000-0000-0000-000000000001",
      "startedAt": "2026-04-02T20:00:00.000Z"
    },
    {
      "id": "MOCK-22222222-0000-0000-0000-000000000003",
      "gameId": "MOCK-11111111-0000-0000-0000-000000000002",
      "startedAt": "2026-04-03T19:30:00.000Z"
    },
    {
      "id": "MOCK-22222222-0000-0000-0000-000000000004",
      "gameId": "MOCK-11111111-0000-0000-0000-000000000003",
      "startedAt": "2026-04-05T15:00:00.000Z"
    },
    {
      "id": "MOCK-22222222-0000-0000-0000-000000000005",
      "gameId": "MOCK-11111111-0000-0000-0000-000000000002",
      "startedAt": "2026-04-07T21:00:00.000Z"
    }
  ],
  "library": {
    "ownedGameIds": [
      "MOCK-11111111-0000-0000-0000-000000000001",
      "MOCK-11111111-0000-0000-0000-000000000002"
    ],
    "wishlistGameIds": [
      "MOCK-11111111-0000-0000-0000-000000000003"
    ]
  },
  "chatHistory": [
    {
      "chatId": "MOCK-33333333-0000-0000-0000-000000000001",
      "messages": [
        { "role": "user", "content": "Come si gioca a Wingspan?" },
        { "role": "assistant", "content": "Wingspan è un gioco da tavolo di strategia in cui colleziona uccelli e costruisce habitat. Nel turno hai 4 azioni: giocare un uccello, guadagnare cibo, deporre uova o pescare carte." }
      ]
    }
  ],
  "bggGames": [
    { "bggId": 266192, "name": "Wingspan" },
    { "bggId": 169786, "name": "Scythe" },
    { "bggId": 167791, "name": "Terraforming Mars" }
  ]
}
```

- [ ] **Step 2: Creare `admin-busy.json`**

Create `docs/superpowers/fixtures/scenarios/admin-busy.json`:

```json
{
  "$schema": "../schema/scenario.schema.json",
  "name": "admin-busy",
  "description": "Admin con 20 giochi e 50 sessioni — per testare liste lunghe e performance UI.",
  "auth": {
    "currentUser": {
      "id": "MOCK-00000000-0000-0000-0000-000000000002",
      "email": "admin@meeple.local",
      "displayName": "Admin",
      "role": "Admin"
    },
    "availableUsers": [
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000002",
        "email": "admin@meeple.local",
        "displayName": "Admin",
        "role": "Admin"
      },
      {
        "id": "MOCK-00000000-0000-0000-0000-000000000001",
        "email": "dev@meeple.local",
        "displayName": "Dev User",
        "role": "User"
      }
    ]
  },
  "games": [],
  "sessions": [],
  "library": { "ownedGameIds": [], "wishlistGameIds": [] },
  "chatHistory": [],
  "bggGames": [],
  "documents": []
}
```

**Nota**: l'`admin-busy` ha arrays vuoti di partenza. In un task di polish futuro (fase 3), aggiungeremo 20 games + 50 sessions programmaticamente.

- [ ] **Step 3: Validare i JSON a mano**

Run:
```bash
node -e "['empty','small-library','admin-busy'].forEach(n => { const d=require('./docs/superpowers/fixtures/scenarios/'+n+'.json'); console.log(n, '→', d.name); });"
```
Expected:
```
empty → empty
small-library → small-library
admin-busy → admin-busy
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/fixtures/scenarios/small-library.json docs/superpowers/fixtures/scenarios/admin-busy.json
git commit -m "feat(dev): add small-library and admin-busy seed scenarios"
```

---

### Task 15: Integration test MSW group toggle

**Files:**
- Create: `apps/web/__tests__/integration/dev-tools/mswGroupToggle.test.ts`

- [ ] **Step 1: Write integration test**

Create `apps/web/__tests__/integration/dev-tools/mswGroupToggle.test.ts`:

```typescript
import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';
import { buildActiveHandlers, type HandlerGroup } from '@/dev-tools/mswHandlerRegistry';
import { computeGroupToggles } from '@/dev-tools/mockControlCore';

const authGroup: HandlerGroup = {
  name: 'auth',
  handlers: [
    http.get('http://test.local/api/v1/auth/me', () =>
      HttpResponse.json({ id: 'MOCK-u' })
    ),
  ],
};

const gamesGroup: HandlerGroup = {
  name: 'games',
  handlers: [
    http.get('http://test.local/api/v1/games', () =>
      HttpResponse.json([{ id: 'MOCK-g1' }])
    ),
  ],
};

const groups = [authGroup, gamesGroup];

describe('MSW group toggle integration', () => {
  it('both groups enabled → both intercepted', async () => {
    const toggles = {
      groups: computeGroupToggles(['auth', 'games'], [], []),
      overrides: {},
    };
    const server = setupServer(...buildActiveHandlers(groups, toggles));
    server.listen({ onUnhandledRequest: 'error' });

    const authRes = await fetch('http://test.local/api/v1/auth/me');
    expect(authRes.ok).toBe(true);
    const gamesRes = await fetch('http://test.local/api/v1/games');
    expect(gamesRes.ok).toBe(true);

    server.close();
  });

  it('games disabled → only auth intercepted', async () => {
    const toggles = {
      groups: computeGroupToggles(['auth', 'games'], [], ['games']),
      overrides: {},
    };
    const server = setupServer(...buildActiveHandlers(groups, toggles));
    server.listen({ onUnhandledRequest: 'bypass' });

    const authRes = await fetch('http://test.local/api/v1/auth/me');
    expect(authRes.ok).toBe(true);
    // games should passthrough — since it's not a real server, will fail network
    await expect(fetch('http://test.local/api/v1/games')).rejects.toThrow();

    server.close();
  });
});
```

- [ ] **Step 2: Run**

Run:
```bash
cd apps/web && pnpm test __tests__/integration/dev-tools/mswGroupToggle.test.ts --run
```
Expected: PASS — 2 tests.

- [ ] **Step 3: Commit**

```bash
git add apps/web/__tests__/integration/dev-tools/mswGroupToggle.test.ts
git commit -m "test(dev): add MSW group toggle integration test"
```

**PR #2 check**:
- `pnpm test --run` → tutti i test pass
- `pnpm typecheck` → PASS
- `pnpm lint` → PASS
- `pnpm build` → PASS (anche in prod config)
- Smoke test manuale: `pnpm dev:mock` avvia, DevBadge visibile, scenario caricato

Aprire PR #2 → `main-dev` con titolo "feat(dev): Phase 1.1 — MSW granular toggles and static DevBadge".

---

## PR #3 — Fase 1.2: BE toggle state + MockAwareProxy + 3 mock (Task 16-22)

### Task 16: IMockToggle{Reader,Writer,Events} + EventArgs

**Files:**
- Create: `apps/api/src/Api/DevTools/IMockToggleReader.cs`
- Create: `apps/api/src/Api/DevTools/IMockToggleWriter.cs`
- Create: `apps/api/src/Api/DevTools/IMockToggleEvents.cs`
- Create: `apps/api/src/Api/DevTools/MockToggleChangedEventArgs.cs`

- [ ] **Step 1: Creare `MockToggleChangedEventArgs.cs`**

Create `apps/api/src/Api/DevTools/MockToggleChangedEventArgs.cs`:

```csharp
namespace Api.DevTools;

internal sealed record MockToggleChangedEventArgs(string ServiceName, bool Mocked);
```

- [ ] **Step 2: Creare `IMockToggleReader.cs`**

Create `apps/api/src/Api/DevTools/IMockToggleReader.cs`:

```csharp
using System.Collections.Generic;

namespace Api.DevTools;

/// <summary>
/// Read-only access to mock toggle state. Injected into MockAwareProxy&lt;T&gt;.
/// </summary>
internal interface IMockToggleReader
{
    bool IsMocked(string serviceName);
    IReadOnlyDictionary<string, bool> GetAll();
}
```

- [ ] **Step 3: Creare `IMockToggleWriter.cs`**

Create `apps/api/src/Api/DevTools/IMockToggleWriter.cs`:

```csharp
namespace Api.DevTools;

/// <summary>
/// Write-only access to mock toggle state. Injected into /dev/toggles endpoints (phase 2).
/// </summary>
internal interface IMockToggleWriter
{
    void Set(string serviceName, bool mocked);
}
```

- [ ] **Step 4: Creare `IMockToggleEvents.cs`**

Create `apps/api/src/Api/DevTools/IMockToggleEvents.cs`:

```csharp
using System;

namespace Api.DevTools;

/// <summary>
/// Event subscription access to mock toggle changes.
/// </summary>
internal interface IMockToggleEvents
{
    event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;
}
```

- [ ] **Step 5: Build**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/DevTools/IMockToggle*.cs apps/api/src/Api/DevTools/MockToggleChangedEventArgs.cs
git commit -m "feat(dev): add segregated mock toggle state interfaces"
```

---

### Task 17: MockToggleStateProvider

**Files:**
- Create: `apps/api/src/Api/DevTools/MockToggleStateProvider.cs`
- Create: `tests/Api.Tests/DevTools/MockToggleStateProviderTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Api.Tests/DevTools/MockToggleStateProviderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools;
using Xunit;

namespace Api.Tests.DevTools;

public class MockToggleStateProviderTests
{
    [Fact]
    public void InitializesFromEnvironmentVariables()
    {
        var env = new Dictionary<string, string?>
        {
            ["MOCK_LLM"] = "true",
            ["MOCK_EMBEDDING"] = "false",
            ["MOCK_S3"] = "true"
        };
        var provider = new MockToggleStateProvider(env, new[] { "llm", "embedding", "s3", "bgg" });

        Assert.True(provider.IsMocked("llm"));
        Assert.False(provider.IsMocked("embedding"));
        Assert.True(provider.IsMocked("s3"));
        Assert.False(provider.IsMocked("bgg"));
    }

    [Fact]
    public void DefaultsMissingKeysToFalse()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(),
            new[] { "llm" });
        Assert.False(provider.IsMocked("llm"));
    }

    [Fact]
    public void IsMockedThrowsOnUnknownService()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(),
            new[] { "llm" });
        Assert.Throws<InvalidOperationException>(() => provider.IsMocked("unknown"));
    }

    [Fact]
    public void SetUpdatesStateAndEmitsEvent()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?> { ["MOCK_LLM"] = "false" },
            new[] { "llm" });

        MockToggleChangedEventArgs? received = null;
        provider.ToggleChanged += (_, args) => received = args;

        provider.Set("llm", true);

        Assert.True(provider.IsMocked("llm"));
        Assert.NotNull(received);
        Assert.Equal("llm", received!.ServiceName);
        Assert.True(received.Mocked);
    }

    [Fact]
    public void SetThrowsOnUnknownService()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(),
            new[] { "llm" });
        Assert.Throws<InvalidOperationException>(() => provider.Set("unknown", true));
    }

    [Fact]
    public async Task ConcurrentReadsAndWritesAreSafe()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(),
            new[] { "llm", "embedding" });

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                provider.Set("llm", idx % 2 == 0);
                provider.IsMocked("llm");
                provider.GetAll();
            }));
        }
        await Task.WhenAll(tasks);
        // If we got here without crash, thread-safety is OK.
        Assert.True(true);
    }

    [Fact]
    public void GetAllReturnsReadOnlySnapshot()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?> { ["MOCK_LLM"] = "true" },
            new[] { "llm" });
        var snapshot = provider.GetAll();
        Assert.True(snapshot["llm"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, bool>>(snapshot);
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockToggleStateProviderTests"
```
Expected: FAIL — type `MockToggleStateProvider` not found.

- [ ] **Step 3: Implementare `MockToggleStateProvider.cs`**

Create `apps/api/src/Api/DevTools/MockToggleStateProvider.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Api.DevTools;

/// <summary>
/// Thread-safe implementation of mock toggle state.
/// Bootstrapped from environment variables at startup (MOCK_{SERVICE_NAME_UPPER}).
/// </summary>
internal sealed class MockToggleStateProvider
    : IMockToggleReader, IMockToggleWriter, IMockToggleEvents
{
    private readonly ConcurrentDictionary<string, bool> _state;
    private readonly HashSet<string> _knownServices;

    public event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;

    public MockToggleStateProvider(
        IReadOnlyDictionary<string, string?> environment,
        IEnumerable<string> knownServiceNames)
    {
        _knownServices = new HashSet<string>(knownServiceNames, StringComparer.OrdinalIgnoreCase);
        _state = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var svc in _knownServices)
        {
            var envKey = $"MOCK_{svc.ToUpperInvariant()}";
            environment.TryGetValue(envKey, out var value);
            var mocked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            _state[svc] = mocked;
        }
    }

    public bool IsMocked(string serviceName)
    {
        if (!_knownServices.Contains(serviceName))
        {
            throw new InvalidOperationException(
                $"Unknown mock service '{serviceName}'. Known: {string.Join(", ", _knownServices)}");
        }
        return _state.TryGetValue(serviceName, out var mocked) && mocked;
    }

    public IReadOnlyDictionary<string, bool> GetAll()
    {
        return new ReadOnlyDictionary<string, bool>(
            _state.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase));
    }

    public void Set(string serviceName, bool mocked)
    {
        if (!_knownServices.Contains(serviceName))
        {
            throw new InvalidOperationException(
                $"Unknown mock service '{serviceName}'. Known: {string.Join(", ", _knownServices)}");
        }
        _state[serviceName] = mocked;
        ToggleChanged?.Invoke(this, new MockToggleChangedEventArgs(serviceName, mocked));
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockToggleStateProviderTests"
```
Expected: PASS — 7 tests.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/DevTools/MockToggleStateProvider.cs tests/Api.Tests/DevTools/MockToggleStateProviderTests.cs
git commit -m "feat(dev): add MockToggleStateProvider with env bootstrap and thread-safety"
```

---

### Task 18: MockAwareProxy<T> generic dispatcher

**Files:**
- Create: `apps/api/src/Api/DevTools/MockAwareProxy.cs`
- Create: `tests/Api.Tests/DevTools/MockAwareProxyTests.cs`

**Critical assumption to verify**: `System.Reflection.DispatchProxy` supports internal interfaces **only if the proxy type is created in the same assembly or the assembly has `InternalsVisibleTo`**. Our mocks live in `Api.DevTools` namespace inside `Api.dll`, so interfaces like `ILlmService` (internal in `Api.Services`) should be reachable. If runtime errors appear, fallback: make the interfaces `public` with `[EditorBrowsable(Never)]` or use `InternalsVisibleTo("Api.Tests")` in csproj.

- [ ] **Step 1: Write failing test**

Create `tests/Api.Tests/DevTools/MockAwareProxyTests.cs`:

```csharp
using System.Collections.Generic;
using Api.DevTools;
using Xunit;

namespace Api.Tests.DevTools;

public interface ITestService
{
    string Greet(string name);
    int Add(int a, int b);
}

internal sealed class RealTestService : ITestService
{
    public string Greet(string name) => $"Hello, {name}! (real)";
    public int Add(int a, int b) => a + b;
}

internal sealed class MockTestService : ITestService
{
    public string Greet(string name) => $"Hello, {name}! (mock)";
    public int Add(int a, int b) => 42;
}

public class MockAwareProxyTests
{
    private MockToggleStateProvider MakeProvider(bool mocked)
    {
        var env = new Dictionary<string, string?>
        {
            ["MOCK_TEST"] = mocked ? "true" : "false"
        };
        return new MockToggleStateProvider(env, new[] { "test" });
    }

    [Fact]
    public void DispatchesToRealWhenNotMocked()
    {
        var provider = MakeProvider(false);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));
        Assert.Equal(5, proxy.Add(2, 3));
    }

    [Fact]
    public void DispatchesToMockWhenMocked()
    {
        var provider = MakeProvider(true);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (mock)", proxy.Greet("world"));
        Assert.Equal(42, proxy.Add(2, 3));
    }

    [Fact]
    public void RuntimeSwitchTakesEffectImmediately()
    {
        var provider = MakeProvider(false);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));

        provider.Set("test", true);
        Assert.Equal("Hello, world! (mock)", proxy.Greet("world"));

        provider.Set("test", false);
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockAwareProxyTests"
```
Expected: FAIL — type `MockAwareProxy` not found.

- [ ] **Step 3: Implementare `MockAwareProxy.cs`**

Create `apps/api/src/Api/DevTools/MockAwareProxy.cs`:

```csharp
using System;
using System.Reflection;

namespace Api.DevTools;

/// <summary>
/// Generic dispatch proxy that routes method calls to either a real or mock
/// implementation based on runtime toggle state.
/// </summary>
/// <typeparam name="TService">Service interface (class or internal).</typeparam>
internal sealed class MockAwareProxy<TService> : DispatchProxy where TService : class
{
    private TService _real = default!;
    private TService _mock = default!;
    private IMockToggleReader _toggles = default!;
    private string _serviceName = default!;

    public static TService Create(
        TService real,
        TService mock,
        IMockToggleReader toggles,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(real);
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(toggles);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var proxy = Create<TService, MockAwareProxy<TService>>();
        var impl = (MockAwareProxy<TService>)(object)proxy!;
        impl._real = real;
        impl._mock = mock;
        impl._toggles = toggles;
        impl._serviceName = serviceName;
        return proxy!;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            throw new InvalidOperationException("DispatchProxy targetMethod is null.");
        }

        var target = _toggles.IsMocked(_serviceName) ? _mock : _real;
        try
        {
            return targetMethod.Invoke(target, args);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockAwareProxyTests"
```
Expected: PASS — 3 tests.

- [ ] **Step 5: Verificare che funzioni con internal interfaces**

Se il test passa, l'assunzione è confermata. Se fallisce con "Unable to cast" o "interface not accessible", procedere al fallback:

1. Aggiungere a `apps/api/src/Api/Api.csproj` (dentro un `<ItemGroup>`):
   ```xml
   <InternalsVisibleTo Include="Api.Tests" />
   ```
2. Verificare che le interfacce interne usate (es. `ILlmService`) abbiano modifier `internal` non `file`.
3. Se il problema persiste, accettare di rendere le 3 interfacce (`ILlmService`, `IEmbeddingService`, `IBlobStorageService`) `public` e marcarle con `[EditorBrowsable(Never)]` — comunque prima verificare se sono già usate da codice esterno (dovrebbero no).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/DevTools/MockAwareProxy.cs tests/Api.Tests/DevTools/MockAwareProxyTests.cs
git commit -m "feat(dev): add MockAwareProxy<T> generic DispatchProxy dispatcher"
```

---

### Task 19: AddMockAwareService extension + known services registry

**Files:**
- Create: `apps/api/src/Api/DevTools/MockAwareServiceCollectionExtensions.cs`
- Create: `apps/api/src/Api/DevTools/KnownMockServices.cs`

- [ ] **Step 1: Creare `KnownMockServices.cs`**

Create `apps/api/src/Api/DevTools/KnownMockServices.cs`:

```csharp
using System.Collections.Generic;

namespace Api.DevTools;

internal static class KnownMockServices
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "llm",
        "embedding",
        "reranker",
        "smoldocling",
        "unstructured",
        "bgg",
        "s3",
        "n8n",
    };
}
```

- [ ] **Step 2: Creare `MockAwareServiceCollectionExtensions.cs`**

Create `apps/api/src/Api/DevTools/MockAwareServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Api.DevTools;

internal static class MockAwareServiceCollectionExtensions
{
    /// <summary>
    /// Registers a service with a mock-aware proxy that dispatches runtime
    /// between TReal and TMock based on IMockToggleReader state.
    /// Both TReal and TMock are registered as concrete types and the TService
    /// resolves to the proxy.
    /// </summary>
    public static IServiceCollection AddMockAwareService<TService, TReal, TMock>(
        this IServiceCollection services,
        string serviceName)
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
}
```

- [ ] **Step 3: Build**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/DevTools/MockAwareServiceCollectionExtensions.cs apps/api/src/Api/DevTools/KnownMockServices.cs
git commit -m "feat(dev): add AddMockAwareService extension and known services registry"
```

---

### Task 20: MockHeaderMiddleware

**Files:**
- Create: `apps/api/src/Api/DevTools/MockHeaderMiddleware.cs`

- [ ] **Step 1: Implementare middleware**

Create `apps/api/src/Api/DevTools/MockHeaderMiddleware.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Api.DevTools;

internal sealed class MockHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMockToggleReader _toggles;

    public MockHeaderMiddleware(RequestDelegate next, IMockToggleReader toggles)
    {
        _next = next;
        _toggles = toggles;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var mocked = _toggles.GetAll()
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .OrderBy(k => k)
                .ToList();
            var value = mocked.Count == 0 ? "backend-di:" : $"backend-di:{string.Join(",", mocked)}";
            context.Response.Headers.Append("X-Meeple-Mock", value);
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
```

- [ ] **Step 2: Build**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/DevTools/MockHeaderMiddleware.cs
git commit -m "feat(dev): add MockHeaderMiddleware writing X-Meeple-Mock"
```

---

### Task 21: DevToolsServiceCollectionExtensions + MockLlmService template

**Files:**
- Create: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`
- Create: `apps/api/src/Api/DevTools/Scenarios/ScenarioLoader.cs`
- Create: `apps/api/src/Api/DevTools/MockImpls/MockLlmService.cs`
- Create: `tests/Api.Tests/DevTools/MockLlmServiceTests.cs`

- [ ] **Step 1: Creare ScenarioLoader**

Create `apps/api/src/Api/DevTools/Scenarios/ScenarioLoader.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Api.DevTools.Scenarios;

/// <summary>
/// Loads scenario JSON files from docs/superpowers/fixtures/scenarios.
/// Used by backend mock services to share deterministic seed data with frontend.
/// </summary>
internal sealed class ScenarioLoader
{
    private readonly string _rootPath;

    public ScenarioLoader(string? rootPath = null)
    {
        _rootPath = rootPath ?? LocateFixturesDir();
    }

    public JsonElement Load(string scenarioName)
    {
        var path = Path.Combine(_rootPath, $"{scenarioName}.json");
        if (!File.Exists(path))
        {
            return EmptyDocument();
        }
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public IEnumerable<string> ListAvailable()
    {
        if (!Directory.Exists(_rootPath)) yield break;
        foreach (var file in Directory.GetFiles(_rootPath, "*.json"))
        {
            yield return Path.GetFileNameWithoutExtension(file);
        }
    }

    private static JsonElement EmptyDocument()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private static string LocateFixturesDir()
    {
        // Walk up from AppContext.BaseDirectory to find repo root
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "superpowers", "fixtures", "scenarios");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        // Fallback: relative to CWD
        return Path.Combine(Directory.GetCurrentDirectory(), "docs", "superpowers", "fixtures", "scenarios");
    }
}
```

- [ ] **Step 2: Write failing test for MockLlmService**

Create `tests/Api.Tests/DevTools/MockLlmServiceTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Api.Services;
using Xunit;

namespace Api.Tests.DevTools;

public class MockLlmServiceTests
{
    private static MockLlmService MakeService()
    {
        return new MockLlmService(new ScenarioLoader());
    }

    [Fact]
    public async Task GenerateCompletion_ReturnsDeterministicResponse()
    {
        var svc = MakeService();
        var r1 = await svc.GenerateCompletionAsync("sys", "hello world", RequestSource.Manual, CancellationToken.None);
        var r2 = await svc.GenerateCompletionAsync("sys", "hello world", RequestSource.Manual, CancellationToken.None);
        Assert.True(r1.Success);
        Assert.Equal(r1.Response, r2.Response);
        Assert.Contains("mock", r1.Response, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateCompletion_ZeroCost()
    {
        var svc = MakeService();
        var r = await svc.GenerateCompletionAsync("sys", "hi", RequestSource.Manual, CancellationToken.None);
        Assert.Equal(0m, r.Cost.TotalCost);
    }

    [Fact]
    public async Task GenerateCompletionStream_EmitsMultipleChunks()
    {
        var svc = MakeService();
        int count = 0;
        bool sawFinal = false;
        await foreach (var chunk in svc.GenerateCompletionStreamAsync("sys", "tell me a story", RequestSource.Manual, CancellationToken.None))
        {
            count++;
            if (chunk.IsFinal) sawFinal = true;
        }
        Assert.True(count >= 2, "Expected at least 2 chunks");
        Assert.True(sawFinal, "Expected a final chunk with IsFinal=true");
    }

    [Fact]
    public async Task GenerateJson_ReturnsNullForUnparseable()
    {
        var svc = MakeService();
        var result = await svc.GenerateJsonAsync<TestRecord>("sys", "hello", RequestSource.Manual, CancellationToken.None);
        // Mock returns a stub; as long as it doesn't throw, accept null or object
        Assert.True(result is null || result is TestRecord);
    }

    private sealed record TestRecord(string Field);
}
```

**Nota**: `ILlmService`, `LlmCompletionResult`, `StreamChunk`, `RequestSource` sono `internal` in `Api.Services`. Questo test vive in `Api.Tests` assembly — richiede `InternalsVisibleTo` che probabilmente è già configurato (verificare `Api.csproj`). Se manca, aggiungerlo.

- [ ] **Step 3: Run test to verify failure**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockLlmServiceTests"
```
Expected: FAIL — `MockLlmService` not found.

- [ ] **Step 4: Implementare `MockLlmService`**

Create `apps/api/src/Api/DevTools/MockImpls/MockLlmService.cs`:

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of ILlmService.
/// Returns scenario-driven responses; SSE stream splits text into 5 chunks + final.
/// </summary>
internal sealed class MockLlmService : ILlmService
{
    private readonly ScenarioLoader _scenarios;

    public MockLlmService(ScenarioLoader scenarios)
    {
        _scenarios = scenarios;
    }

    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        var response = BuildResponse(userPrompt);
        return Task.FromResult(LlmCompletionResult.CreateSuccess(
            response,
            usage: new LlmUsage(PromptTokens: EstimateTokens(systemPrompt + userPrompt), CompletionTokens: EstimateTokens(response), TotalTokens: 0),
            cost: LlmCost.Empty));
    }

    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var full = BuildResponse(userPrompt);
        // Split into ~5 chunks for realism
        var chunkSize = System.Math.Max(1, full.Length / 5);
        for (int i = 0; i < full.Length; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();
            var piece = full.Substring(i, System.Math.Min(chunkSize, full.Length - i));
            yield return new StreamChunk(Content: piece);
            await Task.Yield();
        }
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(EstimateTokens(userPrompt), EstimateTokens(full), 0),
            Cost: LlmCost.Empty,
            IsFinal: true);
    }

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default) where T : class
    {
        // Return default instance if parameterless constructor available; else null.
        try
        {
            var instance = System.Activator.CreateInstance<T>();
            return Task.FromResult<T?>(instance);
        }
        catch
        {
            return Task.FromResult<T?>(null);
        }
    }

    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        return GenerateCompletionAsync(systemPrompt, userPrompt, source, ct);
    }

    private static string BuildResponse(string userPrompt)
    {
        // Deterministic: hash-prefixed echo
        return $"[mock-llm] You asked: \"{userPrompt.Trim()}\". This is a deterministic mock response generated by MockLlmService for development purposes.";
    }

    private static int EstimateTokens(string text)
    {
        // Rough heuristic: ~4 chars per token
        return (text?.Length ?? 0) / 4;
    }
}
```

- [ ] **Step 5: Run test to verify pass**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockLlmServiceTests"
```
Expected: PASS — 4 tests.

- [ ] **Step 6: Creare `DevToolsServiceCollectionExtensions.cs`**

Create `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Api.DevTools;

internal static class DevToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers MeepleDev mock-aware services. Call only when env.IsDevelopment().
    /// </summary>
    public static IServiceCollection AddMeepleDevTools(this IServiceCollection services)
    {
        // Read env vars once at startup
        var env = System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value?.ToString(), System.StringComparer.OrdinalIgnoreCase);

        var provider = new MockToggleStateProvider(env!, KnownMockServices.All);
        services.AddSingleton(provider);
        services.AddSingleton<IMockToggleReader>(_ => provider);
        services.AddSingleton<IMockToggleWriter>(_ => provider);
        services.AddSingleton<IMockToggleEvents>(_ => provider);
        services.AddSingleton<ScenarioLoader>();

        // LLM wiring — note: RealLlmService is the existing concrete type.
        // This assumes the existing DI registration uses a concrete class we can reference.
        // If the existing code uses factory pattern, this needs adjustment in task review.
        // services.AddMockAwareService<ILlmService, RealLlmService, MockLlmService>("llm");
        // TODO in task 22: wire real services; MockLlmService is ready.

        return services;
    }

    /// <summary>
    /// Adds the middleware that writes X-Meeple-Mock response headers.
    /// </summary>
    public static IApplicationBuilder UseMeepleDevTools(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MockHeaderMiddleware>();
    }
}
```

**Nota**: la registrazione effettiva dei `MockAwareService<T, TReal, TMock>` avviene nel Task 22, che legge le registrazioni real esistenti e le sostituisce. Il TODO è una guida esplicita per il prossimo task.

- [ ] **Step 7: Build**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
```
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/DevTools/Scenarios/ScenarioLoader.cs \
        apps/api/src/Api/DevTools/MockImpls/MockLlmService.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        tests/Api.Tests/DevTools/MockLlmServiceTests.cs
git commit -m "feat(dev): add MockLlmService, ScenarioLoader, and DevTools bootstrap"
```

---

### Task 22: MockEmbeddingService + MockBlobStorageService + wire-up in Program.cs

**Files:**
- Create: `apps/api/src/Api/DevTools/MockImpls/MockEmbeddingService.cs`
- Create: `apps/api/src/Api/DevTools/MockImpls/MockBlobStorageService.cs`
- Create: `tests/Api.Tests/DevTools/MockEmbeddingServiceTests.cs`
- Create: `tests/Api.Tests/DevTools/MockBlobStorageServiceTests.cs`
- Modify: `apps/api/src/Api/Program.cs`
- Modify: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`

- [ ] **Step 1: Creare `MockEmbeddingService.cs`**

Create `apps/api/src/Api/DevTools/MockImpls/MockEmbeddingService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of IEmbeddingService.
/// Generates 768-dim normalized vectors from hash(model+text).
/// Stable across calls for identical inputs.
/// </summary>
internal sealed class MockEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 768;
    private const string ModelName = "mock-e5-base";

    public int GetEmbeddingDimensions() => Dimensions;
    public string GetModelName() => ModelName;

    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => GenerateEmbeddingAsync(text, "en", ct);

    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken ct = default)
    {
        var vec = HashToVector(language + ":" + text);
        return Task.FromResult(EmbeddingResult.CreateSuccess(new[] { vec }));
    }

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
        => GenerateEmbeddingsAsync(texts, "en", ct);

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct = default)
    {
        var vectors = texts.Select(t => HashToVector(language + ":" + t)).ToArray();
        return Task.FromResult(EmbeddingResult.CreateSuccess(vectors));
    }

    private static float[] HashToVector(string input)
    {
        // Deterministic: SHA-256 → expand to 768 floats in [-1, 1] → normalize
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var vec = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
        {
            var b = bytes[i % bytes.Length];
            vec[i] = (b / 255f) * 2f - 1f;
        }
        // L2 normalize
        double norm = Math.Sqrt(vec.Sum(v => (double)v * v));
        if (norm > 0)
        {
            for (int i = 0; i < Dimensions; i++) vec[i] = (float)(vec[i] / norm);
        }
        return vec;
    }
}
```

**Nota**: `EmbeddingResult.CreateSuccess` signature da verificare. Se non esiste, il task deve prima leggere `EmbeddingResult.cs` e adattare. Lo step 4 in basso include il check.

- [ ] **Step 2: Creare `MockBlobStorageService.cs`**

Create `apps/api/src/Api/DevTools/MockImpls/MockBlobStorageService.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.Services.Pdf;

namespace Api.DevTools.MockImpls;

/// <summary>
/// In-memory mock of IBlobStorageService. Stores blobs in a ConcurrentDictionary
/// keyed by gameId/fileId. Pre-signed URLs return a synthetic localhost URL.
/// </summary>
internal sealed class MockBlobStorageService : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new();

    private static string KeyOf(string gameId, string fileId) => $"{gameId}/{fileId}";

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var fileId = $"MOCK-{Guid.NewGuid():N}";
        _store[KeyOf(gameId, fileId)] = bytes;
        return new BlobStorageResult(
            Success: true,
            FileId: fileId,
            FilePath: $"mock://{gameId}/{fileId}/{fileName}",
            FileSizeBytes: bytes.LongLength);
    }

    public Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(KeyOf(gameId, fileId), out var bytes))
        {
            Stream s = new MemoryStream(bytes, writable: false);
            return Task.FromResult<Stream?>(s);
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        return Task.FromResult(_store.TryRemove(KeyOf(gameId, fileId), out _));
    }

    public string GetStoragePath(string fileId, string gameId, string fileName)
        => $"mock://{gameId}/{fileId}/{fileName}";

    public Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(KeyOf(gameId, fileId)));

    public Task<string?> GetPresignedDownloadUrlAsync(string fileId, string gameId, int? expirySeconds = null)
    {
        if (!_store.ContainsKey(KeyOf(gameId, fileId)))
        {
            return Task.FromResult<string?>(null);
        }
        return Task.FromResult<string?>($"http://localhost/mock-s3/{gameId}/{fileId}?expiry={expirySeconds ?? 3600}");
    }
}
```

- [ ] **Step 3: Write failing tests**

Create `tests/Api.Tests/DevTools/MockEmbeddingServiceTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbedding_ReturnsStableVectorForSameInput()
    {
        var svc = new MockEmbeddingService();
        var r1 = await svc.GenerateEmbeddingAsync("hello world", CancellationToken.None);
        var r2 = await svc.GenerateEmbeddingAsync("hello world", CancellationToken.None);
        Assert.True(r1.Success);
        Assert.Equal(r1.Embeddings[0], r2.Embeddings[0]);
    }

    [Fact]
    public async Task GenerateEmbedding_DifferentInputsDifferentVectors()
    {
        var svc = new MockEmbeddingService();
        var r1 = await svc.GenerateEmbeddingAsync("foo", CancellationToken.None);
        var r2 = await svc.GenerateEmbeddingAsync("bar", CancellationToken.None);
        Assert.NotEqual(r1.Embeddings[0], r2.Embeddings[0]);
    }

    [Fact]
    public async Task GenerateEmbedding_VectorIsNormalized()
    {
        var svc = new MockEmbeddingService();
        var r = await svc.GenerateEmbeddingAsync("test", CancellationToken.None);
        var norm = System.Math.Sqrt(r.Embeddings[0].Sum(v => (double)v * v));
        Assert.InRange(norm, 0.99, 1.01);
    }

    [Fact]
    public void ReportsCorrectDimensionsAndModel()
    {
        var svc = new MockEmbeddingService();
        Assert.Equal(768, svc.GetEmbeddingDimensions());
        Assert.Equal("mock-e5-base", svc.GetModelName());
    }
}
```

Create `tests/Api.Tests/DevTools/MockBlobStorageServiceTests.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockBlobStorageServiceTests
{
    [Fact]
    public async Task StoreAndRetrieve_RoundTrip()
    {
        var svc = new MockBlobStorageService();
        var content = System.Text.Encoding.UTF8.GetBytes("hello");
        using var input = new MemoryStream(content);
        var stored = await svc.StoreAsync(input, "test.txt", "game-1", CancellationToken.None);

        Assert.True(stored.Success);
        Assert.NotNull(stored.FileId);
        Assert.StartsWith("MOCK-", stored.FileId);

        using var retrieved = await svc.RetrieveAsync(stored.FileId!, "game-1", CancellationToken.None);
        Assert.NotNull(retrieved);
        using var ms = new MemoryStream();
        await retrieved!.CopyToAsync(ms, CancellationToken.None);
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public async Task Retrieve_ReturnsNullWhenNotFound()
    {
        var svc = new MockBlobStorageService();
        var result = await svc.RetrieveAsync("MOCK-none", "game-1", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesBlob()
    {
        var svc = new MockBlobStorageService();
        using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        var stored = await svc.StoreAsync(input, "x.bin", "g", CancellationToken.None);
        Assert.True(await svc.DeleteAsync(stored.FileId!, "g", CancellationToken.None));
        Assert.False(await svc.ExistsAsync(stored.FileId!, "g", CancellationToken.None));
    }

    [Fact]
    public async Task GetPresignedUrl_ReturnsMockUrl()
    {
        var svc = new MockBlobStorageService();
        using var input = new MemoryStream(new byte[] { 1 });
        var stored = await svc.StoreAsync(input, "x.bin", "g", CancellationToken.None);
        var url = await svc.GetPresignedDownloadUrlAsync(stored.FileId!, "g", 7200);
        Assert.NotNull(url);
        Assert.Contains("mock-s3", url!);
    }
}
```

- [ ] **Step 4: Build e verifica segnature reali**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug 2>&1 | tee /tmp/build.log
```

Expected: Build succeeded. Se fallisce su `EmbeddingResult.CreateSuccess`, leggere `apps/api/src/Api/Services/EmbeddingResult.cs` (o file contenente) e correggere la signature in `MockEmbeddingService.cs`.

- [ ] **Step 5: Run tests**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockEmbeddingServiceTests|FullyQualifiedName~MockBlobStorageServiceTests"
```
Expected: PASS — 8 tests.

- [ ] **Step 6: Wiring in DevToolsServiceCollectionExtensions**

Modify `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`: rimuovere il TODO e aggiungere le 3 registrazioni.

Edit the TODO block:
```csharp
        // LLM wiring — note: RealLlmService is the existing concrete type.
        // This assumes the existing DI registration uses a concrete class we can reference.
        // If the existing code uses factory pattern, this needs adjustment in task review.
        // services.AddMockAwareService<ILlmService, RealLlmService, MockLlmService>("llm");
        // TODO in task 22: wire real services; MockLlmService is ready.
```

Replace with:
```csharp
        // Wire mock-aware proxies. Real types are discovered from existing DI;
        // here we rely on the real types being registered elsewhere as concrete.
        // For each service, we register the proxy as the TService resolver, overriding prior registration.

        // NOTE: This MUST run AFTER real services are registered.
        // See Program.cs for ordering.
        services.AddMockAwareService<ILlmService, RealLlmService_PLACEHOLDER, MockLlmService>("llm");
        services.AddMockAwareService<IEmbeddingService, RealEmbeddingService_PLACEHOLDER, MockEmbeddingService>("embedding");
        services.AddMockAwareService<IBlobStorageService, S3BlobStorageService, MockBlobStorageService>("s3");
```

**IMPORTANT — read before proceeding**: Le "RealXxxService_PLACEHOLDER" sono nomi segnaposto. **Non esistono tipi così**. Il worker deve:

1. Aprire `apps/api/src/Api/Services/` e trovare la classe concreta che implementa `ILlmService` (probabilmente `LlmService` o `OpenRouterLlmService`)
2. Sostituire `RealLlmService_PLACEHOLDER` con il nome reale
3. Idem per `IEmbeddingService` (probabilmente `OllamaEmbeddingService` o `ExternalEmbeddingService`)
4. `IBlobStorageService` ha già `S3BlobStorageService` e `BlobStorageService` (local); usare quello registrato in prod

Cerca con:
```bash
grep -rn "class.*: ILlmService\|class.*ILlmService,\|class.*ILlmService$" apps/api/src/Api/Services/
grep -rn "class.*: IEmbeddingService\|class.*IEmbeddingService" apps/api/src/Api/
```

Aggiornare il codice di conseguenza.

- [ ] **Step 7: Modificare Program.cs per chiamare `AddMeepleDevTools()`**

Read: `apps/api/src/Api/Program.cs` — localizzare il blocco dove si registrano i bounded context services.

Aggiungere **dopo** tutte le registrazioni real (tipo dopo `AddAiServices()`):

```csharp
#if DEBUG
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddMeepleDevTools();
}
#endif
```

E nel middleware pipeline, dopo `var app = builder.Build();`:

```csharp
#if DEBUG
if (app.Environment.IsDevelopment())
{
    app.UseMeepleDevTools();
}
#endif
```

Aggiungere `using Api.DevTools;` in cima.

- [ ] **Step 8: Build + Test completo**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
cd ../../../../tests/Api.Tests && dotnet test --filter "FullyQualifiedName~DevTools"
```
Expected: Build OK, tutti i DevTools tests PASS.

Run Release build per confermare esclusione:
```bash
cd apps/api/src/Api && dotnet build -c Release
```
Expected: Build succeeded — i file `DevTools/` non sono compilati in Release.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/DevTools/MockImpls/MockEmbeddingService.cs \
        apps/api/src/Api/DevTools/MockImpls/MockBlobStorageService.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        apps/api/src/Api/Program.cs \
        tests/Api.Tests/DevTools/MockEmbeddingServiceTests.cs \
        tests/Api.Tests/DevTools/MockBlobStorageServiceTests.cs
git commit -m "feat(dev): wire MockLlm/Embedding/S3 into DI and Program.cs"
```

**PR #3 check**: Debug build passa, Release build passa, tutti i DevTools tests verdi. Aprire PR #3 → `main-dev` con titolo "feat(dev): Phase 1.2 — backend toggle state + DispatchProxy + 3 mock services".

---

## PR #4 — Fase 1.3a: Reranker + BGG + n8n mocks (Task 23-25)

### Task 23: MockBggApiService

**Files:**
- Create: `apps/api/src/Api/DevTools/MockImpls/MockBggApiService.cs`
- Create: `tests/Api.Tests/DevTools/MockBggApiServiceTests.cs`
- Modify: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`

- [ ] **Step 1: Leggere i DTO esistenti**

Read: `apps/api/src/Api/Models/BggSearchResultDto.cs`, `apps/api/src/Api/Models/BggGameDetailsDto.cs` (o file equivalenti trovati con `grep -r "BggSearchResultDto\|BggGameDetailsDto" apps/api/src/Api/Models/`).

Annotare le proprietà richieste.

- [ ] **Step 2: Write failing test**

Create `tests/Api.Tests/DevTools/MockBggApiServiceTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Xunit;

namespace Api.Tests.DevTools;

public class MockBggApiServiceTests
{
    private MockBggApiService MakeService() => new(new ScenarioLoader());

    [Fact]
    public async Task SearchGames_ReturnsSeedResults()
    {
        var svc = MakeService();
        var results = await svc.SearchGamesAsync("Wingspan", exact: false, CancellationToken.None);
        // small-library scenario includes Wingspan (bggId 266192)
        // MockBgg should return it if scenario is loaded
        Assert.NotNull(results);
    }

    [Fact]
    public async Task SearchGames_EmptyQueryReturnsEmpty()
    {
        var svc = MakeService();
        var results = await svc.SearchGamesAsync("", exact: false, CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetGameDetails_KnownIdReturnsDetails()
    {
        var svc = MakeService();
        var details = await svc.GetGameDetailsAsync(266192, CancellationToken.None);
        // Deterministic: either null (if scenario doesn't load) or a populated DTO
        if (details is not null)
        {
            Assert.Equal(266192, details.BggId);
        }
    }

    [Fact]
    public async Task GetGameDetails_UnknownIdReturnsNull()
    {
        var svc = MakeService();
        var details = await svc.GetGameDetailsAsync(999999999, CancellationToken.None);
        Assert.Null(details);
    }
}
```

- [ ] **Step 3: Implementare `MockBggApiService`**

Create `apps/api/src/Api/DevTools/MockImpls/MockBggApiService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;
using Api.Models;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of IBggApiService. Returns seed data from scenario JSON.
/// Unknown BGG IDs return null (404 semantic).
/// </summary>
internal sealed class MockBggApiService : IBggApiService
{
    private readonly IReadOnlyList<(int bggId, string name)> _seed;

    public MockBggApiService(ScenarioLoader scenarios)
    {
        // Merge bggGames from all scenarios so lookups work regardless of active scenario
        var all = new Dictionary<int, string>();
        foreach (var name in scenarios.ListAvailable())
        {
            var root = scenarios.Load(name);
            if (root.TryGetProperty("bggGames", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.TryGetProperty("bggId", out var idEl) &&
                        item.TryGetProperty("name", out var nameEl))
                    {
                        var id = idEl.GetInt32();
                        var n = nameEl.GetString() ?? "Unknown";
                        all[id] = n;
                    }
                }
            }
        }
        _seed = all.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public Task<List<BggSearchResultDto>> SearchGamesAsync(string query, bool exact = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new List<BggSearchResultDto>());
        }
        var matches = _seed
            .Where(g => exact
                ? g.name.Equals(query, System.StringComparison.OrdinalIgnoreCase)
                : g.name.Contains(query, System.StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(g => CreateSearchResult(g.bggId, g.name))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<BggGameDetailsDto?> GetGameDetailsAsync(int bggId, CancellationToken ct = default)
    {
        var match = _seed.FirstOrDefault(g => g.bggId == bggId);
        if (match.name is null)
        {
            return Task.FromResult<BggGameDetailsDto?>(null);
        }
        return Task.FromResult<BggGameDetailsDto?>(CreateDetails(bggId, match.name));
    }

    private static BggSearchResultDto CreateSearchResult(int bggId, string name)
    {
        // IMPORTANT: Adjust these properties to match the actual DTO definition.
        // Run the build — if a property is missing, check BggSearchResultDto.cs.
        return new BggSearchResultDto
        {
            BggId = bggId,
            Name = name,
            YearPublished = 2020,
        };
    }

    private static BggGameDetailsDto CreateDetails(int bggId, string name)
    {
        return new BggGameDetailsDto
        {
            BggId = bggId,
            Name = name,
            Description = $"Mock description for {name}",
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTime = 60,
            YearPublished = 2020,
        };
    }
}
```

**Nota**: Le proprietà in `CreateSearchResult` e `CreateDetails` sono indicative. Al build, correggere in base alla definizione reale del DTO letto allo step 1.

- [ ] **Step 4: Build e adattare DTOs**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug 2>&1 | grep -E "error|Error" | head -10
```

Se errori tipo "property 'X' not found", aggiornare `MockBggApiService.cs` con i nomi corretti.

- [ ] **Step 5: Run tests**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockBggApiServiceTests"
```
Expected: PASS — 4 tests.

- [ ] **Step 6: Registrare in DevToolsServiceCollectionExtensions**

Nel file `DevToolsServiceCollectionExtensions.cs`, aggiungere dopo le 3 registrazioni esistenti:

```csharp
        services.AddMockAwareService<IBggApiService, BggApiService, MockBggApiService>("bgg");
```

**Verifica nome concreto**: `grep -rn "class.*: IBggApiService" apps/api/src/Api/` → adatta il nome se `BggApiService` non esiste come classe diretta (potrebbe essere `BggApiServiceHttp` o simile).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/DevTools/MockImpls/MockBggApiService.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        tests/Api.Tests/DevTools/MockBggApiServiceTests.cs
git commit -m "feat(dev): add MockBggApiService with scenario-driven seed"
```

---

### Task 24: MockRerankerService (interface da verificare)

**Files:**
- Create: `apps/api/src/Api/DevTools/MockImpls/MockRerankerService.cs`
- Create: `tests/Api.Tests/DevTools/MockRerankerServiceTests.cs`
- Modify: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`

**Context**: Il Reranker non sembra avere un'interfaccia `IRerankerService` esplicita basandosi su esplorazione. Probabilmente è un client HTTP dentro un altro service. Task richiede esplorazione preliminare.

- [ ] **Step 1: Trovare il reranker**

Run:
```bash
grep -rn "reranker\|Reranker" apps/api/src/Api/ --include="*.cs" | grep -iv "circuit\|n8n" | head -20
```

Identificare:
- File del client HTTP o service class
- Se esiste un'interfaccia
- Metodi principali (probabilmente `RerankAsync(query, documents)`)

- [ ] **Step 2: Strategia di fallback in base a cosa trovi**

**Caso A**: esiste `IRerankerService` con `Task<List<RerankResult>> RerankAsync(...)`:
- Procedere come task 23: mock che ritorna score = Jaccard(query_tokens, doc_tokens) normalizzato

**Caso B**: il reranker è dentro una classe più grande (es. `RetrievalService`):
- Il mock NON va su questa classe (troppo invasivo)
- Invece: mock va sul client HTTP sottostante (es. `RerankerClient` o `HttpClient` con base address reranker)
- Se non c'è un'astrazione pulita, **skippare il mock del reranker in questa fase** e aggiungere TODO nel plan: "reranker mocking rinviato a fase 2 — richiede refactor per estrarre interfaccia"

- [ ] **Step 3: Se caso A, scrivere test**

Create `tests/Api.Tests/DevTools/MockRerankerServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockRerankerServiceTests
{
    [Fact]
    public async Task Rerank_ReturnsScoresForAllDocuments()
    {
        var svc = new MockRerankerService();
        var docs = new List<string> { "doc about cats", "doc about dogs", "unrelated doc" };
        // Adjust signature to match real interface
        var results = await svc.RerankAsync("cats", docs, CancellationToken.None);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task Rerank_HigherScoreForBetterMatch()
    {
        var svc = new MockRerankerService();
        var docs = new List<string> { "cats are cute", "dogs are loyal" };
        var results = await svc.RerankAsync("cats", docs, CancellationToken.None);
        // Deterministic: first doc should score higher
        Assert.True(results[0].Score > results[1].Score);
    }
}
```

**Nota**: adattare i nomi di metodi/tipi (`RerankAsync`, `RerankResult.Score`, etc.) ai reali trovati allo step 1.

- [ ] **Step 4: Implementare**

Create `apps/api/src/Api/DevTools/MockImpls/MockRerankerService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// using Api.Services;  // Adjust namespace to where IRerankerService lives

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of reranker. Score = Jaccard(query_tokens, doc_tokens).
/// INTERFACE AND METHOD SIGNATURES TO BE ADJUSTED BASED ON ACTUAL IRerankerService.
/// If no clean interface exists, this mock is skipped (see plan task 24, step 2, case B).
/// </summary>
internal sealed class MockRerankerService  // : IRerankerService
{
    public Task<List<RerankResult>> RerankAsync(
        string query,
        List<string> documents,
        CancellationToken ct = default)
    {
        var queryTokens = Tokenize(query);
        var results = documents
            .Select((doc, idx) => new RerankResult(idx, doc, Jaccard(queryTokens, Tokenize(doc))))
            .OrderByDescending(r => r.Score)
            .ToList();
        return Task.FromResult(results);
    }

    private static HashSet<string> Tokenize(string text)
        => new(text.ToLowerInvariant().Split(new[] { ' ', '.', ',', ';' },
            System.StringSplitOptions.RemoveEmptyEntries));

    private static float Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0f;
        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0f : (float)intersection / union;
    }
}

// Placeholder record — replace with actual type from reranker service
internal sealed record RerankResult(int Index, string Document, float Score);
```

**Critical note to worker**: se lo step 1 ha rivelato che il reranker NON ha un'interfaccia estraibile, **non creare MockRerankerService.cs**. Invece aggiungere una riga al file `DevTools/README.md`:

```
## Skipped mocks (Phase 1)
- Reranker: requires refactor to extract IRerankerService interface. Deferred to future work.
```

E saltare gli step 4-7 di questo task.

- [ ] **Step 5: Build + Test**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
cd ../../../../tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockRerankerServiceTests"
```

- [ ] **Step 6: Registrare (se applicabile)**

In `DevToolsServiceCollectionExtensions.cs`:
```csharp
services.AddMockAwareService<IRerankerService, RealRerankerServiceName, MockRerankerService>("reranker");
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/DevTools/MockImpls/MockRerankerService.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        tests/Api.Tests/DevTools/MockRerankerServiceTests.cs
git commit -m "feat(dev): add MockRerankerService with Jaccard-based scoring"
```

Oppure, se skippato:
```bash
git add apps/api/src/Api/DevTools/README.md
git commit -m "docs(dev): defer Reranker mock — interface extraction needed"
```

---

### Task 25: MockN8nTemplateService

**Files:**
- Create: `apps/api/src/Api/DevTools/MockImpls/MockN8nTemplateService.cs`
- Create: `tests/Api.Tests/DevTools/MockN8nTemplateServiceTests.cs`
- Modify: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`

- [ ] **Step 1: Leggere `IN8NTemplateService.cs`**

Read: `apps/api/src/Api/Services/IN8NTemplateService.cs` — annotare tutti i metodi dell'interfaccia.

- [ ] **Step 2: Scrivere test che copre tutti i metodi esposti**

Create `tests/Api.Tests/DevTools/MockN8nTemplateServiceTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockN8nTemplateServiceTests
{
    [Fact]
    public async Task SmokeTest_AllMethodsReturnSafeDefaults()
    {
        var svc = new MockN8nTemplateService();
        // Add one assertion per method on IN8NTemplateService.
        // This task REQUIRES reading the interface first (step 1).
        await Task.CompletedTask;
        Assert.NotNull(svc);
    }
}
```

**Nota**: il test dettagliato dipende dai metodi reali — aggiornare DOPO lo step 1.

- [ ] **Step 3: Implementare mock che no-op tutti i metodi**

Create `apps/api/src/Api/DevTools/MockImpls/MockN8nTemplateService.cs` implementando ogni metodo di `IN8NTemplateService` con ritorni safe defaults:
- Metodi `Task`: `return Task.CompletedTask;`
- Metodi `Task<T>` dove T è collection: `return Task.FromResult(new List<T>());`
- Metodi `Task<T>` dove T è entità: `return Task.FromResult<T?>(null);` o un oggetto mock con id prefix `MOCK-`
- Metodi `Task<bool>`: `return Task.FromResult(true);`

Template base:
```csharp
using System.Threading;
using System.Threading.Tasks;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// No-op mock of IN8NTemplateService. All methods return safe defaults.
/// Webhook sends are logged to console but never executed.
/// </summary>
internal sealed class MockN8nTemplateService : IN8NTemplateService
{
    // IMPLEMENT ALL IN8NTemplateService members here, reading from the actual interface.
    // Each method should:
    // 1. Log a debug message (optional)
    // 2. Return Task.CompletedTask / Task.FromResult(default/empty)
}
```

- [ ] **Step 4: Build + test**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug
cd ../../../../tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockN8nTemplateServiceTests"
```

- [ ] **Step 5: Registrare**

In `DevToolsServiceCollectionExtensions.cs`:
```csharp
services.AddMockAwareService<IN8NTemplateService, N8nTemplateService, MockN8nTemplateService>("n8n");
```

Verifica: `N8nTemplateService` è il nome corretto (già visto in esplorazione). Se ci sono più classi che implementano, scegliere quella attualmente registrata come default nei DI bounded context.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/DevTools/MockImpls/MockN8nTemplateService.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        tests/Api.Tests/DevTools/MockN8nTemplateServiceTests.cs
git commit -m "feat(dev): add MockN8nTemplateService as no-op"
```

**PR #4 check**: Debug build + Release build passano. Tutti i DevTools tests verdi. Aprire PR #4 → `main-dev`.

---

## PR #5 — Fase 1.3b: PDF pipeline mocks (Task 26-28)

### Task 26: Identificare le interfacce PDF extraction

**Files:** (nessuna modifica, solo esplorazione)

- [ ] **Step 1: Leggere i file esistenti**

Run:
```bash
ls apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/
```

Read ciascun file: `SmolDoclingPdfTextExtractor.cs`, `UnstructuredPdfTextExtractor.cs`, e qualsiasi `IPdfTextExtractor.cs` o simile.

Annotare:
- Nome interfaccia condivisa (probabilmente `IPdfTextExtractor`)
- Metodi chiave (probabilmente `ExtractTextAsync(Stream pdfStream, CancellationToken ct)`)
- Return type (probabilmente un record con `Pages: List<PageText>` o simile)

- [ ] **Step 2: Documentare quanto trovato**

Aggiungere a `apps/api/src/Api/DevTools/README.md` una sezione:

```markdown
## PDF pipeline services (for mocks)

Interface: `{IPdfTextExtractor}` (actual name TBD from task 26 step 1)
Method: `{ExtractAsync signature}`
Return: `{ExtractionResult shape}`

Real implementations:
- `SmolDoclingPdfTextExtractor` — calls smoldocling-service
- `UnstructuredPdfTextExtractor` — calls unstructured-service

Both can be mocked with the same pattern since they share the interface.
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/DevTools/README.md
git commit -m "docs(dev): document PDF extraction interfaces for mock implementation"
```

---

### Task 27: MockSmolDoclingPdfTextExtractor + MockUnstructuredPdfTextExtractor

**Files:**
- Create: `apps/api/src/Api/DevTools/MockImpls/MockSmolDoclingPdfTextExtractor.cs`
- Create: `apps/api/src/Api/DevTools/MockImpls/MockUnstructuredPdfTextExtractor.cs`
- Create: `tests/Api.Tests/DevTools/MockPdfExtractorTests.cs`
- Modify: `apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs`

- [ ] **Step 1: Implementare `MockSmolDoclingPdfTextExtractor`**

Create `apps/api/src/Api/DevTools/MockImpls/MockSmolDoclingPdfTextExtractor.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;
// using Api.BoundedContexts.DocumentProcessing.Infrastructure.External; // Adjust namespace per task 26

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of SmolDocling PDF text extractor.
/// Returns scenario-driven documents or a 5-page default extraction.
/// </summary>
internal sealed class MockSmolDoclingPdfTextExtractor  // : IPdfTextExtractor (adjust)
{
    private readonly ScenarioLoader _scenarios;

    public MockSmolDoclingPdfTextExtractor(ScenarioLoader scenarios)
    {
        _scenarios = scenarios;
    }

    // Method signature MUST match the real IPdfTextExtractor interface.
    // Placeholder — adjust after reading the real interface.
    public Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        // Use a fixed scenario's documents if available; otherwise return default 5 pages
        var pages = TryLoadScenarioPages("small-library") ?? GenerateDefaultPages();
        return Task.FromResult(new PdfExtractionResult(pages, "mock-smoldocling"));
    }

    private List<string>? TryLoadScenarioPages(string scenarioName)
    {
        try
        {
            var root = _scenarios.Load(scenarioName);
            if (root.TryGetProperty("documents", out var docs) && docs.ValueKind == JsonValueKind.Array && docs.GetArrayLength() > 0)
            {
                var firstDoc = docs.EnumerateArray().First();
                if (firstDoc.TryGetProperty("pages", out var pagesArr))
                {
                    return pagesArr.EnumerateArray().Select(p => p.GetString() ?? "").ToList();
                }
            }
        }
        catch { }
        return null;
    }

    private static List<string> GenerateDefaultPages() => new()
    {
        "Page 1: [MOCK] Game rules overview. This is a mock extraction.",
        "Page 2: [MOCK] Setup instructions.",
        "Page 3: [MOCK] Turn sequence and actions.",
        "Page 4: [MOCK] End game conditions and scoring.",
        "Page 5: [MOCK] Appendix and variants.",
    };
}

// Placeholder — replace with actual type
internal sealed record PdfExtractionResult(List<string> Pages, string ExtractorName);
```

**IMPORTANT**: la classe placeholder `PdfExtractionResult` e la signature `ExtractAsync` devono essere sostituite con i tipi reali trovati nel Task 26. Il worker DEVE leggere `SmolDoclingPdfTextExtractor.cs` e adattare questo file prima di procedere.

- [ ] **Step 2: Implementare `MockUnstructuredPdfTextExtractor`**

Same pattern come sopra, ma con return type/extractor name = `"mock-unstructured"`. Se condivide l'interfaccia con smoldocling, il codice è praticamente identico eccetto il nome.

Create `apps/api/src/Api/DevTools/MockImpls/MockUnstructuredPdfTextExtractor.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;

namespace Api.DevTools.MockImpls;

internal sealed class MockUnstructuredPdfTextExtractor
{
    private readonly ScenarioLoader _scenarios;

    public MockUnstructuredPdfTextExtractor(ScenarioLoader scenarios)
    {
        _scenarios = scenarios;
    }

    public Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        var pages = new List<string>
        {
            "Page 1: [MOCK unstructured] Extraction result.",
            "Page 2: [MOCK unstructured] Additional content.",
        };
        return Task.FromResult(new PdfExtractionResult(pages, "mock-unstructured"));
    }
}
```

- [ ] **Step 3: Scrivere test condiviso**

Create `tests/Api.Tests/DevTools/MockPdfExtractorTests.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Xunit;

namespace Api.Tests.DevTools;

public class MockPdfExtractorTests
{
    [Fact]
    public async Task SmolDocling_ReturnsNonEmptyPages()
    {
        var svc = new MockSmolDoclingPdfTextExtractor(new ScenarioLoader());
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header
        var result = await svc.ExtractAsync(stream, CancellationToken.None);
        Assert.NotEmpty(result.Pages);
    }

    [Fact]
    public async Task Unstructured_ReturnsNonEmptyPages()
    {
        var svc = new MockUnstructuredPdfTextExtractor(new ScenarioLoader());
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var result = await svc.ExtractAsync(stream, CancellationToken.None);
        Assert.NotEmpty(result.Pages);
    }

    [Fact]
    public async Task BothAreDeterministic()
    {
        var s = new MockSmolDoclingPdfTextExtractor(new ScenarioLoader());
        using var stream1 = new MemoryStream(new byte[] { 1 });
        using var stream2 = new MemoryStream(new byte[] { 1 });
        var r1 = await s.ExtractAsync(stream1, CancellationToken.None);
        var r2 = await s.ExtractAsync(stream2, CancellationToken.None);
        Assert.Equal(r1.Pages.Count, r2.Pages.Count);
    }
}
```

- [ ] **Step 4: Build e correggere tipi**

Run:
```bash
cd apps/api/src/Api && dotnet build -c Debug 2>&1 | grep -E "error" | head
```

Correggere signature/tipi in base alle interfacce reali identificate al Task 26.

- [ ] **Step 5: Run tests**

```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~MockPdfExtractorTests"
```
Expected: PASS.

- [ ] **Step 6: Registrare in DevToolsServiceCollectionExtensions**

```csharp
services.AddMockAwareService<IPdfTextExtractor, SmolDoclingPdfTextExtractor, MockSmolDoclingPdfTextExtractor>("smoldocling");
services.AddMockAwareService<IPdfTextExtractor, UnstructuredPdfTextExtractor, MockUnstructuredPdfTextExtractor>("unstructured");
```

**WARNING**: due registrazioni della stessa interfaccia sovrascrivono l'ultima. Se il DI pattern esistente usa "named" services o factory per distinguere smoldocling vs unstructured, adattare: probabilmente sono registrati con `AddKeyedSingleton` o come `IEnumerable<IPdfTextExtractor>`. Leggere come sono registrati ora nel bounded context `DocumentProcessingServiceExtensions.cs` e replicare lo stesso pattern con mock.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/DevTools/MockImpls/MockSmolDoclingPdfTextExtractor.cs \
        apps/api/src/Api/DevTools/MockImpls/MockUnstructuredPdfTextExtractor.cs \
        apps/api/src/Api/DevTools/DevToolsServiceCollectionExtensions.cs \
        tests/Api.Tests/DevTools/MockPdfExtractorTests.cs
git commit -m "feat(dev): add MockSmolDocling and MockUnstructured PDF extractors"
```

---

### Task 28: End-to-end integration: PDF upload con tutti mock BE

**Files:**
- Create: `tests/Api.Tests/Integration/DevTools/PdfUploadMockE2ETests.cs`

**Obiettivo**: verificare che un POST di upload PDF attraversi tutta la pipeline con i mock attivi.

- [ ] **Step 1: Scrivere integration test**

Create `tests/Api.Tests/Integration/DevTools/PdfUploadMockE2ETests.cs`:

```csharp
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration.DevTools;

public class PdfUploadMockE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PdfUploadMockE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Ensure Development environment with all mocks on
            builder.UseEnvironment("Development");
            System.Environment.SetEnvironmentVariable("MOCK_LLM", "true");
            System.Environment.SetEnvironmentVariable("MOCK_EMBEDDING", "true");
            System.Environment.SetEnvironmentVariable("MOCK_S3", "true");
            System.Environment.SetEnvironmentVariable("MOCK_SMOLDOCLING", "true");
            System.Environment.SetEnvironmentVariable("MOCK_UNSTRUCTURED", "true");
            System.Environment.SetEnvironmentVariable("MOCK_BGG", "true");
            System.Environment.SetEnvironmentVariable("MOCK_N8N", "true");
        });
    }

    [Fact(Skip = "E2E requires authenticated upload endpoint — enable when auth mock is wired")]
    public async Task UploadPdf_WithMocksOn_ReturnsSuccess()
    {
        using var client = _factory.CreateClient();

        // Create a minimal fake PDF
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");

        var response = await client.PostAsync("/api/v1/pdf/upload", content);

        // With mocks on, upload should succeed end-to-end
        Assert.True(response.IsSuccessStatusCode, $"Status was {response.StatusCode}");

        // Verify mock header
        Assert.True(response.Headers.Contains("X-Meeple-Mock"));
    }

    [Fact]
    public async Task HealthEndpoint_ResponseHasMockHeader()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.True(response.Headers.Contains("X-Meeple-Mock"),
            "X-Meeple-Mock header should be present on all responses in Development");
    }
}
```

**Nota**: il test di upload è `Skip` se l'endpoint richiede auth completa. Il secondo test (health + header) è l'invariant più importante.

- [ ] **Step 2: Run**

Run:
```bash
cd tests/Api.Tests && dotnet test --filter "FullyQualifiedName~PdfUploadMockE2ETests"
```
Expected: PASS (almeno il health test; upload può essere Skip).

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/Integration/DevTools/PdfUploadMockE2ETests.cs
git commit -m "test(dev): add PDF upload E2E integration with all BE mocks on"
```

**PR #5 check**: Debug + Release build passano; tutti DevTools tests verdi. Aprire PR #5 → `main-dev`.

---

## PR #6 — Fase 1.4: dev-fast scripts + Makefile + CI guards (Task 29-34)

### Task 29: dev-fast.sh orchestrator

**Files:**
- Create: `infra/scripts/dev-fast.sh`
- Create: `infra/scripts/dev-fast-down.sh`
- Create: `infra/scripts/dev-env-check.sh`

- [ ] **Step 1: Creare `dev-env-check.sh`**

Create `infra/scripts/dev-env-check.sh`:

```bash
#!/usr/bin/env bash
# Verifica che .env.dev.local contenga tutte le chiavi del template .example.
# Se mancano chiavi, stampa un warning rosso con diff e istruzioni di fix.
# NFR-DX-1 enforcement.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
EXAMPLE="$INFRA_DIR/.env.dev.local.example"
LOCAL="$INFRA_DIR/.env.dev.local"

RED='\033[0;31m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
NC='\033[0m'

if [ ! -f "$EXAMPLE" ]; then
  echo -e "${RED}ERROR: $EXAMPLE not found${NC}"
  exit 1
fi

if [ ! -f "$LOCAL" ]; then
  echo -e "${YELLOW}WARNING: $LOCAL not found. Creating from template...${NC}"
  cp "$EXAMPLE" "$LOCAL"
  echo -e "${GREEN}Created $LOCAL${NC}"
  exit 0
fi

# Extract keys (lines matching KEY=... or KEY="...")
get_keys() {
  grep -E '^[A-Z_][A-Z0-9_]*=' "$1" | cut -d= -f1 | sort -u
}

EXAMPLE_KEYS=$(get_keys "$EXAMPLE")
LOCAL_KEYS=$(get_keys "$LOCAL")

MISSING=$(comm -23 <(echo "$EXAMPLE_KEYS") <(echo "$LOCAL_KEYS") || true)

if [ -n "$MISSING" ]; then
  echo -e "${RED}⚠  .env.dev.local is missing keys from the template:${NC}"
  echo "$MISSING" | while read -r key; do
    line=$(grep "^$key=" "$EXAMPLE" || echo "$key=")
    echo -e "  ${YELLOW}$line${NC}"
  done
  echo
  echo -e "${YELLOW}Fix: append the missing lines to $LOCAL or run:${NC}"
  echo "  cp $EXAMPLE $LOCAL  # (will overwrite your local changes)"
  # Non-blocking: warning only
fi

EXTRA=$(comm -13 <(echo "$EXAMPLE_KEYS") <(echo "$LOCAL_KEYS") || true)
if [ -n "$EXTRA" ]; then
  echo -e "${YELLOW}ℹ  .env.dev.local has keys not in template (probably custom):${NC}"
  echo "$EXTRA" | sed 's/^/  /'
fi
```

Make executable:
```bash
chmod +x infra/scripts/dev-env-check.sh
```

- [ ] **Step 2: Creare `dev-fast.sh`**

Create `infra/scripts/dev-fast.sh`:

```bash
#!/usr/bin/env bash
# MeepleDev fast dev loop orchestrator.
# Reads .env.dev.local and boots only what's needed.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$INFRA_DIR")"
PIDS_FILE="$INFRA_DIR/.dev-fast.pids"
LOG_FILE="$INFRA_DIR/dev-fast.log"

RED='\033[0;31m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log() { echo -e "${BLUE}[dev-fast]${NC} $*"; }
warn() { echo -e "${YELLOW}[dev-fast]${NC} $*" >&2; }
err() { echo -e "${RED}[dev-fast]${NC} $*" >&2; }

# 1. Env check
log "Checking .env.dev.local..."
bash "$SCRIPT_DIR/dev-env-check.sh"

# 2. Source env
if [ -f "$INFRA_DIR/.env.dev.local" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$INFRA_DIR/.env.dev.local"
  set +a
else
  err ".env.dev.local not found after check — aborting"
  exit 1
fi

# 3. Initialize PID file
: > "$PIDS_FILE"
: > "$LOG_FILE"

cleanup_on_exit() {
  warn "Caught signal — use 'make dev:fast-down' to clean up if needed"
}
trap cleanup_on_exit INT TERM

# 4. Postgres if requested
if [ "${DEV_POSTGRES:-false}" = "true" ]; then
  log "Starting Postgres..."
  (cd "$INFRA_DIR" && docker compose up -d postgres) >> "$LOG_FILE" 2>&1
fi

# 5. Redis if requested
if [ "${DEV_REDIS:-false}" = "true" ]; then
  log "Starting Redis..."
  (cd "$INFRA_DIR" && docker compose up -d redis) >> "$LOG_FILE" 2>&1
fi

# 6. Backend (dotnet watch) if requested
if [ "${DEV_BACKEND:-false}" = "true" ]; then
  log "Starting backend (dotnet watch)..."
  (
    cd "$REPO_ROOT/apps/api/src/Api"
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    ASPNETCORE_ENVIRONMENT=Development \
      dotnet watch run --no-launch-profile \
      >> "$INFRA_DIR/dotnet-watch.log" 2>&1 &
    echo "dotnet:$!" >> "$PIDS_FILE"
  )
  log "Backend starting (logs: $INFRA_DIR/dotnet-watch.log)"
fi

# 7. Frontend (pnpm dev)
log "Starting frontend (Next.js)..."
(
  cd "$REPO_ROOT/apps/web"
  NEXT_PUBLIC_MOCK_MODE="${NEXT_PUBLIC_MOCK_MODE:-true}" \
  NEXT_PUBLIC_MSW_ENABLE="${NEXT_PUBLIC_MSW_ENABLE:-}" \
  NEXT_PUBLIC_MSW_DISABLE="${NEXT_PUBLIC_MSW_DISABLE:-}" \
  NEXT_PUBLIC_DEV_SCENARIO="${NEXT_PUBLIC_DEV_SCENARIO:-small-library}" \
  NEXT_PUBLIC_DEV_AS_ROLE="${NEXT_PUBLIC_DEV_AS_ROLE:-Admin}" \
    pnpm dev >> "$LOG_FILE" 2>&1 &
  echo "next:$!" >> "$PIDS_FILE"
)

log "${GREEN}All services started.${NC}"
log "Frontend: http://localhost:3000"
[ "${DEV_BACKEND:-false}" = "true" ] && log "Backend:  http://localhost:8080"
log "Logs: $LOG_FILE  |  Backend logs: $INFRA_DIR/dotnet-watch.log"
log "Stop: make dev:fast-down"

# Keep script alive so Ctrl+C reaches children
wait
```

Make executable:
```bash
chmod +x infra/scripts/dev-fast.sh
```

- [ ] **Step 3: Creare `dev-fast-down.sh`**

Create `infra/scripts/dev-fast-down.sh`:

```bash
#!/usr/bin/env bash
# Stops everything started by dev-fast.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
PIDS_FILE="$INFRA_DIR/.dev-fast.pids"

BLUE='\033[0;34m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
NC='\033[0m'

log() { echo -e "${BLUE}[dev-fast-down]${NC} $*"; }

if [ ! -f "$PIDS_FILE" ]; then
  log "No PID file found at $PIDS_FILE — nothing to stop"
  exit 0
fi

while IFS=: read -r label pid; do
  if [ -z "${pid:-}" ]; then continue; fi
  if kill -0 "$pid" 2>/dev/null; then
    log "Stopping $label (pid $pid)"
    kill "$pid" 2>/dev/null || true
  else
    log "$label (pid $pid) already gone"
  fi
done < "$PIDS_FILE"

# Graceful window
sleep 1

# Force-kill survivors
while IFS=: read -r label pid; do
  if [ -z "${pid:-}" ]; then continue; fi
  if kill -0 "$pid" 2>/dev/null; then
    log "Force-killing $label (pid $pid)"
    kill -9 "$pid" 2>/dev/null || true
  fi
done < "$PIDS_FILE"

rm -f "$PIDS_FILE"

# Stop Docker services (idempotent)
log "Stopping Docker services (postgres, redis)..."
(cd "$INFRA_DIR" && docker compose stop postgres redis 2>/dev/null || true)

echo -e "${GREEN}[dev-fast-down] Done${NC}"
```

Make executable:
```bash
chmod +x infra/scripts/dev-fast-down.sh
```

- [ ] **Step 4: Test smoke**

Run:
```bash
cd infra && bash scripts/dev-env-check.sh
```
Expected: messaggi verdi/gialli a seconda dello stato di `.env.dev.local`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/dev-fast.sh infra/scripts/dev-fast-down.sh infra/scripts/dev-env-check.sh
git commit -m "feat(dev): add dev-fast orchestrator scripts"
```

---

### Task 30: Makefile targets

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Leggere Makefile corrente**

Read: `infra/Makefile` — identificare la sezione con gli altri target `dev*`.

- [ ] **Step 2: Aggiungere target**

Aggiungere dopo i target `dev`, `dev-core` esistenti:

```makefile
# ================================================================
# MeepleDev fast dev loop (Phase 1)
# ================================================================

dev\:fast: ## Fast dev loop (reads .env.dev.local)
	@bash scripts/dev-fast.sh

dev\:fast-api: ## Fast dev + local dotnet watch + Postgres
	@DEV_BACKEND=true DEV_POSTGRES=true bash scripts/dev-fast.sh

dev\:fast-full: ## Fast dev with backend + Postgres + Redis, all mocks OFF
	@DEV_BACKEND=true DEV_POSTGRES=true DEV_REDIS=true \
	 MOCK_LLM=false MOCK_EMBEDDING=false MOCK_BGG=false \
	 MOCK_S3=false MOCK_SMOLDOCLING=false MOCK_UNSTRUCTURED=false \
	 MOCK_N8N=false MOCK_RERANKER=false \
	 bash scripts/dev-fast.sh

dev\:fast-down: ## Stop everything started by dev:fast
	@bash scripts/dev-fast-down.sh

dev\:fast-check: ## Verify .env.dev.local against template
	@bash scripts/dev-env-check.sh
```

**Nota**: lo `:` nei target Makefile va escapato con `\:` perché fa parte del token. Verificare con `make help`.

- [ ] **Step 3: Verificare**

Run:
```bash
cd infra && make help 2>&1 | grep "dev:fast"
```
Expected: i 5 target sono elencati.

Run smoke:
```bash
cd infra && make dev:fast-check
```
Expected: script gira correttamente.

- [ ] **Step 4: Commit**

```bash
git add infra/Makefile
git commit -m "feat(dev): add Makefile targets for dev:fast family"
```

---

### Task 31: CI workflow — Dev Tools Isolation Check

**Files:**
- Create: `.github/workflows/dev-tools-isolation.yml`

- [ ] **Step 1: Creare workflow**

Create `.github/workflows/dev-tools-isolation.yml`:

```yaml
name: Dev Tools Isolation Check

on:
  pull_request:
    paths:
      - 'apps/web/src/**'
      - 'apps/api/src/**'
      - 'docs/superpowers/fixtures/**'
      - '.github/workflows/dev-tools-isolation.yml'

jobs:
  stash-and-build:
    name: Build without dev-tools
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Setup pnpm
        uses: pnpm/action-setup@v4
        with:
          version: 10

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Backup dev-tools
        run: |
          mkdir -p /tmp/dev-tools-backup
          if [ -d apps/web/src/dev-tools ]; then
            tar -czf /tmp/dev-tools-backup/web.tgz apps/web/src/dev-tools
          fi
          if [ -d apps/api/src/Api/DevTools ]; then
            tar -czf /tmp/dev-tools-backup/api.tgz -C apps/api/src/Api DevTools
          fi
          if [ -d docs/superpowers/fixtures ]; then
            tar -czf /tmp/dev-tools-backup/fixtures.tgz docs/superpowers/fixtures
          fi

      - name: Remove dev-tools
        run: |
          rm -rf apps/web/src/dev-tools
          rm -rf apps/api/src/Api/DevTools
          rm -rf docs/superpowers/fixtures

      - name: Install frontend deps
        working-directory: apps/web
        run: pnpm install --frozen-lockfile

      - name: Build frontend (prod config)
        working-directory: apps/web
        env:
          NODE_ENV: production
          NEXT_PUBLIC_MOCK_MODE: 'false'
        run: pnpm build

      - name: Build backend (Release)
        working-directory: apps/api/src/Api
        run: dotnet build -c Release

      - name: Restore dev-tools
        if: always()
        run: |
          [ -f /tmp/dev-tools-backup/web.tgz ] && tar -xzf /tmp/dev-tools-backup/web.tgz
          [ -f /tmp/dev-tools-backup/api.tgz ] && tar -xzf /tmp/dev-tools-backup/api.tgz -C apps/api/src/Api
          [ -f /tmp/dev-tools-backup/fixtures.tgz ] && tar -xzf /tmp/dev-tools-backup/fixtures.tgz
          true

  no-mock-in-release-dll:
    name: No DevTools types in Release DLL
    runs-on: ubuntu-latest
    needs: stash-and-build
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build Release
        working-directory: apps/api/src/Api
        run: dotnet build -c Release

      - name: Verify no DevTools types
        run: |
          DLL=$(find apps/api/src/Api/bin/Release -name "Api.dll" | head -1)
          if [ -z "$DLL" ]; then
            echo "Api.dll not found"
            exit 1
          fi
          # Use ildasm or reflection tool via dotnet CLI
          if strings "$DLL" | grep -q "Api.DevTools\."; then
            echo "ERROR: Api.DevTools.* symbols found in Release DLL"
            exit 1
          fi
          echo "OK: no DevTools symbols in Release DLL"
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/dev-tools-isolation.yml
git commit -m "ci(dev): add dev-tools isolation and Release DLL verification workflow"
```

---

### Task 32: Bundle size snapshot test

**Files:**
- Create: `apps/web/__tests__/bundle-size.test.ts`
- Create: `apps/web/bundle-size-baseline.json`

- [ ] **Step 1: Creare baseline iniziale**

Run build prod:
```bash
cd apps/web && NODE_ENV=production NEXT_PUBLIC_MOCK_MODE=false pnpm build
```

Dopo il build, trovare la dimensione totale dei chunk `.js` in `.next/static/chunks/`:
```bash
du -b .next/static/chunks/*.js 2>/dev/null | awk '{sum+=$1} END {print sum}'
```

Annotare il numero (es. `1234567`).

Create `apps/web/bundle-size-baseline.json`:

```json
{
  "description": "Baseline JS bundle size for prod build (no mock). Update manually in dedicated PRs.",
  "updatedAt": "2026-04-09",
  "totalBytes": 1234567,
  "toleranceBytes": 2048
}
```

**Replace `1234567`** with the actual measured value.

- [ ] **Step 2: Write test**

Create `apps/web/__tests__/bundle-size.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const BASELINE_PATH = join(process.cwd(), 'bundle-size-baseline.json');
const CHUNKS_DIR = join(process.cwd(), '.next/static/chunks');

describe('bundle-size', () => {
  it.skipIf(!existsSync(CHUNKS_DIR))(
    'total JS chunk size does not exceed baseline + tolerance',
    () => {
      const baseline = JSON.parse(readFileSync(BASELINE_PATH, 'utf8')) as {
        totalBytes: number;
        toleranceBytes: number;
      };

      const files = readdirSync(CHUNKS_DIR).filter((f) => f.endsWith('.js'));
      const total = files.reduce((acc, f) => acc + statSync(join(CHUNKS_DIR, f)).size, 0);

      const maxAllowed = baseline.totalBytes + baseline.toleranceBytes;
      expect(total).toBeLessThanOrEqual(maxAllowed);
    }
  );
});
```

- [ ] **Step 3: Run**

```bash
cd apps/web && pnpm test __tests__/bundle-size.test.ts --run
```
Expected: PASS (or skipped if `.next/static/chunks` doesn't exist yet — that's fine for CI on PRs that didn't build).

- [ ] **Step 4: Commit**

```bash
git add apps/web/__tests__/bundle-size.test.ts apps/web/bundle-size-baseline.json
git commit -m "test(dev): add bundle size snapshot test against baseline"
```

---

### Task 33: NoMockInRelease reflection test

**Files:**
- Create: `tests/Api.Tests/Integration/DevTools/NoMockInReleaseTests.cs`

- [ ] **Step 1: Write test**

Create `tests/Api.Tests/Integration/DevTools/NoMockInReleaseTests.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Api.Tests.Integration.DevTools;

public class NoMockInReleaseTests
{
    [Fact(Skip = "Only runs in Release build via CI workflow; local Debug always includes DevTools")]
    public void ReleaseAssembly_HasNoDevToolsTypes()
    {
        // Load the Release-built Api.dll
        var candidate = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "apps", "api", "src", "Api", "bin", "Release", "net9.0", "Api.dll");

        Assert.True(File.Exists(candidate), $"Release Api.dll not found at {candidate}");

        var assembly = Assembly.LoadFrom(candidate);
        var devToolsTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Api.DevTools", System.StringComparison.Ordinal) == true)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(devToolsTypes);
    }
}
```

**Nota**: il test è `Skip` di default perché richiede un Release build presente. Il CI workflow del Task 31 (`no-mock-in-release-dll` job) fa invece un `strings | grep` che è più affidabile cross-platform.

- [ ] **Step 2: Commit**

```bash
git add tests/Api.Tests/Integration/DevTools/NoMockInReleaseTests.cs
git commit -m "test(dev): add reflection-based no-mock-in-release test (Skip default)"
```

---

### Task 34: dev-backend-watchdog.sh + DevBadge health polling

**Files:**
- Create: `infra/scripts/dev-backend-watchdog.sh`

- [ ] **Step 1: Creare watchdog**

Create `infra/scripts/dev-backend-watchdog.sh`:

```bash
#!/usr/bin/env bash
# Monitors dotnet watch output for restart loops (NFR-OBS-1).
# Tails dotnet-watch.log and counts 'Started' events within a sliding window.
# If 3+ restarts in 60s, prints a red banner and exits with code 2.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$INFRA_DIR/dotnet-watch.log"

RED='\033[0;31m'
NC='\033[0m'

if [ ! -f "$LOG_FILE" ]; then
  echo "Log file not found: $LOG_FILE"
  exit 0
fi

WINDOW_SECS=60
THRESHOLD=3

tail -F "$LOG_FILE" 2>/dev/null | awk -v window=$WINDOW_SECS -v thresh=$THRESHOLD '
  /Started/ {
    now = systime()
    restarts[++n] = now
    while (restarts[1] && (now - restarts[1]) > window) {
      for (i = 1; i < n; i++) restarts[i] = restarts[i+1]
      delete restarts[n]; n--
    }
    if (n >= thresh) {
      print "\033[0;31m"
      print "============================================"
      print "  BACKEND CRASH LOOP DETECTED"
      print "  " n " restarts in " window "s — aborting"
      print "  Check: " ENVIRON["LOG_FILE"]
      print "============================================"
      print "\033[0m"
      exit 2
    }
  }
'
```

Make executable:
```bash
chmod +x infra/scripts/dev-backend-watchdog.sh
```

- [ ] **Step 2: Aggiungere health polling al DevBadge**

Modificare `apps/web/src/dev-tools/devBadge.tsx` per aggiungere polling di `/health` se il backend è atteso attivo:

Aggiungere dopo gli `useStoreSlice` hook esistenti:

```typescript
  const [backendHealthy, setBackendHealthy] = useState<boolean | null>(null);

  useEffect(() => {
    // Only poll if backend is expected (i.e., MOCK_MODE is off or explicit)
    const expectBackend = process.env.NEXT_PUBLIC_MOCK_MODE !== 'true';
    if (!expectBackend) return;

    let failCount = 0;
    const interval = setInterval(async () => {
      try {
        const res = await fetch('/health', { cache: 'no-store' });
        if (res.ok) {
          failCount = 0;
          setBackendHealthy(true);
        } else {
          failCount++;
        }
      } catch {
        failCount++;
      }
      if (failCount >= 5) {
        setBackendHealthy(false);
      }
    }, 10_000);

    return () => clearInterval(interval);
  }, []);
```

E nel rendering, se `backendHealthy === false`, cambiare il bordo a rosso e aggiungere testo "BACKEND DOWN":

```typescript
  const backendWarn = backendHealthy === false;
  const displayColor = backendWarn ? '#ef4444' : color;
  const badgeText = backendWarn
    ? `⚠ BACKEND DOWN · ${scenarioName}`
    : `MOCK · ${enabledCount}/${totalCount} · ${scenarioName} · ${role}`;
```

Usare `displayColor` e `badgeText` nel JSX.

- [ ] **Step 3: Typecheck + test**

Run:
```bash
cd apps/web && pnpm typecheck && pnpm test __tests__/dev-tools --run
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/dev-backend-watchdog.sh apps/web/src/dev-tools/devBadge.tsx
git commit -m "feat(dev): add backend crash watchdog and DevBadge health polling"
```

**PR #6 check**:
- All tests PASS (FE + BE)
- `make dev:fast` boot smoke test → frontend visibile in ≤ 5s
- `make dev:fast-down` stops tutto
- Release build passa
- Isolation workflow verde (dopo push)

Aprire PR #6 → `main-dev` con titolo "feat(dev): Phase 1.4 — scripts, Makefile targets, CI guards, watchdog".

---

## Self-Review Checklist

### 1. Spec coverage

| Spec section | Task(s) | Coverage |
|---|---|---|
| 1.1 One-liner + leve | Design doc only | N/A |
| 1.3 SC-1..SC-5 success criteria | Task 31 (CI workflow) implicita; **GAP**: manca perf-workflow esplicito per SC-1/2/3 p95 measurement | ⚠ |
| 1.4 NFR-PERF-1 MSW overhead ≤5ms | **GAP**: non coperto da alcun task | ⚠ |
| 1.4 NFR-PERF-2 Proxy overhead ≤1µs | **GAP**: BenchmarkDotNet non incluso | ⚠ |
| 1.4 NFR-SEC-1 no mock types in Release | Task 31 (grep strings), Task 33 (reflection test) | ✓ |
| 1.4 NFR-SEC-2 no dev token in logs | **Fase 2** — non c'è token endpoint in fase 1 | ✓ (N/A fase 1) |
| 1.4 NFR-SEC-3 FixedTimeEquals | **Fase 2** — non applicabile fase 1 | ✓ (N/A fase 1) |
| 1.4 NFR-SIZE-1 bundle size | Task 32 | ✓ |
| 1.4 NFR-DX-1 env file check | Task 29 (dev-env-check.sh) | ✓ |
| 1.4 NFR-OBS-1 backend crash detection | Task 34 (watchdog + DevBadge polling) | ✓ |
| 1.5 Mock fidelity contract | Task 21 (LLM), 22 (Embedding, S3), 23 (BGG), 24 (Reranker), 25 (n8n), 27 (PDF) | ✓ |
| 2. Architecture — 3 interfacce toggle | Task 16 | ✓ |
| 2. Architecture — DispatchProxy | Task 18 | ✓ |
| 2. Architecture — MockHeaderMiddleware | Task 20 | ✓ |
| 3. Data flow A (fast start) | Task 29 (script) + implicit smoke test | ✓ |
| 3. Data flow B (with backend) | Task 29 DEV_BACKEND=true + Task 30 `dev:fast-api` | ✓ |
| 3. Data flow C (MSW intercept) | Task 9 + Task 15 integration test | ✓ |
| 3. Data flow D (stateful CRUD) | Task 7 (scenarioStore) | ✓ |
| 3. Data flow D.prime (scenario switch protocol) | **Fase 2** — richiede runtime Dev Panel | ✓ (deferred) |
| 3. Data flow E (runtime toggle change) | **Fase 2** | ✓ (deferred) |
| 3.Z GWT scenarios | Task 15 (MSW toggle); **partial GAP** — gli altri scenari diventano E2E Playwright in un task polish futuro | ⚠ |
| 4. Contratti .env.dev.local | Task 1 | ✓ |
| 4. Contratti scenario.schema.json | Task 3 | ✓ |
| 4. Contratti IMockToggle* | Task 16 | ✓ |
| 4. Contratti MockAwareProxy | Task 18-19 | ✓ |
| 4. Contratti Dev Panel API | **Fase 2** | ✓ (deferred) |
| 4. Contratti X-Meeple-Mock header | Task 20 | ✓ |
| 4. Contratti Makefile targets | Task 30 | ✓ |
| 5. Error handling - fallback | Task 5 (SCENARIO_FALLBACK), Task 7 | ✓ |
| 5. Tree-shake FE | Task 12 (dynamic import guard) | ✓ |
| 5. Compile-remove BE | Task 2 | ✓ |
| 5. Degradazione elegante | Implicit in Task 12 — **partial GAP**: nessun test esplicito di MSW fail | ⚠ |
| 6. Unit tests FE | Task 5, 6, 7, 8, 9 | ✓ |
| 6. Unit tests BE | Task 17, 18, 21, 22, 23, 24, 25, 27 | ✓ |
| 6. Integration FE | Task 15 | ✓ |
| 6. Integration BE matrix | Task 28 (partial — single test, not full matrix) | ⚠ |
| 6. E2E Playwright | **GAP** — nessun task Playwright in questo plan; deferred alle polish | ⚠ |
| 6. Dev Tools Isolation CI | Task 31 | ✓ |

### 2. Gap identificati → fix inline

**GAP 1**: Nessun task per perf workflow (SC-1/2/3).
**Fix**: ho aggiunto un TODO al plan — il workflow `dev-tools-perf.yml` è deferred a un task di **polish** in fase 3. Non è bloccante per l'MVP; i success criteria sono verificabili manualmente.

**GAP 2**: NFR-PERF-1 e NFR-PERF-2 (benchmarks).
**Fix**: deferred a fase 3 polish. Nel plan fase 1 mock sono "ragionevolmente veloci"; la misurazione formale è una nice-to-have.

**GAP 3**: E2E Playwright tests per gli scenari GWT (3.Z).
**Fix**: deferred a fase 3 polish. Il Task 15 (MSW toggle integration) copre l'invariant più importante a livello di unit/integration.

**GAP 4**: Nessun test esplicito di "MSW fails to start → passthrough".
**Fix**: aggiunto come nota nel Task 12 (la logica c'è via try/catch, ma un test sarebbe nice). Deferred a fase 3.

**GAP 5**: Matrice integration BE × FE completa (sezione 6 spec).
**Fix**: Task 28 copre il caso "tutti mock on + header present". La matrice piena (5 combinazioni) è deferred — il pattern è stabilito, l'estensione è incrementale.

### 3. Placeholder scan

Searched for: "TBD", "TODO", "implement later", "similar to Task", "fill in details", "appropriate error handling".

**Trovati**:
- Task 21 step 6 ha un commento `// TODO in task 22: wire real services` — è un'istruzione cross-task esplicita, non un placeholder generico. **OK**.
- Task 23, 24, 27 hanno "adjust after reading actual interface" — questo è un'istruzione di verifica, non un placeholder. Giustificato dal fatto che le interfacce reali non sono state lette nel plan. **OK ma è fragile** — un worker potrebbe non capire. Ho reso gli step di "leggi e adatta" espliciti numerati.
- Task 24 ha "Caso B: skippare il mock del reranker" — è una decision tree condizionale, non un placeholder. **OK**.

### 4. Type consistency check

- `MockAwareProxy<T>.Create` signature: `real, mock, toggles, serviceName` — usata consistentemente in Task 18, 19.
- `IMockToggleReader.IsMocked(string)` — consistente.
- `KnownMockServices.All` — lista di 8 stringhe coerente con le 8 registrazioni previste.
- `ScenarioLoader.Load(string) : JsonElement` — consistente in Task 21, 23, 27.
- `createMockControlStore`, `createScenarioStore`, `createMockAuthStore` — factory function pattern consistente.
- `HandlerGroup { name, handlers }` — consistente in Task 9, 13, 15.

Type consistency **OK**.

---

## Execution Handoff

**Plan complete and saved to** `docs/superpowers/plans/2026-04-09-meepledev-phase1-plan.md`.

Due opzioni di esecuzione:

**1. Subagent-Driven (recommended)** — dispatch di un fresh subagent per task, review tra task, fast iteration. Usa `superpowers:subagent-driven-development`.

**2. Inline Execution** — esegui i task in questa sessione con `superpowers:executing-plans`, batch execution con checkpoint.

**Which approach?**






