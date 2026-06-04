# #1835 A6 Catalog FE re-skin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-skin `/admin/catalog-ingestion` consumando endpoint #1861 (status/runs/logs/trigger) con SyncStatusHero + SyncRunTimeline + LogStream + 2 placeholder panel (Queue/Failed wired post-#1874).

**Architecture:** Pattern analogo a #1837 C1 Infra (LiveEventLog + KPISparkline + use-infrastructure-kpis). Polling + visibility guard + transition hook (no SSE in MVP). 4 nuovi React Query hooks + 2 utils + 5 nuovi componenti + refactor `page.tsx` da 3-tab a hero+timeline. Provider dropdown switching attiva modali per CSV/Manual (preserva flussi esistenti `ExcelImportTab` + `AssignBggId`).

**Tech Stack:** Next.js 16 App Router · React 19 · Zustand (`chat-panel-store` pattern) · React Query · Tailwind 4 (semantic tokens) · shadcn/ui · Vitest + Playwright

---

## File structure

### Files to CREATE

```
apps/web/src/app/admin/(dashboard)/catalog-ingestion/
├── _utils/
│   ├── status-mapper.ts                    # FE 4-state chip derivation
│   ├── status-mapper.test.ts
│   ├── run-formatter.ts                    # Duration + relative time formatters
│   └── run-formatter.test.ts
├── hooks/
│   ├── use-document-visibility.ts          # Reusable visibility primitive
│   ├── use-document-visibility.test.tsx
│   ├── use-catalog-sync-status.ts          # Poll + visibility + transition
│   ├── use-catalog-sync-status.test.tsx
│   ├── use-catalog-sync-runs.ts            # Pagination
│   ├── use-catalog-sync-runs.test.tsx
│   ├── use-catalog-sync-run-logs.ts        # Lazy on-demand
│   └── use-catalog-sync-run-logs.test.tsx
├── components/
│   ├── SyncStatusHero.tsx                  # Chip + stats + config + Run sync now
│   ├── SyncStatusHero.test.tsx
│   ├── SyncRunTimeline.tsx                 # Run rows + pagination + drill-down trigger
│   ├── SyncRunTimeline.test.tsx
│   ├── LogStream.tsx                       # Lazy logs drawer
│   ├── LogStream.test.tsx
│   ├── QueuePendingPanel.tsx               # Placeholder MVP -> #1874
│   ├── QueuePendingPanel.test.tsx
│   ├── FailedItemsPanel.tsx                # Placeholder MVP -> #1874
│   ├── FailedItemsPanel.test.tsx
│   ├── CsvImportModal.tsx                  # Wraps ExcelImportTab
│   ├── ManualAssignModal.tsx               # Wraps AssignBggIdForm (new)
│   ├── AssignBggIdForm.tsx                 # NEW form for Manual provider
│   ├── AssignBggIdForm.test.tsx
│   └── ExportCatalogButton.tsx             # Renamed CTA wired to /excel-export
└── __tests__/
    └── page.test.tsx                       # Page-level integration smoke

apps/web/e2e/admin/
└── catalog-ingestion-reskin.spec.ts        # E2E smoke (3 scenarios)
```

### Files to MODIFY

```
apps/web/src/app/admin/(dashboard)/catalog-ingestion/
├── lib/catalog-ingestion-api.ts            # Add 4 new fetchers
└── page.tsx                                # Refactor from 3-tab to hero+timeline+panels
```

### Files to PRESERVE (referenced, not modified)

```
apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/
├── ExcelImportTab.tsx                      # Wrapped by CsvImportModal
├── EnrichmentQueueTab.tsx                  # NOT used in MVP (orphan after refactor; cleanup later)
└── ExportTab.tsx                           # Replaced by ExportCatalogButton
```

---

## Phase 1 — Utils + primitive (foundation)

### Task 1: `status-mapper.ts` — derive 4-state chip

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/_utils/status-mapper.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/_utils/status-mapper.test.ts`

- [ ] **Step 1: Write failing test**

```ts
// status-mapper.test.ts
import { describe, expect, it } from 'vitest';
import { chipPresentation, deriveChipState } from './status-mapper';

describe('deriveChipState', () => {
  it('returns "running" when status is running regardless of lastRun', () => {
    expect(deriveChipState('running', 'Success')).toBe('running');
    expect(deriveChipState('running', 'Failed')).toBe('running');
    expect(deriveChipState('running', null)).toBe('running');
  });

  it('returns "setup" when status is never_run', () => {
    expect(deriveChipState('never_run', null)).toBe('setup');
  });

  it('returns "healthy" when idle with Success last run', () => {
    expect(deriveChipState('idle', 'Success')).toBe('healthy');
  });

  it('returns "degraded" when idle with Failed last run', () => {
    expect(deriveChipState('idle', 'Failed')).toBe('degraded');
  });

  it('returns "degraded" when idle with TimedOut last run', () => {
    expect(deriveChipState('idle', 'TimedOut')).toBe('degraded');
  });

  it('returns "healthy" when idle with null last run (no history)', () => {
    expect(deriveChipState('idle', null)).toBe('healthy');
  });
});

describe('chipPresentation', () => {
  it('has all 4 ChipState entries with required keys', () => {
    const states = ['running', 'healthy', 'degraded', 'setup'] as const;
    for (const state of states) {
      expect(chipPresentation[state]).toMatchObject({
        label: expect.any(String),
        toneClass: expect.any(String),
      });
    }
  });

  it('uses semantic token classes (no hardcoded colors)', () => {
    expect(chipPresentation.healthy.toneClass).toContain('toolkit');
    expect(chipPresentation.degraded.toneClass).toContain('event');
    expect(chipPresentation.running.toneClass).toContain('amber');
    expect(chipPresentation.setup.toneClass).toContain('muted');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/_utils/status-mapper.test.ts
```
Expected: FAIL "Cannot find module './status-mapper'"

- [ ] **Step 3: Write minimal implementation**

```ts
// status-mapper.ts
export type SyncStatus = 'running' | 'idle' | 'never_run';
export type LastRunStatus = 'Success' | 'Failed' | 'TimedOut' | null;
export type ChipState = 'running' | 'healthy' | 'degraded' | 'setup';

export function deriveChipState(status: SyncStatus, lastRunStatus: LastRunStatus): ChipState {
  if (status === 'running') return 'running';
  if (status === 'never_run') return 'setup';
  if (lastRunStatus === 'Failed' || lastRunStatus === 'TimedOut') return 'degraded';
  return 'healthy';
}

export const chipPresentation: Record<ChipState, { label: string; toneClass: string }> = {
  running: {
    label: 'Running',
    toneClass: 'bg-amber-500/15 text-amber-500 ring-amber-500/30',
  },
  healthy: {
    label: 'Idle',
    toneClass: 'bg-toolkit/15 text-toolkit ring-toolkit/30',
  },
  degraded: {
    label: 'Last sync failed',
    toneClass: 'bg-event/15 text-event ring-event/30',
  },
  setup: {
    label: 'Setup',
    toneClass: 'bg-muted/40 text-muted-foreground ring-border',
  },
};
```

- [ ] **Step 4: Run test to verify it passes**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/_utils/status-mapper.test.ts
```
Expected: PASS all 8 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/_utils/status-mapper.ts apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/_utils/status-mapper.test.ts
git commit -m "feat(admin-catalog): #1835 add status-mapper for 4-state chip derivation"
```

---

### Task 2: `run-formatter.ts` — duration + relative time

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/_utils/run-formatter.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/_utils/run-formatter.test.ts`

- [ ] **Step 1: Write failing test**

```ts
// run-formatter.test.ts
import { describe, expect, it } from 'vitest';
import { formatDuration, formatRelativeTime } from './run-formatter';

describe('formatDuration', () => {
  it('formats milliseconds under 1s', () => {
    expect(formatDuration(250)).toBe('250ms');
  });
  it('formats seconds under 1m with 1 decimal', () => {
    expect(formatDuration(2400)).toBe('2.4s');
  });
  it('formats minutes + seconds pad', () => {
    expect(formatDuration(258000)).toBe('4m 18s');
    expect(formatDuration(232000)).toBe('3m 52s');
    expect(formatDuration(362000)).toBe('6m 02s'); // pad to 2 digits
  });
});

describe('formatRelativeTime', () => {
  const now = new Date('2026-06-04T14:22:00Z');

  it('returns "Xs fa" under 60s', () => {
    expect(formatRelativeTime('2026-06-04T14:21:30Z', now)).toBe('30s fa');
  });
  it('returns "X min fa" under 1h', () => {
    expect(formatRelativeTime('2026-06-04T14:08:00Z', now)).toBe('14 min fa');
  });
  it('returns "Xh fa" under 24h', () => {
    expect(formatRelativeTime('2026-06-04T08:22:00Z', now)).toBe('6h fa');
  });
  it('returns "Xgg fa" beyond 24h', () => {
    expect(formatRelativeTime('2026-06-01T14:22:00Z', now)).toBe('3gg fa');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/_utils/run-formatter.test.ts
```
Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write minimal implementation**

```ts
// run-formatter.ts
export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  const m = Math.floor(ms / 60000);
  const s = Math.floor((ms % 60000) / 1000);
  return `${m}m ${s.toString().padStart(2, '0')}s`;
}

export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const diff = (now.getTime() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return `${Math.floor(diff)}s fa`;
  if (diff < 3600) return `${Math.floor(diff / 60)} min fa`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h fa`;
  return `${Math.floor(diff / 86400)}gg fa`;
}
```

- [ ] **Step 4: Run test**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/_utils/run-formatter.test.ts
```
Expected: PASS all 7 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/_utils/run-formatter.ts apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/_utils/run-formatter.test.ts
git commit -m "feat(admin-catalog): #1835 add run-formatter for duration + relative time"
```

---

### Task 3: `use-document-visibility.ts` — visibility primitive

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-document-visibility.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-document-visibility.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// use-document-visibility.test.tsx
import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useDocumentVisibility } from './use-document-visibility';

describe('useDocumentVisibility', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'hidden', { value: false, configurable: true });
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns true when document is visible on mount', () => {
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(true);
  });

  it('returns false when document is hidden on mount', () => {
    Object.defineProperty(document, 'hidden', { value: true, configurable: true });
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(false);
  });

  it('updates when visibilitychange event fires', () => {
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(true);

    act(() => {
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(result.current).toBe(false);

    act(() => {
      Object.defineProperty(document, 'hidden', { value: false, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(result.current).toBe(true);
  });

  it('removes listener on unmount', () => {
    const removeSpy = vi.spyOn(document, 'removeEventListener');
    const { unmount } = renderHook(() => useDocumentVisibility());
    unmount();
    expect(removeSpy).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
  });
});
```

- [ ] **Step 2: Run test**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-document-visibility.test.tsx
```
Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```ts
// use-document-visibility.ts
'use client';
import { useEffect, useState } from 'react';

export function useDocumentVisibility(): boolean {
  const [isVisible, setIsVisible] = useState<boolean>(() =>
    typeof document === 'undefined' ? true : !document.hidden
  );

  useEffect(() => {
    const handler = () => setIsVisible(!document.hidden);
    document.addEventListener('visibilitychange', handler);
    return () => document.removeEventListener('visibilitychange', handler);
  }, []);

  return isVisible;
}
```

- [ ] **Step 4: Run test**

Expected: PASS all 4 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-document-visibility.ts apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-document-visibility.test.tsx
git commit -m "feat(admin-catalog): #1835 add useDocumentVisibility primitive"
```

---

## Phase 2 — API client + React Query hooks

### Task 4: Extend `catalog-ingestion-api.ts` with 4 new fetchers

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/catalog-ingestion-api.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/__tests__/catalog-sync-fetchers.test.ts`

- [ ] **Step 1: Read existing file to learn patterns**

```bash
cat apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/lib/catalog-ingestion-api.ts | head -50
```

- [ ] **Step 2: Write failing test**

```ts
// __tests__/catalog-sync-fetchers.test.ts
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  fetchCatalogSyncStatus,
  fetchCatalogSyncRuns,
  fetchCatalogSyncRunLogs,
  triggerCatalogSync,
} from '../catalog-ingestion-api';

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('fetchCatalogSyncStatus', () => {
  it('GETs /status and returns parsed body', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ status: 'idle', lastRun: null, cumulative: { gamesTotal: 4812 }, nextScheduled: null }),
    });
    const result = await fetchCatalogSyncStatus();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/admin/catalog-ingestion/status'),
      expect.objectContaining({ method: 'GET', credentials: 'include' })
    );
    expect(result.status).toBe('idle');
    expect(result.cumulative.gamesTotal).toBe(4812);
  });

  it('throws on non-ok response', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
    await expect(fetchCatalogSyncStatus()).rejects.toThrow();
  });
});

describe('fetchCatalogSyncRuns', () => {
  it('GETs /runs with page+pageSize query', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ items: [], total: 0, page: 1, pageSize: 12, hasMore: false }),
    });
    await fetchCatalogSyncRuns({ page: 2, pageSize: 24 });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/runs\?page=2&pageSize=24/),
      expect.any(Object)
    );
  });
});

describe('fetchCatalogSyncRunLogs', () => {
  it('GETs /runs/{id}/logs?tail=N', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ runId: 'r1', logsAvailable: true, logs: ['line1'] }),
    });
    await fetchCatalogSyncRunLogs('r1', 100);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/runs\/r1\/logs\?tail=100/),
      expect.any(Object)
    );
  });

  it('returns null on 404', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 404 });
    const result = await fetchCatalogSyncRunLogs('missing', 100);
    expect(result).toBeNull();
  });
});

describe('triggerCatalogSync', () => {
  it('POSTs /trigger with provider in body', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 202,
      json: async () => ({ runId: 'r2' }),
    });
    await triggerCatalogSync('BggApi');
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/admin/catalog-ingestion/trigger'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ provider: 'BggApi' }),
      })
    );
  });

  it('throws ConflictError on 409', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ error: 'Sync already running' }),
    });
    await expect(triggerCatalogSync('BggApi')).rejects.toMatchObject({
      status: 409,
      message: expect.stringContaining('Sync already running'),
    });
  });
});
```

- [ ] **Step 3: Run test (fail)**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/lib/__tests__/catalog-sync-fetchers.test.ts
```
Expected: FAIL "fetchCatalogSyncStatus is not a function"

- [ ] **Step 4: Add fetchers to existing api file**

Append to `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/catalog-ingestion-api.ts`:

```ts
// ====== #1861/#1835 — Catalog sync run history ======

export type CatalogSyncStatus = 'running' | 'idle' | 'never_run';
export type CatalogRunStatus = 'Success' | 'Failed' | 'TimedOut' | 'Running';
export type CatalogSyncProvider = 'BggApi' | 'CsvImport' | 'Manual';

export interface CatalogSyncStatusResponse {
  status: CatalogSyncStatus;
  lastRun: {
    id: string;
    status: CatalogRunStatus;
    startedAt: string;
    completedAt: string | null;
    durationMs: number | null;
    itemsAdded: number;
    itemsUpdated: number;
    itemsFailed: number;
    errorCode: string | null;
  } | null;
  currentRun: { id: string; startedAt: string } | null;
  cumulative: { gamesTotal: number };
  nextScheduled: string | null;
  activeProvider: CatalogSyncProvider | null;
}

export interface CatalogSyncRunSummary {
  id: string;
  provider: CatalogSyncProvider;
  status: CatalogRunStatus;
  title: string;
  startedAt: string;
  completedAt: string | null;
  durationMs: number | null;
  itemsAdded: number;
  itemsUpdated: number;
  itemsFailed: number;
  errorCode: string | null;
  triggeredByUserId: string | null;
}

export interface PagedCatalogSyncRunsResponse {
  items: CatalogSyncRunSummary[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

export interface CatalogSyncRunLogsResponse {
  runId: string;
  status: CatalogRunStatus;
  errorCode: string | null;
  errorDetail: string | null;
  logsAvailable: boolean;
  logs: string[];
}

export interface TriggerCatalogSyncResponse {
  runId: string;
}

export class CatalogSyncApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'CatalogSyncApiError';
  }
}

const BASE = '/api/v1/admin/catalog-ingestion';

export async function fetchCatalogSyncStatus(): Promise<CatalogSyncStatusResponse> {
  const res = await fetch(`${BASE}/status`, { method: 'GET', credentials: 'include' });
  if (!res.ok) throw new CatalogSyncApiError(res.status, `Failed to fetch status: ${res.statusText}`);
  return res.json();
}

export async function fetchCatalogSyncRuns(
  { page = 1, pageSize = 12 }: { page?: number; pageSize?: number } = {}
): Promise<PagedCatalogSyncRunsResponse> {
  const res = await fetch(`${BASE}/runs?page=${page}&pageSize=${pageSize}`, {
    method: 'GET',
    credentials: 'include',
  });
  if (!res.ok) throw new CatalogSyncApiError(res.status, `Failed to fetch runs: ${res.statusText}`);
  return res.json();
}

export async function fetchCatalogSyncRunLogs(
  runId: string,
  tail = 100
): Promise<CatalogSyncRunLogsResponse | null> {
  const res = await fetch(`${BASE}/runs/${runId}/logs?tail=${tail}`, {
    method: 'GET',
    credentials: 'include',
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new CatalogSyncApiError(res.status, `Failed to fetch logs: ${res.statusText}`);
  return res.json();
}

export async function triggerCatalogSync(provider: CatalogSyncProvider): Promise<TriggerCatalogSyncResponse> {
  const res = await fetch(`${BASE}/trigger`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ provider }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new CatalogSyncApiError(res.status, body.error ?? `Trigger failed: ${res.statusText}`);
  }
  return res.json();
}
```

- [ ] **Step 5: Run test (pass)**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/lib/__tests__/catalog-sync-fetchers.test.ts
```
Expected: PASS all 8 tests

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/lib/
git commit -m "feat(admin-catalog): #1835 add 4 fetchers for #1861 BE endpoints"
```

---

### Task 5: `use-catalog-sync-status.ts` — poll + visibility + transition

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-status.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-status.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// use-catalog-sync-status.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useCatalogSyncStatus } from './use-catalog-sync-status';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function TestHarness({ onData }: { onData: (data: unknown) => void }) {
  const { data } = useCatalogSyncStatus();
  if (data) onData(data);
  return null;
}

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncStatus', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'hidden', { value: false, configurable: true });
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('fetches status on mount', async () => {
    const fetchSpy = vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
      status: 'idle', lastRun: null, currentRun: null,
      cumulative: { gamesTotal: 0 }, nextScheduled: null, activeProvider: null,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const observed = vi.fn();
    render(<TestHarness onData={observed} />, { wrapper: wrapper(client) });
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1));
  });

  it('invalidates runs cache on running -> idle transition', async () => {
    const fetchSpy = vi.mocked(api.fetchCatalogSyncStatus)
      .mockResolvedValueOnce({ status: 'running' } as any)
      .mockResolvedValueOnce({ status: 'idle' } as any);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');
    const observed = vi.fn();

    render(<TestHarness onData={observed} />, { wrapper: wrapper(client) });
    await waitFor(() => expect(observed).toHaveBeenCalledWith(expect.objectContaining({ status: 'running' })));

    await act(async () => {
      await client.refetchQueries({ queryKey: ['catalog-sync-status'] });
    });
    await waitFor(() => expect(observed).toHaveBeenCalledWith(expect.objectContaining({ status: 'idle' })));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['catalog-sync-runs'] });
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module './use-catalog-sync-status'"

- [ ] **Step 3: Write implementation**

```ts
// use-catalog-sync-status.ts
'use client';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { fetchCatalogSyncStatus, type CatalogSyncStatusResponse } from '../lib/catalog-ingestion-api';
import { useDocumentVisibility } from './use-document-visibility';

export const CATALOG_SYNC_STATUS_KEY = ['catalog-sync-status'] as const;
export const CATALOG_SYNC_RUNS_KEY = ['catalog-sync-runs'] as const;

export function useCatalogSyncStatus() {
  const isVisible = useDocumentVisibility();
  const queryClient = useQueryClient();
  const previousStatusRef = useRef<string | null>(null);

  const query = useQuery<CatalogSyncStatusResponse>({
    queryKey: CATALOG_SYNC_STATUS_KEY,
    queryFn: fetchCatalogSyncStatus,
    refetchInterval: (q) =>
      isVisible && q.state.data?.status === 'running' ? 5000 : false,
    refetchIntervalInBackground: false,
  });

  useEffect(() => {
    const current = query.data?.status ?? null;
    if (previousStatusRef.current === 'running' && current === 'idle') {
      queryClient.invalidateQueries({ queryKey: CATALOG_SYNC_RUNS_KEY });
    }
    previousStatusRef.current = current;
  }, [query.data?.status, queryClient]);

  return query;
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 2 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-catalog-sync-status.*
git commit -m "feat(admin-catalog): #1835 add useCatalogSyncStatus poll+visibility+transition"
```

---

### Task 6: `use-catalog-sync-runs.ts` — pagination

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-runs.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-runs.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as api from '../lib/catalog-ingestion-api';
import { useCatalogSyncRuns } from './use-catalog-sync-runs';

vi.mock('../lib/catalog-ingestion-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncRuns', () => {
  it('fetches runs with default page=1 pageSize=12', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [], total: 0, page: 1, pageSize: 12, hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRuns(), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 1, pageSize: 12 }));
  });

  it('passes custom page+pageSize', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [], total: 0, page: 3, pageSize: 24, hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRuns(3, 24), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 3, pageSize: 24 }));
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```ts
// use-catalog-sync-runs.ts
'use client';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { fetchCatalogSyncRuns, type PagedCatalogSyncRunsResponse } from '../lib/catalog-ingestion-api';
import { CATALOG_SYNC_RUNS_KEY } from './use-catalog-sync-status';

export function useCatalogSyncRuns(page = 1, pageSize = 12) {
  return useQuery<PagedCatalogSyncRunsResponse>({
    queryKey: [...CATALOG_SYNC_RUNS_KEY, page, pageSize],
    queryFn: () => fetchCatalogSyncRuns({ page, pageSize }),
    placeholderData: keepPreviousData,
  });
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 2 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-catalog-sync-runs.*
git commit -m "feat(admin-catalog): #1835 add useCatalogSyncRuns pagination hook"
```

---

### Task 7: `use-catalog-sync-run-logs.ts` — lazy on-demand

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-run-logs.ts`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/hooks/use-catalog-sync-run-logs.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as api from '../lib/catalog-ingestion-api';
import { useCatalogSyncRunLogs } from './use-catalog-sync-run-logs';

vi.mock('../lib/catalog-ingestion-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncRunLogs', () => {
  it('does NOT fetch when runId is null', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs(null), { wrapper: wrapper(client) });
    await new Promise((r) => setTimeout(r, 50));
    expect(spy).not.toHaveBeenCalled();
  });

  it('fetches logs when runId is provided', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1', status: 'Failed', errorCode: 'X', errorDetail: 'y', logsAvailable: true, logs: ['log1'],
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs('r1'), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith('r1', 100));
  });

  it('uses custom tail value', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1', status: 'Success', errorCode: null, errorDetail: null, logsAvailable: true, logs: [],
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs('r1', 50), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith('r1', 50));
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```ts
// use-catalog-sync-run-logs.ts
'use client';
import { useQuery } from '@tanstack/react-query';
import { fetchCatalogSyncRunLogs, type CatalogSyncRunLogsResponse } from '../lib/catalog-ingestion-api';

export function useCatalogSyncRunLogs(runId: string | null, tail = 100) {
  return useQuery<CatalogSyncRunLogsResponse | null>({
    queryKey: ['catalog-sync-run-logs', runId, tail],
    queryFn: () => fetchCatalogSyncRunLogs(runId!, tail),
    enabled: runId !== null,
  });
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 3 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/hooks/use-catalog-sync-run-logs.*
git commit -m "feat(admin-catalog): #1835 add useCatalogSyncRunLogs lazy hook"
```

---

## Phase 3 — SyncStatusHero

### Task 8: `SyncStatusHero.tsx` — chip + stats + provider config + Run sync now

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/SyncStatusHero.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/SyncStatusHero.test.tsx`

This task is split into 3 sub-steps because the component combines 3 responsibilities (chip+stats render, config visibility, trigger flow). One commit, focused tests per concern.

- [ ] **Step 1: Write failing tests (3 test groups)**

```tsx
// SyncStatusHero.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SyncStatusHero } from './SyncStatusHero';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setup(initialStatus: Partial<api.CatalogSyncStatusResponse> = {}) {
  vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
    status: 'idle',
    lastRun: { id: 'r1', status: 'Success', startedAt: '2026-06-04T14:00:00Z', completedAt: '2026-06-04T14:08:00Z',
              durationMs: 480000, itemsAdded: 12, itemsUpdated: 847, itemsFailed: 0, errorCode: null },
    currentRun: null,
    cumulative: { gamesTotal: 4812 },
    nextScheduled: null,
    activeProvider: 'BggApi',
    ...initialStatus,
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

describe('SyncStatusHero — chip + stats rendering', () => {
  it('renders 🟢 Idle chip when status=idle && lastRun=Success', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    expect(screen.getByText(/Giochi importati totali/)).toBeInTheDocument();
    expect(screen.getByText('4.812')).toBeInTheDocument();
  });

  it('renders 🔴 Last sync failed chip + errorCode badge', async () => {
    const { wrapper } = setup({
      lastRun: { id: 'r1', status: 'Failed', startedAt: 'x', completedAt: 'y',
                 durationMs: 360000, itemsAdded: 3, itemsUpdated: 218, itemsFailed: 14,
                 errorCode: 'BGG_API_RATE_LIMIT_429' },
    });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Last sync failed')).toBeInTheDocument());
    expect(screen.getByText('BGG_API_RATE_LIMIT_429')).toBeInTheDocument();
  });

  it('renders 🟠 Running chip when status=running', async () => {
    const { wrapper } = setup({ status: 'running', currentRun: { id: 'rx', startedAt: 'now' } });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Running')).toBeInTheDocument());
  });

  it('renders ⚪ Setup chip when status=never_run', async () => {
    const { wrapper } = setup({ status: 'never_run', lastRun: null, cumulative: { gamesTotal: 0 } });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Setup')).toBeInTheDocument());
  });

  it('HIDES "Next scheduled" row when nextScheduled is null', async () => {
    const { wrapper } = setup({ nextScheduled: null });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    expect(screen.queryByText(/Next scheduled/)).not.toBeInTheDocument();
  });

  it('SHOWS "Next scheduled" row when present', async () => {
    const { wrapper } = setup({ nextScheduled: '2026-06-04T20:00:00Z' });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Next scheduled/)).toBeInTheDocument());
  });
});

describe('SyncStatusHero — provider config visibility', () => {
  it('SHOWS Batch size / Rate limit / Auto-retry when provider=BggApi', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Batch size/i)).toBeInTheDocument());
    expect(screen.getByLabelText(/Rate limit/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Auto-retry/i)).toBeInTheDocument();
  });

  it('HIDES config when provider=CsvImport', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    expect(screen.queryByLabelText(/Batch size/i)).not.toBeInTheDocument();
  });

  it('HIDES config when provider=Manual', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    expect(screen.queryByLabelText(/Batch size/i)).not.toBeInTheDocument();
  });
});

describe('SyncStatusHero — trigger flow', () => {
  it('disables Run sync now when status=running', async () => {
    const { wrapper } = setup({ status: 'running', currentRun: { id: 'rx', startedAt: 'now' } });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByRole('button', { name: /Run sync now/i })).toBeDisabled());
  });

  it('POSTs /trigger when BGG provider + button clicked', async () => {
    const spy = vi.mocked(api.triggerCatalogSync).mockResolvedValue({ runId: 'r2' });
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByRole('button', { name: /Run sync now/i })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    await waitFor(() => expect(spy).toHaveBeenCalledWith('BggApi'));
  });

  it('emits onOpenCsvModal callback when CSV provider + button clicked', async () => {
    const onOpenCsvModal = vi.fn();
    const { wrapper } = setup();
    render(<SyncStatusHero onOpenCsvModal={onOpenCsvModal} />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    expect(onOpenCsvModal).toHaveBeenCalled();
  });

  it('emits onOpenManualModal callback when Manual provider + button clicked', async () => {
    const onOpenManualModal = vi.fn();
    const { wrapper } = setup();
    render(<SyncStatusHero onOpenManualModal={onOpenManualModal} />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    expect(onOpenManualModal).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module './SyncStatusHero'"

- [ ] **Step 3: Write implementation**

```tsx
// SyncStatusHero.tsx
'use client';
import { useState } from 'react';
import { toast } from 'sonner';
import { Loader2, Play } from 'lucide-react';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/inputs/button';
import { useCatalogSyncStatus } from '../hooks/use-catalog-sync-status';
import {
  type CatalogSyncProvider,
  triggerCatalogSync,
} from '../lib/catalog-ingestion-api';
import { chipPresentation, deriveChipState } from '../_utils/status-mapper';
import { formatRelativeTime } from '../_utils/run-formatter';

interface SyncStatusHeroProps {
  onOpenCsvModal?: () => void;
  onOpenManualModal?: () => void;
}

export function SyncStatusHero({ onOpenCsvModal, onOpenManualModal }: SyncStatusHeroProps) {
  const { data } = useCatalogSyncStatus();
  const [provider, setProvider] = useState<CatalogSyncProvider>('BggApi');
  const [batchSize, setBatchSize] = useState('100');
  const [rateLimit, setRateLimit] = useState('60/min');
  const [autoRetry, setAutoRetry] = useState(true);
  const [isTriggering, setIsTriggering] = useState(false);

  if (!data) return <Card className="h-40 animate-pulse" />;

  const chipState = deriveChipState(data.status, data.lastRun?.status ?? null);
  const chip = chipPresentation[chipState];
  const isRunning = data.status === 'running';
  const showBggConfig = provider === 'BggApi';

  const handleRunSyncNow = async () => {
    if (provider === 'CsvImport') {
      onOpenCsvModal?.();
      return;
    }
    if (provider === 'Manual') {
      onOpenManualModal?.();
      return;
    }
    setIsTriggering(true);
    try {
      await triggerCatalogSync('BggApi');
      toast.success('Sync queued');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Trigger failed';
      toast.error(message);
    } finally {
      setIsTriggering(false);
    }
  };

  return (
    <Card className="border-toolkit/25 bg-gradient-to-br from-toolkit/[0.14] to-entity-game/[0.08] p-5">
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        {/* Left: status + stats */}
        <div>
          <div className="flex items-center gap-2.5">
            <h2 className="font-quicksand text-xl font-extrabold text-foreground">
              🔄 BGG Catalog Sync
            </h2>
            <span
              role="status"
              aria-live="polite"
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${chip.toneClass} ${isRunning ? 'animate-pulse' : ''}`}
            >
              {chip.label}
            </span>
          </div>
          {chipState === 'degraded' && data.lastRun?.errorCode && (
            <div className="mt-1 inline-flex items-center rounded bg-event/10 px-2 py-0.5 font-mono text-[11px] text-event">
              {data.lastRun.errorCode}
            </div>
          )}
          <p className="mt-1.5 text-sm text-muted-foreground">
            Sincronizzazione automatica da BoardGameGeek API. Cron schedule ogni 6h.
          </p>
          <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 font-mono text-[11px] text-muted-foreground">
            <span>
              Ultima sync:{' '}
              <span className="font-bold text-foreground">
                {data.lastRun ? formatRelativeTime(data.lastRun.completedAt ?? data.lastRun.startedAt) : 'Mai eseguita'}
              </span>
            </span>
            <span>
              Giochi importati totali:{' '}
              <span className="font-bold text-foreground">{data.cumulative.gamesTotal.toLocaleString('it-IT')}</span>
            </span>
            {data.nextScheduled && (
              <span>
                Next scheduled: <span className="font-bold text-foreground">{data.nextScheduled}</span>
              </span>
            )}
            {data.activeProvider && (
              <span>
                Provider: <span className="font-bold text-foreground">{data.activeProvider}</span>
              </span>
            )}
          </div>
        </div>

        {/* Right: provider + config + Run sync now */}
        <div className="flex flex-col gap-2 rounded-md bg-card p-3">
          <label className="flex items-center gap-2 text-xs">
            <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
              Provider
            </span>
            <select
              aria-label="Provider"
              value={provider}
              onChange={(e) => setProvider(e.target.value as CatalogSyncProvider)}
              className="flex-1 rounded border border-border bg-background px-2 py-1 text-xs"
            >
              <option value="BggApi">BGG API v2</option>
              <option value="CsvImport">CSV import</option>
              <option value="Manual">Manual</option>
            </select>
          </label>
          {showBggConfig && (
            <>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Batch size
                </span>
                <input
                  aria-label="Batch size"
                  className="max-w-[80px] rounded border border-border bg-background px-2 py-1 font-mono text-xs"
                  value={batchSize}
                  onChange={(e) => setBatchSize(e.target.value)}
                />
              </label>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Rate limit
                </span>
                <input
                  aria-label="Rate limit"
                  className="max-w-[80px] rounded border border-border bg-background px-2 py-1 font-mono text-xs"
                  value={rateLimit}
                  onChange={(e) => setRateLimit(e.target.value)}
                />
              </label>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Auto-retry
                </span>
                <input
                  aria-label="Auto-retry"
                  type="checkbox"
                  checked={autoRetry}
                  onChange={(e) => setAutoRetry(e.target.checked)}
                />
              </label>
            </>
          )}
          <Button
            onClick={handleRunSyncNow}
            disabled={isRunning || isTriggering}
            title={isRunning ? 'Sync già in corso' : undefined}
            className="mt-1.5"
          >
            {isTriggering ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Play className="mr-1 h-3.5 w-3.5" />}
            Run sync now
          </Button>
        </div>
      </div>
    </Card>
  );
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS all 12 tests (3 groups × ~4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/SyncStatusHero.*
git commit -m "feat(admin-catalog): #1835 add SyncStatusHero with 4-state chip + provider config"
```

---

## Phase 4 — SyncRunTimeline

### Task 9: `SyncRunTimeline.tsx` — run rows + pagination + drill-down trigger

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/SyncRunTimeline.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/SyncRunTimeline.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SyncRunTimeline } from './SyncRunTimeline';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setup(runs: api.CatalogSyncRunSummary[] = []) {
  vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
    items: runs, total: runs.length, page: 1, pageSize: 12, hasMore: false,
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

const sampleRun = (overrides: Partial<api.CatalogSyncRunSummary> = {}): api.CatalogSyncRunSummary => ({
  id: 'r1',
  provider: 'BggApi',
  status: 'Success',
  title: 'BGG full sync',
  startedAt: '2026-06-04T14:08:00Z',
  completedAt: '2026-06-04T14:12:18Z',
  durationMs: 258000,
  itemsAdded: 12,
  itemsUpdated: 847,
  itemsFailed: 0,
  errorCode: null,
  triggeredByUserId: null,
  ...overrides,
});

describe('SyncRunTimeline', () => {
  it('renders header with success rate', async () => {
    const { wrapper } = setup([
      sampleRun({ id: 'r1', status: 'Success' }),
      sampleRun({ id: 'r2', status: 'Success' }),
      sampleRun({ id: 'r3', status: 'Failed' }),
    ]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Sync history/)).toBeInTheDocument());
    expect(screen.getByText(/66.7%/)).toBeInTheDocument();
  });

  it('renders run rows with title, duration, counts', async () => {
    const { wrapper } = setup([sampleRun({ id: 'r1' })]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    expect(screen.getByText('4m 18s')).toBeInTheDocument();
    expect(screen.getByText('+12')).toBeInTheDocument();
    expect(screen.getByText('~847')).toBeInTheDocument();
  });

  it('applies failed-row tint for Failed status', async () => {
    const { wrapper } = setup([sampleRun({ id: 'r1', status: 'Failed' })]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    const row = screen.getByText('BGG full sync').closest('[data-testid="run-row"]');
    expect(row).toHaveClass(/event/);
  });

  it('calls onDrillDown when › button clicked', async () => {
    const onDrillDown = vi.fn();
    const { wrapper } = setup([sampleRun({ id: 'r1' })]);
    render(<SyncRunTimeline onDrillDown={onDrillDown} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Open logs for run r1/i }));
    expect(onDrillDown).toHaveBeenCalledWith('r1');
  });

  it('shows empty state when no runs', async () => {
    const { wrapper } = setup([]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Nessun run/i)).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```tsx
// SyncRunTimeline.tsx
'use client';
import { ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { useCatalogSyncRuns } from '../hooks/use-catalog-sync-runs';
import { formatDuration } from '../_utils/run-formatter';
import type { CatalogRunStatus, CatalogSyncRunSummary } from '../lib/catalog-ingestion-api';

interface SyncRunTimelineProps {
  onDrillDown: (runId: string) => void;
}

function statusDotClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-event';
  if (status === 'Running') return 'bg-kb animate-pulse';
  return 'bg-toolkit';
}

function rowBgClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-event/[0.04]';
  return '';
}

function successRate(runs: CatalogSyncRunSummary[]): string {
  if (runs.length === 0) return '—';
  const successCount = runs.filter((r) => r.status === 'Success').length;
  return `${((successCount / runs.length) * 100).toFixed(1)}%`;
}

export function SyncRunTimeline({ onDrillDown }: SyncRunTimelineProps) {
  const { data, isLoading } = useCatalogSyncRuns();

  if (isLoading) return <Card className="h-40 animate-pulse" />;
  if (!data || data.items.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Sync history</CardTitle>
        </CardHeader>
        <div className="px-4 py-8 text-center text-sm text-muted-foreground">
          Nessun run registrato.
        </div>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Sync history · ultime {data.items.length} run</CardTitle>
        <span className="font-mono text-xs text-muted-foreground">
          success rate {successRate(data.items)}
        </span>
      </CardHeader>
      <div>
        {/* Header sub-row */}
        <div className="grid grid-cols-[32px_1fr_90px_60px_60px_60px_24px] gap-3 border-b border-border bg-muted/30 px-3.5 py-2 font-mono text-[9.5px] font-bold uppercase tracking-wider text-muted-foreground">
          <div />
          <div>Run</div>
          <div className="text-right">Durata</div>
          <div className="text-right">+add</div>
          <div className="text-right">~upd</div>
          <div className="text-right">×fail</div>
          <div />
        </div>
        {data.items.map((run) => (
          <div
            key={run.id}
            data-testid="run-row"
            className={`grid grid-cols-[32px_1fr_90px_60px_60px_60px_24px] gap-3 border-b border-border/70 px-3.5 py-3 text-xs last:border-b-0 ${rowBgClass(run.status)}`}
          >
            <div className="flex items-center">
              <span className={`h-2.5 w-2.5 rounded-full ${statusDotClass(run.status)}`} />
            </div>
            <div>
              <div className="font-quicksand font-bold text-foreground">{run.title}</div>
              <div className="mt-0.5 font-mono text-[10px] text-muted-foreground">
                {new Date(run.startedAt).toLocaleString('it-IT')}
                {run.triggeredByUserId === null ? ' · cron' : ` · by user`}
                {run.errorCode && ` · ${run.errorCode}`}
              </div>
            </div>
            <div className="text-right font-mono font-bold text-foreground">
              {run.durationMs !== null ? formatDuration(run.durationMs) : '—'}
            </div>
            <div className="text-right font-mono font-bold text-toolkit">+{run.itemsAdded}</div>
            <div className="text-right font-mono font-bold text-chat">~{run.itemsUpdated}</div>
            <div className="text-right font-mono font-bold text-event">{run.itemsFailed}</div>
            <button
              onClick={() => onDrillDown(run.id)}
              aria-label={`Open logs for run ${run.id}`}
              className="text-muted-foreground hover:text-foreground"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>
    </Card>
  );
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/SyncRunTimeline.*
git commit -m "feat(admin-catalog): #1835 add SyncRunTimeline with drill-down trigger"
```

---

## Phase 5 — LogStream

### Task 10: `LogStream.tsx` — lazy logs drawer

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/LogStream.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/LogStream.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LogStream } from './LogStream';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

describe('LogStream', () => {
  it('does not render when runId is null', () => {
    const { wrapper } = setup();
    render(<LogStream runId={null} onClose={vi.fn()} />, { wrapper });
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });

  it('renders logs when runId is provided and BE returns data', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1', status: 'Success', errorCode: null, errorDetail: null,
      logsAvailable: true, logs: ['[2026-06-04 14:08:00] BGG sync started', '[2026-06-04 14:08:14] +12 items'],
    });
    const { wrapper } = setup();
    render(<LogStream runId="r1" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/BGG sync started/)).toBeInTheDocument());
    expect(screen.getByText(/\+12 items/)).toBeInTheDocument();
  });

  it('shows errorCode + errorDetail when status=Failed', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r2', status: 'Failed', errorCode: 'BGG_API_RATE_LIMIT_429',
      errorDetail: '4 retry esauriti', logsAvailable: true, logs: [],
    });
    const { wrapper } = setup();
    render(<LogStream runId="r2" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG_API_RATE_LIMIT_429')).toBeInTheDocument());
    expect(screen.getByText(/4 retry esauriti/)).toBeInTheDocument();
  });

  it('shows "Logs not available" when logsAvailable=false', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r3', status: 'Success', errorCode: null, errorDetail: null,
      logsAvailable: false, logs: [],
    });
    const { wrapper } = setup();
    render(<LogStream runId="r3" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Logs not available/i)).toBeInTheDocument());
  });

  it('shows "Run not found" on 404 (null result)', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue(null);
    const { wrapper } = setup();
    render(<LogStream runId="missing" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Run not found/i)).toBeInTheDocument());
  });

  it('calls onClose when close button clicked', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1', status: 'Success', errorCode: null, errorDetail: null,
      logsAvailable: true, logs: ['x'],
    });
    const onClose = vi.fn();
    const { wrapper } = setup();
    render(<LogStream runId="r1" onClose={onClose} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/x/)).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Close logs/i }));
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```tsx
// LogStream.tsx
'use client';
import { X } from 'lucide-react';
import { Card, CardHeader } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/inputs/button';
import { useCatalogSyncRunLogs } from '../hooks/use-catalog-sync-run-logs';

interface LogStreamProps {
  runId: string | null;
  onClose: () => void;
}

export function LogStream({ runId, onClose }: LogStreamProps) {
  const { data, isLoading } = useCatalogSyncRunLogs(runId);

  if (runId === null) return null;

  return (
    <Card role="region" aria-label="Sync run logs" className="border-border">
      <CardHeader className="flex flex-row items-center justify-between border-b border-border pb-3">
        <div>
          <h3 className="font-quicksand font-bold text-foreground">Run logs</h3>
          <p className="font-mono text-[11px] text-muted-foreground">{runId}</p>
        </div>
        <Button variant="ghost" size="icon" onClick={onClose} aria-label="Close logs">
          <X className="h-4 w-4" />
        </Button>
      </CardHeader>
      <div className="p-4">
        {isLoading && <p className="text-sm text-muted-foreground">Loading logs…</p>}

        {!isLoading && data === null && (
          <p className="text-sm text-muted-foreground">Run not found.</p>
        )}

        {!isLoading && data && !data.logsAvailable && (
          <p className="text-sm text-muted-foreground">Logs not available (file mancante o non leggibile).</p>
        )}

        {!isLoading && data && data.logsAvailable && (
          <>
            {data.status !== 'Success' && data.errorCode && (
              <div className="mb-3 rounded-md border-l-4 border-event bg-event/[0.04] px-3 py-2">
                <div className="font-mono text-xs font-bold text-event">{data.errorCode}</div>
                {data.errorDetail && (
                  <div className="mt-1 font-mono text-[11px] text-muted-foreground">
                    {data.errorDetail}
                  </div>
                )}
              </div>
            )}
            <pre className="max-h-96 overflow-y-auto rounded bg-muted/40 p-3 font-mono text-[11px] text-foreground">
              {data.logs.join('\n')}
            </pre>
          </>
        )}
      </div>
    </Card>
  );
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 6 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/LogStream.*
git commit -m "feat(admin-catalog): #1835 add LogStream lazy drawer for run drill-down"
```

---

## Phase 6 — Placeholder panels + Export button

### Task 11: `QueuePendingPanel.tsx` + `FailedItemsPanel.tsx` (MVP placeholders)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/QueuePendingPanel.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/QueuePendingPanel.test.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/FailedItemsPanel.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/FailedItemsPanel.test.tsx`

- [ ] **Step 1: Write failing tests (both panels)**

```tsx
// QueuePendingPanel.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { QueuePendingPanel } from './QueuePendingPanel';

describe('QueuePendingPanel', () => {
  it('renders MVP placeholder with link to #1874', () => {
    render(<QueuePendingPanel />);
    expect(screen.getByText(/Queue pending/i)).toBeInTheDocument();
    expect(screen.getByText(/feature in arrivo/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /#1874/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('1874'));
  });
});
```

```tsx
// FailedItemsPanel.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FailedItemsPanel } from './FailedItemsPanel';

describe('FailedItemsPanel', () => {
  it('renders MVP placeholder with link to #1874', () => {
    render(<FailedItemsPanel />);
    expect(screen.getByText(/Failed items/i)).toBeInTheDocument();
    expect(screen.getByText(/feature in arrivo/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /#1874/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('1874'));
  });
});
```

- [ ] **Step 2: Run tests (fail)**

Expected: FAIL "Cannot find module" for both

- [ ] **Step 3: Write implementations**

```tsx
// QueuePendingPanel.tsx
import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';

const ISSUE_URL = 'https://github.com/meepleAi-app/meepleai-monorepo/issues/1874';

export function QueuePendingPanel() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>⏳ Queue pending re-sync</CardTitle>
      </CardHeader>
      <div className="px-4 py-6 text-center">
        <p className="text-sm text-muted-foreground">
          Queue pending: feature in arrivo (BE{' '}
          <a href={ISSUE_URL} target="_blank" rel="noopener noreferrer" className="text-toolkit underline">
            #1874
          </a>
          ).
        </p>
      </div>
    </Card>
  );
}
```

```tsx
// FailedItemsPanel.tsx
import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';

const ISSUE_URL = 'https://github.com/meepleAi-app/meepleai-monorepo/issues/1874';

export function FailedItemsPanel() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>✕ Failed items (last 30gg)</CardTitle>
      </CardHeader>
      <div className="px-4 py-6 text-center">
        <p className="text-sm text-muted-foreground">
          Failed items: feature in arrivo (BE{' '}
          <a href={ISSUE_URL} target="_blank" rel="noopener noreferrer" className="text-toolkit underline">
            #1874
          </a>
          ).
        </p>
      </div>
    </Card>
  );
}
```

- [ ] **Step 4: Run tests (pass)**

Expected: PASS both tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/QueuePendingPanel.* apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/FailedItemsPanel.*
git commit -m "feat(admin-catalog): #1835 add Queue+Failed placeholder panels (full wire post-#1874)"
```

---

### Task 12: `ExportCatalogButton.tsx` (renamed CTA)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExportCatalogButton.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExportCatalogButton.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ExportCatalogButton } from './ExportCatalogButton';

describe('ExportCatalogButton', () => {
  it('renders "Export catalog" label (NOT "Export history")', () => {
    render(<ExportCatalogButton />);
    expect(screen.getByRole('link', { name: /Export catalog/i })).toBeInTheDocument();
    expect(screen.queryByText(/Export history/i)).not.toBeInTheDocument();
  });

  it('links to /excel-export endpoint', () => {
    render(<ExportCatalogButton />);
    const link = screen.getByRole('link', { name: /Export catalog/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('/excel-export'));
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write implementation**

```tsx
// ExportCatalogButton.tsx
import { Download } from 'lucide-react';

export function ExportCatalogButton() {
  return (
    <a
      href="/api/v1/admin/catalog-ingestion/excel-export"
      className="inline-flex items-center gap-1.5 rounded-md border border-border bg-background px-3 py-1.5 text-xs font-medium text-foreground hover:bg-muted"
    >
      <Download className="h-3.5 w-3.5" />
      Export catalog
    </a>
  );
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 2 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/ExportCatalogButton.*
git commit -m "feat(admin-catalog): #1835 add ExportCatalogButton renamed CTA"
```

---

## Phase 7 — Modals + page refactor

### Task 13: `AssignBggIdForm.tsx` + `ManualAssignModal.tsx`

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/AssignBggIdForm.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/AssignBggIdForm.test.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ManualAssignModal.tsx`

- [ ] **Step 1: Write failing test for form**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AssignBggIdForm } from './AssignBggIdForm';

describe('AssignBggIdForm', () => {
  it('renders sharedGameId + bggId inputs', () => {
    render(<AssignBggIdForm onSubmit={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByLabelText(/Shared Game ID/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/BGG ID/i)).toBeInTheDocument();
  });

  it('calls onSubmit with parsed values', async () => {
    const onSubmit = vi.fn();
    render(<AssignBggIdForm onSubmit={onSubmit} onCancel={vi.fn()} />);
    await userEvent.type(screen.getByLabelText(/Shared Game ID/i), '00000000-0000-0000-0000-000000000001');
    await userEvent.type(screen.getByLabelText(/BGG ID/i), '12345');
    await userEvent.click(screen.getByRole('button', { name: /Assign/i }));
    expect(onSubmit).toHaveBeenCalledWith({
      sharedGameId: '00000000-0000-0000-0000-000000000001',
      bggId: 12345,
    });
  });

  it('calls onCancel when cancel button clicked', async () => {
    const onCancel = vi.fn();
    render(<AssignBggIdForm onSubmit={vi.fn()} onCancel={onCancel} />);
    await userEvent.click(screen.getByRole('button', { name: /Cancel/i }));
    expect(onCancel).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Write `AssignBggIdForm` implementation**

```tsx
// AssignBggIdForm.tsx
'use client';
import { useState } from 'react';
import { Button } from '@/components/ui/inputs/button';

export interface AssignBggIdFormValues {
  sharedGameId: string;
  bggId: number;
}

interface AssignBggIdFormProps {
  onSubmit: (values: AssignBggIdFormValues) => void;
  onCancel: () => void;
}

export function AssignBggIdForm({ onSubmit, onCancel }: AssignBggIdFormProps) {
  const [sharedGameId, setSharedGameId] = useState('');
  const [bggIdStr, setBggIdStr] = useState('');

  const isValid = sharedGameId.length > 0 && /^\d+$/.test(bggIdStr);

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (!isValid) return;
        onSubmit({ sharedGameId, bggId: Number.parseInt(bggIdStr, 10) });
      }}
      className="space-y-3"
    >
      <label className="block">
        <span className="text-xs font-mono uppercase text-muted-foreground">Shared Game ID</span>
        <input
          aria-label="Shared Game ID"
          value={sharedGameId}
          onChange={(e) => setSharedGameId(e.target.value)}
          className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm"
        />
      </label>
      <label className="block">
        <span className="text-xs font-mono uppercase text-muted-foreground">BGG ID</span>
        <input
          aria-label="BGG ID"
          value={bggIdStr}
          onChange={(e) => setBggIdStr(e.target.value)}
          inputMode="numeric"
          className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm"
        />
      </label>
      <div className="flex gap-2">
        <Button type="submit" disabled={!isValid}>Assign</Button>
        <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 4: Write `ManualAssignModal` wrapper**

```tsx
// ManualAssignModal.tsx
'use client';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { AssignBggIdForm } from './AssignBggIdForm';

interface ManualAssignModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ManualAssignModal({ open, onOpenChange }: ManualAssignModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Manual BGG assignment</DialogTitle>
        </DialogHeader>
        <AssignBggIdForm
          onSubmit={async (values) => {
            const res = await fetch('/api/v1/admin/catalog-ingestion/assign-bgg-id', {
              method: 'POST',
              credentials: 'include',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(values),
            });
            if (res.ok) {
              toast.success('BGG ID assigned');
              onOpenChange(false);
            } else {
              const body = await res.json().catch(() => ({}));
              toast.error(body.error ?? 'Assignment failed');
            }
          }}
          onCancel={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 5: Run test (pass)**

Expected: PASS 3 tests for form (modal smoke covered later in page test)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/AssignBggIdForm.* apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/ManualAssignModal.tsx
git commit -m "feat(admin-catalog): #1835 add AssignBggIdForm + ManualAssignModal for Manual provider"
```

---

### Task 14: `CsvImportModal.tsx` — wraps `ExcelImportTab`

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/CsvImportModal.tsx`

- [ ] **Step 1: Write implementation (no test, smoke covered by page test)**

```tsx
// CsvImportModal.tsx
'use client';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { ExcelImportTab } from './ExcelImportTab';

interface CsvImportModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CsvImportModal({ open, onOpenChange }: CsvImportModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>CSV / Excel import</DialogTitle>
        </DialogHeader>
        <ExcelImportTab />
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/components/CsvImportModal.tsx
git commit -m "feat(admin-catalog): #1835 add CsvImportModal wrapping ExcelImportTab"
```

---

### Task 15: Refactor `page.tsx` — wire hero + timeline + panels + modals

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/page.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/__tests__/page.test.tsx`

- [ ] **Step 1: Write failing integration test**

```tsx
// __tests__/page.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import CatalogIngestionPage from '../page';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setupMocks() {
  vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
    status: 'idle',
    lastRun: {
      id: 'r1', status: 'Success', startedAt: '2026-06-04T14:08:00Z',
      completedAt: '2026-06-04T14:12:18Z', durationMs: 258000,
      itemsAdded: 12, itemsUpdated: 847, itemsFailed: 0, errorCode: null,
    },
    currentRun: null,
    cumulative: { gamesTotal: 4812 },
    nextScheduled: null,
    activeProvider: 'BggApi',
  });
  vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
    items: [
      {
        id: 'r1', provider: 'BggApi', status: 'Success', title: 'BGG full sync',
        startedAt: '2026-06-04T14:08:00Z', completedAt: '2026-06-04T14:12:18Z',
        durationMs: 258000, itemsAdded: 12, itemsUpdated: 847, itemsFailed: 0,
        errorCode: null, triggeredByUserId: null,
      },
    ],
    total: 1, page: 1, pageSize: 12, hasMore: false,
  });
}

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('CatalogIngestionPage', () => {
  it('renders header + hero + timeline + placeholder panels + export button', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByText(/Catalog ingestion/i)).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    expect(screen.getByText(/Queue pending/i)).toBeInTheDocument();
    expect(screen.getByText(/Failed items/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Export catalog/i })).toBeInTheDocument();
  });

  it('opens CSV modal when provider=CsvImport + Run sync now clicked', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    await waitFor(() => expect(screen.getByText(/CSV \/ Excel import/i)).toBeInTheDocument());
  });

  it('opens Manual modal when provider=Manual + Run sync now clicked', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    await waitFor(() => expect(screen.getByText(/Manual BGG assignment/i)).toBeInTheDocument());
  });

  it('opens LogStream when drill-down arrow clicked', async () => {
    setupMocks();
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1', status: 'Success', errorCode: null, errorDetail: null,
      logsAvailable: true, logs: ['line1'],
    });
    render(<CatalogIngestionPage />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Open logs for run r1/i }));
    await waitFor(() => expect(screen.getByText(/line1/)).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Run test (fail)**

Expected: FAIL — page.tsx still has 3-tab structure

- [ ] **Step 3: Refactor `page.tsx`**

```tsx
// page.tsx (REPLACE entirely)
'use client';
import { useState } from 'react';
import { CsvImportModal } from './components/CsvImportModal';
import { ExportCatalogButton } from './components/ExportCatalogButton';
import { FailedItemsPanel } from './components/FailedItemsPanel';
import { LogStream } from './components/LogStream';
import { ManualAssignModal } from './components/ManualAssignModal';
import { QueuePendingPanel } from './components/QueuePendingPanel';
import { SyncRunTimeline } from './components/SyncRunTimeline';
import { SyncStatusHero } from './components/SyncStatusHero';

export default function CatalogIngestionPage() {
  const [csvOpen, setCsvOpen] = useState(false);
  const [manualOpen, setManualOpen] = useState(false);
  const [drillDownRunId, setDrillDownRunId] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      {/* Header */}
      <header className="flex items-start justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Catalog ingestion
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Admin · Catalog · BoardGameGeek sync
          </p>
        </div>
        <ExportCatalogButton />
      </header>

      <SyncStatusHero
        onOpenCsvModal={() => setCsvOpen(true)}
        onOpenManualModal={() => setManualOpen(true)}
      />

      <SyncRunTimeline onDrillDown={setDrillDownRunId} />

      <div className="grid gap-3.5 lg:grid-cols-2">
        <QueuePendingPanel />
        <FailedItemsPanel />
      </div>

      {drillDownRunId !== null && (
        <LogStream runId={drillDownRunId} onClose={() => setDrillDownRunId(null)} />
      )}

      <CsvImportModal open={csvOpen} onOpenChange={setCsvOpen} />
      <ManualAssignModal open={manualOpen} onOpenChange={setManualOpen} />
    </div>
  );
}
```

- [ ] **Step 4: Run test (pass)**

Expected: PASS 4 page integration tests

- [ ] **Step 5: Run full test suite to check no regressions**

```bash
pnpm vitest run src/app/admin/\(dashboard\)/catalog-ingestion/
```
Expected: PASS all (~40 tests total across phases 1-7)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/page.tsx apps/web/src/app/admin/\(dashboard\)/catalog-ingestion/__tests__/page.test.tsx
git commit -m "feat(admin-catalog): #1835 refactor page.tsx to hero+timeline+panels per mockup"
```

---

## Phase 8 — E2E smoke

### Task 16: E2E smoke test `catalog-ingestion-reskin.spec.ts`

**Files:**
- Create: `apps/web/e2e/admin/catalog-ingestion-reskin.spec.ts`

- [ ] **Step 1: Read existing E2E pattern**

```bash
ls apps/web/e2e/admin/ | head
cat apps/web/e2e/admin/admin-feature-flags.spec.ts | head -30  # example pattern
```

- [ ] **Step 2: Write E2E spec (3 scenarios)**

```ts
// apps/web/e2e/admin/catalog-ingestion-reskin.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Catalog ingestion re-skin (#1835)', () => {
  test.beforeEach(async ({ page }) => {
    // Assume seeded admin user (cfr admin-feature-flags.spec.ts setup)
    await page.goto('/admin/catalog-ingestion');
  });

  test('page renders hero with chip + stats + timeline + 2 placeholder panels', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /Catalog ingestion/i })).toBeVisible();
    // Hero with status chip (any of 4 states)
    await expect(page.getByText(/(Idle|Running|Last sync failed|Setup)/)).toBeVisible();
    // Stats
    await expect(page.getByText(/Giochi importati totali/i)).toBeVisible();
    // Provider dropdown
    await expect(page.getByLabel(/Provider/i)).toBeVisible();
    // Run sync now button
    await expect(page.getByRole('button', { name: /Run sync now/i })).toBeVisible();
    // Timeline
    await expect(page.getByText(/Sync history/i)).toBeVisible();
    // 2 placeholder panels
    await expect(page.getByText(/Queue pending/i)).toBeVisible();
    await expect(page.getByText(/Failed items/i)).toBeVisible();
    // Export catalog button (renamed)
    await expect(page.getByRole('link', { name: /Export catalog/i })).toBeVisible();
    // OLD label NOT present
    await expect(page.getByText(/Export history/i)).not.toBeVisible();
  });

  test('Provider switching shows/hides BGG config controls', async ({ page }) => {
    await expect(page.getByLabel(/Batch size/i)).toBeVisible();
    await page.getByLabel(/Provider/i).selectOption('CsvImport');
    await expect(page.getByLabel(/Batch size/i)).not.toBeVisible();
    await page.getByLabel(/Provider/i).selectOption('Manual');
    await expect(page.getByLabel(/Batch size/i)).not.toBeVisible();
    await page.getByLabel(/Provider/i).selectOption('BggApi');
    await expect(page.getByLabel(/Batch size/i)).toBeVisible();
  });

  test('CSV provider + Run sync now opens import modal', async ({ page }) => {
    await page.getByLabel(/Provider/i).selectOption('CsvImport');
    await page.getByRole('button', { name: /Run sync now/i }).click();
    await expect(page.getByText(/CSV \/ Excel import/i)).toBeVisible();
  });
});
```

- [ ] **Step 3: Run E2E (assume staging up; if not, document skip)**

```bash
cd apps/web && pnpm test:e2e --grep "Catalog ingestion re-skin"
```
Expected: PASS 3 specs (or document skip with reason if local env not available)

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/admin/catalog-ingestion-reskin.spec.ts
git commit -m "test(admin-catalog): #1835 add E2E smoke for re-skin (3 scenarios)"
```

---

## Phase 9 — Lint + manual visual verify + PR

### Task 17: Run lint + typecheck + full FE test suite

- [ ] **Step 1: Run lint (tokens enforcement)**

```bash
cd apps/web && pnpm lint
```
Expected: PASS — no `local/no-hardcoded-color-utility` errors in new files

- [ ] **Step 2: Run typecheck**

```bash
pnpm typecheck
```
Expected: PASS — no TS errors

- [ ] **Step 3: Run full FE test suite (no regressions)**

```bash
pnpm test
```
Expected: PASS all (new tests + existing intact)

- [ ] **Step 4: Manual visual verify**

Start dev shell and navigate manually:
```bash
cd infra && make dev-core
# wait for healthy, then in browser:
# 1. Login as admin
# 2. Navigate to /admin/catalog-ingestion
# 3. Verify hero matches mockup (chip color, stats layout, provider config visible for BGG)
# 4. Switch provider dropdown: verify config hides for CSV/Manual
# 5. Click "Run sync now" with CSV: verify modal opens with ExcelImportTab
# 6. Click "Run sync now" with Manual: verify modal opens with AssignBggIdForm
# 7. Click drill-down › on any run: verify LogStream opens with logs (or "Logs not available" if no file)
# 8. Verify "Queue pending" + "Failed items" panels show placeholder with #1874 link
# 9. Click "Export catalog" button: verify .xlsx download
```

NOTE: If unable to test UI live, state explicitly in PR description: "UI verified via unit + integration tests; manual browser verify pending."

---

### Task 18: Open PR

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-1835-a6-catalog-fe-reskin
```

- [ ] **Step 2: Create PR**

```bash
gh pr create --base main-dev --title "feat(admin-catalog): #1835 F4-A6 re-skin SP5 + 2 placeholder panels" --body-file - << 'PRBODY'
## Summary

Re-skin completo di `/admin/catalog-ingestion` per chiudere **#1835 (F4-A6, parent epic #1833)** post-merge BE foundation #1861.

- ✅ SyncStatusHero con chip 4-stati FE-derived + provider config + Run sync now (POST /trigger)
- ✅ SyncRunTimeline con run rows + status-dot color + drill-down `›`
- ✅ LogStream drawer lazy-loaded per drill-down logs
- ✅ Polling `/status` 5s gated by `useDocumentVisibility` (tab nascosto = pausa) + transition `running→idle` invalida cache `['catalog-sync-runs']`
- ✅ Provider dropdown: BGG → POST /trigger · CSV → modale ExcelImportTab · Manual → modale AssignBggIdForm
- ⚠️ QueuePendingPanel + FailedItemsPanel: **placeholder MVP** wired ai panel post-#1874 (follow-up BE 14h)

## Decisions resolved (spec-panel review 2026-06-04, issue #1835 comment)

| BLOCKER | Resolution |
|---|---|
| BLOCKER-1 status chip | 4-stati FE-derived (running/healthy/degraded/setup); chip 🔴 "Last sync failed" se idle+failed |
| BLOCKER-2 polling | visibility guard + transition hook (no SSE in MVP) |
| BLOCKER-3 failed/queue panels | placeholder MVP, full wire post-#1874 (event-sourced 2 tables) |
| GAP-4 Export CTA | rinominato "Export catalog" → `/excel-export` (semantic match) |
| GAP-5 Provider config | visible solo se `provider==BggApi` |
| GAP-6 Trigger UX | toast "Sync queued" + spinner; no fake state override |
| GAP-7 nextScheduled | hide row se null (graceful degradation) |

## Test plan

- [x] Unit tests (vitest): ~40 nuovi test su utils + hooks + 7 componenti
- [x] Integration test page-level (4 scenari: render, CSV modal, Manual modal, drill-down)
- [x] E2E smoke (Playwright): 3 scenarios (render, provider switching, CSV modal open)
- [x] Lint: 0 errori `local/no-hardcoded-color-utility`
- [x] Typecheck: 0 errori TS
- [ ] Manual browser verify (admin login → navigate → verify mockup parity)
- [ ] Code review

## Mockup parity

| Element | Implementation | Status |
|---|---|---|
| `.catalog-hero` | SyncStatusHero | ✅ |
| Status chip + stats | chipPresentation 4-state | ✅ |
| Provider config (BGG only) | conditional render | ✅ |
| Sync history timeline | SyncRunTimeline | ✅ |
| Drill-down `›` logs | LogStream | ✅ |
| Queue pending panel | QueuePendingPanel placeholder | ⚠️ post-#1874 |
| Failed items panel | FailedItemsPanel placeholder | ⚠️ post-#1874 |
| Export CTA | ExportCatalogButton (renamed) | ✅ |

## References

- Spec: [`docs/for-developers/specs/2026-06-04-1835-a6-catalog-fe-reskin.md`](./docs/for-developers/specs/2026-06-04-1835-a6-catalog-fe-reskin.md)
- Plan: [`docs/superpowers/plans/2026-06-04-1835-a6-catalog-fe-reskin.md`](./docs/superpowers/plans/2026-06-04-1835-a6-catalog-fe-reskin.md)
- BE foundation: #1861 ✅ MERGED PR #1865
- BE follow-up (blocker per panel full): #1874 OPEN
- Parent epic: #1833 (F4 Ondata Ops)
- Sibling pattern: #1837 C1 Infra MERGED PR #1872

🤖 Generated with [Claude Code](https://claude.com/claude-code)
PRBODY
```

- [ ] **Step 3: Wait for CI + request review**

```bash
gh pr checks
gh pr view --json url -q .url
```

- [ ] **Step 4: Run code-review skill on PR**

```bash
# Manual: invoke /code-review:code-review <PR-URL> as per /implementa workflow Phase 6
```

- [ ] **Step 5: Address review comments + merge when green**

---

## Self-Review (against spec)

**Spec coverage**:
- ✅ All 11 acceptance scenarios (A-K) map to tasks:
  - A/B/C/J Status chip rendering → Task 8 sub-tests
  - D/E/F Trigger flows → Task 8 + Task 15 page integration
  - G Timeline rendering → Task 9
  - H Drill-down logs → Task 10 + Task 15 page integration
  - I Export CTA → Task 12
  - K Placeholder panels → Task 11

- ✅ All 5 components from mockup parity matrix planned (SyncStatusHero, SyncRunTimeline, LogStream, QueuePending placeholder, FailedItems placeholder)
- ✅ All 4 hooks (use-document-visibility, use-catalog-sync-status, use-catalog-sync-runs, use-catalog-sync-run-logs) planned
- ✅ All 2 utils (status-mapper, run-formatter) planned
- ✅ API client extension (4 fetchers) planned
- ✅ Page refactor + 3 modals (CSV, Manual, LogStream drawer) planned
- ✅ E2E smoke planned (3 scenarios)
- ✅ Lint + typecheck + manual verify planned

**Placeholder scan**: No "TBD", "TODO", "implement later" patterns in code blocks. Edge cases inlined in handlers (`Logs not available`, `Run not found`, `Mai eseguita`).

**Type consistency**:
- `CatalogSyncStatus` `'running' | 'idle' | 'never_run'` consistent across Task 4 (API types), Task 5 (hook), Task 8 (component)
- `CatalogRunStatus` `'Success' | 'Failed' | 'TimedOut' | 'Running'` consistent across Task 4, 9, 10
- `ChipState` `'running' | 'healthy' | 'degraded' | 'setup'` defined Task 1, consumed Task 8
- Hook query keys: `['catalog-sync-status']` (Task 5), `[...CATALOG_SYNC_RUNS_KEY, page, pageSize]` (Task 6), `['catalog-sync-run-logs', runId, tail]` (Task 7) — invalidation in Task 5 uses correct CATALOG_SYNC_RUNS_KEY constant

**Out-of-spec deliberate**:
- KPISparkline NOT included (different from #1837 — A6 mockup has no sparkline KPI strip, only hero stats)
- SSE endpoint NOT added (deferred to follow-up, decision documented in #1835 comment BLOCKER-2)
- Lifecycle hook BggImportQueueBackgroundService↔CatalogSyncRun NOT included (out of #1835 scope, tracked in #1861 carry-forward)

---

## Effort recap

| Phase | Tasks | Estimated time |
|---|---|---|
| 1. Utils + primitive | 3 (status-mapper, run-formatter, useDocumentVisibility) | ~1.5h |
| 2. API client + hooks | 4 (fetchers, 3 hooks) | ~2.5h |
| 3. SyncStatusHero | 1 | ~3h |
| 4. SyncRunTimeline | 1 | ~2h |
| 5. LogStream | 1 | ~1.5h |
| 6. Placeholders + Export | 2 | ~1h |
| 7. Modals + page refactor | 3 | ~2.5h |
| 8. E2E smoke | 1 | ~1h |
| 9. Lint + manual + PR | 2 | ~1h |
| **Total** | **18 tasks** | **~16h** |

Aligned with spec estimate (~15h MVP + minor buffer).
