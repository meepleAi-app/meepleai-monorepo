# MeepleDev Phase 2 — Dev Panel Runtime

**Date**: 2026-04-10
**Status**: Design (v1, pre spec-panel review)
**Author**: Brainstorming session with visual companion
**Related**: `docs/superpowers/specs/2026-04-09-meepledev-fast-dev-loop-design.md` (Phase 1)
**Phase 1 merged**: PR #356 + #360 + #361 (main-dev @ `ee1c9e51d`)

---

## 1. Vision & Scope

### One-liner

Un Dev Panel in-browser che permette al developer di controllare runtime — senza restart e senza toccare file — tutti gli stati ephimeri del dev loop: toggle mock/real per i servizi, scenario dati attivo, ruolo utente simulato, ispezione delle ultime 50 request HTTP.

### Capability aggiunte rispetto a Phase 1

| Feature | Phase 1 (statico) | Phase 2 (runtime) |
|---|---|---|
| Toggle mock/real MSW | env var `NEXT_PUBLIC_MSW_ENABLE` → restart FE | Click in panel → prossima request usa nuovo stato |
| Toggle mock/real BE | env var `MOCK_LLM=true` → restart BE | Click in panel → `PATCH /dev/toggles` → `IMockToggleWriter.Set()` → prossima request usa nuovo stato, zero restart |
| Scenario dati | env var `NEXT_PUBLIC_DEV_SCENARIO` → restart FE | Dropdown scenari → `scenarioStore.loadScenario()` + `queryClient.invalidateQueries()` |
| Ruolo utente | env var `NEXT_PUBLIC_DEV_AS_ROLE` o `?dev-role=X` → reload | Dropdown role → `mockAuthStore.setRole()` + invalidation `/auth/me` |
| Request observability | nessuno | Ring buffer 50 requests con filtri per mock/real/group/method |

### 1.3 Success Criteria

| ID | Metrica | Target | Verifica |
|---|---|---|---|
| **SC2-1** | Toggle BE runtime: tempo tra click e next-request-uses-new-state | ≤ 50ms p95 | E2E test `toggle-runtime.spec.ts` |
| **SC2-2** | Scenario switch live: tempo tra click e UI refetchata | ≤ 300ms p95 | E2E test `scenario-switch.spec.ts` |
| **SC2-3** | Role switch live: tempo tra click e navbar aggiornata | ≤ 200ms p95 | E2E test `role-switch.spec.ts` |
| **SC2-4** | DevPanel first open: Ctrl+Shift+M → panel visibile | ≤ 100ms (no cold chunk load) | Manual + E2E |
| **SC2-5** | Request Inspector overhead su fetch: monkey-patch delta | ≤ 5ms p95 per request | Benchmark test |
| **SC2-6** | Zero `Api.DevTools.*` e zero `panel/*` in Release bundle | 0 bytes/types | CI dev-tools-isolation workflow (esistente) |

### 1.4 Non-goal

- Persistere runtime state tra sessioni (i toggle cambiati runtime si resettano all'env defaults ad ogni `make dev-fast` — by design)
- Remote control (es. condividere state con un collega via URL encoded)
- Full HTTP replay o modifica di request/response (nice to have, rinviato a Phase 3)
- UI customization avanzata del panel (themes, layouts alternativi) — dark mode hardcoded
- Export/import scenari (editor visual) — rinviato a Phase 3

### 1.5 Personas

- **Frontend dev** apre il panel per cambiare role e testare permessi, o per switchare scenario tra "empty" e "admin-busy" durante sviluppo di una tabella
- **Full-stack dev** usa il panel per testare alternativamente "LLM reale, resto mockato" → "tutto mockato" durante debug di un flusso chat
- **QA / demo** apre il panel con `?devpanel=1` per cambiare scenario al volo durante una demo

---

## 2. Architettura

### 2.1 Diagramma alto livello

```
┌─────────────────────────────────────────────────────────────────┐
│                    BROWSER (dev + mock mode)                    │
│                                                                 │
│  ┌──────────────┐  Ctrl+Shift+M / badge click / ?devpanel=1    │
│  │  Pagine app  │  ┌──────────────────────────────────────────┐ │
│  │  React Query ├──▶ DevPanel (drawer laterale destro)        │ │
│  └──────────────┘  │  ┌──────────────────────────────────────┐ │ │
│         │          │  │ Tabs: Toggles│Scenarios│Auth│Inspect │ │ │
│         │ fetch()  │  └──────────────────────────────────────┘ │ │
│         │          │  ┌─ TogglesSection ──────────────────────┐ │ │
│         │          │  │  MSW groups (13)                      │ │ │
│         │          │  │  Backend services (8) ─ via devPanel  │ │ │
│         │          │  │                         Client HTTP   │ │ │
│         │          │  └───────────────────────────────────────┘ │ │
│         │          │  ┌─ ScenariosSection ───────────────────┐ │ │
│         │          │  │  Dropdown scenari + reset            │ │ │
│         │          │  └───────────────────────────────────────┘ │ │
│         │          │  ┌─ AuthSection ────────────────────────┐ │ │
│         │          │  │  Dropdown ruoli da scenario.auth     │ │ │
│         │          │  └───────────────────────────────────────┘ │ │
│         │          │  ┌─ InspectorSection ───────────────────┐ │ │
│         │          │  │  Ring buffer table (50), filtri      │ │ │
│         │          │  └───────────────────────────────────────┘ │ │
│         │          └──────────────────────────────────────────┘ │
│         │                                                       │
│  ┌──────▼─────────┐                                             │
│  │ fetchIntercept │ ─ monkey patch su window.fetch              │
│  │ (ring buffer)  │ ─ registra metadata in requestInspectorStore│
│  └──────┬─────────┘                                             │
│         │                                                       │
│  ┌──────▼─────────┐       ┌──────────────────────────────┐      │
│  │  MSW worker    │──────▶│  Next.js proxy /api/[...path]│      │
│  └────────────────┘       └──────────┬───────────────────┘      │
└───────────────────────────────────────┼────────────────────────┘
                                        │
                                        ▼
┌───────────────────────────────────────────────────────────────────┐
│                  .NET API (Development, localhost:8080)           │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐      │
│  │  /dev/toggles endpoints   (ONLY if env.IsDevelopment)   │      │
│  │  ┌────────────────┐  ┌──────────────────┐  ┌─────────┐  │      │
│  │  │ GET  /toggles  │  │ PATCH /toggles   │  │ POST    │  │      │
│  │  │                │  │ body: {toggles}  │  │ /reset  │  │      │
│  │  └───────┬────────┘  └─────────┬────────┘  └────┬────┘  │      │
│  │          │                     │                │       │      │
│  │          ▼                     ▼                ▼       │      │
│  │     IMockToggle           IMockToggle       ReadEnv +   │      │
│  │     Reader                Writer            Set all    │      │
│  │          │                     │                │       │      │
│  │          └──────────┬──────────┴────────────────┘       │      │
│  │                     ▼                                   │      │
│  │          MockToggleStateProvider (P1, thread-safe)      │      │
│  │                     ▲                                   │      │
│  │                     │ IsMocked(serviceName)              │      │
│  │          ┌──────────┴───────────────────┐               │      │
│  │          │ MockAwareProxy<T> (P1)       │               │      │
│  │          │ dispatches real ⇄ mock       │               │      │
│  │          └──────────────────────────────┘               │      │
│  └─────────────────────────────────────────────────────────┘      │
└───────────────────────────────────────────────────────────────────┘
```

### 2.2 Nuovi componenti frontend (`apps/web/src/dev-tools/panel/`)

| Componente | Responsabilità | Dipendenze |
|---|---|---|
| `DevPanel.tsx` | Shell container: drawer fisso, tabs nav, collapse, mount guard | `panelUiStore`, React, mouseenter focus |
| `api/devPanelClient.ts` | HTTP client per `/dev/toggles` endpoints; gestione errori → toast | `window.fetch` (bypass interceptor con flag) |
| `stores/panelUiStore.ts` | Zustand store: `isOpen`, `activeTab`, `collapsed`, `drawerWidth`. Persist su `sessionStorage` | — |
| `stores/requestInspectorStore.ts` | Ring buffer FIFO, cap 50. Metadata only: `{url, method, status, durationMs, isMock, mockSource, timestamp}`. Filtri computati con `useMemo` | — |
| `hooks/useBackendToggles.ts` | Fetch `GET /dev/toggles` + cache in-memory; expone `toggles, refetch, setToggle(name, value), resetAll` | `devPanelClient` |
| `hooks/useKeyboardShortcut.ts` | Listener Ctrl+Shift+M; toggle `panelUiStore.isOpen` | `panelUiStore`, React effect |
| `hooks/useFetchInterceptor.ts` | Monkey-patch `window.fetch`, StrictMode-safe con ref sentinel; registra ogni call in `requestInspectorStore` | `requestInspectorStore` |
| `hooks/useQueryStringPanelOpen.ts` | Al mount legge `?devpanel=1` e apre il panel, rimuove param dalla URL | `panelUiStore`, `window.location` |
| `sections/TogglesSection.tsx` | UI list per MSW groups + backend services; switch click → `mockControlCore.setGroup()` o `useBackendToggles.setToggle()` | FE store + BE hook |
| `sections/ScenariosSection.tsx` | Dropdown scenari da `listScenarioNames()` + reset; click → scenario switch protocol (cancel queries, close SSE, load, commit, invalidate) | `scenarioStore`, `queryClient` |
| `sections/AuthSection.tsx` | Dropdown ruoli da `scenario.auth.availableUsers`; click → `mockAuthStore.setRole()` + invalidate `/auth/me` | `mockAuthStore`, `queryClient` |
| `sections/InspectorSection.tsx` | Tabella filtrabile; row click → expand inline | `requestInspectorStore` |
| `scenarioSwitchProtocol.ts` | Anti-race orchestration: beginSwitch→cancel→close SSE→load→endSwitch→invalidate, try/finally safety + timeout 2s | `scenarioStore`, `mockAuthStore`, `queryClient`, `sseRegistry` |
| `sseRegistry.ts` | Track open EventSource instances, close-all-on-scenario-switch | — |
| `index.ts` | Barrel export: `{ DevPanel, installPanel }` | — |
| `README.md` | Sub-module doc: come aggiungere tab, come gestire state, testing | — |

### 2.3 Componenti Phase 1 modificati

| File | Change |
|---|---|
| `dev-tools/devBadge.tsx` | Click handler: apre il panel via `panelUiStore.setOpen(true)` |
| `dev-tools/install.ts` | Dynamic import `panel/` + `installPanel()` + `useFetchInterceptor.install()` |
| `dev-tools/mockControlCore.ts` | Nessun cambio struttura; il panel chiama solo `setGroup`/`setEndpointOverride` esistenti |
| `dev-tools/scenarioStore.ts` | `beginSwitch/endSwitch` da stub → usati dal scenario switch protocol |
| `dev-tools/mockAuthStore.ts` | Nessun cambio (già ha `setRole`) |
| `app/mock-provider.tsx` | Dynamic import del panel (già supporta `DevToolsBundle` opaco) |

### 2.4 Nuovi componenti backend (`apps/api/src/Api/DevTools/`)

| File | Responsabilità |
|---|---|
| `Http/DevToolsEndpoints.cs` | Static class che registra `MapGet("/dev/toggles")`, `MapPatch("/dev/toggles")`, `MapPost("/dev/toggles/reset")` con guard `env.IsDevelopment()` |
| `Http/DevToggleDtos.cs` | Record DTO: `GetTogglesResponse`, `PatchTogglesRequest`, error responses |
| `Http/DevToolsEndpointsExtensions.cs` | `IEndpointRouteBuilder.MapMeepleDevTools()` — wiring in `Program.cs` |

### 2.5 Componenti backend modificati

| File | Change |
|---|---|
| `DevTools/MockToggleStateProvider.cs` | Cache degli env defaults al ctor (already in memory); expose tramite nuovo metodo `ResetToDefaults()` |
| `Program.cs` | In `#if DEBUG && env.IsDevelopment()` block dopo `UseMeepleDevTools()`: `app.MapMeepleDevTools()` |

### 2.6 Principi di isolamento

- **panel/ è self-contained**: nessun import dai file P1 esterni al panel (solo: `mockControlCore`, `scenarioStore`, `mockAuthStore`, `scenarioManifest`, `mswHandlerRegistry` — i lib P1). Il panel è *consumer*, non estende.
- **`devBadge.tsx` rimane leggero**: importa solo il setter `panelUiStore.setOpen`
- **Sezioni indipendenti**: ciascuna `*Section.tsx` è testabile isolata da sola. `<TogglesSection />` con mock store funziona senza `<DevPanel />` wrapper.
- **`devPanelClient.ts` bypassa il fetch interceptor**: marker header `X-Meepledev-Internal: 1` + interceptor skippa queste call, altrimenti il panel loggherebbe le sue stesse chiamate al inspector.

---

## 3. Data Flow

### 3.1 Flow A — Boot sequence (install dev-tools + lazy-mount panel)

```
1. Next.js layout.tsx → providers.tsx → MockProvider (se IS_DEV_MOCK)
2. MockProvider useEffect:
   └─ await import('@/dev-tools').installDevTools()
      ├─ Read .env.dev.local variables
      ├─ Load scenarioManifest (static JSON imports)
      ├─ Create stores: mockControlCore, scenarioStore, mockAuthStore (Phase 1)
      ├─ NEW: dynamic import('./panel').installPanel(stores)
      │   ├─ Mount useFetchInterceptor (monkey-patch window.fetch, ref sentinel)
      │   ├─ Create panelUiStore (read sessionStorage: activeTab, collapsed, width)
      │   ├─ Create requestInspectorStore (empty ring buffer, cap=50)
      │   ├─ Register keyboard listener (Ctrl+Shift+M)
      │   ├─ Read ?devpanel=1 query string → open if present, strip from URL
      │   └─ Return { DevPanel, panelUiStore, requestInspectorStore }
      └─ Return complete InstalledDevTools
3. MockProvider state setTools(installed)
4. MSW worker.start()
5. Render tree:
   {children}
   <DevBadge /> (P1)
   {tools.panel && <DevPanel />} (Phase 2 — reads panelUiStore.isOpen)
```

**Note**: `DevPanel` è sempre nel tree ma con `display: none` quando `isOpen === false`. Il mount avviene una sola volta per evitare re-fetch di `/dev/toggles` ad ogni riapertura.

### 3.2 Flow B — Apertura panel (3 trigger)

```
Trigger (3 sources):
  (1) Keyboard: useKeyboardShortcut intercetta keydown Ctrl+Shift+M
  (2) DevBadge click: onClick → panelUiStore.setOpen(!isOpen)
  (3) URL mount: useQueryStringPanelOpen legge ?devpanel=1 → setOpen(true)

→ panelUiStore.setOpen(true)
→ subscribe listeners fire
→ DevPanel re-render:
   - CSS transition slide-in from right (transform: translateX(0))
   - Active tab read from panelUiStore.activeTab (default 'toggles')
   - If activeTab === 'toggles':
     - TogglesSection mounts
     - useBackendToggles hook:
       - If first mount, fetch GET /dev/toggles (via devPanelClient, skip interceptor)
       - Else read cached state
     - Render MSW groups (from mockControlCore.getState().toggles.groups)
     - Render backend services (from useBackendToggles cache)
→ Panel visible, ready for interaction
```

### 3.3 Flow C — Toggle MSW group runtime (frontend-only)

```
User action: click switch "games" in TogglesSection
  │
  ▼
onChange handler:
  mockControlCore.setGroup('games', false)
  │
  ▼
Zustand store updates
  │
  ├─ panelUiStore subscribers notified (nothing to do)
  ├─ TogglesSection re-renders (switch UI reflects new state)
  │
  │  NOTE: MSW is already started — active handlers set at worker.start() time.
  │  To apply the new toggle live, we need worker.use() with the new filtered set.
  │
  ▼
useBackendToggles.onFrontendToggleChange (registered listener):
  const newGroups = mockControlCore.getState().toggles.groups
  const active = buildActiveHandlers(HANDLER_GROUPS, { groups: newGroups, overrides: {} })
  worker.resetHandlers(...active)   // MSW v2 API: replaces active handlers
  │
  ▼
Next fetch('/api/v1/games'):
  MSW checks active handlers → games handler NOT present → passthrough
  → Next.js proxy → backend (500 if no BE) OR mock on BE (if dev-fast-api)
  │
  ▼
fetchInterceptor captures response → requestInspectorStore.record({
  isMock: response.headers.has('X-Meeple-Mock'),
  mockSource: response.headers.get('X-Meeple-Mock') ?? undefined,
  ...
})
```

**Key detail**: MSW v2 expone `worker.resetHandlers(...handlers)` che rimpiazza la lista attiva in place. Non serve restart del worker. Zero race condition perché l'istanza del worker è la stessa.

### 3.4 Flow D — Toggle backend service runtime (HTTP)

```
User action: click switch "llm" in TogglesSection → Backend services list
  │
  ▼
onChange handler:
  useBackendToggles.setToggle('llm', false)
  │
  ▼
devPanelClient.patchToggles({ toggles: { llm: false } })
  → fetch('/api/_meepledev/toggles', {
      method: 'PATCH',
      headers: { 'X-Meepledev-Internal': '1' },  // skip interceptor
      body: JSON.stringify({ toggles: { llm: false } })
    })
  │
  ▼
MSW: check if '/api/_meepledev/toggles' is in a handler group
  → NOT in any group → passthrough
  │
  ▼
Next.js proxy route /api/[...path] → localhost:8080/dev/toggles
  │
  ▼
Backend DevToolsEndpoints.PatchToggles:
  if (!env.IsDevelopment()) return NotFound()
  var writer = sp.GetRequiredService<IMockToggleWriter>()
  foreach (var kv in request.Toggles)
      writer.Set(kv.Key, kv.Value)
  return Ok(new { updated = request.Toggles.Keys })
  │
  ▼
MockToggleStateProvider fires ToggleChanged event
  │
  ▼
Response back → devPanelClient returns → useBackendToggles.setToggle updates local cache
  │
  ▼
TogglesSection re-renders (switch reflects new state)
  │
  ▼
NEXT chat request from app:
  httpClient.post('/api/v1/chat', ...)
  → Backend: ChatCommandHandler injects ILlmService
  → MockAwareProxy<ILlmService>.Invoke():
    var mocked = _toggles.IsMocked("llm")  // NOW reads false → dispatches to REAL
    → RealLlmService (HybridLlmService) → OpenRouter HTTP call
  → Response returned with X-Meeple-Mock: backend-di:embedding,s3,... (no 'llm')
```

### 3.5 Flow E — Scenario switch live (protocol anti-race)

```
User action: Select 'admin-busy' in ScenariosSection dropdown
  │
  ▼
onChange handler invokes scenarioSwitchProtocol(newScenarioName):

  STEP 1 — Begin switch (block handlers):
    scenarioStore.beginSwitch()   // sets isSwitching=true
    All MSW handlers that read scenario data check isSwitching and return 503:
      if (scenarioStore.getState().isSwitching) {
        return HttpResponse.json({ error: 'scenario-switching' }, { status: 503 })
      }

  STEP 2 — Cancel in-flight queries:
    queryClient.cancelQueries({ type: 'active' })

  STEP 3 — Close SSE streams:
    for (const es of sseRegistry.getAll()) es.close()

  STEP 4 — Load new scenario:
    const newScenario = SCENARIO_MANIFEST[newScenarioName] as Scenario
    const result = validateScenario(newScenario)
    if (!result.valid) {
      scenarioStore.endSwitch()
      toast.error('Scenario invalid')
      return
    }
    scenarioStore.loadScenario(newScenario)
    mockAuthStore.reset(newScenario.auth.currentUser, newScenario.auth.availableUsers)

  STEP 5 — End switch (unblock handlers):
    scenarioStore.endSwitch()  // sets isSwitching=false

  STEP 6 — Invalidate and refetch:
    queryClient.invalidateQueries()

Total time: target ≤ 300ms p95 (SC2-2)
```

**Timeout safety**: il protocol è wrapped in `try/finally` — se qualcosa lancia tra step 1 e 5, `endSwitch()` viene comunque chiamato nel `finally` per evitare che `isSwitching=true` rimanga bloccato. Inoltre un `setTimeout(2000)` watchdog force-chiude lo switch e mostra toast rosso.

### 3.6 Flow F — Role switch live

```
User action: Select 'Editor' in AuthSection dropdown
  │
  ▼
onChange handler:
  mockAuthStore.setRole('Editor')
    → finds user with role 'Editor' in availableUsers
    → updates currentUser
  │
  ▼
queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
  │
  ▼
MSW handler for /api/v1/auth/me re-executes:
  reads mockAuthStore.getState().currentUser
  returns new user payload
  │
  ▼
Components using useAuth() re-render with new role
  → Navbar admin items appear/disappear
  → Route guards re-evaluate
  │
Total time: target ≤ 200ms p95 (SC2-3)
```

### 3.7 Flow G — Request inspector capture (monkey-patch fetch)

```typescript
// useFetchInterceptor hook (mounted once in install.ts)
if (interceptorRef.current) return  // StrictMode guard
interceptorRef.current = true

const originalFetch = window.fetch
window.fetch = async (input, init) => {
  const start = performance.now()

  // Skip inspector for internal devPanel calls
  const headers = new Headers(init?.headers)
  const isInternal = headers.get('X-Meepledev-Internal') === '1'

  try {
    const res = await originalFetch(input, init)
    if (!isInternal) {
      requestInspectorStore.record({
        url: typeof input === 'string' ? input : input.url,
        method: init?.method ?? 'GET',
        status: res.status,
        durationMs: performance.now() - start,
        isMock: res.headers.has('X-Meeple-Mock'),
        mockSource: res.headers.get('X-Meeple-Mock') ?? undefined,
        timestamp: Date.now(),
      })
    }
    return res
  } catch (err) {
    if (!isInternal) {
      requestInspectorStore.record({
        url: ..., method: ..., status: 0,  // 0 = network error
        durationMs: performance.now() - start,
        isMock: false, timestamp: Date.now(),
      })
    }
    throw err
  }
}
```

### 3.8 Flow H — Inspector display + filtering

```typescript
// InspectorSection.tsx
const entries = useStoreSlice(requestInspectorStore, s => s.entries)
const [filter, setFilter] = useState({ mock: 'all', group: 'all', methods: [] })

const filtered = useMemo(() => {
  return entries.filter(e => {
    if (filter.mock === 'mock' && !e.isMock) return false
    if (filter.mock === 'real' && e.isMock) return false
    if (filter.group !== 'all' && !e.mockSource?.includes(filter.group)) return false
    if (filter.methods.length > 0 && !filter.methods.includes(e.method)) return false
    return true
  })
}, [entries, filter])

// Render:
// - Filter bar
// - Table with rows
// - Row click → expand inline with all headers
// - Footer: "Showing N of 50"
```

**StrictMode safety**: ref sentinel (`interceptorRef.current`) assicura monkey-patch applicato una sola volta anche se React StrictMode monta 2 volte il componente in dev.

---

## 4. Contratti

### 4.1 Backend HTTP API

Tutti gli endpoint sono montati **solo se** `env.IsDevelopment()`. In Release config ritornano 404 senza neanche registrarsi (compile-removed + runtime check).

#### `GET /dev/toggles`

**Response 200**:
```json
{
  "toggles": {
    "llm": true,
    "embedding": true,
    "reranker": true,
    "smoldocling": true,
    "unstructured": true,
    "bgg": true,
    "s3": true,
    "n8n": true
  },
  "knownServices": [
    "llm", "embedding", "reranker", "smoldocling",
    "unstructured", "bgg", "s3", "n8n"
  ]
}
```

**Response 404**: se `env.IsDevelopment()` è false.

#### `PATCH /dev/toggles`

**Request**:
```json
{
  "toggles": {
    "llm": false,
    "embedding": true
  }
}
```

- Body parziale: solo i toggle da modificare (batch)
- Chiavi devono essere in `KnownMockServices.All`; altre lanciano 400

**Response 200**:
```json
{
  "updated": ["llm", "embedding"],
  "toggles": { /* stato completo aggiornato */ }
}
```

**Response 400** (validation):
```json
{
  "error": "unknown-service",
  "message": "Unknown mock service 'xyz'. Known: llm, embedding, ...",
  "unknownKeys": ["xyz"]
}
```

#### `POST /dev/toggles/reset`

**Request**: nessun body.

**Response 200**: stessa shape del `GET /dev/toggles`, con i toggle reimpostati ai valori letti dalle env var al boot.

Implementato leggendo lo snapshot iniziale cached in `MockToggleStateProvider` nel ctor (`IReadOnlyDictionary<string, bool> _bootstrapDefaults`).

### 4.2 Frontend API client — `devPanelClient.ts`

```typescript
export interface BackendTogglesState {
  toggles: Record<string, boolean>;
  knownServices: string[];
}

export interface PatchTogglesResponse {
  updated: string[];
  toggles: Record<string, boolean>;
}

const INTERNAL_HEADER = { 'X-Meepledev-Internal': '1' } as const;

export class DevPanelClient {
  constructor(private baseUrl: string = '/api/_meepledev') {}

  async getToggles(): Promise<BackendTogglesState> { /* ... */ }
  async patchToggles(toggles: Record<string, boolean>): Promise<PatchTogglesResponse> { /* ... */ }
  async resetToggles(): Promise<BackendTogglesState> { /* ... */ }
}

export const devPanelClient = new DevPanelClient();
```

**Route risoluzione**: il baseUrl `/api/_meepledev` viene risolto via Next.js proxy catch-all `/api/[...path]` verso `http://localhost:8080/dev/*`. Non serve proxy route dedicato.

**Errori**:
- `fetch` throw → re-throw con `DevPanelClientError(message, cause)`
- Response 4xx/5xx → parse JSON, lancia `DevPanelClientError(error, status, traceId)`
- `AbortController` con timeout 5s per evitare hang

### 4.3 Zustand stores

#### `panelUiStore.ts`

```typescript
export type PanelTab = 'toggles' | 'scenarios' | 'auth' | 'inspector';

export interface PanelUiState {
  isOpen: boolean;
  collapsed: boolean;       // sidebar collapsed to 40px icon rail
  activeTab: PanelTab;
  drawerWidth: number;      // px, default 420

  setOpen: (open: boolean) => void;
  toggle: () => void;
  setCollapsed: (c: boolean) => void;
  setActiveTab: (tab: PanelTab) => void;
  setDrawerWidth: (w: number) => void;
}
```

#### `requestInspectorStore.ts`

```typescript
export interface InspectorEntry {
  id: string;
  timestamp: number;
  method: string;
  url: string;
  status: number;            // 0 = network error
  durationMs: number;
  isMock: boolean;
  mockSource?: string;
}

const RING_CAPACITY = 50;

export interface RequestInspectorState {
  entries: InspectorEntry[];
  record: (entry: Omit<InspectorEntry, 'id'>) => void;
  clear: () => void;
}
```

### 4.4 sessionStorage keys

Tutti con prefix `meepledev-panel-` per namespacing:

| Key | Value shape | Written by |
|---|---|---|
| `meepledev-panel-is-open` | `"true" \| "false"` | `panelUiStore.setOpen` |
| `meepledev-panel-collapsed` | `"true" \| "false"` | `panelUiStore.setCollapsed` |
| `meepledev-panel-active-tab` | `"toggles" \| "scenarios" \| "auth" \| "inspector"` | `panelUiStore.setActiveTab` |
| `meepledev-panel-drawer-width` | integer string | `panelUiStore.setDrawerWidth` |

**Note**: `requestInspectorStore.entries` NON è persistito — il buffer è volatile per design.

### 4.5 React hooks (public surface)

```typescript
export function useBackendToggles(): {
  toggles: Record<string, boolean>;
  knownServices: string[];
  isLoading: boolean;
  error: DevPanelClientError | null;
  setToggle: (name: string, value: boolean) => Promise<void>;
  refetch: () => Promise<void>;
  resetAll: () => Promise<void>;
};

export function useKeyboardShortcut(
  combo: { ctrl?: boolean; shift?: boolean; alt?: boolean; key: string },
  handler: () => void
): void;

export function useQueryStringPanelOpen(): void;
export function useFetchInterceptor(): { installed: boolean };

export function useStoreSlice<T, U>(
  store: StoreApi<T>,
  selector: (state: T) => U
): U;
```

### 4.6 Internal header contract

**`X-Meepledev-Internal: 1`** è un header magic usato in 3 punti coordinati:

| Emittente | Scopo | Consumatore |
|---|---|---|
| `devPanelClient.*` | Ogni fetch dal panel verso `/dev/*` | `useFetchInterceptor` skip |
| MSW handlers `/api/_meepledev/*` | Non esiste handler → passthrough naturale | — |
| `useFetchInterceptor` | Controlla questo header e skippa la registrazione | — |

### 4.7 Keyboard shortcut contract

- **Combo**: `Ctrl+Shift+M` (`Cmd+Shift+M` su macOS — `event.metaKey || event.ctrlKey`)
- **Scope**: window listener
- **Behavior**: toggle `panelUiStore.isOpen`
- **Conflict**: nessuna collision nota in Chrome/Firefox/Safari. Fallback: badge click o `?devpanel=1`

---

## 5. Error Handling & Safety

### 5.1 Failure modes e gestione

| Failure | Detection | Behavior | Fallback |
|---|---|---|---|
| **Backend down** durante `GET /dev/toggles` | `devPanelClient` fetch error | Banner rosso "Backend unreachable — showing FE toggles only"; retry ogni 10s | MSW toggles funzionanti. Backend list nascosta. |
| **PATCH /dev/toggles** fallisce | Response != 200 o throw | Optimistic rollback UI + toast errore con traceId | Stato locale non aggiornato. Retry safe. |
| **Unknown service** in PATCH | Try/catch → 400 | Toast "Unknown service 'xyz'" | Non dovrebbe mai accadere |
| **`/dev/toggles` 404** | Fetch ritorna 404 | Panel nasconde sezione Backend, mostra banner "Backend mode not available" | 3 sezioni residue funzionanti |
| **Scenario switch invalid JSON** | `validateScenario` ritorna `{valid: false}` | Toast + rollback + endSwitch | Stato intatto |
| **Scenario switch hang** | `setTimeout(2000)` watchdog | Force endSwitch + toast rosso "timeout" | User può F5 manuale |
| **Role switch role-not-in-scenario** | `findByRole` undefined | No-op + toast warning | Stato intatto |
| **Monkey-patch loop** | Ref sentinel | Re-applica se serve, log warning | Perdita transiente metriche |
| **Inspector OOM** | Cap hardcoded 50, no body | Impossible by construction | — |
| **`sessionStorage` quota** | Try/catch su setItem | Fallback in-memory | Persistenza disabilitata |
| **Keyboard conflict** | Non detectable | Badge click o `?devpanel=1` | Documentare in README |
| **Multi-tab stesso app** | sessionStorage per-tab | Backend state condiviso, refetch on focus | Accettabile |
| **Switch durante mutation in-flight** | `cancelQueries` aborta | Mutation cancellata + toast | Accettabile |
| **Monkey-patch catch AbortError** | try/catch re-throw | Record status 0 | Rumore filtrable |
| **Panel in prod per sbaglio** | Build guard + runtime check | Chunk assente; forzato → 404 | Nessun side effect |

### 5.2 Tree-shake e dead code elimination

**Frontend**: `panel/` importato SOLO da `install.ts` via dynamic `import()`:

```typescript
if (IS_DEV_MOCK) {
  const panelModule = await import(`${'@'}/dev-tools/panel` as string);
  const panel = await panelModule.installPanel(stores);
  return { ...stores, panel };
}
```

- Stesso pattern P1 (template literal defeat static analysis)
- `mock-provider.tsx` già supporta `DevToolsBundle` opaco — si estende con optional `panel` field

**Backend**: `DevTools/Http/*` è sotto-cartella di `DevTools/`, coperta dal `<Compile Remove="DevTools/**/*.cs">` esistente.

### 5.3 Security invariants

1. **Endpoint registrati solo se `env.IsDevelopment()`**:
   ```csharp
   public static IEndpointRouteBuilder MapMeepleDevTools(this IEndpointRouteBuilder app)
   {
       var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
       if (!env.IsDevelopment()) return app;
       app.MapGet("/dev/toggles", DevToolsEndpoints.GetToggles);
       app.MapPatch("/dev/toggles", DevToolsEndpoints.PatchToggles);
       app.MapPost("/dev/toggles/reset", DevToolsEndpoints.ResetToggles);
       return app;
   }
   ```

2. **`Program.cs` wiring** — doppio guard `#if DEBUG` + `env.IsDevelopment()`:
   ```csharp
   #if DEBUG
   if (app.Environment.IsDevelopment())
   {
       Api.DevTools.DevToolsServiceCollectionExtensions.UseMeepleDevTools(app);
       Api.DevTools.Http.DevToolsEndpointsExtensions.MapMeepleDevTools(app);
   }
   #endif
   ```

3. **CORS non necessario**: Next.js proxy catch-all inoltra server-side.

4. **No rate limiting**: accettabile perché gated + dev only.

5. **Audit log**: `PATCH /dev/toggles` logga ogni cambio via `ILogger<DevToolsEndpoints>` a `Information`.

6. **No token auth**: valutato nella brainstorming (Q4), deciso di usare solo `env.IsDevelopment()` guard. Removing `MEEPLE_DEV_TOKEN` from `.env.dev.local.example` è cleanup parte di M4.

### 5.4 CI Isolation check

Workflow esistente `dev-tools-isolation.yml` rimuove `apps/api/src/Api/DevTools` + `apps/web/src/dev-tools` + `docs/superpowers/fixtures`. I file Phase 2 sono tutti sotto le stesse directory, **nessuna modifica al workflow necessaria**.

**Opzionale M4 addition**: un check "no X-Meepledev-Internal in prod bundle":
```bash
grep -r "X-Meepledev-Internal" apps/web/.next/static/ && exit 1 || exit 0
```

### 5.5 Graceful degradation philosophy

Ogni feature può fallire indipendentemente:
- Inspector down → Toggles OK
- Backend toggles 404 → MSW + Scenarios + Auth OK
- Scenario switch fails → Toggles + Auth + Inspector OK
- Role switch → no-op silenzioso (non fallisce)
- Panel mount fails → app continua, DevBadge in statico

Ogni sezione wrappata in `<ErrorBoundary>` con `<SectionErrorFallback name="X">`.

---

## 6. Testing Strategy

### 6.1 Test pyramid

```
                  ┌───────────────────────────┐
                  │  E2E Playwright (5 test)  │   ← SC2-1..4 validation
                  └───────────────────────────┘
              ┌───────────────────────────────────┐
              │  Integration (MSW+setupServer,    │
              │  WebApplicationFactory)  ~8 test  │
              └───────────────────────────────────┘
        ┌───────────────────────────────────────────┐
        │  Unit tests (Vitest FE + xUnit BE) ~35    │
        └───────────────────────────────────────────┘
```

### 6.2 Unit tests — Frontend (`apps/web/__tests__/dev-tools/panel/`)

| File | Copertura |
|---|---|
| `panelUiStore.test.ts` | Initial state da sessionStorage, setOpen/toggle/setActiveTab/setCollapsed/setDrawerWidth, persist su mutation, fallback in-memory |
| `requestInspectorStore.test.ts` | Ring buffer FIFO cap 50, order newest-first, clear svuota, no body |
| `devPanelClient.test.ts` | getToggles parse, patchToggles header `X-Meepledev-Internal`, DevPanelClientError wrap, timeout 5s, resetToggles |
| `fetchInterceptor.test.ts` | Monkey-patch una sola volta (ref sentinel), skip header internal, metadata corretta, AbortError handling, status 0 network error |
| `useBackendToggles.test.ts` | Fetch iniziale, cache, setToggle optimistic + rollback, resetAll, retry on error |
| `useKeyboardShortcut.test.ts` | Ctrl+Shift+M + Cmd+Shift+M, no trigger con solo Shift+M, cleanup listener |
| `useQueryStringPanelOpen.test.ts` | Rileva ?devpanel=1, remove via history.replaceState |
| `scenarioSwitchProtocol.test.ts` | Sequenza begin→cancel→close→load→end→invalidate, error path calls endSwitch in finally, watchdog 2s |

**Target**: ~25 test, coverage ≥85% sui nuovi file.

### 6.3 Unit tests — Backend (`apps/api/tests/Api.Tests/DevTools/Http/`)

| File | Copertura |
|---|---|
| `DevToolsEndpointsTests.cs` | GetToggles shape, PatchToggles batch + update, 400 unknown key, ResetToggles bootstrap defaults, ToggleChanged event fires |
| `MockToggleStateProviderResetTests.cs` | ResetToDefaults ripristina post-Set(), thread-safe concurrent Set+Reset |

**Target**: ~10 test.

### 6.4 Integration tests — Frontend

| File | Copertura |
|---|---|
| `togglesSectionIntegration.test.tsx` | Mount + MSW setupServer → click MSW group → `worker.resetHandlers` lista corretta; click backend toggle → PATCH con header |
| `scenarioSwitchIntegration.test.tsx` | Mount ScenariosSection → trigger switch → verifica sequenza (begin→cancel→load→end→invalidate), isSwitching true→false |
| `inspectorCapture.test.ts` | Mount fetchInterceptor → mock fetch con X-Meeple-Mock headers → verifica capture + skip internal |

**Target**: ~3 integration test.

### 6.5 Integration tests — Backend

| File | Copertura |
|---|---|
| `DevToolsEndpointsIntegrationTests.cs` | Full HTTP round-trip con WebApplicationFactory. GET/PATCH/POST ritornano 200, 404 in Production, audit log scritto |
| `MockAwareProxyRuntimeSwitchTests.cs` | Chat request con MOCK_LLM=true → mock response; PATCH → mock response real (via HttpClient handler); dimostra runtime switch end-to-end |

**Target**: ~2 integration test.

### 6.6 E2E Playwright tests (`apps/web/e2e/dev-loop/`, tag `@dev-loop`)

#### `devpanel-opens.spec.ts` (SC2-4)

```typescript
test('Ctrl+Shift+M opens panel within 100ms', async ({ page }) => {
  await page.goto('/');
  await page.waitForSelector('[data-testid="dev-badge"]');

  const start = Date.now();
  await page.keyboard.press('Control+Shift+M');
  await page.waitForSelector('[data-testid="dev-panel"]', { state: 'visible' });
  const elapsed = Date.now() - start;

  expect(elapsed).toBeLessThanOrEqual(110); // 10ms tolerance
});
```

Più tests: `?devpanel=1` URL, DevBadge click.

#### `toggle-runtime.spec.ts` (SC2-1)

```typescript
test('backend LLM toggle switches runtime without restart', async ({ page, request }) => {
  await page.goto('/?devpanel=1');
  await page.click('[data-testid="panel-tab-toggles"]');

  const initial = await request.get('/api/_meepledev/toggles');
  expect((await initial.json()).toggles.llm).toBe(true);

  const start = Date.now();
  await page.click('[data-testid="toggle-llm"]');
  await page.waitForSelector('[data-testid="toggle-llm"][data-state="off"]');
  const patchTime = Date.now() - start;
  expect(patchTime).toBeLessThanOrEqual(55); // 5ms tolerance

  const after = await request.get('/api/_meepledev/toggles');
  expect((await after.json()).toggles.llm).toBe(false);
});
```

#### `scenario-switch.spec.ts` (SC2-2)

```typescript
test('scenario switch updates UI within 300ms', async ({ page }) => {
  await page.goto('/library?devpanel=1');
  await page.click('[data-testid="panel-tab-scenarios"]');

  const start = Date.now();
  await page.selectOption('[data-testid="scenario-select"]', 'admin-busy');
  await page.waitForSelector('[data-testid="game-card"]', { state: 'visible' });
  const elapsed = Date.now() - start;

  expect(elapsed).toBeLessThanOrEqual(330);
  expect(await page.locator('[data-testid="game-card"]').count()).toBe(20);
});
```

#### `role-switch.spec.ts` (SC2-3)

```typescript
test('role switch updates navbar within 200ms', async ({ page }) => {
  await page.goto('/?devpanel=1&dev-role=Admin');
  await page.waitForSelector('[data-testid="navbar-admin-link"]');
  await page.click('[data-testid="panel-tab-auth"]');

  const start = Date.now();
  await page.selectOption('[data-testid="role-select"]', 'User');
  await page.waitForSelector('[data-testid="navbar-admin-link"]', { state: 'hidden' });
  expect(Date.now() - start).toBeLessThanOrEqual(220);
});
```

#### `inspector-capture.spec.ts`

```typescript
test('inspector captures fetch calls with mock headers', async ({ page }) => {
  await page.goto('/games?devpanel=1');
  await page.click('[data-testid="panel-tab-inspector"]');

  await page.waitForSelector('[data-testid="inspector-row"]');
  expect(await page.locator('[data-testid="inspector-row"]').count()).toBeGreaterThan(0);

  const firstRow = page.locator('[data-testid="inspector-row"]').first();
  await expect(firstRow.locator('[data-testid="mock-indicator"]')).toBeVisible();
  await expect(firstRow.locator('[data-testid="mock-source"]')).toContainText('msw:games');

  const urls = await page.locator('[data-testid="inspector-url"]').allTextContents();
  expect(urls.every(u => !u.includes('_meepledev'))).toBe(true);
});
```

### 6.7 Benchmark test (opzionale)

**NFR SC2-5**: overhead fetch interceptor ≤ 5ms p95.

Vitest bench:
```typescript
// bench/fetchInterceptor.bench.ts
bench('fetch interceptor overhead', () => {
  // Baseline vs instrumented, 1000 calls, delta p95 ≤ 5ms
});
```

### 6.8 Meta-test: dev-tools-isolation (invariato)

Workflow esistente continua a validare che rimuovendo `dev-tools/` e `DevTools/` la prod build passi. Nessuna modifica Phase 2.

### 6.9 Compatibilità con test Phase 1

- 83 test BE Phase 1 invariati (`MockToggleStateProvider` ha solo un nuovo metodo retrocompatibile)
- 33 test FE Phase 1 invariati (store P1 non cambiano)

### 6.10 Riepilogo conteggio

| Categoria | Phase 2 |
|---|---|
| FE unit | ~25 |
| FE integration | ~3 |
| BE unit | ~10 |
| BE integration | ~2 |
| E2E Playwright | 5 |
| **Totale** | **~45** |

Phase 1 baseline: 116. Dopo Phase 2: ~161.

---

## 7. Implementation Phases

### Overview

5 milestones, ~5 giorni totali (~3.5 con parallelismo).

| PR | Milestone | Scope | Est |
|---|---|---|---|
| **PR #1** | **M0** — Scaffold panel sub-module + backend endpoints | 0.5g |
| **PR #2** | **M1** — Toggles section | 1.5g |
| **PR #3** | **M2** — Scenarios + Auth sections | 1g |
| **PR #4** | **M3** — Request inspector + fetchInterceptor | 1g |
| **PR #5** | **M4** — E2E tests + polish + doc update | 1g |

### PR #1 — M0: Scaffold (0.5g)

**Obiettivo**: struttura vuota `panel/` + backend endpoints funzionanti senza UI.

**Deliverables**:
- `apps/web/src/dev-tools/panel/` subfolder con `index.ts`, `README.md`, `DevPanel.tsx` minimal, `stores/panelUiStore.ts`, `hooks/useKeyboardShortcut.ts`, `hooks/useQueryStringPanelOpen.ts`
- `apps/api/src/Api/DevTools/Http/DevToolsEndpoints.cs` con GET/PATCH/POST/reset
- `apps/api/src/Api/DevTools/Http/DevToggleDtos.cs`
- `apps/api/src/Api/DevTools/Http/DevToolsEndpointsExtensions.cs`
- `MockToggleStateProvider.ResetToDefaults()` + test
- `Program.cs` wiring (`MapMeepleDevTools()`)
- `install.ts` modificato per dynamic import `panel/` + wire shortcut
- `mock-provider.tsx` modificato per mount DevPanel
- `devPanelClient.ts` completo
- Unit tests: panelUiStore, devPanelClient, useKeyboardShortcut, useQueryStringPanelOpen, DevToolsEndpointsTests, MockToggleStateProviderResetTests

**Definition of done**:
- `make dev-fast-api` avviato
- `Ctrl+Shift+M` apre un drawer vuoto con 4 tab nav
- `curl http://localhost:8080/dev/toggles` ritorna JSON con 8 servizi
- `curl -X PATCH ... {"toggles": {"llm": false}}` cambia lo stato
- Unit tests verdi
- `make dev-fast` (FE only) funziona senza BE (banner "Backend unreachable")

### PR #2 — M1: Toggles section (1.5g)

**Obiettivo**: sezione Toggles completa — feature più importante Phase 2.

**Deliverables**:
- `panel/sections/TogglesSection.tsx` — 2 gruppi: "Frontend (MSW)" e "Backend services"
- `panel/hooks/useBackendToggles.ts`
- MSW groups: click → `mockControlCore.setGroup()` → `worker.resetHandlers()`
- Backend services: click → `useBackendToggles.setToggle()` → PATCH + optimistic + rollback
- "Reset to defaults" button entrambi i gruppi
- ErrorBoundary wrapper
- Banner "Backend unreachable" con retry 10s
- Unit + integration tests

**Definition of done**:
- Click switch MSW group: next request passthrough (verificato devtools)
- Click backend: next chat request ha/non ha mock source
- Reset MSW: tutti tornano a env enable
- Reset backend: PATCH reset → tutti tornano a env defaults
- Con BE down: MSW OK, banner, backend list nascosto
- Tutti test verdi

### PR #3 — M2: Scenarios + Auth sections (1g)

**Obiettivo**: sezioni Scenarios e Auth con switch live.

**Deliverables**:
- `panel/sections/ScenariosSection.tsx` — dropdown + descrizione + reset
- `panel/sections/AuthSection.tsx` — dropdown ruoli + info user
- `panel/scenarioSwitchProtocol.ts` — protocol function try/finally + timeout 2s
- `panel/sseRegistry.ts` — tracking EventSource
- `scenarioStore.test.ts` aggiornato con protocol
- Integration test `scenarioSwitchIntegration`
- Handler MSW 503 se `isSwitching` (applicato ai 3-4 più esposti)

**Definition of done**:
- Selezione scenario cambia dati senza F5
- Switch rapido (3 scenari in 2s) non corrompe state
- Timeout watchdog 2s → rollback + toast
- Role switch aggiorna navbar senza F5
- Test integration verde

### PR #4 — M3: Request inspector + fetchInterceptor (1g)

**Obiettivo**: sezione Inspector con capture runtime.

**Deliverables**:
- `panel/hooks/useFetchInterceptor.ts` — monkey-patch ref sentinel
- `panel/stores/requestInspectorStore.ts` — ring buffer 50
- `panel/sections/InspectorSection.tsx` — tabella + 3 filtri (mock/real/all, group, method)
- Row click → expand inline header list
- Unit tests: fetchInterceptor, requestInspectorStore
- Integration test: inspectorCapture
- Benchmark opzionale: bench/fetchInterceptor.bench.ts

**Definition of done**:
- Ogni fetch dell'app nel ring buffer
- Skip `/api/_meepledev/*` via header
- Filtro "mock only" funziona
- StrictMode double-mount non duplica entries
- Benchmark ≤ 5ms p95

### PR #5 — M4: E2E + polish + doc (1g)

**Obiettivo**: validazione end-to-end + documentazione.

**Deliverables**:
- 5 E2E Playwright tests (tag `@dev-loop`)
- `CLAUDE.md` aggiornato con sezione Dev Panel
- `docs/development/README.md` aggiornato
- `dev-tools/panel/README.md`
- DevBadge title attribute "Click to open panel"
- Drawer collapse mode (barra 40px) se non già in M0
- Polish animazioni
- `dev-tools-isolation.yml` local run verifica
- `MEEPLE_DEV_TOKEN` rimosso da `.env.dev.local.example`

**Definition of done**:
- 5 E2E passano in CI
- Isolation workflow passa
- Manual smoke test completo
- Doc aggiornata
- Screenshot nella PR

### Stima totale

| Milestone | Giorni | Dipendenze |
|---|---|---|
| M0 — Scaffold | 0.5 | main-dev (Phase 1 completata) |
| M1 — Toggles | 1.5 | M0 |
| M2 — Scenarios + Auth | 1.0 | M0 |
| M3 — Inspector | 1.0 | M0 |
| M4 — E2E + polish | 1.0 | M1, M2, M3 |
| **Totale seriale** | **5.0** | |
| **Totale parallelo (M1\|M2\|M3)** | **~3.5** | |

### Valore incrementale

- **Dopo M0**: endpoint HTTP funzionanti, scriptabile via curl
- **Dopo M1**: ⭐ **feature killer** — toggle runtime zero restart
- **Dopo M2**: scenario switch live → demos fluide
- **Dopo M3**: observability debug
- **Dopo M4**: regression-proof + docs

---

## Appendix — Open tactical decisions (per fase plan)

1. **`worker.resetHandlers` signature esatta MSW v2** — verificare API changelog durante M1
2. **ErrorBoundary implementation** — shadcn/ui ha un componente o serve custom?
3. **Toast library** — verificare se `sonner` è già dep (altrimenti custom minimal)
4. **Scenario MSW handler 503 guard** — quali handler specifici toccare (games, chat, library, sessions?)
5. **Inspector row expand** — come renderizzare header list compatta (JSON tree vs flat list?)
