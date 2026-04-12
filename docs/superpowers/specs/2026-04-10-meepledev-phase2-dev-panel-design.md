# MeepleDev Phase 2 — Dev Panel Runtime

**Date**: 2026-04-10
**Status**: Design (v2, post spec-panel review)
**Author**: Brainstorming session with visual companion
**Reviewed by**: Expert panel (Wiegers, Adzic, Fowler, Nygard, Crispin, Hightower) via `/sc:spec-panel`
**Related**: `docs/superpowers/specs/2026-04-09-meepledev-fast-dev-loop-design.md` (Phase 1)
**Phase 1 merged**: PR #356 + #360 + #361 (main-dev @ `ee1c9e51d`)

## Changelog

- **v1 (initial)**: Vision, architettura sub-module `panel/`, 4 feature runtime, 6 SC, 5 milestone, ~45 test
- **v2 (post spec-panel review)**: Applied all P0 + P1 + P2 fixes:
  - **P0-W2-1**: SC2-1 split in SC2-1a (PATCH round-trip ≤50ms p95) + SC2-1b (correctness header); test `toggle-runtime.spec.ts` aggiornato con median-of-5 + CI budget 3x
  - **P0-A2-1**: Aggiunta sezione 3.Z con 7 Given/When/Then executable scenarios
  - **P0-F2-1**: Split `useBackendToggles` in Query + Mutation hooks (CQS)
  - **P0-N2-1**: Mutex `switchInProgressController` + last-click-wins policy su scenario switcher
  - **P0-C2-1**: `NoMockInReleaseTests` esteso per verificare zero tipi `Api.DevTools.Http.*` in Release
  - **P1-W2-2**: Benchmark SC2-5 obbligatorio con CI workflow `bench-phase2.yml`
  - **P1-W2-3**: Nuova sezione 1.6 Accessibility (ARIA, keyboard, focus management, axe-core E2E)
  - **P1-A2-2**: Nuovo E2E `scenario-switch-race.spec.ts` (last-click-wins stress)
  - **P1-F2-2**: `scenarioSwitcher.ts` spostato fuori da `panel/` a `dev-tools/` (boundary clean)
  - **P1-F2-3**: `sseRegistry` sostituito da CustomEvent `meepledev:scenario-switch-begin` (zero coupling)
  - **P1-N2-2**: Mutations in-flight durante scenario switch documentate come known limitation
  - **P1-N2-3**: Debounce + per-name lock su `setToggle` (no flicker)
  - **P1-N2-4**: Exponential backoff retry (10s→30s→60s→300s→stop)
  - **P1-C2-2**: `fetchInterceptor.test.ts` espanso a 9 test case
  - **P1-C2-4**: Flakiness-resistant E2E pattern (warm-up + median of 5 + CI budget 3x)
  - **P1-H2-1**: Initial `getToggles()` fetch spostato all'`installPanel()` invece del first mount Toggles section
  - **P2-F2-4**: `DevPanelClient` da class a pure function module
  - **P2-C2-5**: Sonner dep check spostato da appendix a M0 deliverable
  - **P2-W2-4**: Keyboard shortcut collision test come M0 smoke (documentato in README)
  - **P2-H2-2**: Multi-dev `make integration` warning in CLAUDE.md (M4)
  - **P2-H2-4**: DevBadge health indicator quando backend toggles error (M4)

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
| **SC2-1a** | Toggle BE runtime: PATCH round-trip (click → response OK) | ≤ 50ms p95 | E2E test `toggle-runtime.spec.ts` (step 1) |
| **SC2-1b** | Toggle BE correctness: post-PATCH, first subsequent chat request header reflects new state | ≤ 1 request window | E2E test `toggle-runtime.spec.ts` (step 2) |
| **SC2-2** | Scenario switch live: tempo tra click e UI refetchata | ≤ 300ms p95 | E2E test `scenario-switch.spec.ts` |
| **SC2-3** | Role switch live: tempo tra click e navbar aggiornata | ≤ 200ms p95 | E2E test `role-switch.spec.ts` |
| **SC2-4** | DevPanel first open: Ctrl+Shift+M → panel visibile | ≤ 100ms (no cold chunk load) | Manual + E2E |
| **SC2-5** | Request Inspector overhead su fetch: monkey-patch delta | ≤ 5ms p95 per request | Benchmark test (**obbligatorio**, CI-enforced, vedi 6.7) |
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

### 1.6 Accessibility requirements

Anche se il Dev Panel è un tool dev (non user-facing), rispetta le best practice a11y per preservare la codebase dalle cattive abitudini e permettere uso via screen reader (es. dev blind/low-vision).

**ARIA roles**:
- Drawer container: `role="dialog"` + `aria-modal="false"` (non bloccante) + `aria-label="MeepleDev Panel"`
- Tabs nav: `role="tablist"` con 4 `role="tab"` figli, `aria-selected="true"` sul tab attivo
- Tab panels: `role="tabpanel"` + `aria-labelledby="tab-{id}"`
- Toggle switches: `role="switch"` + `aria-checked={boolean}` + `aria-label="Toggle {serviceName}"`
- Error toasts: `role="alert"` + `aria-live="assertive"`
- Info banners: `role="status"` + `aria-live="polite"`
- Inspector table: `role="table"` + `role="row"` + `role="columnheader"` + `role="cell"`

**Keyboard navigation**:
- `Escape`: chiude il panel
- `Tab` / `Shift+Tab`: naviga tra focusable elements
- `ArrowLeft` / `ArrowRight` sul tablist: naviga tra tab (WAI tab pattern)
- `Space` / `Enter` su switch: attiva toggle
- `Enter` su row inspector: expand/collapse dettagli
- Primo focusable al mount: il tab attivo (non il bottone close, per ridurre distrazione)

**Focus management**:
- Focus trap quando il panel è aperto: **NO** (il panel non è modale, l'app sotto resta interattiva)
- Focus restore alla chiusura: riporta focus all'elemento che ha triggerato l'apertura (badge, keyboard, URL)
- `@testing-library/user-event` testa il flusso Tab navigation

**Screen reader announcements**:
- State changes emettono `aria-live` updates: "LLM mock disabled", "Scenario changed to admin-busy", "Role changed to User"
- Pattern: `<div role="status" aria-live="polite" id="devpanel-announcer" />` in cui scriviamo il messaggio via `setAnnouncement()`

**Testing**:
- Unit test con `@testing-library/react` + `@testing-library/user-event` per keyboard flow
- Axe-core check come parte di E2E test `devpanel-a11y.spec.ts`

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
| `hooks/useBackendTogglesQuery.ts` | **Read-only** hook. Fetch `GET /dev/toggles` + cache in-memory; expone `toggles, knownServices, isLoading, error, refetch` | `devPanelClient` |
| `hooks/useBackendTogglesMutation.ts` | **Write-only** hook (CQS split). Expone `setToggle(name, value), resetAll, isMutating, mutationError`. Debounce 300ms per toggle name, lock per-name contro click concorrenti | `devPanelClient` |
| `hooks/useKeyboardShortcut.ts` | Listener Ctrl+Shift+M; toggle `panelUiStore.isOpen` | `panelUiStore`, React effect |
| `hooks/useFetchInterceptor.ts` | Monkey-patch `window.fetch`, StrictMode-safe con ref sentinel; registra ogni call in `requestInspectorStore` | `requestInspectorStore` |
| `hooks/useQueryStringPanelOpen.ts` | Al mount legge `?devpanel=1` e apre il panel, rimuove param dalla URL | `panelUiStore`, `window.location` |
| `sections/TogglesSection.tsx` | UI list per MSW groups + backend services; switch click → `mockControlCore.setGroup()` o `useBackendToggles.setToggle()` | FE store + BE hook |
| `sections/ScenariosSection.tsx` | Dropdown scenari da `listScenarioNames()` + reset; click → scenario switch protocol (cancel queries, close SSE, load, commit, invalidate) | `scenarioStore`, `queryClient` |
| `sections/AuthSection.tsx` | Dropdown ruoli da `scenario.auth.availableUsers`; click → `mockAuthStore.setRole()` + invalidate `/auth/me` | `mockAuthStore`, `queryClient` |
| `sections/InspectorSection.tsx` | Tabella filtrabile; row click → expand inline | `requestInspectorStore` |
| `index.ts` | Barrel export: `{ DevPanel, installPanel }` | — |
| `README.md` | Sub-module doc: come aggiungere tab, come gestire state, testing | — |

**Files added OUTSIDE `panel/`** (a livello `dev-tools/` per non violare il boundary "panel is consumer, not extender"):

| File | Responsabilità | Dipendenze |
|---|---|---|
| `dev-tools/scenarioSwitcher.ts` | Anti-race orchestration function `switchScenario(name)`: mutex lock + beginSwitch→cancel→dispatch event→load→endSwitch→invalidate, try/finally safety + timeout 2s. **Pure function**, non React. | `scenarioStore`, `mockAuthStore`, `queryClient`, `scenarioManifest`, `scenarioValidator` |

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
      │   ├─ **(H2-1 FIX)** Fire initial GET /dev/toggles fetch NON-blocking
      │   │   — this ensures the backend state is already synced when the user
      │   │   first opens the Toggles tab, even after HMR reload. If it fails,
      │   │   the banner is shown; if it succeeds, the tab opens instantly.
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
onChange handler invokes switchScenario(newScenarioName):

  STEP 0 — Mutex check (last-click-wins policy):
    if (switchInProgressController !== null) {
      // A switch is already running. Cancel it by aborting its controller.
      // The running switch will observe the abort in STEP 4 and return early.
      switchInProgressController.abort()
    }
    switchInProgressController = new AbortController()
    const { signal } = switchInProgressController

  STEP 1 — Begin switch (block handlers):
    scenarioStore.beginSwitch()   // sets isSwitching=true
    All MSW handlers that read scenario data check isSwitching and return 503:
      if (scenarioStore.getState().isSwitching) {
        return HttpResponse.json({ error: 'scenario-switching' }, { status: 503 })
      }

  STEP 2 — Cancel in-flight queries:
    queryClient.cancelQueries({ type: 'active' })

  STEP 3 — Close SSE streams via CustomEvent:
    window.dispatchEvent(new CustomEvent('meepledev:scenario-switch-begin'))
    // SSE consumer hooks in the app listen for this event and close their
    // EventSource instances. Zero coupling: the app code does NOT import
    // anything from dev-tools; only the pre-existing chat SSE hook gains
    // one extra useEffect listener (guarded by NODE_ENV check so it's
    // dead-code eliminated in prod).

  STEP 4 — Load new scenario:
    if (signal.aborted) return  // A newer switch has taken over
    const newScenario = SCENARIO_MANIFEST[newScenarioName] as Scenario
    const result = validateScenario(newScenario)
    if (!result.valid) {
      toast.error('Scenario invalid')
      return  // finally block will cleanup
    }
    scenarioStore.loadScenario(newScenario)
    mockAuthStore.reset(newScenario.auth.currentUser, newScenario.auth.availableUsers)

  STEP 5 — End switch (unblock handlers):
    scenarioStore.endSwitch()  // sets isSwitching=false

  STEP 6 — Invalidate and refetch:
    if (signal.aborted) return
    queryClient.invalidateQueries()

  FINALLY — Clear mutex (only if this invocation is the current one):
    if (switchInProgressController?.signal === signal) {
      switchInProgressController = null
    }

Total time: target ≤ 300ms p95 (SC2-2)
```

**Concurrency policy**: **last-click-wins**. Se l'utente clicca scenario A, poi B prima che A completi, A viene abortito e B prende il controllo. Scelta rationale: dev environment, user aspettativa = "l'ultimo click è quello che voglio".

**Timeout safety**: il protocol è wrapped in `try/finally` — se qualcosa lancia tra step 1 e 5, `endSwitch()` + clear mutex vengono comunque chiamati nel `finally`. Inoltre un `setTimeout(2000)` watchdog force-abort e mostra toast rosso.

**Mutations in-flight (N2-2)**: `queryClient.cancelQueries()` in STEP 2 cancella solo **queries**, non **mutations**. Se un `useMutation` è in volo (es. POST /games in progress), procede normalmente — ma la response refererà a dati del vecchio scenario. Il codice del protocol logga un warning se rileva mutations pending:
```typescript
const pendingMutations = queryClient.getMutationCache().getAll()
  .filter(m => m.state.status === 'pending')
if (pendingMutations.length > 0) {
  console.warn('[MeepleDev] Mutations in flight during scenario switch:', pendingMutations.length)
}
```
Il dev è responsabile di attendere il completamento prima di switchare se vuole semantica esatta.

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

### 3.Z Executable scenarios (Given/When/Then)

I seguenti scenari diventano direttamente E2E test Playwright (`apps/web/e2e/dev-loop/`). Sono il criterio di accettazione formale per le feature Phase 2.

```gherkin
Feature: Runtime toggle backend service
  Scenario: Switch LLM from mock to real during active session
    Given `make dev-fast-api` is running with MOCK_LLM=true
    And the Dev Panel is open via Ctrl+Shift+M
    And the Toggles tab is active
    When the user clicks the LLM switch to OFF
    Then within 50ms the PATCH response is 200 and the switch UI reflects OFF
    And GET /api/_meepledev/toggles returns llm=false
    When the user sends POST /api/v1/chat {"message":"hi"}
    Then the response header "X-Meeple-Mock: backend-di:..." does NOT contain "llm"
    And the chat response body is from the real LLM provider (verified by absence of "[mock-llm]" prefix)

Feature: Scenario switch preserves navigation context
  Scenario: Switch scenario while on /library page
    Given `make dev-fast` is running with NEXT_PUBLIC_DEV_SCENARIO=small-library
    And the user is on the /library page showing 3 games
    When the user opens the Dev Panel and switches scenario to "admin-busy"
    Then within 300ms the /library page shows 20 games
    And the URL remains "/library" (no navigation triggered)
    And no unhandled promise rejection appears in console
    And no "scenario-switching" 503 errors are visible to the user

Feature: Concurrent scenario switches converge to final selection
  Scenario: Rapid scenario switching (stress)
    Given the Dev Panel is open on the Scenarios tab
    When the user selects "empty", then immediately "admin-busy", then "small-library" within 500ms
    Then the final scenarioStore.scenario.name equals "small-library"
    And scenarioStore.isSwitching is false within 1s
    And queryClient has zero in-flight queries
    And the UI shows exactly 3 games (from small-library seed)
    And no stale data from intermediate scenarios appears

Feature: Role switch during active form input
  Scenario: Switch role while editing a form
    Given the user is Admin editing a game form with unsaved changes
    When the user switches role to User via Dev Panel
    Then within 200ms the admin route unmounts (redirect or 403)
    And a toast appears: "Role changed to User"
    And the previous form state is discarded (not persisted in sessionStorage)

Feature: Inspector reflects toggle change within next request
  Scenario: Inspector updates after toggle switch
    Given the Dev Panel is open on the Inspector tab
    And the app has made at least 3 fetch calls (seeded history)
    When the user switches to the Toggles tab, disables LLM, switches back to Inspector
    And triggers a fresh POST /api/v1/chat by interacting with the chat UI
    Then a new row appears in the Inspector table within 500ms
    And the new row has method=POST, status=200
    And the new row's mockSource does NOT contain "llm"
    And the inspector does NOT show any row for /api/_meepledev/toggles (internal calls skipped)

Feature: Panel graceful degradation when backend is down
  Scenario: Open panel with backend unreachable
    Given `make dev-fast` is running WITHOUT backend (DEV_BACKEND=false)
    When the user opens the Dev Panel via Ctrl+Shift+M
    Then the Toggles tab is visible
    And the MSW groups section is fully functional (13 switches)
    And the Backend services section shows a red banner "Backend unreachable"
    And no console error is visible (error logged at warn level only)
    And clicking Retry in the banner attempts a new GET /api/_meepledev/toggles

Feature: Release build excludes DevTools HTTP surface
  Scenario: Verify prod build has no dev endpoints
    Given the Api.dll is built in Release configuration
    When reflection enumerates all types in the assembly
    Then no type exists in the namespace "Api.DevTools.Http"
    And no method "MapMeepleDevTools" is discoverable
    When the app starts in Release with ASPNETCORE_ENVIRONMENT=Development
    Then GET /dev/toggles returns 404
    And PATCH /dev/toggles returns 404
```

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

const BASE_URL = '/api/_meepledev';
const INTERNAL_HEADER = { 'X-Meepledev-Internal': '1' } as const;
const TIMEOUT_MS = 5000;

// Pure function module — no class, no singleton, no state.
export async function getToggles(): Promise<BackendTogglesState> { /* ... */ }
export async function patchToggles(toggles: Record<string, boolean>): Promise<PatchTogglesResponse> { /* ... */ }
export async function resetToggles(): Promise<BackendTogglesState> { /* ... */ }

// Aggregate export for convenience
export const devPanelClient = { getToggles, patchToggles, resetToggles } as const;
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

CQS split: query e mutation hooks separati per evitare coupling rendering/mutation e semplificare testing isolato.

```typescript
/** Read-only query hook. Components that only display state use this. */
export function useBackendTogglesQuery(): {
  toggles: Record<string, boolean>;
  knownServices: string[];
  isLoading: boolean;
  error: DevPanelClientError | null;
  refetch: () => Promise<void>;
};

/** Write-only mutation hook. Components that mutate state use this. */
export function useBackendTogglesMutation(): {
  setToggle: (name: string, value: boolean) => Promise<void>;
  resetAll: () => Promise<void>;
  isMutating: boolean;
  mutationError: DevPanelClientError | null;
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

**Debounce + lock (mutation hook internals)**:

```typescript
const inFlightToggles = useRef<Set<string>>(new Set());

async function setToggle(name: string, value: boolean) {
  if (inFlightToggles.current.has(name)) return; // ignore concurrent click
  inFlightToggles.current.add(name);
  try {
    // Optimistic update via query cache
    await devPanelClient.patchToggles({ [name]: value });
  } catch (err) {
    // Rollback handled in query cache via refetch
    throw err;
  } finally {
    inFlightToggles.current.delete(name);
  }
}
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
- **Conflict**: collision test manuale richiesto durante M0 (Chrome/Firefox/Safari + macOS/Linux/Windows). Eventuali collision documentate in `panel/README.md`. Fallback universale: badge click o `?devpanel=1` URL param (sempre funziona)

---

## 5. Error Handling & Safety

### 5.1 Failure modes e gestione

| Failure | Detection | Behavior | Fallback |
|---|---|---|---|
| **Backend down** durante `GET /dev/toggles` | `devPanelClient` fetch error | Banner rosso "Backend unreachable — showing FE toggles only"; exponential backoff retry: **10s, 30s, 60s, 300s**, poi stop. Manual "Retry" button nel banner forza retry immediato. | MSW toggles funzionanti. Backend list nascosta. |
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
| **Multi-dev shared backend via `make integration`** | No detection automatica | `CLAUDE.md` warning section: "il backend state è shared, cambi sono visibili a tutti i dev" (H2-2 doc fix) | Accettabile, dev responsibility |
| **Panel health not visible from DevBadge** | N/A | DevBadge mostra indicatore rosso se `useBackendTogglesQuery.error != null`, tooltip "Dev Panel backend unreachable" (H2-4) | DevBadge pattern già presente in P1, estensione minimale |
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
| `fetchInterceptor.test.ts` | 8+ test cases: (1) monkey-patch applicato una volta (ref sentinel), (2) double install idempotent, (3) skip header `X-Meepledev-Internal`, (4) headers normalization (Headers vs object vs array), (5) metadata corretta, (6) AbortError handling preserva eccezione, (7) status 0 su network error, (8) context `this=window` preservato nel chiamare originalFetch, (9) restore `window.fetch` su `uninstall()` |
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
| `NoMockInReleaseTests.cs` (esistente P1, estendere) | **(C2-1 FIX)** Aggiungere test `ReleaseAssembly_HasNoDevToolsHttpTypes()` che verifica via reflection: zero `Api.DevTools.Http.*` tipi presenti in `Api.dll` built in Release config. Inoltre aggiungere `DevToolsEndpoints_NotRegisteredInRelease()` che boota WebApplicationFactory con `UseEnvironment("Development")` + `Configuration=Release` e asserta GET/PATCH/POST `/dev/toggles` → 404 |

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

#### `toggle-runtime.spec.ts` (SC2-1a + SC2-1b)

```typescript
test('backend LLM toggle switches runtime — PATCH round-trip + correctness', async ({ page, request }) => {
  await page.goto('/?devpanel=1');
  await page.click('[data-testid="panel-tab-toggles"]');

  // Warm up: first click (not measured) to prime caches
  await page.click('[data-testid="toggle-llm"]');
  await page.waitForSelector('[data-testid="toggle-llm"][data-state="off"]');
  await page.click('[data-testid="toggle-llm"]');  // back to on
  await page.waitForSelector('[data-testid="toggle-llm"][data-state="on"]');

  // Measurement: median of 5 runs for latency robustness
  const timings: number[] = [];
  for (let i = 0; i < 5; i++) {
    const target = i % 2 === 0 ? 'off' : 'on';
    const start = performance.now();
    await page.click('[data-testid="toggle-llm"]');
    await page.waitForSelector(`[data-testid="toggle-llm"][data-state="${target}"]`);
    timings.push(performance.now() - start);
  }
  const sorted = [...timings].sort((a, b) => a - b);
  const median = sorted[Math.floor(sorted.length / 2)];

  // SC2-1a: PATCH round-trip p95 ≤ 50ms. In CI (shared runner), tolerance is
  // 3x for flakiness. Local dev should hit ≤50ms easily.
  const budget = process.env.CI ? 150 : 55;
  expect(median).toBeLessThanOrEqual(budget);

  // SC2-1b: Correctness — first request AFTER the PATCH must reflect new state
  // Ensure final state is off
  const finalState = await request.get('/api/_meepledev/toggles');
  expect((await finalState.json()).toggles.llm).toBe(false);

  // Trigger a chat request (or any endpoint that uses ILlmService)
  // Via the app UI or direct backend call
  const chatRes = await request.post('/api/v1/chat', {
    data: { message: 'test' },
  });
  const mockHeader = chatRes.headers()['x-meeple-mock'] ?? '';
  // llm should NOT be in the mock list
  expect(mockHeader).not.toContain('llm');
});
```

**Flakiness strategy** (applied to all E2E latency tests):
1. **Warm-up run**: first iteration is discarded
2. **Median of N runs**: N=5 for latency, use median to reduce spike impact
3. **CI budget**: env variable `CI=true` → 3x local budget (es. local 50ms → CI 150ms)
4. **Functional assertions always strict**: only latency assertions are relaxed in CI

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

#### `scenario-switch-race.spec.ts` (A2-2 FIX — rapid switch stress)

```typescript
test('rapid scenario switching converges to final selection', async ({ page }) => {
  await page.goto('/library?devpanel=1');
  await page.click('[data-testid="panel-tab-scenarios"]');

  // Fire 3 switches in <500ms (last-click-wins policy)
  await page.selectOption('[data-testid="scenario-select"]', 'empty');
  await page.selectOption('[data-testid="scenario-select"]', 'admin-busy');
  await page.selectOption('[data-testid="scenario-select"]', 'small-library');

  // Wait for final state
  await page.waitForSelector('[data-testid="game-card"]');
  expect(await page.locator('[data-testid="game-card"]').count()).toBe(3);

  // Verify no leftover isSwitching flag
  const isSwitching = await page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return (window as any).__meepledev__?.scenarioStore?.getState().isSwitching ?? null;
  });
  expect(isSwitching).toBe(false);

  // Verify no corruption: exactly the small-library games
  const titles = await page.locator('[data-testid="game-title"]').allTextContents();
  expect(titles).toEqual(expect.arrayContaining(['Wingspan', 'Scythe', 'Terraforming Mars']));
});
```

#### `devpanel-a11y.spec.ts` (W2-3 FIX — a11y smoke)

```typescript
import AxeBuilder from '@axe-core/playwright';

test('dev panel has no critical a11y violations', async ({ page }) => {
  await page.goto('/?devpanel=1');
  await page.waitForSelector('[data-testid="dev-panel"]');

  const results = await new AxeBuilder({ page })
    .include('[data-testid="dev-panel"]')
    .withTags(['wcag2a', 'wcag2aa'])
    .analyze();

  const critical = results.violations.filter(v => v.impact === 'critical');
  expect(critical).toEqual([]);
});

test('Escape closes the panel', async ({ page }) => {
  await page.goto('/?devpanel=1');
  await page.waitForSelector('[data-testid="dev-panel"]');
  await page.keyboard.press('Escape');
  await expect(page.locator('[data-testid="dev-panel"]')).not.toBeVisible();
});

test('ArrowRight on tablist moves focus between tabs', async ({ page }) => {
  await page.goto('/?devpanel=1');
  await page.locator('[data-testid="panel-tab-toggles"]').focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('[data-testid="panel-tab-scenarios"]')).toBeFocused();
});
```

### 6.7 Benchmark test (obbligatorio, CI-enforced)

**NFR SC2-5**: overhead fetch interceptor ≤ 5ms p95.

Vitest bench con asserzione hard-fail:

```typescript
// apps/web/__tests__/bench/fetchInterceptor.bench.ts
import { bench, describe, expect } from 'vitest';
import { installFetchInterceptor, uninstallFetchInterceptor } from '@/dev-tools/panel/hooks/useFetchInterceptor';

describe('fetch interceptor overhead', () => {
  const N = 1000;

  bench('baseline (no interceptor)', async () => {
    const t0 = performance.now();
    for (let i = 0; i < N; i++) {
      await fetch('data:application/json,{}');
    }
    const delta = (performance.now() - t0) / N;
    // baseline recorded in snapshot file
  });

  bench('instrumented (with interceptor)', async () => {
    installFetchInterceptor();
    try {
      const t0 = performance.now();
      for (let i = 0; i < N; i++) {
        await fetch('data:application/json,{}');
      }
      const delta = (performance.now() - t0) / N;
      // Compare to baseline; assert delta ≤ 5ms
    } finally {
      uninstallFetchInterceptor();
    }
  });
});
```

**CI wiring** (M4 deliverable):
```yaml
# .github/workflows/bench-phase2.yml
- name: Run fetch interceptor benchmark
  working-directory: apps/web
  run: pnpm bench fetchInterceptor
  env:
    BENCH_FAIL_ON_REGRESSION: true
```

Se `delta p95 > 5ms`, il job fallisce. Baseline aggiornato manualmente in PR dedicate (stesso pattern di `bundle-size-baseline.json`).

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
- **(C2-5 FIX)** Verifica dipendenza toast library: `grep sonner apps/web/package.json` — se non presente, aggiungi `pnpm add sonner` come primo step di M0
- `apps/web/src/dev-tools/panel/` subfolder con `index.ts`, `README.md`, `DevPanel.tsx` minimal, `stores/panelUiStore.ts`, `hooks/useKeyboardShortcut.ts`, `hooks/useQueryStringPanelOpen.ts`
- `apps/api/src/Api/DevTools/Http/DevToolsEndpoints.cs` con GET/PATCH/POST/reset
- `apps/api/src/Api/DevTools/Http/DevToggleDtos.cs`
- `apps/api/src/Api/DevTools/Http/DevToolsEndpointsExtensions.cs`
- `MockToggleStateProvider.ResetToDefaults()` + test
- `Program.cs` wiring (`MapMeepleDevTools()`)
- `install.ts` modificato per dynamic import `panel/` + wire shortcut + **initial `getToggles()` fetch** (H2-1)
- `mock-provider.tsx` modificato per mount DevPanel
- `devPanelClient.ts` completo (pure functions)
- `scenarioSwitcher.ts` (a livello `dev-tools/` fuori da `panel/`) — mutex + anti-race protocol (stub vuoto in M0, implementation in M2)
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
2. **ErrorBoundary implementation** — shadcn/ui ha un componente o serve custom? Verificare in M0
3. **Scenario MSW handler 503 guard** — quali handler specifici toccare (games, chat, library, sessions?). Decisione in M2
4. **Inspector row expand** — come renderizzare header list compatta (JSON tree vs flat list?). Decisione in M3

*(Decisioni originali su Toast library, DevPanelClient class vs functions, sonner, keyboard collision, a11y: tutte risolte nel spec review v2)*
