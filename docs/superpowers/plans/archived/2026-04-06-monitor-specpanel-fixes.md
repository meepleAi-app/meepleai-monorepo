# Monitor Hub — Spec-Panel Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere 7 bug e applicare 4 miglioramenti UX alla pagina `/admin/monitor` emersi dall'analisi spec-panel.

**Architecture:** Modifiche chirurgiche a componenti esistenti — nessuna nuova architettura. Un solo metodo aggiunto al monitor client (`getAlertHistory`). Tutte le modifiche sono auto-contenute nei rispettivi file tab o sub-componenti.

**Tech Stack:** Next.js 16 App Router · React 19 · TypeScript · Vitest + Testing Library · Zod · TanStack Query · Sonner (toast)

---

## File Map

| File | Modifica |
|------|----------|
| `apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx` | Add toast success/error on clearKBCache |
| `apps/web/src/app/admin/(dashboard)/monitor/__tests__/CacheTab.test.tsx` | Add test for toast feedback |
| `apps/web/src/components/admin/alert-rules/AlertRuleList.tsx` | Make `onEdit` optional → hide Edit button if undefined |
| `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx` | Remove `onEdit` prop + add auto-refresh 30s |
| `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx` | Update test for removed edit button |
| `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts` | Add `getAlertHistory()` method + Zod schema |
| `apps/web/src/app/admin/(dashboard)/monitor/AlertHistoryTab.tsx` | Replace raw `fetch` with `api.admin.getAlertHistory()` |
| `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertHistoryTab.test.tsx` | Update mock to use `api.admin` |
| `apps/web/src/app/admin/(dashboard)/monitor/containers/ContainerDashboard.tsx` | Fix View Logs href → `?tab=logs` |
| `apps/web/src/app/admin/(dashboard)/monitor/mau/MauDashboard.tsx` | Remove `.slice(-14)` da daily breakdown |
| `apps/web/src/app/admin/(dashboard)/monitor/EmailManagementTab.tsx` | Replace module-level client con `api.admin` |
| `apps/web/src/app/admin/(dashboard)/monitor/grafana/GrafanaDashboard.tsx` | Nascondi selettore dashboard quando non configurato |
| `apps/web/src/app/admin/(dashboard)/monitor/logs/LogViewer.tsx` | Add h2 header |
| `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` | Rinomina tab Cache→Metrics, rimuovi tab Operations |

---

## Task 1 — CacheTab: feedback toast su Clear Cache

**Problema:** `handleClearCache` inghiotte silenziosamente successi e fallimenti. Il commento nel codice dice esplicitamente "toast could be added later".

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/CacheTab.test.tsx`

- [ ] **Step 1.1 — Aggiorna il test per aspettarsi il toast di successo**

In `CacheTab.test.tsx`, aggiungi il mock di sonner e il test:

```typescript
// Aggiungi dopo gli import esistenti, prima di vi.mock('@/lib/api', ...)
const mockToastSuccess = vi.hoisted(() => vi.fn());
const mockToastError = vi.hoisted(() => vi.fn());

vi.mock('sonner', () => ({
  toast: {
    success: mockToastSuccess,
    error: mockToastError,
  },
}));
```

Aggiungi questi due test alla fine del describe block:

```typescript
  it('shows success toast after clearing cache', async () => {
    const user = userEvent.setup();
    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /clear cache/i }));
    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Cache KB svuotata');
    });
  });

  it('shows error toast when clear cache fails', async () => {
    mockClearKBCache.mockRejectedValue(new Error('Network error'));
    const user = userEvent.setup();
    render(<CacheTab />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /clear cache/i }));
    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Errore nel svuotamento della cache');
    });
  });
```

- [ ] **Step 1.2 — Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/CacheTab.test.tsx
```

Atteso: FAIL — "mockToastSuccess is not called"

- [ ] **Step 1.3 — Aggiorna CacheTab.tsx**

Aggiungi l'import di `toast` da sonner (già usato altrove nel progetto):

```typescript
// In cima al file, dopo gli import esistenti
import { toast } from 'sonner';
```

Sostituisci l'intero `handleClearCache`:

```typescript
  const handleClearCache = useCallback(async () => {
    setClearing(true);
    try {
      await api.admin.clearKBCache();
      toast.success('Cache KB svuotata');
    } catch {
      toast.error('Errore nel svuotamento della cache');
    } finally {
      setClearing(false);
    }
  }, []);
```

- [ ] **Step 1.4 — Esegui i test**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/CacheTab.test.tsx
```

Atteso: PASS (5 tests)

- [ ] **Step 1.5 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/CacheTab.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/CacheTab.test.tsx
git commit -m "fix(monitor): add toast feedback on CacheTab clear cache action"
```

---

## Task 2 — AlertRuleList + AlertsTab: rimuovi Edit button + auto-refresh

**Problema A:** Il button Edit in AlertRuleList chiama `handleEdit` che fa solo toggle — comportamento ingannnevole. L'editing non è ancora implementato.

**Problema B:** Gli alert sono dati critici ma non si auto-aggiornano.

**Files:**
- Modify: `apps/web/src/components/admin/alert-rules/AlertRuleList.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx`

- [ ] **Step 2.1 — Scrivi test per verificare: nessun Edit button, presenza auto-refresh**

Controlla prima l'attuale test `AlertsTab.test.tsx`:

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/AlertsTab.test.tsx
```

Verifica che passa con lo stato attuale.

- [ ] **Step 2.2 — Rendi `onEdit` opzionale in AlertRuleList.tsx**

Sostituisci l'interfaccia e la firma del componente:

```typescript
interface AlertRuleListProps {
  rules: AlertRule[];
  onEdit?: (rule: AlertRule) => void;  // opzionale
  onDelete: (id: string) => void;
  onToggle: (id: string) => void;
}

export function AlertRuleList({ rules, onEdit, onDelete, onToggle }: AlertRuleListProps) {
```

Sostituisci il blocco Actions nella TableCell (riga 75-83):

```typescript
                <TableCell>
                  <div className="flex gap-2">
                    {onEdit && (
                      <Button variant="ghost" size="sm" onClick={() => onEdit(rule)}>
                        <Edit className="h-4 w-4" />
                      </Button>
                    )}
                    <Button variant="ghost" size="sm" onClick={() => onDelete(rule.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
```

- [ ] **Step 2.3 — Aggiorna AlertsTab.tsx: rimuovi onEdit + aggiungi auto-refresh**

Aggiungi `useRef` agli import di React (già c'è `useState, useEffect`):

```typescript
import { useState, useEffect, useRef } from 'react';
```

Sostituisci il blocco degli hook (dopo `const [createDialogOpen, setCreateDialogOpen] = useState(false);`), aggiungendo il ref per l'intervallo:

```typescript
  const refreshIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
```

Sostituisci il `useEffect` esistente con questo (che include auto-refresh 30s):

```typescript
  useEffect(() => {
    loadData();

    refreshIntervalRef.current = setInterval(loadData, 30_000);

    return () => {
      if (refreshIntervalRef.current) {
        clearInterval(refreshIntervalRef.current);
      }
    };
  }, []);
```

Rimuovi `handleEdit` (righe 71-76) e aggiorna il render di `AlertRuleList` rimuovendo `onEdit`:

```typescript
      <AlertRuleList
        rules={rules}
        onDelete={handleDelete}
        onToggle={handleToggle}
      />
```

Rimuovi anche l'import inutilizzato se `handleEdit` era l'unico consumatore di `toast.info` — controlla che `toast` sia ancora usato altrove nel file (sì: `toast.error` e `toast.success` sono usati, quindi l'import rimane).

- [ ] **Step 2.4 — Esegui i test**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/AlertsTab.test.tsx
```

Atteso: PASS (i test non dovrebbero aspettarsi il Edit button — verificare e aggiornare se necessario rimuovendo qualsiasi test che cerca un Edit icon).

- [ ] **Step 2.5 — Commit**

```bash
git add apps/web/src/components/admin/alert-rules/AlertRuleList.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/AlertsTab.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/AlertsTab.test.tsx
git commit -m "fix(monitor): remove misleading Edit button from AlertRuleList, add 30s auto-refresh"
```

---

## Task 3 — AlertHistoryTab: sostituisci raw fetch con api.admin

**Problema:** `AlertHistoryTab` usa `fetch('/api/v1/admin/alerts')` direttamente, bypassando il client condiviso (auth, retry, error normalization). Non esiste ancora un metodo `getAlertHistory` in `adminMonitorClient`.

**Files:**
- Modify: `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/AlertHistoryTab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertHistoryTab.test.tsx`

- [ ] **Step 3.1 — Aggiungi `getAlertHistory` ad adminMonitorClient.ts**

Aggiungi l'import Zod schema in cima al file (nella sezione imports, dopo `import { z } from 'zod';`):

```typescript
// Aggiunge nello stesso blocco degli altri import z
const AlertHistoryItemSchema = z.object({
  id: z.string(),
  alertType: z.string(),
  severity: z.enum(['Critical', 'Warning', 'Info']),
  message: z.string(),
  metadata: z.record(z.unknown()).nullable(),
  triggeredAt: z.string(),
  resolvedAt: z.string().nullable(),
  isActive: z.boolean(),
  channelSent: z.record(z.boolean()).nullable(),
});
```

Aggiungi il metodo all'oggetto ritornato da `createAdminMonitorClient`, dopo il metodo `getCircuitBreakerStates`:

```typescript
    // ========== Alert History ==========

    async getAlertHistory(): Promise<z.infer<typeof AlertHistoryItemSchema>[]> {
      const res = await http.get('/api/v1/admin/alerts');
      return z.array(AlertHistoryItemSchema).parse(res ?? []);
    },
```

- [ ] **Step 3.2 — Aggiorna AlertHistoryTab.tsx**

Sostituisci l'import di `useQuery` con la versione che usa `api.admin`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { History } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
```

(rimuovi l'interfaccia locale `AlertHistoryItem` — ora viene dall'API client)

Sostituisci la `queryFn`:

```typescript
  const { data: alerts = [], isLoading } = useQuery({
    queryKey: ['admin', 'alerts', 'history'],
    queryFn: () => api.admin.getAlertHistory(),
    refetchInterval: 30_000,
    retry: false,
  });
```

Rimuovi l'interfaccia `AlertHistoryItem` locale (righe 12-22 del file originale) e aggiorna il tipo nella riga di filtro:

```typescript
  const filtered = alerts.filter(alert => {
```

(il tipo viene ora inferito automaticamente da Zod)

- [ ] **Step 3.3 — Aggiorna AlertHistoryTab.test.tsx**

Controlla il contenuto attuale del test:

```bash
cat "apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertHistoryTab.test.tsx"
```

Sostituisci il mock da `global.fetch` / mock fetch a `api.admin`:

```typescript
import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetAlertHistory = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAlertHistory: mockGetAlertHistory,
    },
  },
}));

import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AlertHistoryTab } from '../AlertHistoryTab';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

const SAMPLE_ALERT = {
  id: '1',
  alertType: 'HighErrorRate',
  severity: 'Critical' as const,
  message: 'Error rate above threshold',
  metadata: null,
  triggeredAt: '2026-01-01T10:00:00Z',
  resolvedAt: null,
  isActive: true,
  channelSent: { email: true },
};

describe('AlertHistoryTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAlertHistory.mockResolvedValue([]);
  });

  it('renders heading', async () => {
    renderWithQuery(<AlertHistoryTab />);
    await waitFor(() => {
      expect(screen.getByText('Alert History')).toBeInTheDocument();
    });
  });

  it('shows empty state when no alerts', async () => {
    renderWithQuery(<AlertHistoryTab />);
    await waitFor(() => {
      expect(screen.getByText('No alerts')).toBeInTheDocument();
    });
  });

  it('renders alert rows', async () => {
    mockGetAlertHistory.mockResolvedValue([SAMPLE_ALERT]);
    renderWithQuery(<AlertHistoryTab />);
    await waitFor(() => {
      expect(screen.getByText('HighErrorRate')).toBeInTheDocument();
      expect(screen.getByText('Critical')).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 3.4 — Esegui i test**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/AlertHistoryTab.test.tsx
```

Atteso: PASS

- [ ] **Step 3.5 — Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminMonitorClient.ts \
        apps/web/src/app/admin/\(dashboard\)/monitor/AlertHistoryTab.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/AlertHistoryTab.test.tsx
git commit -m "fix(monitor): replace raw fetch with api.admin.getAlertHistory in AlertHistoryTab"
```

---

## Task 4 — ContainerDashboard: fix href View Logs

**Problema:** Il link "View Logs" punta a `/admin/monitor/logs` (sotto-route separata), non al tab `?tab=logs` nella stessa pagina hub.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/containers/ContainerDashboard.tsx`

- [ ] **Step 4.1 — Aggiorna l'href in ContainerCard**

In `ContainerDashboard.tsx`, riga ~99, sostituisci:

```typescript
          <Link
            href={`/admin/monitor/logs`}
```

con:

```typescript
          <Link
            href={`/admin/monitor?tab=logs`}
```

- [ ] **Step 4.2 — Esegui i test containers**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/containers/__tests__/
```

Atteso: PASS

- [ ] **Step 4.3 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/containers/ContainerDashboard.tsx
git commit -m "fix(monitor): fix View Logs link to use tab navigation instead of sub-route"
```

---

## Task 5 — MauDashboard: rimuovi slice(-14) dal daily breakdown

**Problema:** Con periodo 90d selezionato, la tabella mostra solo 14 righe senza nessuna indicazione. Il `.slice(-14)` è hardcoded indipendentemente dal periodo scelto.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/mau/MauDashboard.tsx`

- [ ] **Step 5.1 — Sostituisci slice hardcoded con slice dinamico**

In `MauDashboard.tsx`, riga ~147, sostituisci:

```typescript
                  {data.dailyBreakdown.slice(-14).map(day => (
```

con:

```typescript
                  {data.dailyBreakdown.slice(-period).map(day => (
```

La variabile `period` (tipo `7 | 30 | 90`) è già disponibile nello scope del componente — filtra correttamente in base al periodo selezionato.

- [ ] **Step 5.2 — Esegui i test MAU**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/mau/__tests__/
```

Atteso: PASS

- [ ] **Step 5.3 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/mau/MauDashboard.tsx
git commit -m "fix(monitor): fix MauDashboard daily breakdown to respect selected period instead of hardcoded 14 days"
```

---

## Task 6 — EmailManagementTab: usa api.admin singleton

**Problema:** Il componente crea due istanze module-level (`new HttpClient()` + `createAdminClient`) invece di usare il singleton `api.admin` come tutti gli altri tab.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/EmailManagementTab.tsx`

- [ ] **Step 6.1 — Aggiorna EmailManagementTab.tsx**

Rimuovi le righe 20-25 (module-level instantiation):

```typescript
// RIMUOVI queste due righe:
const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });
```

Aggiungi l'import di `api`:

```typescript
import { api } from '@/lib/api';
```

Rimuovi gli import inutilizzati:

```typescript
// RIMUOVI da @/lib/api/clients/adminClient:
import { createAdminClient, type EmailQueueItem } from '@/lib/api/clients/adminClient';
// RIMUOVI:
import { HttpClient } from '@/lib/api/core/httpClient';
```

Aggiungi il tipo `EmailQueueItem` dall'import corretto:

```typescript
import type { EmailQueueItem } from '@/lib/api/clients/adminClient';
```

Sostituisci tutte le occorrenze `adminClient.` con `api.admin.` nelle queryFn e mutationFn. L'elenco completo:

```typescript
// statsQuery
queryFn: () => api.admin.getEmailQueueStats(),

// deadLetterQuery
queryFn: () => api.admin.getDeadLetterEmails({ take: 50 }),

// historyQuery
queryFn: () => api.admin.getEmailHistory({ take: 20, search: searchTerm || undefined }),

// retryMutation
mutationFn: (id: string) => api.admin.retryEmail(id),

// retryAllMutation
mutationFn: () => api.admin.retryAllDeadLetters(),

// sendTestMutation
mutationFn: (to: string) => api.admin.sendTestEmail(to),
```

- [ ] **Step 6.2 — Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i emailmanagement
```

Atteso: nessun errore relativo a EmailManagementTab

- [ ] **Step 6.3 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/EmailManagementTab.tsx
git commit -m "fix(monitor): replace module-level httpClient instantiation with api.admin singleton in EmailManagementTab"
```

---

## Task 7 — GrafanaDashboard: nascondi selettore quando non configurato

**Problema:** Quando `NEXT_PUBLIC_GRAFANA_URL` non è settato, l'utente può selezionare dashboard e poi vede "Grafana Not Configured". Il selettore non ha senso se Grafana non è operativo.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/grafana/GrafanaDashboard.tsx`

- [ ] **Step 7.1 — Aggiungi early return se non configurato**

⚠️ **ATTENZIONE hooks order**: il return deve essere posizionato DOPO tutti i hook (`useState`, `useMemo`, `useCallback`) — non dopo `isConfigured` che è prima di `useMemo`.

In `GrafanaDashboard.tsx`, aggiungi il return condizionale dopo `handleOpenFullscreen` (l'ultimo hook, riga ~291) e prima del `return` del JSX principale:

```typescript
  const handleOpenFullscreen = useCallback(() => {
    if (!iframeUrl) return;
    window.open(iframeUrl, '_blank', 'noopener,noreferrer');
  }, [iframeUrl]);

  // Early return DOPO tutti gli hook — valido per Rules of Hooks
  if (!isConfigured) {
    return <GrafanaNotConfigured />;
  }

  return (
    <div className="space-y-5" data-testid="grafana-dashboard">
```

Rimuovi la riga `{!selectedId ? ... : isConfigured ? ... : <GrafanaNotConfigured />}` in fondo al JSX (ora non serve più perché il caso non-configurato è già gestito sopra).

- [ ] **Step 7.2 — Esegui test Grafana**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/GrafanaTab.test.tsx
```

Atteso: PASS — il test `grafana-not-configured` già esiste e testa questo scenario.

- [ ] **Step 7.3 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/grafana/GrafanaDashboard.tsx
git commit -m "fix(monitor): hide Grafana dashboard selector when NEXT_PUBLIC_GRAFANA_URL is not configured"
```

---

## Task 8 — page.tsx: rinomina tab Cache→Metrics, rimuovi tab Operations

**Problema A:** Il tab "Cache" mostra metriche Prometheus (latency, error rate, LLM cost), non metriche di cache. Nome fuorviante.

**Problema B:** Il tab "Operations" contiene solo 4 link-card a sotto-route — nessun contenuto proprio. È un intermediario inutile.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`

- [ ] **Step 8.1 — Aggiorna TABS in page.tsx**

Sostituisci nel array `TABS` la voce cache:

```typescript
  { id: 'cache', label: 'Metrics', href: '/admin/monitor?tab=cache', icon: <Database /> },
```

(l'id rimane `cache` per compatibilità URL — chi aveva il link salvato continua a funzionare)

Rimuovi la voce operations dall'array `TABS`:

```typescript
// RIMUOVI questa riga:
  {
    id: 'operations',
    label: 'Operations',
    href: '/admin/monitor?tab=operations',
    icon: <Settings />,
  },
```

- [ ] **Step 8.2 — Rimuovi import e case non più usati**

Rimuovi l'import di `OperationsLinkTab`:

```typescript
// RIMUOVI:
import { OperationsLinkTab } from './OperationsLinkTab';
```

Rimuovi l'import di `Settings` da lucide-react se non usato altrove:

```typescript
// In import { ..., Settings } from 'lucide-react':
// rimuovi Settings
```

Rimuovi il case `operations` da `renderTabContent`:

```typescript
// RIMUOVI:
    case 'operations':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <OperationsLinkTab />
        </Suspense>
      );
```

- [ ] **Step 8.3 — Esegui test page**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/__tests__/page.test.tsx
```

Atteso: PASS — aggiornare il test se cerca la label "Cache" o il tab "Operations".

- [ ] **Step 8.4 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/page.tsx
git commit -m "fix(monitor): rename Cache tab to Metrics, remove redundant Operations link tab"
```

---

## Task 9 — LogViewer: aggiungi header

**Problema:** `LogViewer` è l'unico componente tab senza h2/header, creando inconsistenza visiva con tutti gli altri tab.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/logs/LogViewer.tsx`

- [ ] **Step 9.1 — Aggiungi header a LogViewer.tsx**

Nella funzione `LogViewer`, dopo l'early return per empty state (riga ~89), il return principale inizia con `<div className="flex gap-4"`. Wrappa tutto in un `div` con header:

Sostituisci il `return` principale (riga 89):

```typescript
  return (
    <div className="space-y-4">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Container Logs
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Seleziona un container per visualizzare i log in tempo reale.
        </p>
      </div>
      <div className="flex gap-4" data-testid="log-viewer">
        {/* Container sidebar */}
        <div
          data-testid="container-list"
          className="w-64 shrink-0 space-y-2 rounded-xl border bg-white/70 p-4 backdrop-blur-md dark:bg-zinc-900/70"
        >
          <h3 className="mb-3 text-sm font-semibold text-muted-foreground">Containers</h3>
          {containers.map(container => (
            <button
              key={container.id}
              data-testid={`container-item-${container.id}`}
              type="button"
              onClick={() => handleContainerClick(container.id)}
              className={`w-full rounded-lg px-3 py-2 text-left text-sm transition-colors ${
                selectedContainer === container.id ? 'bg-primary/10 text-primary' : 'hover:bg-muted'
              }`}
            >
              <div className="font-medium">{container.name}</div>
              <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <span
                  className={`inline-block h-2 w-2 rounded-full ${
                    container.state === 'running' ? 'bg-green-500' : 'bg-red-500'
                  }`}
                />
                {container.status}
              </div>
            </button>
          ))}
        </div>

        {/* Log output panel */}
        <div className="min-w-0 flex-1 rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
          {logs ? (
            <div className="flex h-full flex-col">
              <div className="flex items-center justify-between border-b px-4 py-3">
                <h3 className="text-sm font-semibold">{logs.containerName} — Logs</h3>
                <button
                  data-testid="log-refresh-btn"
                  type="button"
                  onClick={handleRefresh}
                  disabled={logsLoading}
                  className="rounded-md px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/10 disabled:opacity-50"
                >
                  {logsLoading ? 'Loading...' : 'Refresh'}
                </button>
              </div>
              <pre
                data-testid="log-output"
                className="flex-1 overflow-auto p-4 font-mono text-xs leading-relaxed text-foreground"
              >
                {logs.lines.join('\n')}
              </pre>
            </div>
          ) : (
            <div className="flex h-64 items-center justify-center text-muted-foreground">
              Select a container to view logs
            </div>
          )}
        </div>
      </div>
    </div>
  );
```

- [ ] **Step 9.2 — Esegui test LogViewer**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/logs/__tests__/LogViewer.test.tsx
```

Atteso: PASS — il `data-testid="log-viewer"` rimane intatto.

- [ ] **Step 9.3 — Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/logs/LogViewer.tsx
git commit -m "fix(monitor): add missing header to LogViewer for visual consistency"
```

---

## Task 10 — Run completo dei test + typecheck

- [ ] **Step 10.1 — Typecheck completo**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 10.2 — Test suite completa monitor**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/monitor/
```

Atteso: tutti PASS

- [ ] **Step 10.3 — Lint**

```bash
cd apps/web && pnpm lint --max-warnings 0 src/app/admin/\\(dashboard\\)/monitor/
```

Atteso: 0 warnings

- [ ] **Step 10.4 — Commit finale se tutto OK**

```bash
git log --oneline -10
```

Verifica che tutti i commit del piano siano presenti nell'history.

---

## Self-Review Checklist

- [x] **Bug #1 CacheTab silent** → Task 1
- [x] **Bug #2 AlertsTab edit=toggle** → Task 2
- [x] **Bug #3 AlertHistoryTab raw fetch** → Task 3
- [x] **Bug #4 ContainerDashboard View Logs href** → Task 4
- [x] **Bug #5 MAU slice(-14)** → Task 5
- [x] **Bug #6 EmailManagementTab module-level client** → Task 6
- [x] **Bug #7 GrafanaDashboard selector sempre visibile** → Task 7
- [x] **Improvement: Rename Cache→Metrics** → Task 8
- [x] **Improvement: Remove Operations tab** → Task 8
- [x] **Improvement: Auto-refresh AlertsTab** → Task 2
- [x] **Improvement: LogViewer header** → Task 9
- [x] Nessun placeholder TBD/TODO nel piano
- [x] I tipi Zod in Task 3 corrispondono all'interfaccia `AlertHistoryItem` originale
- [x] Il `data-testid="log-viewer"` rimane nel Task 9
- [x] L'id tab `cache` rimane invariato in Task 8 (compatibilità URL)
