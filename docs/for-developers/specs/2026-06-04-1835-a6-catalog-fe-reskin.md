# SP5 F4-A6 — `/admin/catalog-ingestion` re-skin (SyncStatusHero + SyncRunTimeline + LogStream)

**Issue**: [#1835](https://github.com/meepleAi-app/meepleai-monorepo/issues/1835) · **Parent epic**: [#1833](https://github.com/meepleAi-app/meepleai-monorepo/issues/1833) F4 Ondata Ops · **Date**: 2026-06-04

## Goal

Re-skin completo della pagina `/admin/catalog-ingestion` secondo il mockup `admin-mockups/design_handoff_admin/admin/sp5-admin-catalog.html`, consumando i 4 endpoint introdotti dal merge BE foundation [#1861](https://github.com/meepleAi-app/meepleai-monorepo/issues/1861) (PR #1865). I 3 tab esistenti (Import/Enrichment/Export) vengono **accorpati sotto un Provider dropdown** del nuovo `SyncStatusHero`, preservando i flussi funzionali esistenti come modali on-demand.

## Context — cosa è già fatto

**BE foundation #1861 (merged 2026-06-03)**:
- Aggregate `CatalogSyncRun` in BC `SharedGameCatalog` (29 unit + 10 IT + 17 unit handlers + 12 IT endpoint + 3 IT cron = 71 verdi)
- 4 endpoint mappati in `apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs:236-334`:
  ```
  GET    /api/v1/admin/catalog-ingestion/status                → CatalogSyncStatusResult
  GET    /api/v1/admin/catalog-ingestion/runs?page&pageSize    → PagedCatalogSyncRunsResult
  GET    /api/v1/admin/catalog-ingestion/runs/{id}/logs?tail=N → LogsResult | 404
  POST   /api/v1/admin/catalog-ingestion/trigger               → 202 + Location | 409
  ```
- Status enum DTO `"running" | "idle" | "never_run"` (NO `"failed"` globale)
- `NextScheduled` sempre `null` finché Phase 5 cron hook lifecycle completato (follow-up tracciato)
- `LogTailJsonPath` file system locale `data/catalog-sync-logs/{runId}.log`, gestione graceful in `GetCatalogSyncRunLogsQueryHandler` quando assente
- Cron service opt-in (`CatalogSyncCron:Enabled=false` default)

**FE esistente (`apps/web/src/app/admin/(dashboard)/catalog-ingestion/`)**:
- `page.tsx` (55 righe) — 3 tab Import/Enrichment/Export
- `components/ExcelImportTab.tsx` — preview + confirm flow (Excel bulk import)
- `components/EnrichmentQueueTab.tsx` — coda enrichment esistente
- `components/ExportTab.tsx` — download `/excel-export` catalog
- `lib/catalog-ingestion-api.ts` — fetcher esistenti per 7 endpoint Excel/BGG

## Scope — cosa aggiungere

### Componenti nuovi (5)

#### 1. `SyncStatusHero.tsx`

Hero card a 2 colonne (info sinistra, controls destra) con:

**Status chip 4-stati FE-derived** (decisione spec-panel review):
| Condizione BE | Chip mostrato |
|---|---|
| `status=="running"` | 🟠 **Running** (animato, pulse) |
| `status=="idle"` && `lastRun.status=="Success"` | 🟢 **Idle** |
| `status=="idle"` && `lastRun.status ∈ {Failed, TimedOut}` | 🔴 **Last sync failed** + badge `errorCode` |
| `status=="never_run"` | ⚪ **Setup** (no data) |

**Stats line** (mono font, color-muted):
- `Ultima sync: <relative-time> · <abs-time>` (oppure "Mai eseguita" se never_run)
- `Giochi importati totali: <cumulative.gamesTotal>`
- `Next scheduled: <next>` — **hidden se `nextScheduled==null`** (graceful degradation post-#1861)
- `Provider: <activeProvider>` (es. "BGG API v2")

**Provider config controls** (right column):
- `Provider` dropdown — opzioni: `BGG API v2` (default) / `CSV import` / `Manual`
- Config sezione visibile **solo se `provider=="BGG API v2"`**:
  - `Batch size` input (default 100)
  - `Rate limit` input (default `60/min`)
  - `Auto-retry` toggle (default on)
- Per `CSV import`: nessun config, click `Run sync now` → apri modale `ExcelImportTab`
- Per `Manual`: nessun config, click `Run sync now` → apri modale `AssignBggIdForm` (input game + bggId)
- `▶ Run sync now` button — disabilitato se `status=="running"` (tooltip: "Sync già in corso")

**Trigger flow (BGG provider)**:
1. Click `Run sync now` → POST `/trigger { provider: "BggApi" }`
2. Su 202 Accepted: toast "Sync queued" + button spinner state
3. Polling `/status` (5s) attende transition `idle→running`
4. Quando transition occurs: chip aggiorna `🟠 Running`, button torna normale

#### 2. `SyncRunTimeline.tsx`

Pannello "Sync history · ultime 12 run" con grid table:

**Layout colonne** (mockup riga 75-77, grid `32px 1fr 90px 60px 60px 60px 24px`):
```
[status-dot] [Run title + ts/by] [duration mono] [+add green] [~upd blue] [×fail red] [›]
```

**Status dot color** (mockup riga 22-25):
- `Success` / `Idle` → emerald (toolkit token)
- `Failed` / `TimedOut` → rose (event token) + bg tint `hsl(var(--c-event) / .04)`
- `Running` → blue (kb token) + animation `admin-pulse 2s infinite`

**Header row** (sub-meta uppercase mono, mockup riga 75): `Run · Durata · +add · ~upd · ×fail`

**Pagination**: default `page=1&pageSize=12` (mockup mostra 12). Footer button "Load more" → `page=2`.

**Click `›`** su una row → seleziona run + apre `LogStream` drill-down (slide-in pane sotto o accanto).

**Success rate calc** (header meta): `success runs / total * 100` calcolato dal batch ricevuto, mostrato come `success rate 91.7%`.

#### 3. `LogStream.tsx`

Pane drill-down logs per run selezionata, **load on demand** (no fetch finché non selezionato).

**Fetch trigger**: `useCatalogSyncRunLogs(runId)` quando `runId !== null`.

**Layout**:
- Header: `<runTitle> · <runStartedAt>` + close button (×)
- Status badge primario (Success/Failed/TimedOut) + errorCode + errorDetail se Failed
- Pre-formatted block `<pre>` con log lines (font-mono, max-height 400px scroll)
- Empty state: "Logs not available" se `logsAvailable===false`
- 404: "Run not found"

**Default tail**: `tail=100`. Se BE supporta paginazione futura, aggiungere "Load more" button.

#### 4. `QueuePendingPanel.tsx` (sub-panel, MVP placeholder)

Pannello "⏳ Queue pending re-sync" (mockup colonna sinistra, riga 136-148).

**MVP scope (questa issue)**: placeholder con messaggio:
```
"Queue pending: feature in arrivo (BE #1874)"
```
+ link a #1874.

**Post-#1874 wire** (follow-up):
- `GET /enrichment-queue?limit=25` → list items con priority badge + queuedAt
- Footer buttons: `▶ Run pending now` + `⏰ Schedule`

#### 5. `FailedItemsPanel.tsx` (sub-panel, MVP placeholder)

Pannello "✕ Failed items (last 30gg)" (mockup colonna destra, riga 150-168).

**MVP scope (questa issue)**: placeholder con messaggio:
```
"Failed items: feature in arrivo (BE #1874)"
```
+ link a #1874.

**Post-#1874 wire** (follow-up):
- `GET /failed-items?days=30&limit=50` → list items con errorCode left-border-3px rose
- Per-item retry button → `POST /enqueue-enrichment { sharedGameIds: [id] }`
- Footer: `↻ Retry bulk` + `⤓ Export errors`

### Hooks nuovi (4)

#### `use-document-visibility.ts`

Primitive reusable cross-screen admin. Wrappa `document.visibilitychange` event:
```ts
export function useDocumentVisibility(): boolean {
  const [isVisible, setIsVisible] = useState(
    typeof document === 'undefined' || !document.hidden
  );
  useEffect(() => {
    const onVisibilityChange = () => setIsVisible(!document.hidden);
    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, []);
  return isVisible;
}
```

#### `use-catalog-sync-status.ts`

Polling + visibility guard + transition hook:
```ts
export function useCatalogSyncStatus() {
  const isVisible = useDocumentVisibility();
  const queryClient = useQueryClient();
  const previousStatusRef = useRef<string | null>(null);

  const query = useQuery({
    queryKey: ['catalog-sync-status'],
    queryFn: () => fetchCatalogSyncStatus(),
    refetchInterval: (data) =>
      isVisible && data?.status === 'running' ? 5000 : false,
  });

  useEffect(() => {
    const current = query.data?.status;
    if (previousStatusRef.current === 'running' && current === 'idle') {
      queryClient.invalidateQueries({ queryKey: ['catalog-sync-runs'] });
    }
    previousStatusRef.current = current ?? null;
  }, [query.data?.status, queryClient]);

  return query;
}
```

#### `use-catalog-sync-runs.ts`

Pagination con `useQuery` standard:
```ts
export function useCatalogSyncRuns(page = 1, pageSize = 12) {
  return useQuery({
    queryKey: ['catalog-sync-runs', page, pageSize],
    queryFn: () => fetchCatalogSyncRuns({ page, pageSize }),
    placeholderData: keepPreviousData,
  });
}
```

#### `use-catalog-sync-run-logs.ts`

Lazy on-demand fetch when `runId` selezionato:
```ts
export function useCatalogSyncRunLogs(runId: string | null, tail = 100) {
  return useQuery({
    queryKey: ['catalog-sync-run-logs', runId, tail],
    queryFn: () => fetchCatalogSyncRunLogs(runId!, tail),
    enabled: runId !== null,
  });
}
```

### Utils nuovi (2)

#### `_utils/status-mapper.ts`

FE-side derivation 4-stati chip + presentation:
```ts
export type ChipState = 'running' | 'healthy' | 'degraded' | 'setup';

export function deriveChipState(
  status: 'running' | 'idle' | 'never_run',
  lastRunStatus: 'Success' | 'Failed' | 'TimedOut' | null
): ChipState {
  if (status === 'running') return 'running';
  if (status === 'never_run') return 'setup';
  if (lastRunStatus === 'Failed' || lastRunStatus === 'TimedOut') return 'degraded';
  return 'healthy';
}

export const chipPresentation: Record<ChipState, { color: string; icon: string; label: string }> = {
  running:  { color: 'amber-500', icon: 'pulse', label: 'Running' },
  healthy:  { color: 'toolkit',   icon: 'check', label: 'Idle' },
  degraded: { color: 'event',     icon: 'alert', label: 'Last sync failed' },
  setup:    { color: 'muted',     icon: 'circle-dashed', label: 'Setup' },
};
```

#### `_utils/run-formatter.ts`

Duration + timestamp formatting:
```ts
export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  const m = Math.floor(ms / 60000);
  const s = Math.floor((ms % 60000) / 1000);
  return `${m}m ${s.toString().padStart(2, '0')}s`;
}

export function formatRelativeTime(iso: string, now = new Date()): string {
  const diff = (now.getTime() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return `${Math.floor(diff)}s fa`;
  if (diff < 3600) return `${Math.floor(diff / 60)} min fa`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h fa`;
  return `${Math.floor(diff / 86400)}gg fa`;
}
```

### API client estensione

`lib/catalog-ingestion-api.ts` aggiunge 4 fetcher:
- `fetchCatalogSyncStatus()` → `Promise<CatalogSyncStatusResponse>`
- `fetchCatalogSyncRuns({ page, pageSize })` → `Promise<PagedRunsResponse>`
- `fetchCatalogSyncRunLogs(runId, tail)` → `Promise<RunLogsResponse | null>` (null su 404)
- `triggerCatalogSync(provider)` → `Promise<TriggerResponse>` (200 ok, throws on 409)

### Page refactor (`page.tsx`)

Da 3-tab → struttura mockup:
```tsx
export default function CatalogIngestionPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Catalog ingestion" 
                  subtitle="Admin · Catalog · BoardGameGeek sync"
                  actions={<ExportCatalogButton />} />
      <SyncStatusHero />
      <SyncRunTimeline />
      <div className="grid grid-cols-2 gap-3.5">
        <QueuePendingPanel />
        <FailedItemsPanel />
      </div>
      {/* Modals (opened by SyncStatusHero based on Provider dropdown) */}
      <CsvImportModal />        {/* wraps ExcelImportTab */}
      <ManualAssignModal />     {/* wraps AssignBggIdForm */}
      <LogStreamDrawer />       {/* opened by SyncRunTimeline row click */}
    </div>
  );
}
```

## Acceptance Criteria

### Scenario A — Status hero idle (lastRun Success)

```gherkin
Given GET /status returns { status: "idle", lastRun: { status: "Success", completedAt: "14 min ago" }, cumulative: { gamesTotal: 4812 }, nextScheduled: null }
When admin opens /admin/catalog-ingestion
Then SyncStatusHero chip shows 🟢 "Idle" emerald
And stats show "Ultima sync: 14 min fa · 2026-06-04 14:08"
And stats show "Giochi importati totali: 4.812"
And "Next scheduled" row is HIDDEN (nextScheduled is null)
And button "Run sync now" enabled
```

### Scenario B — Status hero running (polling active)

```gherkin
Given GET /status returns { status: "running", currentRun: { id, startedAt: 2min ago } }
And tab is visible (document.hidden === false)
When admin opens /admin/catalog-ingestion
Then SyncStatusHero chip shows 🟠 "Running" pulse animation
And button "Run sync now" disabled with tooltip "Sync già in corso"
And useQuery refetchInterval is 5000ms
And SyncRunTimeline top row has status-dot animated running

Given tab becomes hidden (document.hidden === true)
When 5 seconds pass
Then refetchInterval becomes false (no polling while hidden)

Given tab becomes visible again
When status transitions from "running" to "idle"
Then queryClient invalidates ['catalog-sync-runs'] cache
And SyncRunTimeline auto-refreshes with new completed run
```

### Scenario C — Status hero idle with last failed (BLOCKER-1 resolution)

```gherkin
Given GET /status returns { status: "idle", lastRun: { status: "Failed", errorCode: "BGG_API_RATE_LIMIT_429", completedAt: "14 min ago" } }
When admin opens /admin/catalog-ingestion
Then SyncStatusHero chip shows 🔴 "Last sync failed" rose
And errorCode badge "BGG_API_RATE_LIMIT_429" visible under chip
And button "Run sync now" ENABLED (retry consentito)
```

### Scenario D — Trigger BGG provider

```gherkin
Given status idle, provider dropdown "BGG API v2" selected
When admin clicks "Run sync now"
Then POST /trigger { provider: "BggApi" } sent
And on 202 response: toast "Sync queued" appears
And button shows spinner state
And useCatalogSyncStatus polls every 5s

When next /status poll returns status="running"
Then chip transitions to 🟠 Running
And button returns to normal disabled-with-tooltip state
And toast disappears

When /trigger returns 409 (sync already running race)
Then toast error "Sync già in corso (run X)"
And no UI status change
```

### Scenario E — Provider switch CSV

```gherkin
Given admin selects provider "CSV import" from dropdown
Then Batch size / Rate limit / Auto-retry config controls HIDDEN
When admin clicks "Run sync now"
Then NO POST /trigger sent
And CsvImportModal opens with ExcelImportTab content (preview + confirm flow)
```

### Scenario F — Provider switch Manual

```gherkin
Given admin selects provider "Manual" from dropdown
Then Batch size / Rate limit / Auto-retry config controls HIDDEN
When admin clicks "Run sync now"
Then NO POST /trigger sent
And ManualAssignModal opens with AssignBggIdForm (input game name + bggId)
On submit: POST /assign-bgg-id called
```

### Scenario G — Timeline render with mixed runs

```gherkin
Given GET /runs?page=1&pageSize=12 returns 12 runs: 11 Success, 1 Failed
When SyncRunTimeline renders
Then header shows "Sync history · ultime 12 run" + "success rate 91.7%"
And 12 rows displayed with grid layout (32px dot / title / duration / +add / ~upd / ×fail / ›)
And success runs have emerald status-dot
And failed run has rose status-dot + bg tint hsl(var(--c-event) / .04)
And running run (if any) has blue pulsing dot
```

### Scenario H — Timeline drill-down logs

```gherkin
Given timeline shows 12 runs, run R-failed visible with errorCode "BGG_API_RATE_LIMIT_429"
When admin clicks "›" on R-failed
Then LogStream drawer/pane opens
And useCatalogSyncRunLogs(R-failed.id, 100) triggered
And on response: <pre> shows tail-100 log lines
And errorCode + errorDetail visible in header
And status badge "Failed" rose color

When R-failed.id has no log file (logsAvailable === false)
Then "Logs not available" message shown

When admin clicks "›" on non-existent runId (404 from BE)
Then "Run not found" message shown
```

### Scenario I — Export catalog CTA (GAP-4 resolution)

```gherkin
Given admin top-bar shows "⤓ Export catalog" button (RENAMED from "Export history")
When admin clicks button
Then GET /excel-export downloads catalog-export.xlsx
And no run-history endpoint called (semantic match: catalog snapshot, not run log)
```

### Scenario J — Idle with last run failed (BLOCKER-1 explicit AC)

```gherkin
Given last run R-failed completed 14 min ago, no running sync
When admin opens /admin/catalog-ingestion
Then SyncStatusHero shows 🔴 "Last sync failed" chip (NOT green)
And button "Run sync now" enabled (no block on prior failure)
And SyncRunTimeline top row has rose status-dot
```

### Scenario K — QueuePendingPanel + FailedItemsPanel placeholder MVP

```gherkin
Given BE issue #1874 not yet merged (no /enrichment-queue + /failed-items endpoints)
When admin opens /admin/catalog-ingestion
Then QueuePendingPanel shows placeholder message "Queue pending: feature in arrivo (BE #1874)"
And FailedItemsPanel shows placeholder "Failed items: feature in arrivo (BE #1874)"
And both panels link to GitHub issue #1874

Post #1874 merge (FE follow-up):
Then panels wire to real endpoints (NOT in this issue scope)
```

## Mockup parity matrix

| Mockup element (riga) | Implementation | Status MVP |
|---|---|---|
| `.catalog-hero` 2-col (49-67) | `SyncStatusHero.tsx` | ✅ in scope |
| Status chip (51) | `chipPresentation` 4-state | ✅ derived FE |
| Stats line (53-58) | `SyncStatusHero stats` | ✅ + nextScheduled hide if null |
| Provider config controls (60-66) | `SyncStatusHero controls` | ✅ visible solo BGG |
| `.admin-panel` Sync history (69-133) | `SyncRunTimeline.tsx` | ✅ in scope |
| Row drill-down `›` (riga 85) | `LogStream.tsx` drawer | ✅ in scope |
| Queue pending re-sync panel (136-148) | `QueuePendingPanel.tsx` | ⚠️ placeholder MVP, full wire post-#1874 |
| Failed items panel (150-168) | `FailedItemsPanel.tsx` | ⚠️ placeholder MVP, full wire post-#1874 |
| "⤓ Export history" CTA (43) | `ExportCatalogButton` | ✅ renamed "Export catalog" → `/excel-export` |
| Bulk retry buttons (146, 166) | nel placeholder, follow-up | ❌ post-#1874 |

## Tech notes

**Token discipline (ESLint `local/no-hardcoded-color-utility`)**:
- Status-dot colors → semantic utility classes `bg-toolkit` / `bg-event` / `bg-kb` (token mapping in `_utils/status-mapper.ts`)
- NO `bg-emerald-500` / `bg-rose-500` literals
- Background tints → `bg-event/[0.04]` arbitrary opacity (consentito)

**Font choice** (mockup):
- Headings: `font-quicksand` (display)
- Stats / counts / durations: `font-mono` (JetBrains Mono)

**Accessibility**:
- Chip ha `role="status"` + `aria-live="polite"` per screen reader status updates
- `LogStream` ha `role="region" aria-label="Sync run logs"`
- Drill-down drawer trap focus + Escape close

**Performance**:
- `SyncRunTimeline` memoizzato (`React.memo` + deep equal su `runs[]`)
- Sparkline data NON necessario in MVP (no time-series sync KPI come #1837 KPISparkline)
- `useDocumentVisibility` listener attaccato 1 volta a mount

## References

- **Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-catalog.html` (175 righe)
- **BE foundation**: #1861 ✅ MERGED PR #1865 (5 commits, 71/71 verdi)
- **BE follow-up** (blocker panel full): #1874 OPEN (`enrichment_queue_entries` + `enrichment_attempts` ~14h BE)
- **Parent epic**: #1833 (F4 Ondata Ops sub-task tracker)
- **Sibling pattern** (analoga arch): #1837 C1 Infra MERGED PR #1872 (LiveEventLog + KPISparkline + use-infrastructure-kpis hook)
- **Spec consolidamento**: `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5 Gruppo A (riga A6)
- **Decisions discussion**: comment in issue #1835 dated 2026-06-04 (spec-panel review session)
