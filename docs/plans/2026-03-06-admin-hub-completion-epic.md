# Admin Hub Completion Epic - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete the 4 stub admin hub pages (Monitor, Config, Analytics, AI) by wiring pre-built components into tab layouts, and fix navigation so all 10 admin sections are discoverable.

**Architecture:** Each hub page already exists with tab structure and NavConfig. The migration replaces placeholder divs with real tab content components. Each tab is a `'use client'` component that fetches data via `adminClient` and renders existing presentational components. Pattern: follow `content/SharedGamesTab.tsx`.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Zod, adminClient API

**Source of truth:** Spec-panel analysis session 2026-03-06. Backend is 100% ready (60+ commands, 90+ queries, 150+ endpoints). This is purely frontend work.

---

## Phase 0: Quick Wins (Navigation + Route Consolidation)

**Branch:** `feature/issue-5040-admin-nav-completion`
**Parent:** `main-dev`
**Issues:** Part of #5040

### Task 0.1: Extend DASHBOARD_SECTIONS to include hub pages

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`
- Test: `apps/web/src/config/__tests__/admin-dashboard-navigation.test.ts` (create)

**Context:** Currently `DASHBOARD_SECTIONS` has 5 sections (overview, users, shared-games, agents, knowledge-base). The 5 hub pages (ai, analytics, config, content, monitor) are unreachable from navigation. Add them as sections so `getActiveSection()` resolves correctly and sidebar shows their items.

**Step 1: Write failing test**

Create `apps/web/src/config/__tests__/admin-dashboard-navigation.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  getSection,
} from '../admin-dashboard-navigation';

describe('admin-dashboard-navigation', () => {
  it('should have 10 dashboard sections', () => {
    expect(DASHBOARD_SECTIONS).toHaveLength(10);
  });

  it('should resolve /admin/monitor to monitor section', () => {
    const section = getActiveSection('/admin/monitor');
    expect(section?.id).toBe('monitor');
  });

  it('should resolve /admin/config to config section', () => {
    const section = getActiveSection('/admin/config');
    expect(section?.id).toBe('config');
  });

  it('should resolve /admin/analytics to analytics section', () => {
    const section = getActiveSection('/admin/analytics');
    expect(section?.id).toBe('analytics');
  });

  it('should resolve /admin/ai to ai section', () => {
    const section = getActiveSection('/admin/ai');
    expect(section?.id).toBe('ai');
  });

  it('should resolve /admin/content to content section', () => {
    const section = getActiveSection('/admin/content');
    expect(section?.id).toBe('content');
  });

  it('should have monitor section with alerts sidebar item', () => {
    const section = getSection('monitor');
    expect(section?.sidebarItems.some(i => i.label === 'Alerts')).toBe(true);
  });

  it('should have config section with feature flags sidebar item', () => {
    const section = getSection('config');
    expect(section?.sidebarItems.some(i => i.label === 'Feature Flags')).toBe(true);
  });

  // Existing sections still work
  it('should still resolve /admin/overview', () => {
    expect(getActiveSection('/admin/overview')?.id).toBe('overview');
  });

  it('should still resolve /admin/agents/pipeline', () => {
    expect(getActiveSection('/admin/agents/pipeline')?.id).toBe('agents');
  });
});
```

**Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/admin-dashboard-navigation.test.ts`
Expected: FAIL — "should have 10 dashboard sections" (currently 5)

**Step 3: Add 5 new sections to navigation config**

Modify `apps/web/src/config/admin-dashboard-navigation.ts`:

Add imports at top (alongside existing ones):
```typescript
import {
  // ... existing imports ...
  MonitorIcon,
  SlidersIcon,
  PieChartIcon,
  FolderOpenIcon,
  BellIcon,
  HardDriveIcon,
  GaugeIcon,
  ToggleLeftIcon,
  KeyIcon,
  ClipboardListIcon,
  MailIcon,
  ZapIcon,
} from 'lucide-react';
```

Add 5 new sections to `DASHBOARD_SECTIONS` array (after knowledge-base):

```typescript
  {
    id: 'monitor',
    label: 'Monitor',
    icon: MonitorIcon,
    baseRoute: '/admin/monitor',
    description: 'Alerts, cache, infrastructure, and operations',
    group: 'core',
    sidebarItems: [
      { href: '/admin/monitor', label: 'Dashboard', icon: MonitorIcon, activePattern: /^\/admin\/monitor$/ },
      { href: '/admin/monitor?tab=alerts', label: 'Alerts', icon: BellIcon },
      { href: '/admin/monitor?tab=cache', label: 'Cache', icon: HardDriveIcon },
      { href: '/admin/monitor?tab=infra', label: 'Infrastructure', icon: ServerIcon },
      { href: '/admin/monitor?tab=services', label: 'Services', icon: GaugeIcon },
      { href: '/admin/monitor?tab=command', label: 'Command Center', icon: TerminalIcon },
    ],
  },
  {
    id: 'config',
    label: 'Config',
    icon: SlidersIcon,
    baseRoute: '/admin/config',
    description: 'Feature flags, rate limits, and system settings',
    group: 'core',
    sidebarItems: [
      { href: '/admin/config', label: 'General', icon: SlidersIcon, activePattern: /^\/admin\/config$/ },
      { href: '/admin/config?tab=flags', label: 'Feature Flags', icon: ToggleLeftIcon },
      { href: '/admin/config?tab=limits', label: 'Limits', icon: SettingsIcon },
      { href: '/admin/config?tab=rate-limits', label: 'Rate Limits', icon: ZapIcon },
    ],
  },
  {
    id: 'analytics',
    label: 'Analytics',
    icon: PieChartIcon,
    baseRoute: '/admin/analytics',
    description: 'Usage statistics, AI analytics, and reports',
    group: 'core',
    sidebarItems: [
      { href: '/admin/analytics', label: 'Overview', icon: PieChartIcon, activePattern: /^\/admin\/analytics$/ },
      { href: '/admin/analytics?tab=ai-usage', label: 'AI Usage', icon: BarChartIcon },
      { href: '/admin/analytics?tab=audit', label: 'Audit Log', icon: ClipboardListIcon },
      { href: '/admin/analytics?tab=reports', label: 'Reports', icon: FileTextIcon },
      { href: '/admin/analytics?tab=api-keys', label: 'API Keys', icon: KeyIcon },
    ],
  },
  {
    id: 'content',
    label: 'Content',
    icon: FolderOpenIcon,
    baseRoute: '/admin/content',
    description: 'Games, shared content, FAQs, and sessions',
    group: 'core',
    sidebarItems: [
      { href: '/admin/content', label: 'Shared Games', icon: FolderOpenIcon, activePattern: /^\/admin\/content$/ },
      { href: '/admin/content?tab=games', label: 'All Games', icon: ListIcon },
      { href: '/admin/content?tab=kb', label: 'Knowledge Base', icon: BookOpenIcon },
      { href: '/admin/content?tab=faqs', label: 'FAQs', icon: MessageSquareIcon },
    ],
  },
  {
    id: 'ai',
    label: 'AI',
    icon: BrainCircuitIcon,
    baseRoute: '/admin/ai',
    description: 'AI agents, typologies, models, and RAG',
    group: 'ai',
    sidebarItems: [
      { href: '/admin/ai', label: 'Overview', icon: BrainCircuitIcon, activePattern: /^\/admin\/ai$/ },
      { href: '/admin/ai?tab=agents', label: 'Agents', icon: BotIcon },
      { href: '/admin/ai?tab=definitions', label: 'Definitions', icon: ListIcon },
      { href: '/admin/ai?tab=models', label: 'Models', icon: CpuIcon },
      { href: '/admin/ai?tab=prompts', label: 'Prompts', icon: MessageSquareIcon },
    ],
  },
```

**Step 4: Run tests and verify they pass**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/admin-dashboard-navigation.test.ts`
Expected: PASS (10/10 tests)

**Step 5: Commit**
```bash
git add apps/web/src/config/admin-dashboard-navigation.ts apps/web/src/config/__tests__/admin-dashboard-navigation.test.ts
git commit -m "feat(web): extend DASHBOARD_SECTIONS with 5 hub sections (#5040)"
```

---

### Task 0.2: Verify typecheck and existing tests still pass

**Step 1: Run typecheck**
Run: `cd apps/web && pnpm typecheck`
Expected: PASS (no new TS errors)

**Step 2: Run existing admin tests**
Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | tail -20`
Expected: All existing tests still PASS

**Step 3: Commit if any adjustments needed**

---

## Phase 1: Monitor Hub Migration (#5053)

**Branch:** `feature/issue-5053-admin-monitor-hub`
**Parent:** `main-dev` (or from Phase 0 branch if not yet merged)
**Issues:** #5053

**Pre-built components to reuse:**
- `AlertRuleForm` (`components/admin/alert-rules/AlertRuleForm.tsx`)
- `AlertRuleList` (`components/admin/alert-rules/AlertRuleList.tsx`)
- `AlertsBanner` (`components/admin/AlertsBanner.tsx`)
- `ServiceHealthMatrix` (`components/admin/ServiceHealthMatrix.tsx`)
- `CommandCenterDashboard` (`components/admin/command-center/CommandCenterDashboard.tsx`)
- `GrafanaEmbed` (`components/admin/GrafanaEmbed.tsx`)

**AdminClient methods already available:**
- Alerts: `getStats()` has alert data, alert rules via dedicated schemas
- Cache: `clearKBCache()`, resource metrics via `getInfrastructureDetails()`
- Infrastructure: `getInfrastructureDetails()`, `getMetricsTimeSeries()`
- Services: data from infrastructure details response
- Testing: `getAccessibilityMetrics()`, `getPerformanceMetrics()`, `getE2EMetrics()`
- Export: `exportUsersToCSV()`, `exportAuditLogs()`, `exportApiKeysToCSV()`

### Task 1.1: Create AlertsTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx`

**Step 1: Write failing test**

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getStats: vi.fn().mockResolvedValue({ activeAlerts: 2, totalAlerts: 10 }),
    },
  },
}));

// Shallow mock the child components to test composition
vi.mock('@/components/admin/AlertsBanner', () => ({
  AlertsBanner: () => <div data-testid="alerts-banner">AlertsBanner</div>,
}));

vi.mock('@/components/admin/alert-rules/AlertRuleList', () => ({
  AlertRuleList: () => <div data-testid="alert-rule-list">AlertRuleList</div>,
}));

import { AlertsTab } from '../AlertsTab';

describe('AlertsTab', () => {
  it('renders alerts banner', async () => {
    render(<AlertsTab />);
    expect(await screen.findByTestId('alerts-banner')).toBeInTheDocument();
  });

  it('renders alert rule list', async () => {
    render(<AlertsTab />);
    expect(await screen.findByTestId('alert-rule-list')).toBeInTheDocument();
  });
});
```

**Step 2: Implement AlertsTab**

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx
'use client';

import { useState, useEffect } from 'react';

import { AlertsBanner } from '@/components/admin/AlertsBanner';
import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';
import { api } from '@/lib/api';

export function AlertsTab() {
  const [rules, setRules] = useState<AlertRule[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // TODO: Replace with actual alert rules fetch when adminClient method is added
    setLoading(false);
  }, []);

  return (
    <div className="space-y-6">
      <AlertsBanner />
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Alert Rules
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Configure alert thresholds and notification targets.
        </p>
      </div>
      <AlertRuleList
        rules={rules}
        onEdit={(rule) => { /* TODO: open edit modal */ }}
        onDelete={(id) => { /* TODO: confirm + delete */ }}
        onToggle={(id) => { /* TODO: toggle rule */ }}
      />
    </div>
  );
}
```

**Step 3: Run test, verify pass, commit**

```bash
cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/monitor/__tests__/AlertsTab.test.tsx
git add apps/web/src/app/admin/\(dashboard\)/monitor/AlertsTab.tsx apps/web/src/app/admin/\(dashboard\)/monitor/__tests__/AlertsTab.test.tsx
git commit -m "feat(web): add AlertsTab for Monitor hub (#5053)"
```

---

### Task 1.2: Create InfrastructureTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/InfrastructureTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/InfrastructureTab.test.tsx`

**Step 1: Write failing test**

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/InfrastructureTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/components/admin/ServiceHealthMatrix', () => ({
  ServiceHealthMatrix: (props: { loading?: boolean }) => (
    <div data-testid="service-health-matrix">
      {props.loading ? 'Loading...' : 'ServiceHealthMatrix'}
    </div>
  ),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getInfrastructureDetails: vi.fn().mockResolvedValue({
        services: [
          { name: 'PostgreSQL', status: 'Healthy', responseTimeMs: 12 },
          { name: 'Qdrant', status: 'Healthy', responseTimeMs: 5 },
          { name: 'Redis', status: 'Healthy', responseTimeMs: 1 },
        ],
      }),
    },
  },
}));

import { InfrastructureTab } from '../InfrastructureTab';

describe('InfrastructureTab', () => {
  it('renders service health matrix', async () => {
    render(<InfrastructureTab />);
    expect(await screen.findByTestId('service-health-matrix')).toBeInTheDocument();
  });

  it('renders heading', () => {
    render(<InfrastructureTab />);
    expect(screen.getByText('Infrastructure Health')).toBeInTheDocument();
  });
});
```

**Step 2: Implement InfrastructureTab**

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/InfrastructureTab.tsx
'use client';

import { useState, useEffect } from 'react';

import { ServiceHealthMatrix } from '@/components/admin/ServiceHealthMatrix';
import type { ServiceHealthStatus } from '@/lib/api';
import { api } from '@/lib/api';

export function InfrastructureTab() {
  const [services, setServices] = useState<ServiceHealthStatus[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getInfrastructureDetails()
      .then((data) => {
        setServices(data?.services ?? []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Infrastructure Health
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Real-time status of PostgreSQL, Qdrant, Redis, and external services.
        </p>
      </div>
      <ServiceHealthMatrix services={services} loading={loading} layout="auto" />
    </div>
  );
}
```

**Step 3: Run test, verify pass, commit**

```bash
cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/monitor/__tests__/InfrastructureTab.test.tsx
git commit -m "feat(web): add InfrastructureTab for Monitor hub (#5053)"
```

---

### Task 1.3: Create CacheTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/CacheTab.test.tsx`

**Step 1: Write test, Step 2: Implement**

The CacheTab shows cache metrics (hit rate, memory usage, keys) and a "Clear Cache" button. Uses `adminClient.clearKBCache()` for the action and `adminClient.getInfrastructureDetails()` for metrics.

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';

import { HardDrive, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface CacheMetrics {
  hitRate: number;
  memoryUsageMb: number;
  totalKeys: number;
  evictions: number;
}

export function CacheTab() {
  const [metrics, setMetrics] = useState<CacheMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [clearing, setClearing] = useState(false);

  const fetchMetrics = useCallback(() => {
    setLoading(true);
    api.admin
      .getInfrastructureDetails()
      .then((data) => {
        // Extract cache metrics from infrastructure details
        setMetrics({
          hitRate: data?.cacheHitRate ?? 0,
          memoryUsageMb: data?.cacheMemoryMb ?? 0,
          totalKeys: data?.cacheKeys ?? 0,
          evictions: data?.cacheEvictions ?? 0,
        });
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { fetchMetrics(); }, [fetchMetrics]);

  const handleClearCache = async () => {
    setClearing(true);
    try {
      await api.admin.clearKBCache();
      fetchMetrics();
    } catch {
      // toast error
    } finally {
      setClearing(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Cache Management
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Redis + HybridCache metrics and operations.
          </p>
        </div>
        <Button variant="destructive" size="sm" onClick={handleClearCache} disabled={clearing}>
          <Trash2 className="mr-2 h-4 w-4" />
          {clearing ? 'Clearing...' : 'Clear Cache'}
        </Button>
      </div>
      {loading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="h-24 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse" />
          ))}
        </div>
      ) : metrics ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <MetricCard label="Hit Rate" value={`${(metrics.hitRate * 100).toFixed(1)}%`} />
          <MetricCard label="Memory" value={`${metrics.memoryUsageMb.toFixed(1)} MB`} />
          <MetricCard label="Total Keys" value={metrics.totalKeys.toLocaleString()} />
          <MetricCard label="Evictions" value={metrics.evictions.toLocaleString()} />
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Unable to load cache metrics.</p>
      )}
    </div>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md p-4">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 text-2xl font-bold tracking-tight text-foreground">{value}</p>
    </div>
  );
}
```

**Step 3: Commit**
```bash
git commit -m "feat(web): add CacheTab for Monitor hub (#5053)"
```

---

### Task 1.4: Create CommandCenterTab and TestingTab wrappers

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/CommandCenterTab.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/TestingTab.tsx`

These are thin wrappers around existing components:

```typescript
// CommandCenterTab.tsx
'use client';
import { CommandCenterDashboard } from '@/components/admin/command-center/CommandCenterDashboard';
export function CommandCenterTab() {
  return <CommandCenterDashboard />;
}
```

```typescript
// TestingTab.tsx
'use client';
import { useState, useEffect } from 'react';
import { api } from '@/lib/api';

export function TestingTab() {
  const [metrics, setMetrics] = useState<{
    accessibility: unknown;
    performance: unknown;
    e2e: unknown;
  } | null>(null);

  useEffect(() => {
    Promise.all([
      api.admin.getAccessibilityMetrics().catch(() => null),
      api.admin.getPerformanceMetrics().catch(() => null),
      api.admin.getE2EMetrics().catch(() => null),
    ]).then(([a, p, e]) => setMetrics({ accessibility: a, performance: p, e2e: e }));
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Testing Metrics
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Accessibility, performance, and E2E test results.
        </p>
      </div>
      {/* Render metrics cards - adapt based on actual DTO shapes */}
      <pre className="text-xs bg-muted/50 rounded-lg p-4 overflow-auto">
        {JSON.stringify(metrics, null, 2)}
      </pre>
    </div>
  );
}
```

**Commit:**
```bash
git commit -m "feat(web): add CommandCenterTab and TestingTab for Monitor hub (#5053)"
```

---

### Task 1.5: Wire tabs into Monitor hub page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/page.test.tsx`

**Step 1: Write failing test**

```typescript
// apps/web/src/app/admin/(dashboard)/monitor/__tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock all tab components
vi.mock('../AlertsTab', () => ({ AlertsTab: () => <div data-testid="alerts-tab" /> }));
vi.mock('../CacheTab', () => ({ CacheTab: () => <div data-testid="cache-tab" /> }));
vi.mock('../InfrastructureTab', () => ({ InfrastructureTab: () => <div data-testid="infra-tab" /> }));
vi.mock('../CommandCenterTab', () => ({ CommandCenterTab: () => <div data-testid="command-tab" /> }));
vi.mock('../TestingTab', () => ({ TestingTab: () => <div data-testid="testing-tab" /> }));
vi.mock('../NavConfig', () => ({ AdminMonitorNavConfig: () => null }));

import AdminMonitorPage from '../page';

describe('AdminMonitorPage', () => {
  it('renders alerts tab by default', async () => {
    const page = await AdminMonitorPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('alerts-tab')).toBeInTheDocument();
  });

  it('renders cache tab when tab=cache', async () => {
    const page = await AdminMonitorPage({ searchParams: Promise.resolve({ tab: 'cache' }) });
    render(page);
    expect(screen.getByTestId('cache-tab')).toBeInTheDocument();
  });

  it('renders infrastructure tab when tab=infra', async () => {
    const page = await AdminMonitorPage({ searchParams: Promise.resolve({ tab: 'infra' }) });
    render(page);
    expect(screen.getByTestId('infra-tab')).toBeInTheDocument();
  });
});
```

**Step 2: Replace placeholder in page.tsx with real tab rendering**

Follow the `content/page.tsx` pattern exactly. Replace the placeholder `<div>` with a `renderTabContent()` switch that returns Suspense-wrapped tab components.

```typescript
// Updated monitor/page.tsx — key changes:
import { Suspense } from 'react';
import { AlertsTab } from './AlertsTab';
import { CacheTab } from './CacheTab';
import { InfrastructureTab } from './InfrastructureTab';
import { CommandCenterTab } from './CommandCenterTab';
import { TestingTab } from './TestingTab';

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="h-[600px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
  );
}

function renderTabContent(tab: TabId) {
  switch (tab) {
    case 'alerts':
      return <Suspense fallback={<TabSkeleton />}><AlertsTab /></Suspense>;
    case 'cache':
      return <Suspense fallback={<TabSkeleton />}><CacheTab /></Suspense>;
    case 'infra':
      return <Suspense fallback={<TabSkeleton />}><InfrastructureTab /></Suspense>;
    case 'services':
      return <Suspense fallback={<TabSkeleton />}><InfrastructureTab /></Suspense>;
    case 'command':
      return <Suspense fallback={<TabSkeleton />}><CommandCenterTab /></Suspense>;
    case 'testing':
      return <Suspense fallback={<TabSkeleton />}><TestingTab /></Suspense>;
    case 'export':
      return <ComingSoonTab label="Bulk Export" description="Export users, audit logs, and API keys in bulk." />;
    default:
      return null;
  }
}

// Replace the placeholder div with:
{renderTabContent(tab as TabId)}
```

**Step 3: Run tests, verify pass, commit**

```bash
cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/monitor/__tests__/
git commit -m "feat(web): wire Monitor hub tabs with real content (#5053)"
```

---

### Task 1.6: Typecheck + full test suite

**Step 1:** `cd apps/web && pnpm typecheck`
**Step 2:** `cd apps/web && pnpm vitest run`
**Step 3:** Fix any issues, commit

```bash
git commit -m "fix(web): resolve TS errors from Monitor hub migration (#5053)"
```

---

## Phase 2: Config Hub Migration (#5052)

**Branch:** `feature/issue-5052-admin-config-hub`
**Parent:** `main-dev`

**Pre-built components:**
- `FeatureFlagsTab` (`components/admin/FeatureFlagsTab.tsx`) — FULLY BUILT with toggles, tier management, bulk actions
- `PdfLimitsConfig` (`components/admin/PdfLimitsConfig.tsx`)

**AdminClient methods:**
- Feature flags: uses `api.admin` config endpoints
- Rate limits: `AdminConfigEndpoints.cs` — GET/PUT pdf-limits per tier
- Session limits: `SessionLimitsConfigEndpoints.cs`
- PDF tier limits: `PdfTierUploadLimitsConfigEndpoints.cs`

### Task 2.1: Create FeatureFlagsWrapper (data-fetching wrapper)

Since `FeatureFlagsTab` already accepts `configurations: SystemConfigurationDto[]` props, create a wrapper that fetches and passes data.

```typescript
// apps/web/src/app/admin/(dashboard)/config/FeatureFlagsWrapper.tsx
'use client';
import { useState, useEffect } from 'react';
import { FeatureFlagsTab } from '@/components/admin/FeatureFlagsTab';
import { api, type SystemConfigurationDto } from '@/lib/api';

export function FeatureFlagsWrapper() {
  const [configs, setConfigs] = useState<SystemConfigurationDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Fetch feature flag configurations from backend
    api.configuration?.getAll?.()
      .then(setConfigs)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="h-96 animate-pulse bg-muted/30 rounded-xl" />;
  return <FeatureFlagsTab configurations={configs} />;
}
```

### Task 2.2: Create LimitsTab (PDF + Session limits)

Wraps `PdfLimitsConfig` and adds session limits config.

### Task 2.3: Create RateLimitsTab

New component using rate limit admin endpoints.

### Task 2.4: Wire tabs into Config hub page

Same pattern as Monitor hub: replace placeholder with `renderTabContent()` switch.

### Task 2.5: Tests + typecheck

---

## Phase 3: Analytics Hub Migration (#5051)

**Branch:** `feature/issue-5051-admin-analytics-hub`
**Parent:** `main-dev`

**AdminClient methods available:**
- `getAiRequests(params)` — AI request analytics
- `getAnalytics(params)` — general analytics
- `getAuditLogs(params)` + `exportAuditLogs(params)` — audit log
- `generateReport(request)` + `scheduleReport(request)` — reports
- `getApiKeysWithStats(params)` — API key management
- `getPdfAnalytics(days)`, `getChatAnalytics(days)`, `getModelPerformance(days)`

### Task 3.1: Create AiUsageTab

Charts showing AI request volume, cost breakdown, model performance. Reuses existing chart components from `components/admin/charts/`.

### Task 3.2: Create AuditLogTab

Searchable, filterable table of audit log entries with CSV export button. Uses `adminClient.getAuditLogs()` and `adminClient.exportAuditLogs()`.

### Task 3.3: Create ReportsTab

Report generation UI + scheduled reports list. Uses `adminClient.generateReport()`, `getScheduledReports()`, `getReportExecutions()`.

### Task 3.4: Create ApiKeysTab

API key management table with stats, delete, bulk import/export. Uses `adminClient.getApiKeysWithStats()`, `deleteApiKey()`, `exportApiKeysToCSV()`.

### Task 3.5: Wire tabs into Analytics hub page

### Task 3.6: Tests + typecheck

---

## Phase 4 (Optional): AI Hub Migration (#5048)

**Branch:** `feature/issue-5048-admin-ai-hub`
**Parent:** `main-dev`

Lower priority since most AI features are already accessible via `/admin/agents/*` section. The AI hub consolidates:
- Agent Typologies (existing page content)
- AI Lab (playground wrappers)
- Prompts (link to existing prompt management)
- Models (link to existing models page)
- RAG (link to existing pipeline/debug)

This phase may be deferred as it's more consolidation than new capability.

---

## Commit Strategy

Each phase = 1 PR to `main-dev`:
- Phase 0: `feat(web): extend admin navigation with hub sections (#5040)`
- Phase 1: `feat(web): complete Monitor hub with real tab content (#5053)`
- Phase 2: `feat(web): complete Config hub with real tab content (#5052)`
- Phase 3: `feat(web): complete Analytics hub with real tab content (#5051)`

## Definition of Done per Phase

- [ ] All tabs render real content (no placeholder divs)
- [ ] Tab skeleton loading states work
- [ ] Tests pass (unit + typecheck)
- [ ] No new TS errors introduced
- [ ] Navigation resolves correctly for all hub URLs
- [ ] PR to `main-dev`, code review, merge
- [ ] Close related GitHub issues

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| AdminClient methods don't match actual backend DTOs | Verify with `GET /scalar/v1` API docs before implementing |
| Existing components need props we can't provide | Add fallback/optional props, don't modify component contracts |
| Hub tab URLs (`?tab=x`) don't work with sidebar `isSidebarItemActive` | Test with regex patterns in `activePattern` |
| Existing tests break due to navigation changes | Run full test suite after Phase 0 before proceeding |
