# AI Admin Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate 13 admin AI pages into 7 tabbed views with a Mission Control hub, improving discoverability and reducing fragmentation.

**Architecture:** Recompose existing page components as tabs within new wrapper pages using shadcn `Tabs`. No business logic changes — existing API calls and components are reused. New route `/admin/agents/inspector` and `/admin/agents/config` are created; old routes become redirects.

**Tech Stack:** Next.js 16 App Router, React 19, shadcn/ui Tabs, Zustand, React Query, Tailwind 4, Lucide icons

**Spec:** `docs/superpowers/specs/2026-03-27-ai-admin-consolidation-design.md`

---

## File Structure

### New files
- `apps/web/src/app/admin/(dashboard)/agents/page.tsx` — Mission Control (replaces current)
- `apps/web/src/app/admin/(dashboard)/agents/inspector/page.tsx` — RAG Inspector
- `apps/web/src/app/admin/(dashboard)/agents/config/page.tsx` — Configuration
- `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx` — RAG Playground (new route)
- `apps/web/src/app/admin/(dashboard)/agents/usage/token-balance-tab.tsx` — Token Balance tab component
- `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx` — Compare tab component

### Modified files
- `apps/web/src/config/admin-dashboard-navigation.ts` — sidebar 13→7 items
- `apps/web/src/app/admin/(dashboard)/agents/usage/page.tsx` — add tabs wrapper
- `apps/web/src/app/admin/(dashboard)/agents/analytics/page.tsx` — add tabs wrapper
- `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx` — add builder sheet trigger

### Redirect files (old routes → new)
- `apps/web/src/app/admin/(dashboard)/agents/debug/page.tsx` → redirect to `/admin/agents/inspector`
- `apps/web/src/app/admin/(dashboard)/agents/pipeline/page.tsx` → redirect to `/admin/agents/inspector?tab=pipeline`
- `apps/web/src/app/admin/(dashboard)/agents/debug-chat/page.tsx` → redirect to `/admin/agents/playground?tab=chat`
- `apps/web/src/app/admin/(dashboard)/agents/sandbox/page.tsx` → redirect to `/admin/agents/playground`
- `apps/web/src/app/admin/(dashboard)/agents/builder/page.tsx` → redirect to `/admin/agents/definitions`
- `apps/web/src/app/admin/(dashboard)/agents/strategy/page.tsx` → redirect to `/admin/agents/config`
- `apps/web/src/app/admin/(dashboard)/agents/models/page.tsx` → redirect to `/admin/agents/config?tab=models`
- `apps/web/src/app/admin/(dashboard)/agents/chat-limits/page.tsx` → redirect to `/admin/agents/config?tab=limits`
- `apps/web/src/app/admin/(dashboard)/agents/chat-history/page.tsx` → redirect to `/admin/agents/usage?tab=chat-log`

### Test files
- `apps/web/src/__tests__/app/admin/agents/mission-control.test.tsx`
- `apps/web/src/__tests__/app/admin/agents/inspector.test.tsx`
- `apps/web/src/__tests__/app/admin/agents/playground.test.tsx`
- `apps/web/src/__tests__/app/admin/agents/config.test.tsx`
- `apps/web/src/__tests__/app/admin/agents/usage-tabs.test.tsx`
- `apps/web/src/__tests__/app/admin/agents/sidebar-navigation.test.tsx`

---

## Task 1: Sidebar Navigation Update

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`
- Test: `apps/web/src/__tests__/app/admin/agents/sidebar-navigation.test.tsx`

- [ ] **Step 1: Write failing test for new sidebar structure**

```tsx
// apps/web/src/__tests__/app/admin/agents/sidebar-navigation.test.tsx
import { describe, expect, it } from 'vitest';

import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  isSidebarItemActive,
} from '@/config/admin-dashboard-navigation';

describe('AI Admin Sidebar Navigation', () => {
  const aiSection = DASHBOARD_SECTIONS.find(s => s.id === 'ai');

  it('has exactly 7 sidebar items', () => {
    expect(aiSection).toBeDefined();
    expect(aiSection!.sidebarItems).toHaveLength(7);
  });

  it('contains the expected labels', () => {
    const labels = aiSection!.sidebarItems.map(i => i.label);
    expect(labels).toEqual([
      'Mission Control',
      'RAG Inspector',
      'RAG Playground',
      'Agent Definitions',
      'Configuration',
      'Usage & Costs',
      'Analytics',
    ]);
  });

  it('contains the expected routes', () => {
    const routes = aiSection!.sidebarItems.map(i => i.href);
    expect(routes).toEqual([
      '/admin/agents',
      '/admin/agents/inspector',
      '/admin/agents/playground',
      '/admin/agents/definitions',
      '/admin/agents/config',
      '/admin/agents/usage',
      '/admin/agents/analytics',
    ]);
  });

  it('resolves /admin/agents/inspector to AI section', () => {
    expect(getActiveSection('/admin/agents/inspector')?.id).toBe('ai');
  });

  it('resolves /admin/agents/config to AI section', () => {
    expect(getActiveSection('/admin/agents/config')?.id).toBe('ai');
  });

  it('marks Mission Control active for /admin/agents', () => {
    const mc = aiSection!.sidebarItems[0];
    expect(isSidebarItemActive(mc, '/admin/agents')).toBe(true);
    expect(isSidebarItemActive(mc, '/admin/agents/inspector')).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/sidebar-navigation.test.tsx`
Expected: FAIL — "has exactly 7 sidebar items" fails (currently 13)

- [ ] **Step 3: Update sidebar navigation config**

In `apps/web/src/config/admin-dashboard-navigation.ts`, replace the AI section `sidebarItems` array. Add `SearchIcon` to imports.

Replace the entire sidebarItems array inside the AI section (id: `'ai'`) with:

```typescript
    sidebarItems: [
      {
        href: '/admin/agents',
        label: 'Mission Control',
        icon: GaugeIcon,
        activePattern: /^\/admin\/agents$/,
      },
      {
        href: '/admin/agents/inspector',
        label: 'RAG Inspector',
        icon: SearchIcon,
      },
      {
        href: '/admin/agents/playground',
        label: 'RAG Playground',
        icon: FlaskConicalIcon,
      },
      {
        href: '/admin/agents/definitions',
        label: 'Agent Definitions',
        icon: ListIcon,
        activePattern: /^\/admin\/agents\/definitions/,
      },
      {
        href: '/admin/agents/config',
        label: 'Configuration',
        icon: Settings2Icon,
      },
      {
        href: '/admin/agents/usage',
        label: 'Usage & Costs',
        icon: TrendingUpIcon,
      },
      {
        href: '/admin/agents/analytics',
        label: 'Analytics',
        icon: BarChartIcon,
      },
    ],
```

Add `SearchIcon` to the lucide-react import at the top of the file.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/sidebar-navigation.test.tsx`
Expected: PASS — all 6 tests green

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/config/admin-dashboard-navigation.ts apps/web/src/__tests__/app/admin/agents/sidebar-navigation.test.tsx
git commit -m "refactor(web): consolidate AI sidebar from 13 to 7 items"
```

---

## Task 2: Mission Control Page

**Files:**
- Replace: `apps/web/src/app/admin/(dashboard)/agents/page.tsx`
- Test: `apps/web/src/__tests__/app/admin/agents/mission-control.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/app/admin/agents/mission-control.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents',
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAgentMetrics: vi.fn().mockResolvedValue({
        totalExecutions: 347,
        avgLatencyMs: 1200,
        errorRate: 0.021,
        totalCost: 1.84,
        cacheHitRate: 0.23,
      }),
      getRagExecutions: vi.fn().mockResolvedValue({
        items: [
          {
            id: '1',
            query: 'Come si gioca a Catan?',
            strategy: 'HybridRAG',
            totalLatencyMs: 890,
            totalTokens: 1247,
            totalCost: 0.003,
            confidence: 0.92,
            status: 'ok',
            createdAt: '2026-03-27T14:32:05Z',
          },
        ],
        total: 1,
      }),
      getEmbeddingInfo: vi.fn().mockResolvedValue({ status: 'healthy', model: 'e5-base' }),
      getOpenRouterStatus: vi.fn().mockResolvedValue({ requestsPerMinute: 78, limitPerMinute: 100 }),
    },
  },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Mission Control', () => {
  it('renders KPI cards', async () => {
    const { default: MissionControlPage } = await import(
      '@/app/admin/(dashboard)/agents/page'
    );
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Esecuzioni Oggi')).toBeInTheDocument();
    expect(screen.getByText('Latenza Media')).toBeInTheDocument();
    expect(screen.getByText('Error Rate')).toBeInTheDocument();
    expect(screen.getByText('Token Consumati')).toBeInTheDocument();
    expect(screen.getByText('Costo Oggi')).toBeInTheDocument();
  });

  it('renders service health section', async () => {
    const { default: MissionControlPage } = await import(
      '@/app/admin/(dashboard)/agents/page'
    );
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Service Health')).toBeInTheDocument();
  });

  it('renders quick action buttons', async () => {
    const { default: MissionControlPage } = await import(
      '@/app/admin/(dashboard)/agents/page'
    );
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Test RAG Query')).toBeInTheDocument();
    expect(screen.getByText('Ispeziona Esecuzioni')).toBeInTheDocument();
  });

  it('renders recent executions table', async () => {
    const { default: MissionControlPage } = await import(
      '@/app/admin/(dashboard)/agents/page'
    );
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Ultime Esecuzioni RAG')).toBeInTheDocument();
    expect(screen.getByText('Come si gioca a Catan?')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/mission-control.test.tsx`
Expected: FAIL — current page doesn't have "Esecuzioni Oggi" etc.

- [ ] **Step 3: Implement Mission Control page**

Replace the content of `apps/web/src/app/admin/(dashboard)/agents/page.tsx` with the Mission Control layout. The page is a `'use client'` component that uses `useQuery` to fetch:
- `api.admin.getAgentMetrics()` for KPI data
- `api.admin.getRagExecutions({ take: 5 })` for recent executions
- `api.admin.getEmbeddingInfo()` for embedding health
- `api.admin.getOpenRouterStatus()` for OpenRouter health

Layout: 3 rows as per spec — KPI cards row, health + quick actions row, recent executions mini-table.

Quick action buttons use `router.push()` to navigate to the other 6 pages.

KPI card labels: "Esecuzioni Oggi", "Latenza Media", "Error Rate", "Token Consumati", "Costo Oggi".

Health items: Embedding Service, Reranker Service, OpenRouter API, Vector DB.

Keep the implementation focused — no complex visualizations, just cards + table + links. Use existing shadcn `Card`, `Badge`, `Button`, `Skeleton` components.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/mission-control.test.tsx`
Expected: PASS

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/page.tsx apps/web/src/__tests__/app/admin/agents/mission-control.test.tsx
git commit -m "feat(web): add AI Mission Control landing page"
```

---

## Task 3: RAG Inspector Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/inspector/page.tsx`
- Test: `apps/web/src/__tests__/app/admin/agents/inspector.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/app/admin/agents/inspector.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/inspector',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getRagExecutions: vi.fn().mockResolvedValue({ items: [], total: 0 }),
    getRagExecutionById: vi.fn().mockResolvedValue(null),
    getRagExecutionStats: vi.fn().mockResolvedValue({
      totalExecutions: 0,
      avgLatencyMs: 0,
      errorRate: 0,
      cacheHitRate: 0,
      totalCost: 0,
    }),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: { create: vi.fn() },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RAG Inspector', () => {
  it('renders three tabs', async () => {
    const { default: InspectorPage } = await import(
      '@/app/admin/(dashboard)/agents/inspector/page'
    );
    render(<InspectorPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /esecuzioni/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /pipeline/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /waterfall/i })).toBeInTheDocument();
  });

  it('defaults to Esecuzioni tab', async () => {
    const { default: InspectorPage } = await import(
      '@/app/admin/(dashboard)/agents/inspector/page'
    );
    render(<InspectorPage />, { wrapper: Wrapper });
    const tab = screen.getByRole('tab', { name: /esecuzioni/i });
    expect(tab).toHaveAttribute('data-state', 'active');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/inspector.test.tsx`
Expected: FAIL — file doesn't exist yet

- [ ] **Step 3: Implement RAG Inspector page**

Create `apps/web/src/app/admin/(dashboard)/agents/inspector/page.tsx` as a `'use client'` component.

Structure:
- Page header ("RAG Inspector" / "Monitora e analizza le esecuzioni RAG")
- `<Tabs defaultValue="executions">` with 3 tabs
- Tab "Esecuzioni": embed the core content from the current Debug Console page (`/agents/debug/page.tsx`). Extract the execution table, filters, auto-refresh, and stats bar logic. The original page uses `createAdminClient()` directly — keep the same pattern.
- Tab "Pipeline": embed the PipelineDiagram and timeline content from Pipeline Explorer. Import `PipelineDiagram`, `TimelineStep`, `ConfidenceBadge`, `StrategyBadge` from `@/components/admin/rag`.
- Tab "Waterfall": embed `WaterfallChart` from `@/components/admin/rag`.

Shared state: `selectedExecutionId` (`useState<string | null>`) drives all 3 tabs. Selecting an execution in the table populates Pipeline and Waterfall tabs with that execution's data.

Use `useSearchParams` to support `?tab=pipeline` and `?tab=waterfall` for deep linking from redirects.

**Important**: Do NOT copy-paste the entire Debug Console and Pipeline pages. Instead, extract the rendering logic from each page into the tabs. The data fetching (`createAdminClient`, `getRagExecutions`, etc.) stays the same.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/inspector.test.tsx`
Expected: PASS

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/inspector/
git commit -m "feat(web): add RAG Inspector page merging debug + pipeline"
```

---

## Task 4: RAG Playground Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx`
- Test: `apps/web/src/__tests__/app/admin/agents/playground.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/app/admin/agents/playground.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/playground',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    testRagPipeline: vi.fn().mockResolvedValue({ answer: 'test', tokens: 100 }),
    getRagExecutions: vi.fn().mockResolvedValue({ items: [], total: 0 }),
    compareRagExecutions: vi.fn().mockResolvedValue({ runA: null, runB: null }),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: { create: vi.fn() },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RAG Playground', () => {
  it('renders three tabs', async () => {
    const { default: PlaygroundPage } = await import(
      '@/app/admin/(dashboard)/agents/playground/page'
    );
    render(<PlaygroundPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /query tester/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /chat debug/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /compare/i })).toBeInTheDocument();
  });

  it('shows query input and execute button on Query Tester tab', async () => {
    const { default: PlaygroundPage } = await import(
      '@/app/admin/(dashboard)/agents/playground/page'
    );
    render(<PlaygroundPage />, { wrapper: Wrapper });
    expect(screen.getByPlaceholderText(/scrivi una domanda/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /esegui/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/playground.test.tsx`
Expected: FAIL — files don't exist

- [ ] **Step 3: Implement Playground page**

Create `apps/web/src/app/admin/(dashboard)/agents/playground/page.tsx`:
- `'use client'` component
- `<Tabs defaultValue="query-tester">` with 3 tabs
- Tab "Query Tester": Split layout (grid-cols-2). Left: textarea for query + 6 parameter selects/inputs (Strategy, Model, Temperature, Top-K, Game, Agent) + "Esegui Query" button. Right: response display + chunks list + metrics (latency, tokens, cost). Uses `createAdminClient().testRagPipeline()`.
- Tab "Chat Debug": Embed content from current Debug Chat page. Import `DebugTimeline`, `StrategySelectorBar` from `@/components/admin/debug-chat` and `useDebugChatStream` hook.
- Tab "Compare": Render `<CompareTab />` component (separate file).

Create `apps/web/src/app/admin/(dashboard)/agents/playground/compare-tab.tsx`:
- `'use client'` component
- Two execution selectors (dropdowns from `getRagExecutions()` history)
- Side-by-side display: response, latency, tokens, cost, confidence for each run
- Delta row: computed % differences with color coding (green = better, red = worse)
- Uses `createAdminClient().compareRagExecutions()` if available, otherwise computes deltas client-side from two execution details.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/playground.test.tsx`
Expected: PASS

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/playground/
git commit -m "feat(web): add RAG Playground page with query tester and compare"
```

---

## Task 5: Configuration Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/config/page.tsx`
- Test: `apps/web/src/__tests__/app/admin/agents/config.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/app/admin/agents/config.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/config',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getTierStrategyMatrix: vi.fn().mockResolvedValue({ tiers: [] }),
    getStrategyModelMappings: vi.fn().mockResolvedValue([]),
    getModelHealth: vi.fn().mockResolvedValue([]),
    getModelChangeHistory: vi.fn().mockResolvedValue([]),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: { create: vi.fn() },
}));

vi.mock('@/lib/api', () => ({
  api: {
    config: {
      getChatHistoryLimits: vi.fn().mockResolvedValue({ free: 10, normal: 50, premium: 100, admin: 999 }),
      updateChatHistoryLimits: vi.fn().mockResolvedValue({}),
    },
  },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Configuration', () => {
  it('renders three tabs', async () => {
    const { default: ConfigPage } = await import(
      '@/app/admin/(dashboard)/agents/config/page'
    );
    render(<ConfigPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /strategy/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /models/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /limits/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/config.test.tsx`
Expected: FAIL — file doesn't exist

- [ ] **Step 3: Implement Configuration page**

Create `apps/web/src/app/admin/(dashboard)/agents/config/page.tsx`:
- `'use client'` component
- Page header ("Configurazione AI" / "Strategy, modelli e limiti")
- `<Tabs defaultValue="strategy">` with 3 tabs
- Tab "Strategy": Embed the core rendering from `StrategyConfigPage` (`/agents/strategy/page.tsx`). The strategy page uses `createAdminClient()`, `useAdminConfig`, `useToast`, and renders tier matrix + model mappings + confidence config.
- Tab "Models": Embed the core rendering from `ModelHealthPage` (`/agents/models/page.tsx`). KPI cards, tracked models table, change history.
- Tab "Limits": Embed the core rendering from `ChatLimitsPage` (`/agents/chat-limits/page.tsx`). Form with tier-based limits.

Support `?tab=models` and `?tab=limits` via `useSearchParams` for deep linking from redirects.

**Approach**: Each tab renders the full content of the original page (minus the page header, since the new wrapper provides that). Copy the component JSX and hooks directly — these are self-contained client components.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/config.test.tsx`
Expected: PASS

- [ ] **Step 5: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/config/
git commit -m "feat(web): add Configuration page merging strategy + models + limits"
```

---

## Task 6: Usage & Costs — Add Tabs

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/usage/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/agents/usage/token-balance-tab.tsx`
- Test: `apps/web/src/__tests__/app/admin/agents/usage-tabs.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/app/admin/agents/usage-tabs.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/usage',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getOpenRouterStatus: vi.fn().mockResolvedValue({}),
    getUsageTimeline: vi.fn().mockResolvedValue([]),
    getUsageCosts: vi.fn().mockResolvedValue({}),
    getUsageFreeQuota: vi.fn().mockResolvedValue({}),
    getRecentRequests: vi.fn().mockResolvedValue([]),
    getTokenBalance: vi.fn().mockResolvedValue({ tokensUsed: 0, cost: 0 }),
    getTokenTierUsage: vi.fn().mockResolvedValue([]),
    getTopConsumers: vi.fn().mockResolvedValue([]),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: { create: vi.fn() },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Usage & Costs Tabs', () => {
  it('renders three tabs', async () => {
    const { default: UsagePage } = await import(
      '@/app/admin/(dashboard)/agents/usage/page'
    );
    render(<UsagePage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /openrouter/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /token balance/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /chat log/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/usage-tabs.test.tsx`
Expected: FAIL — current page has no tabs

- [ ] **Step 3: Implement Token Balance tab component**

Create `apps/web/src/app/admin/(dashboard)/agents/usage/token-balance-tab.tsx`:
- `'use client'` component, named export `TokenBalanceTab`
- Uses `createAdminClient()` to call:
  - `getTokenBalance()` → balance card (tokens used, cost, budget %)
  - `getTokenTierUsage()` → tier breakdown table (free/normal/premium usage)
  - `getTopConsumers()` → top 10 consumers table (user, tokens, cost)
- Layout: Balance card at top, then 2-column grid (tier usage left, top consumers right)
- Uses existing shadcn `Card`, `Badge`, `Skeleton` components

- [ ] **Step 4: Wrap Usage page in Tabs**

Modify `apps/web/src/app/admin/(dashboard)/agents/usage/page.tsx`:
- Add `Tabs`, `TabsContent`, `TabsList`, `TabsTrigger` imports
- Wrap existing page content (KPI cards, timeline, cost breakdown, etc.) inside `<TabsContent value="openrouter">`
- Add `<TabsContent value="token-balance"><TokenBalanceTab /></TabsContent>`
- Add `<TabsContent value="chat-log">` with `ChatHistoryFilters` + `ChatHistoryTable` from `@/components/admin/agents/chat-history-filters` and `chat-history-table`
- Support `?tab=chat-log` and `?tab=token-balance` via `useSearchParams`

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/__tests__/app/admin/agents/usage-tabs.test.tsx`
Expected: PASS

- [ ] **Step 6: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/usage/
git commit -m "feat(web): add token balance and chat log tabs to Usage page"
```

---

## Task 7: Analytics — Add Tabs

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/analytics/page.tsx`

- [ ] **Step 1: Wrap Analytics page in Tabs**

Modify `apps/web/src/app/admin/(dashboard)/agents/analytics/page.tsx`:
- The current page already has KPI cards + usage chart + top agents table + top queries
- Wrap in `<Tabs defaultValue="overview">`:
  - Tab "Overview": existing KPI cards + usage chart
  - Tab "Top Agents": existing top agents table + top queries
  - Tab "Trends": new — date range selector + execution stats over time using `getRagExecutionStats()` with `dateFrom`/`dateTo` params. Simple line chart or table showing daily execution count, avg latency, error rate, cost.

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Run existing analytics tests**

Run: `cd apps/web && npx vitest run --reporter=verbose 2>&1 | grep -i analytics`
Expected: Existing tests still pass

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/analytics/page.tsx
git commit -m "feat(web): add tabs to Analytics page (overview, top agents, trends)"
```

---

## Task 8: Agent Definitions — Builder Sheet Integration

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx`

- [ ] **Step 1: Add Builder Sheet to Definitions page**

Modify `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx`:
- Add `Sheet`, `SheetContent`, `SheetTitle` imports from `@/components/ui/navigation/sheet`
- Add `BuilderClient` import from `../builder/BuilderClient`
- Add state: `const [builderOpen, setBuilderOpen] = useState(false)`
- Add a "Builder" button in the page header that opens the sheet
- Add Sheet at bottom of page:

```tsx
<Sheet open={builderOpen} onOpenChange={setBuilderOpen}>
  <SheetContent side="right" className="w-[800px] sm:max-w-[800px] p-0 overflow-y-auto">
    <SheetTitle className="sr-only">Strategy Builder</SheetTitle>
    <BuilderClient />
  </SheetContent>
</Sheet>
```

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/definitions/page.tsx
git commit -m "feat(web): integrate Builder as sheet in Definitions page"
```

---

## Task 9: Redirects for Old Routes

**Files:**
- Modify: 9 old page.tsx files to add `redirect()`

- [ ] **Step 1: Add redirects to all old routes**

For each old route, replace the page content with a server-side redirect. Pattern:

```tsx
// apps/web/src/app/admin/(dashboard)/agents/debug/page.tsx
import { redirect } from 'next/navigation';
export default function DebugConsolePage() {
  redirect('/admin/agents/inspector');
}
```

Apply to all 9 old routes per the spec's Route Migration table:

| File | Redirect To |
|------|-------------|
| `agents/debug/page.tsx` | `/admin/agents/inspector` |
| `agents/pipeline/page.tsx` | `/admin/agents/inspector?tab=pipeline` |
| `agents/debug-chat/page.tsx` | `/admin/agents/playground?tab=chat` |
| `agents/sandbox/page.tsx` | `/admin/agents/playground` |
| `agents/builder/page.tsx` | `/admin/agents/definitions` |
| `agents/strategy/page.tsx` | `/admin/agents/config` |
| `agents/models/page.tsx` | `/admin/agents/config?tab=models` |
| `agents/chat-limits/page.tsx` | `/admin/agents/config?tab=limits` |
| `agents/chat-history/page.tsx` | `/admin/agents/usage?tab=chat-log` |

**Note**: The `sandbox/page.tsx` imports `SandboxClient` from `./client.tsx` — keep `client.tsx` as it may be imported by the new Playground page. Only replace the page.tsx default export with the redirect.

**Note**: `builder/page.tsx` imports `BuilderClient` from `./BuilderClient` — keep `BuilderClient.tsx` as it's imported by the Definitions page sheet. Only replace page.tsx.

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors (redirects are valid Next.js patterns)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/agents/debug/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/pipeline/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/debug-chat/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/sandbox/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/builder/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/strategy/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/models/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/chat-limits/page.tsx \
  apps/web/src/app/admin/\(dashboard\)/agents/chat-history/page.tsx
git commit -m "refactor(web): add redirects from old agent routes to consolidated pages"
```

---

## Task 10: Final Verification

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 2: Run all agent-related tests**

Run: `cd apps/web && npx vitest run --reporter=verbose src/__tests__/app/admin/agents/`
Expected: All new tests pass

- [ ] **Step 3: Run full test suite to catch regressions**

Run: `cd apps/web && npx vitest run`
Expected: No regressions in existing tests

- [ ] **Step 4: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 5: Manual smoke test**

Start dev server and verify:
1. `/admin/agents` shows Mission Control with KPI cards
2. Sidebar shows 7 items (not 13)
3. `/admin/agents/inspector` shows 3 tabs
4. `/admin/agents/playground` shows 3 tabs
5. `/admin/agents/config` shows 3 tabs
6. `/admin/agents/usage` shows 3 tabs
7. Old URLs redirect correctly (try `/admin/agents/debug` → should land on `/admin/agents/inspector`)

- [ ] **Step 6: Final commit if any fixes needed, then create PR**

```bash
# PR to main-dev (parent branch)
gh pr create --base main-dev --title "feat(web): consolidate AI admin from 13 to 7 pages" --body "..."
```
