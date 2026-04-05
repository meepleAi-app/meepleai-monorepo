# Log Hybrid UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere alla pagina `/admin/monitor/logs` un tab "Container Errors" che mostra gli ultimi 100 errori/warning da Loki, con pulsante "Open in Grafana" per analisi approfondita.

**Architecture:** Next.js API route `/api/logs` proxia a Loki HTTP API (server-side, auth-gated via cookie `meepleai_user_role`). Client component `LokiErrorViewer` usa React Query per fetch. La pagina esistente acquisisce un 3° tab senza toccare `AppLogViewer` (Seq) né `LogViewer` (container stdout). `LOKI_URL` è env var server-side; `NEXT_PUBLIC_GRAFANA_URL` è build-time per il link Grafana.

**Tech Stack:** Next.js 15 App Router (route handler), TypeScript, React Query (`@tanstack/react-query`), Tailwind CSS, Vitest + `@testing-library/react`, `next/headers` per cookies.

---

## File Structure

| Operazione | File | Responsabilità |
|------------|------|----------------|
| CREATE | `apps/web/src/lib/loki/types.ts` | Tipi TypeScript per risposta Loki HTTP API + `LokiLogEntry` normalizzato |
| CREATE | `apps/web/src/app/api/logs/route.ts` | GET handler: auth cookie → query Loki → 100 entry error/warn |
| CREATE | `apps/web/src/app/admin/(dashboard)/monitor/logs/LokiErrorViewer.tsx` | Client component: useQuery → tabella entry con badge livello |
| MODIFY | `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx` | Aggiunta tab "Container Errors" + pulsante "Open in Grafana" |
| MODIFY | `infra/compose.staging.yml` | `LOKI_URL: http://loki:3100` + `NEXT_PUBLIC_GRAFANA_URL: /grafana` nel servizio `web` |
| CREATE | `apps/web/src/app/api/logs/__tests__/route.test.ts` | Unit test API route (auth, Loki unavailable, successo) |
| CREATE | `apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/LokiErrorViewer.test.tsx` | Component test (loading, vuoto, entries, errore) |

---

## Task 1: Tipi Loki + API Route `/api/logs`

**Files:**
- Create: `apps/web/src/lib/loki/types.ts`
- Create: `apps/web/src/app/api/logs/route.ts`
- Test: `apps/web/src/app/api/logs/__tests__/route.test.ts`

- [ ] **Step 1: Creare il file dei tipi**

```ts
// apps/web/src/lib/loki/types.ts

/** Risposta grezza dell'API Loki query_range */
export interface LokiQueryRangeResponse {
  status: 'success' | 'error';
  data: {
    resultType: 'streams';
    result: LokiStream[];
  };
}

export interface LokiStream {
  stream: Record<string, string>;
  /** Array di [nanosecond-timestamp-string, log-line] */
  values: [string, string][];
}

/** Entry normalizzata per il frontend */
export interface LokiLogEntry {
  /** ID sintetico: `${streamIndex}-${valueIndex}` */
  id: string;
  /** ISO 8601 */
  timestamp: string;
  /** Nome container, es. "meepleai-api" */
  container: string;
  /** Linea di log grezza */
  message: string;
  level: 'error' | 'warning' | 'info' | 'unknown';
}

/** Risposta della Next.js API route /api/logs */
export interface LogsApiResponse {
  entries: LokiLogEntry[];
  /** Indica se Loki è configurato ma non disponibile */
  lokiUnavailable?: boolean;
}
```

- [ ] **Step 2: Scrivere il test fallente per la API route**

```ts
// apps/web/src/app/api/logs/__tests__/route.test.ts
import { NextRequest } from 'next/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// Mock process.env
vi.stubEnv('LOKI_URL', 'http://loki:3100');

describe('GET /api/logs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns 401 when meepleai_user_role cookie is missing', async () => {
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns 401 when role is not admin', async () => {
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=user' },
    });
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns 503 when LOKI_URL is not configured', async () => {
    vi.stubEnv('LOKI_URL', '');
    // Re-import to pick up new env
    vi.resetModules();
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    const body = await res.json() as { lokiUnavailable: boolean };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
    expect(body.entries).toHaveLength(0);
    // Restore
    vi.stubEnv('LOKI_URL', 'http://loki:3100');
  });

  it('returns 503-like response when Loki fetch fails', async () => {
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockRejectedValueOnce(new Error('connect ECONNREFUSED'));
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    const body = await res.json() as { lokiUnavailable: boolean };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
  });

  it('returns normalized entries sorted descending by timestamp', async () => {
    vi.resetModules();
    const { GET } = await import('../route');
    const lokiResponse = {
      status: 'success',
      data: {
        resultType: 'streams',
        result: [
          {
            stream: { container_name: 'meepleai-api' },
            values: [
              // timestamp più vecchio → ERROR
              ['1712300000000000000', 'ERROR: database connection failed'],
              // timestamp più recente → WARNING (sarà entries[0] dopo sort desc)
              ['1712300001000000000', 'WARNING: high memory usage'],
            ],
          },
        ],
      },
    };
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => lokiResponse,
    });
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    expect(res.status).toBe(200);
    const body = await res.json() as { entries: Array<{ container: string; level: string }> };
    expect(body.entries).toHaveLength(2);
    expect(body.entries[0].container).toBe('meepleai-api');
    // Sort descending: WARNING (più recente) è entries[0], ERROR (più vecchio) è entries[1]
    expect(body.entries[0].level).toBe('warning');
    expect(body.entries[1].level).toBe('error');
  });

  it('returns 200 when role is superadmin', async () => {
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'success', data: { resultType: 'streams', result: [] } }),
    });
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=superadmin' },
    });
    const res = await GET(req);
    expect(res.status).toBe(200);
  });
});
```

- [ ] **Step 3: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/app/api/logs/__tests__/route.test.ts
```
Expected: FAIL — `Cannot find module '../route'`

- [ ] **Step 4: Implementare la API route**

```ts
// apps/web/src/app/api/logs/route.ts
import { NextRequest, NextResponse } from 'next/server';

import type { LokiLogEntry, LokiQueryRangeResponse, LogsApiResponse } from '@/lib/loki/types';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

const ADMIN_ROLES = new Set(['admin', 'superadmin']);
// container_name=~"meepleai-.*" scopea ai soli container MeepleAI (Fluent-bit label standard)
const LOKI_QUERY = '{container_name=~"meepleai-.*"} |~ "(?i)(error|warn|fatal)"';
const LIMIT = 100;
const LOOK_BACK_HOURS = 24;

function isAdminCookie(cookieHeader: string | null): boolean {
  if (!cookieHeader) return false;
  const match = cookieHeader.match(/meepleai_user_role=([^;]+)/);
  if (!match) return false;
  return ADMIN_ROLES.has(match[1].toLowerCase());
}

function detectLevel(line: string): LokiLogEntry['level'] {
  const lower = line.toLowerCase();
  if (lower.includes('fatal') || lower.includes('"level":"fatal"')) return 'error';
  if (lower.includes('error') || lower.includes('"level":"error"')) return 'error';
  if (lower.includes('warn') || lower.includes('"level":"warn"')) return 'warning';
  if (lower.includes('info') || lower.includes('"level":"info"')) return 'info';
  return 'unknown';
}

function nsToIso(ns: string): string {
  // Loki timestamps are nanoseconds since epoch
  const ms = Math.floor(Number(ns) / 1_000_000);
  return new Date(ms).toISOString();
}

export async function GET(request: NextRequest): Promise<NextResponse<LogsApiResponse>> {
  const cookieHeader = request.headers.get('cookie');
  if (!isAdminCookie(cookieHeader)) {
    return NextResponse.json({ entries: [] }, { status: 401 });
  }

  const lokiUrl = process.env.LOKI_URL;
  if (!lokiUrl) {
    return NextResponse.json({ entries: [], lokiUnavailable: true });
  }

  const now = Date.now();
  const start = now - LOOK_BACK_HOURS * 60 * 60 * 1000;
  const params = new URLSearchParams({
    query: LOKI_QUERY,
    start: String(start * 1_000_000), // to nanoseconds
    end: String(now * 1_000_000),
    limit: String(LIMIT),
    direction: 'backward',
  });

  let lokiData: LokiQueryRangeResponse;
  try {
    const response = await fetch(`${lokiUrl}/loki/api/v1/query_range?${params.toString()}`, {
      headers: { 'Content-Type': 'application/json' },
      signal: AbortSignal.timeout(10_000),
    });
    if (!response.ok) {
      return NextResponse.json({ entries: [], lokiUnavailable: true });
    }
    lokiData = await response.json() as LokiQueryRangeResponse;
  } catch {
    return NextResponse.json({ entries: [], lokiUnavailable: true });
  }

  const entries: LokiLogEntry[] = [];
  const result = lokiData.data?.result ?? [];
  for (let si = 0; si < result.length; si++) {
    const stream = result[si];
    const container =
      stream.stream['container_name'] ??
      stream.stream['container'] ??
      stream.stream['job'] ??
      'unknown';
    for (let vi = 0; vi < (stream.values?.length ?? 0); vi++) {
      const [ts, line] = stream.values[vi];
      entries.push({
        id: `${si}-${vi}`,
        timestamp: nsToIso(ts),
        container,
        message: line,
        level: detectLevel(line),
      });
    }
  }

  // Sort descending by timestamp (most recent first)
  entries.sort((a, b) => (a.timestamp > b.timestamp ? -1 : 1));

  return NextResponse.json({ entries });
}
```

- [ ] **Step 5: Eseguire i test per verificare che passino**

```bash
cd apps/web
pnpm test src/app/api/logs/__tests__/route.test.ts
```
Expected: PASS (5 tests)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/loki/types.ts apps/web/src/app/api/logs/route.ts apps/web/src/app/api/logs/__tests__/route.test.ts
git commit -m "feat(logs): add Loki proxy API route /api/logs with admin auth"
```

---

## Task 2: Componente `LokiErrorViewer`

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/LokiErrorViewer.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/LokiErrorViewer.test.tsx`

- [ ] **Step 1: Scrivere il test fallente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/LokiErrorViewer.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

import { LokiErrorViewer } from '../LokiErrorViewer';
import type { LogsApiResponse } from '@/lib/loki/types';

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

describe('LokiErrorViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    mockFetch.mockImplementation(() => new Promise(() => {}));
    render(<LokiErrorViewer />, { wrapper });
    expect(screen.getByTestId('loki-loading')).toBeInTheDocument();
  });

  it('shows unavailable message when Loki is not configured', async () => {
    const body: LogsApiResponse = { entries: [], lokiUnavailable: true };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() =>
      expect(screen.getByTestId('loki-unavailable')).toBeInTheDocument()
    );
  });

  it('shows empty state when no errors found', async () => {
    const body: LogsApiResponse = { entries: [] };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() =>
      expect(screen.getByTestId('loki-empty')).toBeInTheDocument()
    );
  });

  it('renders log entries with container and message', async () => {
    const body: LogsApiResponse = {
      entries: [
        {
          id: '0-0',
          timestamp: new Date().toISOString(),
          container: 'meepleai-api',
          message: 'ERROR: database connection failed',
          level: 'error',
        },
      ],
    };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() => {
      expect(screen.getByTestId('loki-table')).toBeInTheDocument();
      expect(screen.getByText('meepleai-api')).toBeInTheDocument();
      expect(screen.getByText(/database connection failed/i)).toBeInTheDocument();
    });
  });

  it('shows error state when fetch fails', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'));
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() =>
      expect(screen.getByTestId('loki-error')).toBeInTheDocument()
    );
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/app/admin/\\(dashboard\\)/monitor/logs/__tests__/LokiErrorViewer.test.tsx
```
Expected: FAIL — `Cannot find module '../LokiErrorViewer'`

- [ ] **Step 3: Implementare `LokiErrorViewer.tsx`**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/LokiErrorViewer.tsx
'use client';

import { useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertCircle, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import type { LokiLogEntry, LogsApiResponse } from '@/lib/loki/types';

function levelBadgeClass(level: LokiLogEntry['level']): string {
  switch (level) {
    case 'error':
      return 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300';
    case 'warning':
      return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300';
    case 'info':
      return 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300';
    default:
      return 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400';
  }
}

function shortContainer(name: string): string {
  return name.replace(/^meepleai-/, '');
}

function formatTimestamp(ts: string): string {
  try {
    return new Date(ts).toLocaleTimeString(undefined, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  } catch {
    return ts;
  }
}

async function fetchLokiLogs(): Promise<LogsApiResponse> {
  const res = await fetch('/api/logs');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json() as Promise<LogsApiResponse>;
}

export function LokiErrorViewer() {
  const { data, isFetching, isError, refetch } = useQuery({
    queryKey: ['admin', 'loki-errors'],
    queryFn: fetchLokiLogs,
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  const handleRefresh = useCallback(() => {
    void refetch();
  }, [refetch]);

  if (isFetching && !data) {
    return (
      <div className="flex items-center justify-center py-12" data-testid="loki-loading">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (isError) {
    return (
      <div
        className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-950/30 dark:text-red-400"
        data-testid="loki-error"
      >
        <AlertCircle className="h-4 w-4 shrink-0" />
        Unable to load container logs. Check your connection and try again.
      </div>
    );
  }

  if (data?.lokiUnavailable) {
    return (
      <div
        className="rounded-xl border bg-muted/30 p-6 text-center text-sm text-muted-foreground"
        data-testid="loki-unavailable"
      >
        <p className="font-medium">Loki log aggregation not available</p>
        <p className="mt-1 text-xs">
          Start the logging stack with <code className="rounded bg-muted px-1">make logging</code>{' '}
          on the staging server to enable container log collection.
        </p>
      </div>
    );
  }

  const entries = data?.entries ?? [];

  return (
    <div
      className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70"
      data-testid="loki-viewer"
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b px-4 py-3">
        <span className="text-xs text-muted-foreground">
          Last 100 errors/warnings · last 24h
        </span>
        <Button
          size="sm"
          variant="outline"
          onClick={handleRefresh}
          disabled={isFetching}
          data-testid="loki-refresh-btn"
        >
          <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {entries.length === 0 ? (
        <div
          className="py-12 text-center text-sm text-muted-foreground"
          data-testid="loki-empty"
        >
          No errors or warnings in the last 24 hours.
        </div>
      ) : (
        <div className="overflow-auto">
          <table className="w-full text-sm" data-testid="loki-table">
            <thead>
              <tr className="border-b bg-muted/30 text-left font-mono text-xs text-muted-foreground">
                <th className="px-2 py-2">Time</th>
                <th className="px-2 py-2">Level</th>
                <th className="px-2 py-2">Container</th>
                <th className="px-2 py-2">Message</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr
                  key={entry.id}
                  className="border-b transition-colors hover:bg-muted/40"
                  data-testid={`loki-row-${entry.id}`}
                >
                  <td className="whitespace-nowrap px-2 py-1.5 font-mono text-xs tabular-nums text-muted-foreground">
                    {formatTimestamp(entry.timestamp)}
                  </td>
                  <td className="px-2 py-1.5">
                    <Badge className={`text-[10px] font-medium ${levelBadgeClass(entry.level)}`}>
                      {entry.level}
                    </Badge>
                  </td>
                  <td className="whitespace-nowrap px-2 py-1.5 font-mono text-xs text-muted-foreground">
                    {shortContainer(entry.container)}
                  </td>
                  <td className="max-w-xs truncate px-2 py-1.5 font-mono text-xs lg:max-w-lg xl:max-w-2xl">
                    {entry.message}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Eseguire i test per verificare che passino**

```bash
cd apps/web
pnpm test src/app/admin/\\(dashboard\\)/monitor/logs/__tests__/LokiErrorViewer.test.tsx
```
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/logs/LokiErrorViewer.tsx apps/web/src/app/admin/\(dashboard\)/monitor/logs/__tests__/LokiErrorViewer.test.tsx
git commit -m "feat(logs): add LokiErrorViewer component with loading/error/empty states"
```

---

## Task 3: Integrazione nella pagina + infra

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`
- Modify: `infra/compose.staging.yml`

- [ ] **Step 1: Scrivere test per la pagina aggiornata**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/__tests__/page.test.tsx
// NOTA: questo file potrebbe già esistere — verificare prima di creare.
// Se esiste, aggiungere solo i nuovi test al file esistente.

import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

// Mock i componenti figli per isolare i test della pagina
vi.mock('../AppLogViewer', () => ({
  AppLogViewer: () => <div data-testid="mock-app-log-viewer" />,
}));
vi.mock('../LogViewer', () => ({
  LogViewer: () => <div data-testid="mock-log-viewer" />,
}));
vi.mock('../LokiErrorViewer', () => ({
  LokiErrorViewer: () => <div data-testid="mock-loki-viewer" />,
}));

import LogViewerPage from '../page';

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient();
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('LogViewerPage', () => {
  it('shows Container Errors tab', () => {
    render(<LogViewerPage />, { wrapper });
    expect(screen.getByRole('button', { name: /container errors/i })).toBeInTheDocument();
  });

  it('shows LokiErrorViewer when Container Errors tab is active', () => {
    render(<LogViewerPage />, { wrapper });
    fireEvent.click(screen.getByRole('button', { name: /container errors/i }));
    expect(screen.getByTestId('mock-loki-viewer')).toBeInTheDocument();
  });

  it('renders without crashing regardless of NEXT_PUBLIC_GRAFANA_URL', () => {
    // NOTA: NEXT_PUBLIC_GRAFANA_URL è letta a load-time del modulo, non dinamicamente.
    // vi.stubEnv non la influenza dopo l'import. Il link Grafana è presente
    // solo se la var era settata al momento del build/import del modulo.
    // Questo test verifica che la pagina non crashe in entrambi i casi.
    render(<LogViewerPage />, { wrapper });
    expect(screen.getByTestId('logs-page')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca (tab mancante)**

```bash
cd apps/web
pnpm test src/app/admin/\\(dashboard\\)/monitor/logs/__tests__/page.test.tsx
```
Expected: FAIL — "Container Errors" button not found

- [ ] **Step 3: Modificare `page.tsx`**

Sostituire il contenuto completo del file:

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx
'use client';

/**
 * Log Viewer Page — Application + Container logs + Container Errors (Loki)
 * Issue #141 / #1.8 + #3367 (Hybrid Solution C)
 */

import { useState } from 'react';

import { ExternalLink } from 'lucide-react';

import { AppLogViewer } from './AppLogViewer';
import { LokiErrorViewer } from './LokiErrorViewer';
import { LogViewer } from './LogViewer';

type LogTab = 'app' | 'container' | 'loki';

const GRAFANA_URL = process.env.NEXT_PUBLIC_GRAFANA_URL;

// Costruisce l'URL Grafana Explore con JSON correttamente URL-encoded
function buildGrafanaExploreUrl(baseUrl: string): string {
  const state = JSON.stringify({
    datasource: 'loki',
    queries: [{ refId: 'A', expr: '{container_name=~"meepleai-.*"}' }],
  });
  return `${baseUrl}/explore?left=${encodeURIComponent(state)}`;
}

export default function LogViewerPage() {
  const [activeTab, setActiveTab] = useState<LogTab>('app');

  return (
    <div data-testid="logs-page" className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Log Viewer</h1>
          <p className="text-muted-foreground">Application and container log monitoring</p>
        </div>
        {GRAFANA_URL && (
          <a
            href={buildGrafanaExploreUrl(GRAFANA_URL)}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 rounded-md border px-3 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            data-testid="grafana-link"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Open in Grafana
          </a>
        )}
      </div>

      <div className="flex gap-1 border-b">
        <button
          onClick={() => setActiveTab('app')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'app'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Application Logs
        </button>
        <button
          onClick={() => setActiveTab('container')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'container'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Container Logs
        </button>
        <button
          onClick={() => setActiveTab('loki')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'loki'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Container Errors
        </button>
      </div>

      {activeTab === 'app' && <AppLogViewer />}
      {activeTab === 'container' && <LogViewer />}
      {activeTab === 'loki' && <LokiErrorViewer />}
    </div>
  );
}
```

- [ ] **Step 4: Eseguire i test per verificare che passino**

```bash
cd apps/web
pnpm test src/app/admin/\\(dashboard\\)/monitor/logs/__tests__/page.test.tsx
```
Expected: PASS (3 tests)

- [ ] **Step 5: Modificare `infra/compose.staging.yml` — aggiungere env vars al servizio `web`**

Nel blocco `environment:` del servizio `web` (dopo `API_BASE_URL: http://api:8080`), aggiungere:

```yaml
      LOKI_URL: http://loki:3100
      NEXT_PUBLIC_GRAFANA_URL: /grafana
```

Il risultato atteso nella sezione `web.environment`:
```yaml
  web:
    restart: always
    build:
      args:
        NEXT_PUBLIC_API_BASE: "https://meepleai.app"
        NEXT_PUBLIC_APP_URL: "https://meepleai.app"
    environment:
      NODE_ENV: production
      NEXT_PUBLIC_API_BASE: "https://meepleai.app"
      NEXT_PUBLIC_APP_URL: "https://meepleai.app"
      API_BASE_URL: http://api:8080
      LOKI_URL: http://loki:3100
      NEXT_PUBLIC_GRAFANA_URL: /grafana
```

**Nota importante**: `NEXT_PUBLIC_GRAFANA_URL` è una variabile build-time in Next.js. Per averla disponibile in produzione, deve essere presente anche nel blocco `build.args`:
```yaml
    build:
      args:
        NEXT_PUBLIC_API_BASE: "https://meepleai.app"
        NEXT_PUBLIC_APP_URL: "https://meepleai.app"
        NEXT_PUBLIC_GRAFANA_URL: /grafana
```

- [ ] **Step 6: Eseguire typecheck e lint**

```bash
cd apps/web
pnpm typecheck
pnpm lint
```
Expected: 0 errors

- [ ] **Step 7: Eseguire tutti i test del progetto**

```bash
cd apps/web
pnpm test
```
Expected: tutti i test passano

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/logs/page.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/logs/__tests__/page.test.tsx \
        infra/compose.staging.yml
git commit -m "feat(logs): integrate LokiErrorViewer tab + Grafana link + staging env vars"
```

---

## Self-Review

### Verifica copertura spec

| Requisito | Implementato |
|-----------|-------------|
| Visualizzare log container via browser autenticato | ✅ Tab "Container Errors" in admin panel |
| Filtro livello (ERROR/WARN) | ✅ LogQL query filtra automaticamente error/warn/fatal |
| Accesso protetto da autenticazione | ✅ API route verifica cookie `meepleai_user_role` |
| Link a Grafana per analisi approfondita | ✅ Pulsante "Open in Grafana" (solo se `NEXT_PUBLIC_GRAFANA_URL` settato) |
| Graceful degradation quando Loki non disponibile | ✅ Stato `lokiUnavailable` con messaggio esplicativo |
| `LOKI_URL` server-side only | ✅ Non NEXT_PUBLIC |

### Placeholder scan

Nessun TODO, TBD o placeholder nel codice — ogni step ha codice completo.

### Type consistency

- `LokiLogEntry` e `LogsApiResponse` definiti in `types.ts` e usati in `route.ts`, `LokiErrorViewer.tsx`, e nei test — consistenti.
- `fetchLokiLogs` in `LokiErrorViewer` ritorna `Promise<LogsApiResponse>` — type-safe.
- `level` field: `'error' | 'warning' | 'info' | 'unknown'` — consistente tra types, route, component, e test.

### Correzioni applicate post-review (2026-04-05)

| # | Fix | Impatto |
|---|-----|---------|
| 1 | Test T-ROUTE-05: `entries[0]` = warning (più recente), `entries[1]` = error dopo sort desc | Bug runtime risolto |
| 2 | Grafana Explore URL: `encodeURIComponent(JSON.stringify(...))` invece di encoding parziale | Link funzionante in browser |
| 3 | `LokiErrorViewer.test.tsx`: aggiunto `beforeEach(() => vi.clearAllMocks())` | Test isolation |
| 4 | `route.ts`: null guard `lokiData.data?.result ?? []` | Crash prevention |
| 5 | `LOKI_QUERY`: rimosso `| line_format "{{.}}"`, usato `container_name=~"meepleai-.*"` | Query più precisa |
| 6 | Test superadmin aggiunto in `route.test.ts` | Copertura ruoli |
| 7 | `page.test.tsx`: test Grafana link convertito a test anti-crash (env var non testabile post-import) | Honesty |

### Note operative

- **Build-time vs runtime**: `NEXT_PUBLIC_GRAFANA_URL` deve essere in `build.args` IN AGGIUNTA a `environment` per essere inclusa nel bundle client. Documentato nel Task 3 Step 5.
- **Loki non disponibile in dev locale**: Il tab mostra il messaggio `lokiUnavailable` senza errori — degrada gracefully.
- **Worktree esistente**: Il branch `feature/log-viewer` esiste in `.worktrees/feature/log-viewer/` con l'infra Loki già implementata. Questo piano implementa solo il frontend — va eseguito nello stesso worktree.
- **Fluent-bit label**: La query usa `container_name` (label standard Docker socket input). Se il config Fluent-bit usa un label diverso, cambiare `LOKI_QUERY` di conseguenza.
