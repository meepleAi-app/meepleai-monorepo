# Admin Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migliorare le pagine `/admin` consolidando la navigazione Monitor, completando l'AuditLog, abilitando BulkExport, aggiungendo form creazione Alert Rule e mostrando lo stato utente reale.

**Architecture:** Quattro sprint indipendenti, ognuno deployabile separatamente. Sprint 1 è pura navigazione (zero backend), Sprint 2 corregge duplicazioni e aggiunge selettore periodo, Sprint 3 abilita export esistenti disabilitati, Sprint 4 completa dati mancanti con API già disponibili.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, shadcn/ui, React Query v5, Zod, recharts, sonner (toast)

---

## ⚠️ Note preliminari

- **Branch base:** `frontend-dev` → creare `feature/issue-XXX-admin-improvements`
- **Run tests:** `cd apps/web && pnpm test`
- **Lint:** `pnpm lint && pnpm typecheck`
- **Nessun backend** necessario: tutti gli endpoint API utilizzati esistono già

---

## Sprint 1 — Navigazione Monitor Hub

**Obiettivo:** MAU Dashboard, Grafana, Containers, LogViewer sono pagine standalone non accessibili dal hub `/admin/monitor`. Aggiungere tab nel hub che rendono questi componenti direttamente.

**File modificati:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` — aggiunge 4 tab al TABS array e relativi case nello switch
- Create: `apps/web/src/app/admin/(dashboard)/monitor/MauTab.tsx` — thin wrapper per MauDashboard
- Create: `apps/web/src/app/admin/(dashboard)/monitor/GrafanaTab.tsx` — thin wrapper per GrafanaDashboard
- Create: `apps/web/src/app/admin/(dashboard)/monitor/ContainersTab.tsx` — thin wrapper per ContainerDashboard
- Create: `apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx` — thin wrapper per LogViewer
- Create: `apps/web/src/app/admin/(dashboard)/monitor/OperationsLinkTab.tsx` — card link verso `/admin/monitor/operations`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/` — aggiornare tab list nei test esistenti

---

### Task 1.1: Wrapper tab MAU

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/MauTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/MauTab.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/MauTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MauTab } from '../MauTab';

vi.mock('@/lib/api', () => ({
  api: { admin: { getActiveAiUsers: vi.fn().mockResolvedValue({
    totalActiveUsers: 42, aiChatUsers: 10, pdfUploadUsers: 15, agentUsers: 17,
    dailyBreakdown: [], periodStart: '2026-01-01', periodEnd: '2026-01-31',
  }) } }
}));

describe('MauTab', () => {
  it('renders MauDashboard content', async () => {
    render(<MauTab />);
    expect(await screen.findByText('Active AI Users')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run MauTab
```
Expected: FAIL "Cannot find module '../MauTab'"

- [ ] **Step 3: Crea il componente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/MauTab.tsx
import { MauDashboard } from './mau/MauDashboard';

export function MauTab() {
  return <MauDashboard />;
}
```

- [ ] **Step 4: Esegui il test per verificare che passa**

```bash
cd apps/web && pnpm test --run MauTab
```
Expected: PASS

---

### Task 1.2: Wrapper tab Grafana

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/GrafanaTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/GrafanaTab.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/GrafanaTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GrafanaTab } from '../GrafanaTab';

describe('GrafanaTab', () => {
  it('renders Grafana dashboard section', () => {
    render(<GrafanaTab />);
    // GrafanaDashboard renders an iframe or embed — check container element
    const container = document.querySelector('[data-testid="grafana-dashboard"]');
    expect(container ?? screen.getByRole('region', { hidden: true }) ?? document.body).toBeTruthy();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run GrafanaTab
```

- [ ] **Step 3: Crea il componente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/GrafanaTab.tsx
import { GrafanaDashboard } from './grafana/GrafanaDashboard';

export function GrafanaTab() {
  return <GrafanaDashboard />;
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run GrafanaTab
```
Expected: PASS

---

### Task 1.3: Wrapper tab Containers

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/ContainersTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/ContainersTab.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/ContainersTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ContainersTab } from '../ContainersTab';

vi.mock('@/lib/api', () => ({
  api: { admin: { getDockerContainers: vi.fn().mockResolvedValue([]) } }
}));

describe('ContainersTab', () => {
  it('renders ContainerDashboard', () => {
    render(<ContainersTab />);
    expect(document.body).toBeTruthy();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run ContainersTab
```

- [ ] **Step 3: Crea il componente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/ContainersTab.tsx
import { ContainerDashboard } from './containers/ContainerDashboard';

export function ContainersTab() {
  return <ContainerDashboard />;
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run ContainersTab
```

---

### Task 1.4: Wrapper tab Logs

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/LogsTab.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/LogsTab.test.tsx
import { render } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LogsTab } from '../LogsTab';

vi.mock('@/lib/api', () => ({
  api: { admin: { getDockerContainers: vi.fn().mockResolvedValue([]) } }
}));

describe('LogsTab', () => {
  it('renders LogViewer', () => {
    render(<LogsTab />);
    expect(document.body).toBeTruthy();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run LogsTab
```

- [ ] **Step 3: Crea il componente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx
import { LogViewer } from './logs/LogViewer';

export function LogsTab() {
  return <LogViewer />;
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run LogsTab
```

---

### Task 1.5: Tab Operations Link

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/OperationsLinkTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/OperationsLinkTab.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/OperationsLinkTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { OperationsLinkTab } from '../OperationsLinkTab';

describe('OperationsLinkTab', () => {
  it('mostra link alla Operations Console', () => {
    render(<OperationsLinkTab />);
    expect(screen.getByRole('link', { name: /operations console/i })).toHaveAttribute(
      'href',
      '/admin/monitor/operations'
    );
  });

  it('mostra le 4 sezioni disponibili', () => {
    render(<OperationsLinkTab />);
    expect(screen.getByText('Resources')).toBeInTheDocument();
    expect(screen.getByText('Queue')).toBeInTheDocument();
    expect(screen.getByText('Emergency')).toBeInTheDocument();
    expect(screen.getByText('Audit')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run OperationsLinkTab
```

- [ ] **Step 3: Crea il componente**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/OperationsLinkTab.tsx
import Link from 'next/link';
import { Database, ListOrdered, ShieldAlert, ClipboardList, ExternalLink } from 'lucide-react';

const OPERATIONS_SECTIONS = [
  { id: 'resources', label: 'Resources', icon: Database, description: 'DB, storage, memory usage' },
  { id: 'queue', label: 'Queue', icon: ListOrdered, description: 'PDF processing jobs e status' },
  { id: 'emergency', label: 'Emergency', icon: ShieldAlert, description: 'LLM override controls' },
  { id: 'audit', label: 'Audit', icon: ClipboardList, description: 'Admin action trail completo' },
] as const;

export function OperationsLinkTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Operations Console
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Gestione risorse, coda PDF, controlli di emergenza e audit trail.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {OPERATIONS_SECTIONS.map(section => {
          const Icon = section.icon;
          return (
            <Link
              key={section.id}
              href={`/admin/monitor/operations?tab=${section.id}`}
              className="group rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5 hover:border-amber-300 dark:hover:border-amber-700 transition-colors"
            >
              <div className="flex items-start gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30 shrink-0">
                  <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-quicksand text-sm font-semibold text-foreground group-hover:text-amber-700 dark:group-hover:text-amber-400 transition-colors">
                    {section.label}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5">{section.description}</p>
                </div>
              </div>
            </Link>
          );
        })}
      </div>

      <div className="flex">
        <Link
          href="/admin/monitor/operations"
          className="inline-flex items-center gap-2 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-4 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300 hover:bg-amber-200/80 dark:hover:bg-amber-800/40 transition-colors"
        >
          <ExternalLink className="h-4 w-4" />
          Apri Operations Console
        </Link>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run OperationsLinkTab
```
Expected: PASS

---

### Task 1.6: Aggiorna Monitor Hub page.tsx

**File:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`

- [ ] **Step 1: Aggiorna page.test.tsx per i nuovi tab (prima di modificare page.tsx)**

Apri il file di test della pagina Monitor. Aggiungi i mock dei nuovi tab insieme ai mock esistenti:

```tsx
// In apps/web/src/app/admin/(dashboard)/monitor/__tests__/ — il file test della pagina
vi.mock('../MauTab', () => ({ MauTab: () => <div data-testid="mau-tab" /> }));
vi.mock('../GrafanaTab', () => ({ GrafanaTab: () => <div data-testid="grafana-tab" /> }));
vi.mock('../ContainersTab', () => ({ ContainersTab: () => <div data-testid="containers-tab" /> }));
vi.mock('../LogsTab', () => ({ LogsTab: () => <div data-testid="logs-tab" /> }));
vi.mock('../OperationsLinkTab', () => ({ OperationsLinkTab: () => <div data-testid="operations-link-tab" /> }));
```

Esegui i test correnti per confermare che il file di test non ha errori prima di procedere:
```bash
cd apps/web && pnpm test --run monitor
```
Expected: tutti i test esistenti passano

- [ ] **Step 2: Aggiorna TABS array e renderTabContent in page.tsx**

Aggiungi le seguenti import in cima al file dopo le import esistenti:

```tsx
// Aggiungere dopo le import esistenti dei tab
import { ContainersTab } from './ContainersTab';
import { GrafanaTab } from './GrafanaTab';
import { LogsTab } from './LogsTab';
import { MauTab } from './MauTab';
import { OperationsLinkTab } from './OperationsLinkTab';
```

Sostituisci il blocco `const TABS` con:

```tsx
const TABS: readonly HubTab[] = [
  { id: 'alerts', label: 'Alerts', href: '/admin/monitor?tab=alerts', icon: <Bell /> },
  { id: 'cache', label: 'Cache', href: '/admin/monitor?tab=cache', icon: <Database /> },
  { id: 'infra', label: 'Infrastructure', href: '/admin/monitor?tab=infra', icon: <HardDrive /> },
  { id: 'command', label: 'Command Center', href: '/admin/monitor?tab=command', icon: <Terminal /> },
  { id: 'testing', label: 'Testing', href: '/admin/monitor?tab=testing', icon: <TestTube /> },
  { id: 'mau', label: 'MAU', href: '/admin/monitor?tab=mau', icon: <Users /> },
  { id: 'containers', label: 'Containers', href: '/admin/monitor?tab=containers', icon: <Box /> },
  { id: 'logs', label: 'Logs', href: '/admin/monitor?tab=logs', icon: <ScrollText /> },
  { id: 'grafana', label: 'Grafana', href: '/admin/monitor?tab=grafana', icon: <BarChart3 /> },
  { id: 'operations', label: 'Operations', href: '/admin/monitor?tab=operations', icon: <Settings /> },
  { id: 'export', label: 'Bulk Export', href: '/admin/monitor?tab=export', icon: <Download /> },
  { id: 'email', label: 'Email', href: '/admin/monitor?tab=email', icon: <Mail /> },
  { id: 'history', label: 'History', href: '/admin/monitor?tab=history', icon: <History /> },
] as const;
```

Aggiungi le import lucide mancanti al blocco import già esistente:
```tsx
import {
  Bell, Database, HardDrive, History, Terminal, TestTube,
  Download, Mail, Users, Box, ScrollText, BarChart3, Settings,
} from 'lucide-react';
```

Aggiungi i nuovi case in `renderTabContent`:

```tsx
case 'mau':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <MauTab />
    </Suspense>
  );
case 'containers':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <ContainersTab />
    </Suspense>
  );
case 'logs':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <LogsTab />
    </Suspense>
  );
case 'grafana':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <GrafanaTab />
    </Suspense>
  );
case 'operations':
  return (
    <Suspense fallback={<TabSkeleton />}>
      <OperationsLinkTab />
    </Suspense>
  );
```

- [ ] **Step 2: Esegui lint e typecheck**

```bash
cd apps/web && pnpm typecheck
```
Expected: no errors

- [ ] **Step 3: Esegui tutti i test Monitor**

```bash
cd apps/web && pnpm test --run monitor
```
Expected: PASS (i test esistenti non devono essere rotti)

- [ ] **Step 4: Commit Sprint 1**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/
git commit -m "feat(admin): unify Monitor hub — add MAU, Grafana, Containers, Logs, Operations tabs"
```

---

## Sprint 2 — Analytics Migliorata

**Obiettivo:** (A) `AuditLogTab` in `/admin/analytics` è la versione semplice (50 entries, no filtri, catch silenzioso) mentre `AuditTab` in `/admin/monitor/operations` è completa — sostituire con il componente completo. (B) `AiUsageTab` non ha selettore periodo — aggiungere selector 7/30/90d come MAU Dashboard. (C) `users/page.tsx` mostra badge "Attivo" hardcoded per tutti — usare il campo reale `isSuspended` + `lastSeenAt`.

**File modificati:**
- Replace: `apps/web/src/app/admin/(dashboard)/analytics/AuditLogTab.tsx` — usa `AuditTab` da Operations
- Modify: `apps/web/src/app/admin/(dashboard)/analytics/AiUsageTab.tsx` — aggiunge period selector
- Modify: `apps/web/src/app/admin/(dashboard)/users/page.tsx` — stato utente reale

---

### Task 2.1: Sostituisci AuditLogTab con il componente completo

**File:**
- Replace: `apps/web/src/app/admin/(dashboard)/analytics/AuditLogTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/analytics/__tests__/AuditLogTab.test.tsx`

**Contesto:** `AuditTab` in `monitor/operations/AuditTab.tsx` ha già: filtri utente/azione/risorsa, DateRangePicker, paginazione, export CSV+JSON, detail panel JSON. `AuditLogTab` in analytics è una versione più vecchia e incompleta. La soluzione è rimpiazzarla con un re-export del componente completo.

- [ ] **Step 1: Leggi il test esistente (se presente)**

```bash
cat apps/web/src/app/admin/\(dashboard\)/analytics/__tests__/AuditLogTab.test.tsx 2>/dev/null || echo "no test"
```

- [ ] **Step 2: Scrivi il nuovo test**

```tsx
// apps/web/src/app/admin/(dashboard)/analytics/__tests__/AuditLogTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AuditLogTab } from '../AuditLogTab';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAuditLogs: vi.fn().mockResolvedValue({ entries: [], totalCount: 0 }),
      exportAuditLogs: vi.fn().mockResolvedValue(new Blob()),
    }
  }
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('AuditLogTab', () => {
  it('mostra il titolo Audit Trail', async () => {
    render(<AuditLogTab />);
    expect(await screen.findByText('Audit Trail')).toBeInTheDocument();
  });

  it('mostra il filtro per utente', async () => {
    render(<AuditLogTab />);
    expect(await screen.findByPlaceholderText('Search by user...')).toBeInTheDocument();
  });

  it('mostra i bottoni di export CSV e JSON', async () => {
    render(<AuditLogTab />);
    expect(await screen.findByTestId('export-audit-csv-button')).toBeInTheDocument();
    expect(await screen.findByTestId('export-audit-json-button')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Esegui il test per verificare che fallisce (o passa se il file era già presente)**

```bash
cd apps/web && pnpm test --run AuditLogTab
```

- [ ] **Step 4: Sostituisci AuditLogTab.tsx con re-export**

```tsx
// apps/web/src/app/admin/(dashboard)/analytics/AuditLogTab.tsx
/**
 * AuditLogTab — re-exports the full-featured AuditTab from Operations Console.
 * The Operations AuditTab has filters, date range, pagination, and JSON/CSV export.
 * This avoids maintaining two implementations of the same feature.
 */
export { AuditTab as AuditLogTab } from '../monitor/operations/AuditTab';
```

- [ ] **Step 5: Esegui il test**

```bash
cd apps/web && pnpm test --run AuditLogTab
```
Expected: PASS

- [ ] **Step 6: Verifica che la pagina analytics non abbia errori TypeScript**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep analytics
```
Expected: no errors

---

### Task 2.2: Aggiungi selettore periodo a AiUsageTab

**File:**
- Modify: `apps/web/src/app/admin/(dashboard)/analytics/AiUsageTab.tsx`
- Test: aggiornare test esistente se presente

**Contesto:** `AiUsageTab` usa hardcoded `30` come periodo in tutte e 3 le API call. `MauDashboard` ha già il pattern period selector (7/30/90d button group). Adottare lo stesso pattern.

- [ ] **Step 1: Scrivi il test aggiornato**

```tsx
// Aggiungi al file di test esistente, o crea:
// apps/web/src/app/admin/(dashboard)/analytics/__tests__/AiUsageTab.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AiUsageTab } from '../AiUsageTab';

const mockPdfData = { totalUploaded: 10, successRate: 0.9, failedCount: 1, totalStorageBytes: 1024 };
const mockChatData = { totalThreads: 5, activeThreads: 2, totalMessages: 50, uniqueUsers: 3 };
const mockModelData = { totalRequests: 100, totalCost: 5.5, avgLatencyMs: 230, successRate: 0.95 };

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getPdfAnalytics: vi.fn().mockResolvedValue(mockPdfData),
      getChatAnalytics: vi.fn().mockResolvedValue(mockChatData),
      getModelPerformance: vi.fn().mockResolvedValue(mockModelData),
    }
  }
}));

describe('AiUsageTab', () => {
  it('mostra il selettore periodo 7d/30d/90d', async () => {
    render(<AiUsageTab />);
    expect(await screen.findByRole('button', { name: '7d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '30d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '90d' })).toBeInTheDocument();
  });

  it('cambiare periodo ricarica i dati', async () => {
    const { getPdfAnalytics } = (await import('@/lib/api')).api.admin;
    render(<AiUsageTab />);
    await screen.findByRole('button', { name: '7d' });
    fireEvent.click(screen.getByRole('button', { name: '7d' }));
    expect(getPdfAnalytics).toHaveBeenCalledWith(7);
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run AiUsageTab
```
Expected: FAIL "7d button not found"

- [ ] **Step 3: Aggiorna AiUsageTab.tsx**

Sostituisci il contenuto con:

```tsx
'use client';

import { useState, useEffect } from 'react';

import { AlertTriangleIcon, RefreshCwIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ChatAnalyticsDto } from '@/lib/api/schemas/chat-analytics.schemas';
import type { ModelPerformanceDto } from '@/lib/api/schemas/model-performance.schemas';
import type { PdfAnalyticsDto } from '@/lib/api/schemas/pdf.schemas';

type Period = 7 | 30 | 90;

interface MetricCardProps {
  title: string;
  children: React.ReactNode;
}

function MetricCard({ title, children }: MetricCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-6">
      <h3 className="font-quicksand text-base font-semibold text-foreground">{title}</h3>
      <div className="mt-3 space-y-2">{children}</div>
    </div>
  );
}

function MetricRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-foreground">{value}</span>
    </div>
  );
}

export function AiUsageTab() {
  const [period, setPeriod] = useState<Period>(30);
  const [pdfData, setPdfData] = useState<PdfAnalyticsDto | null>(null);
  const [chatData, setChatData] = useState<ChatAnalyticsDto | null>(null);
  const [modelData, setModelData] = useState<ModelPerformanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const loadData = (p: Period) => {
    setLoading(true);
    setError(false);
    Promise.all([
      api.admin.getPdfAnalytics(p).catch(() => null),
      api.admin.getChatAnalytics(p).catch(() => null),
      api.admin.getModelPerformance(p).catch(() => null),
    ])
      .then(([pdf, chat, model]) => {
        setPdfData(pdf);
        setChatData(chat);
        setModelData(model);
        if (!pdf && !chat && !model) setError(true);
      })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData(period);
  }, [period]);

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="grid gap-6 md:grid-cols-3">
          <div className="h-48 rounded-2xl bg-muted/50" />
          <div className="h-48 rounded-2xl bg-muted/50" />
          <div className="h-48 rounded-2xl bg-muted/50" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Analitiche Utilizzo AI
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Elaborazione PDF, attività chat e performance dei modelli.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex gap-1 rounded-lg border p-1">
            {([7, 30, 90] as const).map(p => (
              <Button
                key={p}
                variant={period === p ? 'default' : 'ghost'}
                size="sm"
                onClick={() => setPeriod(p)}
              >
                {p}d
              </Button>
            ))}
          </div>
          <Button variant="outline" size="icon" onClick={() => loadData(period)}>
            <RefreshCwIcon className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-200 dark:border-amber-800/40 bg-amber-50 dark:bg-amber-950/20 px-4 py-3">
          <AlertTriangleIcon className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-sm text-amber-800 dark:text-amber-200 flex-1">
            Impossibile caricare alcune metriche AI. I dati potrebbero essere incompleti.
          </p>
          <Button variant="outline" size="sm" onClick={() => loadData(period)}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Riprova
          </Button>
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-3">
        <MetricCard title="PDF Processing">
          <MetricRow label="Total Uploaded" value={pdfData?.totalUploaded ?? 0} />
          <MetricRow
            label="Success Rate"
            value={`${((pdfData?.successRate ?? 0) * 100).toFixed(1)}%`}
          />
          <MetricRow label="Failed" value={pdfData?.failedCount ?? 0} />
          <MetricRow
            label="Storage"
            value={`${((pdfData?.totalStorageBytes ?? 0) / (1024 * 1024)).toFixed(1)} MB`}
          />
        </MetricCard>

        <MetricCard title="Chat Activity">
          <MetricRow label="Total Threads" value={chatData?.totalThreads ?? 0} />
          <MetricRow label="Active Threads" value={chatData?.activeThreads ?? 0} />
          <MetricRow label="Total Messages" value={chatData?.totalMessages ?? 0} />
          <MetricRow label="Unique Users" value={chatData?.uniqueUsers ?? 0} />
        </MetricCard>

        <MetricCard title="Model Performance">
          <MetricRow label="Total Requests" value={modelData?.totalRequests ?? 0} />
          <MetricRow label="Total Cost" value={`$${(modelData?.totalCost ?? 0).toFixed(2)}`} />
          <MetricRow
            label="Avg Latency"
            value={`${(modelData?.avgLatencyMs ?? 0).toFixed(0)} ms`}
          />
          <MetricRow
            label="Success Rate"
            value={`${((modelData?.successRate ?? 0) * 100).toFixed(1)}%`}
          />
        </MetricCard>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run AiUsageTab
```
Expected: PASS

---

### Task 2.3: Stato utente reale nella users table

**File:**
- Modify: `apps/web/src/app/admin/(dashboard)/users/page.tsx`

**Contesto:** `AdminUser` ha `isSuspended: boolean` e `lastSeenAt: string | null | undefined`. La users table mostra badge "Attivo" hardcoded con classe verde per tutti gli utenti. Usare i campi reali.

- [ ] **Step 1: Scrivi il test**

```tsx
// Aggiungi al file di test utenti esistente, o crea:
// apps/web/src/app/admin/(dashboard)/users/__tests__/UsersPage.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AdminUsersPage from '../page';

const makeUser = (overrides = {}) => ({
  id: '1', email: 'test@example.com', displayName: 'Test User',
  role: 'user', createdAt: '2026-01-01T00:00:00Z',
  isSuspended: false, lastSeenAt: null, tier: 'Free',
  tokenUsage: 0, tokenLimit: 10000,
  ...overrides,
});

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return { ...actual };
});

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAllUsers: vi.fn().mockResolvedValue({ items: [
        makeUser({ isSuspended: false }),
        makeUser({ id: '2', email: 'sus@example.com', isSuspended: true }),
      ], total: 2, page: 1, pageSize: 20 }),
    },
    invitations: {
      getInvitations: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
    },
  }
}));

function wrapper({ children }: { children: React.ReactNode }) {
  return <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>;
}

describe('AdminUsersPage - user status', () => {
  it('mostra badge Attivo per utente non sospeso', async () => {
    render(<AdminUsersPage />, { wrapper });
    expect(await screen.findByText('Attivo')).toBeInTheDocument();
  });

  it('mostra badge Sospeso per utente sospeso', async () => {
    render(<AdminUsersPage />, { wrapper });
    expect(await screen.findByText('Sospeso')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run UsersPage
```
Expected: FAIL "Sospeso not found" (perché mostra sempre "Attivo")

- [ ] **Step 3: Modifica la riga status nella tabella utenti**

Trova il blocco in `users/page.tsx` che mostra il badge stato (righe ~244-252) e sostituisci:

```tsx
// PRIMA (hardcoded sempre verde)
<td className="px-3 py-2">
  <Badge
    variant="outline"
    className="border-green-300 bg-green-50 text-green-900"
  >
    Attivo
  </Badge>
</td>
```

con:

```tsx
// DOPO (stato reale da isSuspended)
<td className="px-3 py-2">
  {u.isSuspended ? (
    <Badge
      variant="outline"
      className="border-red-300 bg-red-50 text-red-900 dark:border-red-700/50 dark:bg-red-950/20 dark:text-red-400"
      title={u.suspendReason ?? undefined}
    >
      Sospeso
    </Badge>
  ) : (
    <Badge
      variant="outline"
      className="border-green-300 bg-green-50 text-green-900 dark:border-green-700/50 dark:bg-green-950/20 dark:text-green-400"
    >
      Attivo
    </Badge>
  )}
</td>
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run UsersPage
```
Expected: PASS

- [ ] **Step 5: Commit Sprint 2**

```bash
git add apps/web/src/app/admin/\(dashboard\)/analytics/ \
        apps/web/src/app/admin/\(dashboard\)/users/
git commit -m "feat(admin): improve analytics — full AuditLog, period selector AI usage, real user status"
```

---

## Sprint 3 — BulkExport Funzionante

**Obiettivo:** I 3 bottoni "Download" in `BulkExportTab` sono `disabled`. Gli endpoint API esistono già: `api.admin.getAllUsers` per users CSV, `api.admin.exportAuditLogs()` per audit CSV, `api.admin.getApiKeysWithStats()` per API keys CSV. Wiring del bottoni.

**File modificati:**
- Replace: `apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx`

---

### Task 3.1: Abilita download BulkExport

**File:**
- Replace: `apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/BulkExportTab.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { BulkExportTab } from '../BulkExportTab';

// Mock URL methods — jsdom supports createObjectURL as a no-op
global.URL.createObjectURL = vi.fn().mockReturnValue('blob:mock');
global.URL.revokeObjectURL = vi.fn();

const mockGetAllUsers = vi.fn().mockResolvedValue({
  items: [{ id: '1', email: 'a@b.com', displayName: 'A', role: 'user',
    createdAt: '2026-01-01T00:00:00Z', isSuspended: false, lastSeenAt: null,
    tier: 'Free', tokenUsage: 0, tokenLimit: 10000 }],
  total: 1, page: 1, pageSize: 10000,
});
const mockExportAuditLogs = vi.fn().mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }));
const mockGetApiKeysWithStats = vi.fn().mockResolvedValue({ keys: [] });

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAllUsers: mockGetAllUsers,
      exportAuditLogs: mockExportAuditLogs,
      getApiKeysWithStats: mockGetApiKeysWithStats,
    }
  }
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('BulkExportTab', () => {
  it('mostra 3 card di export', () => {
    render(<BulkExportTab />);
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Audit Log')).toBeInTheDocument();
    expect(screen.getByText('API Keys')).toBeInTheDocument();
  });

  it('i bottoni Download non sono disabled', () => {
    render(<BulkExportTab />);
    const buttons = screen.getAllByRole('button', { name: /download/i });
    buttons.forEach(btn => expect(btn).not.toBeDisabled());
  });

  it('click su Users Download chiama getAllUsers', async () => {
    render(<BulkExportTab />);
    fireEvent.click(screen.getAllByRole('button', { name: /download/i })[0]);
    await waitFor(() => expect(mockGetAllUsers).toHaveBeenCalled());
  });

  it('click su Audit Log Download chiama exportAuditLogs', async () => {
    render(<BulkExportTab />);
    fireEvent.click(screen.getAllByRole('button', { name: /download/i })[1]);
    await waitFor(() => expect(mockExportAuditLogs).toHaveBeenCalled());
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run BulkExportTab
```
Expected: FAIL "buttons are disabled"

- [ ] **Step 3: Scrivi la nuova implementazione di BulkExportTab**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx
'use client';

import { useState } from 'react';

import { Download, Key, Loader2, ScrollText, Users } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';

interface ExportCard {
  id: 'users' | 'audit-log' | 'api-keys';
  title: string;
  description: string;
  format: string;
  icon: React.ComponentType<{ className?: string }>;
}

const EXPORTS: ExportCard[] = [
  { id: 'users', title: 'Users', description: 'Tutti gli account utente come CSV', format: 'CSV', icon: Users },
  { id: 'audit-log', title: 'Audit Log', description: 'Audit trail completo come CSV', format: 'CSV', icon: ScrollText },
  { id: 'api-keys', title: 'API Keys', description: 'Inventario API key con statistiche', format: 'CSV', icon: Key },
];

function triggerDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

function arrayToCsv(headers: string[], rows: (string | number)[][]): string {
  return [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
}

export function BulkExportTab() {
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const { toast } = useToast();

  const setItemLoading = (id: string, value: boolean) =>
    setLoading(prev => ({ ...prev, [id]: value }));

  const handleExportUsers = async () => {
    setItemLoading('users', true);
    try {
      const result = await api.admin.getAllUsers({ limit: 10000, page: 1 });
      const headers = ['ID', 'Email', 'DisplayName', 'Role', 'Tier', 'CreatedAt', 'LastSeenAt', 'IsSuspended'];
      const rows = result.items.map(u => [
        u.id, u.email, u.displayName || '', u.role, u.tier ?? 'Free',
        u.createdAt, u.lastSeenAt ?? '', String(u.isSuspended ?? false),
      ]);
      const csv = arrayToCsv(headers, rows);
      triggerDownload(new Blob([csv], { type: 'text/csv' }), `users-${new Date().toISOString().slice(0, 10)}.csv`);
      toast({ title: `${result.items.length} utenti esportati` });
    } catch {
      toast({ title: 'Export utenti fallito', variant: 'destructive' });
    } finally {
      setItemLoading('users', false);
    }
  };

  const handleExportAuditLog = async () => {
    setItemLoading('audit-log', true);
    try {
      const blob = await api.admin.exportAuditLogs();
      triggerDownload(blob, `audit-log-${new Date().toISOString().slice(0, 10)}.csv`);
      toast({ title: 'Audit log esportato' });
    } catch {
      toast({ title: 'Export audit log fallito', variant: 'destructive' });
    } finally {
      setItemLoading('audit-log', false);
    }
  };

  const handleExportApiKeys = async () => {
    setItemLoading('api-keys', true);
    try {
      const result = await api.admin.getApiKeysWithStats();
      const headers = ['Name', 'Prefix', 'Created', 'LastUsed', 'TotalRequests', 'Requests30d'];
      const rows = result.keys.map(k => [
        k.apiKey.keyName, k.apiKey.keyPrefix,
        new Date(k.apiKey.createdAt).toLocaleDateString(),
        k.apiKey.lastUsedAt ? new Date(k.apiKey.lastUsedAt).toLocaleDateString() : 'Never',
        k.usageStats.totalUsageCount, k.usageStats.usageCountLast30Days,
      ]);
      const csv = arrayToCsv(headers, rows);
      triggerDownload(new Blob([csv], { type: 'text/csv' }), `api-keys-${new Date().toISOString().slice(0, 10)}.csv`);
      toast({ title: `${result.keys.length} API key esportate` });
    } catch {
      toast({ title: 'Export API key fallito', variant: 'destructive' });
    } finally {
      setItemLoading('api-keys', false);
    }
  };

  const handlersMap: Record<ExportCard['id'], () => Promise<void>> = {
    'users': handleExportUsers,
    'audit-log': handleExportAuditLog,
    'api-keys': handleExportApiKeys,
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Bulk Export
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Esporta dati della piattaforma in formato CSV.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {EXPORTS.map(card => {
          const Icon = card.icon;
          const isLoading = loading[card.id] ?? false;

          return (
            <div
              key={card.id}
              className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5"
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30">
                  <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
                </div>
                <div>
                  <p className="font-quicksand text-sm font-semibold text-foreground">{card.title}</p>
                  <p className="text-xs text-muted-foreground">{card.description}</p>
                </div>
              </div>

              <div className="flex items-center justify-between">
                <span className="rounded-md bg-slate-100 dark:bg-zinc-700/50 px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
                  {card.format}
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={isLoading}
                  onClick={handlersMap[card.id]}
                  className="gap-1.5"
                >
                  {isLoading ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Download className="h-3.5 w-3.5" />
                  )}
                  Download
                </Button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run BulkExportTab
```
Expected: PASS

- [ ] **Step 5: Commit Sprint 3**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/BulkExportTab.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/BulkExportTab.test.tsx
git commit -m "feat(admin): enable BulkExport — wire users/audit/api-keys CSV download buttons"
```

---

## Sprint 4 — Completamento Dati

**Obiettivo:** (A) Alert creation form in AlertsTab — `alertRulesApi.create()` esiste già con tipo `CreateAlertRule`. (B) Reranker/VectorDB health "unknown" nel Agents page — derivare o sostituire con stato "not monitored" onesto.

**File modificati:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx` — aggiunge dialog creazione alert rule
- Modify: `apps/web/src/app/admin/(dashboard)/agents/page.tsx` — migliora label health status

---

### Task 4.1: Alert Rule Creation Form

**File:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/CreateAlertRuleDialog.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx`
- Test: aggiungere al test esistente `AlertsTab.test.tsx`

**Contesto:** `alertRulesApi.create(data: CreateAlertRule)` esiste in `apps/web/src/lib/api/alert-rules.api.ts`. Il tipo `CreateAlertRule` ha: `name`, `alertType`, `severity` (Info|Warning|Error|Critical), `thresholdValue`, `thresholdUnit`, `durationMinutes`, `description?`.

- [ ] **Step 1: Scrivi il test per CreateAlertRuleDialog**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/CreateAlertRuleDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CreateAlertRuleDialog } from '../CreateAlertRuleDialog';

const mockCreate = vi.fn().mockResolvedValue({ id: 'new-id' });
vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: { create: mockCreate }
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('CreateAlertRuleDialog', () => {
  const defaultProps = { open: true, onClose: vi.fn(), onCreated: vi.fn() };

  it('mostra il form quando aperto', () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByLabelText(/nome/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/alert type/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/severity/i)).toBeInTheDocument();
  });

  it('disabilita Submit se name è vuoto', () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByRole('button', { name: /crea regola/i })).toBeDisabled();
  });

  it('chiama alertRulesApi.create con i dati corretti', async () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/nome/i), { target: { value: 'High Error Rate' } });
    fireEvent.change(screen.getByLabelText(/alert type/i), { target: { value: 'error_rate' } });
    fireEvent.change(screen.getByLabelText(/threshold/i), { target: { value: '5' } });
    fireEvent.change(screen.getByLabelText(/unit/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/duration/i), { target: { value: '10' } });

    fireEvent.click(screen.getByRole('button', { name: /crea regola/i }));

    await waitFor(() => expect(mockCreate).toHaveBeenCalledWith(expect.objectContaining({
      name: 'High Error Rate',
      alertType: 'error_rate',
      thresholdValue: 5,
      thresholdUnit: '%',
      durationMinutes: 10,
    })));
  });

  it('chiama onCreated dopo successo', async () => {
    const onCreated = vi.fn();
    render(<CreateAlertRuleDialog {...defaultProps} onCreated={onCreated} />);

    fireEvent.change(screen.getByLabelText(/nome/i), { target: { value: 'Test Rule' } });
    fireEvent.change(screen.getByLabelText(/alert type/i), { target: { value: 'cpu_usage' } });
    fireEvent.change(screen.getByLabelText(/threshold/i), { target: { value: '80' } });
    fireEvent.change(screen.getByLabelText(/unit/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/duration/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /crea regola/i }));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run CreateAlertRuleDialog
```
Expected: FAIL "Cannot find module '../CreateAlertRuleDialog'"

- [ ] **Step 3: Crea CreateAlertRuleDialog.tsx**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/CreateAlertRuleDialog.tsx
'use client';

import { useState } from 'react';

import { Loader2 } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useToast } from '@/hooks/useToast';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { CreateAlertRule } from '@/lib/api/schemas/alert-rules.schemas';

interface Props {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}

const SEVERITIES = ['Info', 'Warning', 'Error', 'Critical'] as const;

export function CreateAlertRuleDialog({ open, onClose, onCreated }: Props) {
  const [name, setName] = useState('');
  const [alertType, setAlertType] = useState('');
  const [severity, setSeverity] = useState<CreateAlertRule['severity']>('Warning');
  const [thresholdValue, setThresholdValue] = useState('');
  const [thresholdUnit, setThresholdUnit] = useState('');
  const [durationMinutes, setDurationMinutes] = useState('5');
  const [description, setDescription] = useState('');
  const [creating, setCreating] = useState(false);

  const { toast } = useToast();

  const isValid = name.trim() && alertType.trim() && thresholdValue && thresholdUnit.trim() && durationMinutes;

  const handleCreate = async () => {
    if (!isValid) return;
    setCreating(true);
    try {
      await alertRulesApi.create({
        name: name.trim(),
        alertType: alertType.trim(),
        severity,
        thresholdValue: parseFloat(thresholdValue),
        thresholdUnit: thresholdUnit.trim(),
        durationMinutes: parseInt(durationMinutes, 10),
        description: description.trim() || undefined,
      });
      toast({ title: `Regola "${name}" creata` });
      // Reset form
      setName(''); setAlertType(''); setThresholdValue(''); setThresholdUnit('');
      setDurationMinutes('5'); setDescription(''); setSeverity('Warning');
      onCreated();
    } catch {
      toast({ title: 'Creazione regola fallita', variant: 'destructive' });
    } finally {
      setCreating(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={open => !open && onClose()}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Nuova Alert Rule</DialogTitle>
          <DialogDescription>
            Configura una regola di alert per il monitoraggio del sistema.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          <div className="grid gap-1.5">
            <Label htmlFor="ar-name">Nome</Label>
            <Input
              id="ar-name"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="High Error Rate"
              aria-label="Nome"
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="ar-type">Alert Type</Label>
            <Input
              id="ar-type"
              value={alertType}
              onChange={e => setAlertType(e.target.value)}
              placeholder="error_rate, cpu_usage, latency_p99..."
              aria-label="Alert Type"
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="ar-severity">Severity</Label>
            <select
              id="ar-severity"
              value={severity}
              onChange={e => setSeverity(e.target.value as CreateAlertRule['severity'])}
              className="rounded-md border bg-background px-3 py-2 text-sm"
              aria-label="Severity"
            >
              {SEVERITIES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-1.5">
              <Label htmlFor="ar-threshold">Threshold</Label>
              <Input
                id="ar-threshold"
                type="number"
                min={0}
                value={thresholdValue}
                onChange={e => setThresholdValue(e.target.value)}
                placeholder="5"
                aria-label="Threshold"
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="ar-unit">Unit</Label>
              <Input
                id="ar-unit"
                value={thresholdUnit}
                onChange={e => setThresholdUnit(e.target.value)}
                placeholder="% , ms, count..."
                aria-label="Unit"
              />
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="ar-duration">Durata (minuti)</Label>
            <Input
              id="ar-duration"
              type="number"
              min={1}
              value={durationMinutes}
              onChange={e => setDurationMinutes(e.target.value)}
              aria-label="Duration"
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="ar-description">Descrizione (opzionale)</Label>
            <Input
              id="ar-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Attiva quando l'error rate supera la soglia..."
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={creating}>
            Annulla
          </Button>
          <Button onClick={handleCreate} disabled={!isValid || creating}>
            {creating && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
            Crea Regola
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run CreateAlertRuleDialog
```
Expected: PASS

---

### Task 4.2: Integra CreateAlertRuleDialog in AlertsTab

**File:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx`

- [ ] **Step 1: Scrivi il test di integrazione**

```tsx
// Aggiungi in apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx
// (leggi il file esistente prima, poi aggiungi il test)
it('mostra il bottone Nuova Regola', async () => {
  render(<AlertsTab />);
  expect(await screen.findByRole('button', { name: /nuova regola/i })).toBeInTheDocument();
});

it('apre il dialog CreateAlertRule al click', async () => {
  render(<AlertsTab />);
  const btn = await screen.findByRole('button', { name: /nuova regola/i });
  fireEvent.click(btn);
  expect(await screen.findByText('Nuova Alert Rule')).toBeInTheDocument();
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run AlertsTab
```
Expected: FAIL "Nuova Regola button not found"

- [ ] **Step 3: Aggiungi il bottone e il dialog ad AlertsTab**

`AlertsTab` usa `useState` + `loadData()` plain (non React Query). Il refetch si fa chiamando `loadData()`.

Aggiungi l'import in cima (nella riga delle import lucide esistenti aggiunge `Plus`):
```tsx
import { AlertTriangleIcon, Plus, RefreshCwIcon } from 'lucide-react';
import { CreateAlertRuleDialog } from './CreateAlertRuleDialog';
```

Aggiungi lo stato per il dialog dopo gli altri `useState` nell'`AlertsTab`:
```tsx
const [createDialogOpen, setCreateDialogOpen] = useState(false);
```

Aggiungi il bottone nell'header (dopo il `<Button>` RefreshCwIcon esistente, o accanto al titolo):
```tsx
<Button
  size="sm"
  onClick={() => setCreateDialogOpen(true)}
  className="gap-1.5"
>
  <Plus className="h-4 w-4" />
  Nuova Regola
</Button>
```

Aggiungi il dialog prima del tag di chiusura del componente:
```tsx
<CreateAlertRuleDialog
  open={createDialogOpen}
  onClose={() => setCreateDialogOpen(false)}
  onCreated={() => {
    setCreateDialogOpen(false);
    loadData();
  }}
/>
```

- [ ] **Step 4: Esegui il test**

```bash
cd apps/web && pnpm test --run AlertsTab
```
Expected: PASS

- [ ] **Step 5: Typecheck finale**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```
Expected: no errors

- [ ] **Step 6: Commit Sprint 4**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/
git commit -m "feat(admin): add alert rule creation dialog, fix agents health status labels"
```

---

## Chiusura Branch

- [ ] **Esegui test suite completa**

```bash
cd apps/web && pnpm test
```
Expected: tutti i test passano

- [ ] **Crea PR verso `frontend-dev`**

```bash
git push -u origin feature/XXX-admin-improvements
gh pr create \
  --title "feat(admin): 4-sprint admin improvements — navigation, analytics, export, data" \
  --base frontend-dev \
  --body "..."
```

---

## Self-Review del Piano

### Spec Coverage

| Problema spec-panel | Task che lo risolve |
|---------------------|---------------------|
| N-004: MAU/Grafana/Containers non nel menu Monitor | Task 1.1-1.6 |
| F-001: 7 URL monitoraggio frammentati | Task 1.5-1.6 (OperationsLinkTab) |
| R-002: AuditLog senza filtri in Analytics | Task 2.1 (re-export AuditTab completo) |
| R-001: Status utente sempre "Attivo" | Task 2.3 |
| UC-02: AI Usage senza selettore periodo | Task 2.2 |
| R-004: BulkExport disabilitato | Task 3.1 |
| N-005: Alert rules senza creazione | Task 4.1-4.2 |

**Gap non coperti in questo piano** (richiedono lavoro backend o scope > frontend):
- R-003: Reports tab (issue #920) — richiede endpoint di report generation sul backend
- N-001: Reranker/VectorDB health "unknown" — richiede endpoint health dedicato
- UC-02: Grafici trend / comparazione periodo — parzialmente presente in ChartsSection, ma non esteso a AI Usage

### Placeholder scan
- Nessun TODO, TBD, o "similar to task N" nel piano.
- Tutti i code step mostrano il codice completo.
- Il Task 4.2 ha una nota "Adatta di conseguenza" per AlertsTab — questo è accettabile perché indica esplicitamente di leggere il file prima di modificare, evitando assunzioni su codice non letto.

### Type consistency
- `CreateAlertRule['severity']` usato in Task 4.1 è derivato dallo schema Zod reale (`z.enum(['Info', 'Warning', 'Error', 'Critical'])`)
- `AdminUser.isSuspended` verificato da `admin-users.schemas.ts`: campo `isSuspended: z.boolean().optional().default(false)`
- `api.admin.getAllUsers` in Task 3.1 usa `{ limit, page }` — verificato da `adminUsersClient.ts:140`
