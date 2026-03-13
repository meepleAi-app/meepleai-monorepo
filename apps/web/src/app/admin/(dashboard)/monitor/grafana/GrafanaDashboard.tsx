'use client';

/**
 * GrafanaDashboard — Embedded Grafana dashboard selector with iframe viewer,
 * time range controls, and auto-refresh toggle.
 * Issue #134 — Grafana Dashboard Embed
 */

import { useCallback, useMemo, useState } from 'react';

import { BarChart3, Clock, ExternalLink, Maximize2, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const GRAFANA_BASE_URL = process.env.NEXT_PUBLIC_GRAFANA_URL ?? 'http://localhost:3001';

const DASHBOARDS = [
  {
    id: 'api-overview',
    name: 'API Overview',
    category: 'Application',
    description: 'Request rates, latency, error rates',
  },
  {
    id: 'api-latency',
    name: 'API Latency',
    category: 'Application',
    description: 'P50/P95/P99 response times',
  },
  {
    id: 'database-overview',
    name: 'Database Overview',
    category: 'Infrastructure',
    description: 'Connections, queries, storage',
  },
  {
    id: 'database-queries',
    name: 'Database Queries',
    category: 'Infrastructure',
    description: 'Slow queries, query plans',
  },
  {
    id: 'redis-overview',
    name: 'Redis Overview',
    category: 'Infrastructure',
    description: 'Memory, hit rate, commands',
  },
  {
    id: 'qdrant-vectors',
    name: 'Qdrant Vectors',
    category: 'Infrastructure',
    description: 'Collections, search latency',
  },
  {
    id: 'llm-usage',
    name: 'LLM Usage',
    category: 'AI Services',
    description: 'Token consumption, costs, models',
  },
  {
    id: 'llm-latency',
    name: 'LLM Latency',
    category: 'AI Services',
    description: 'Provider response times',
  },
  {
    id: 'embedding-service',
    name: 'Embedding Service',
    category: 'AI Services',
    description: 'Throughput, queue depth',
  },
  {
    id: 'pdf-processing',
    name: 'PDF Processing',
    category: 'AI Services',
    description: 'Pipeline status, extraction rates',
  },
  {
    id: 'docker-containers',
    name: 'Docker Containers',
    category: 'Infrastructure',
    description: 'CPU, memory, network per container',
  },
  {
    id: 'system-resources',
    name: 'System Resources',
    category: 'Infrastructure',
    description: 'Host CPU, RAM, disk I/O',
  },
  {
    id: 'auth-sessions',
    name: 'Auth & Sessions',
    category: 'Security',
    description: 'Login attempts, active sessions',
  },
  {
    id: 'audit-trail',
    name: 'Audit Trail',
    category: 'Security',
    description: 'Admin actions, security events',
  },
] as const;

type Dashboard = (typeof DASHBOARDS)[number];
type Category = Dashboard['category'];

const CATEGORIES: Category[] = ['Application', 'Infrastructure', 'AI Services', 'Security'];

const CATEGORY_COLORS: Record<Category, string> = {
  Application: 'text-blue-600 dark:text-blue-400',
  Infrastructure: 'text-emerald-600 dark:text-emerald-400',
  'AI Services': 'text-purple-600 dark:text-purple-400',
  Security: 'text-amber-600 dark:text-amber-400',
};

const CATEGORY_BADGE_COLORS: Record<Category, string> = {
  Application: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  Infrastructure: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400',
  'AI Services': 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400',
  Security: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400',
};

const TIME_RANGES = [
  { label: '15m', value: 'now-15m' },
  { label: '1h', value: 'now-1h' },
  { label: '6h', value: 'now-6h' },
  { label: '24h', value: 'now-24h' },
  { label: '7d', value: 'now-7d' },
] as const;

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function DashboardCard({
  dashboard,
  isSelected,
  onSelect,
}: {
  dashboard: Dashboard;
  isSelected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      className={cn(
        'text-left rounded-lg border p-3 transition-all duration-150',
        'hover:border-primary/40 hover:shadow-sm',
        isSelected
          ? 'border-primary bg-primary/5 ring-1 ring-primary/20'
          : 'border-border bg-white/50 dark:bg-zinc-900/50'
      )}
      onClick={onSelect}
      data-testid={`dashboard-card-${dashboard.id}`}
    >
      <div className="flex items-start gap-2">
        <BarChart3
          className={cn(
            'mt-0.5 h-4 w-4 flex-shrink-0',
            isSelected ? 'text-primary' : 'text-muted-foreground'
          )}
        />
        <div className="min-w-0">
          <p className="text-sm font-medium truncate">{dashboard.name}</p>
          <p className="text-xs text-muted-foreground mt-0.5 line-clamp-1">
            {dashboard.description}
          </p>
        </div>
      </div>
    </button>
  );
}

function CategorySection({
  category,
  dashboards,
  selectedId,
  onSelect,
}: {
  category: Category;
  dashboards: Dashboard[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  return (
    <div data-testid={`category-section-${category.replace(/\s+/g, '-').toLowerCase()}`}>
      <p
        className={cn(
          'text-xs font-semibold uppercase tracking-wider mb-2',
          CATEGORY_COLORS[category]
        )}
      >
        {category}
      </p>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
        {dashboards.map(d => (
          <DashboardCard
            key={d.id}
            dashboard={d}
            isSelected={selectedId === d.id}
            onSelect={() => onSelect(d.id)}
          />
        ))}
      </div>
    </div>
  );
}

function GrafanaNotConfigured() {
  return (
    <div
      className="rounded-xl border border-dashed border-muted-foreground/30 bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-8 text-center"
      data-testid="grafana-not-configured"
    >
      <BarChart3 className="mx-auto h-10 w-10 text-muted-foreground/50 mb-3" />
      <h3 className="font-quicksand text-base font-semibold text-foreground">
        Grafana Not Configured
      </h3>
      <p className="mt-1 text-sm text-muted-foreground max-w-md mx-auto">
        Set the{' '}
        <code className="rounded bg-muted px-1.5 py-0.5 text-xs font-mono">
          NEXT_PUBLIC_GRAFANA_URL
        </code>{' '}
        environment variable to enable embedded dashboards.
      </p>
      <div className="mt-4 rounded-lg bg-muted/50 p-4 text-left max-w-md mx-auto">
        <p className="text-xs font-medium text-muted-foreground mb-1.5">Setup steps:</p>
        <ol className="text-xs text-muted-foreground space-y-1 list-decimal list-inside">
          <li>Deploy Grafana (e.g. via Docker Compose)</li>
          <li>
            Set{' '}
            <code className="rounded bg-muted px-1 py-0.5 font-mono">NEXT_PUBLIC_GRAFANA_URL</code>{' '}
            in <code className="rounded bg-muted px-1 py-0.5 font-mono">.env.local</code>
          </li>
          <li>
            Enable anonymous access or configure{' '}
            <code className="rounded bg-muted px-1 py-0.5 font-mono">allow_embedding = true</code>{' '}
            in Grafana
          </li>
          <li>Provision dashboards matching the IDs listed above</li>
        </ol>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function GrafanaDashboard() {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [timeRange, setTimeRange] = useState<string>('now-1h');
  const [autoRefresh, setAutoRefresh] = useState(false);

  const isConfigured = !!process.env.NEXT_PUBLIC_GRAFANA_URL;

  const grouped = useMemo(() => {
    return CATEGORIES.map(cat => ({
      category: cat,
      dashboards: DASHBOARDS.filter(d => d.category === cat),
    }));
  }, []);

  const selectedDashboard = DASHBOARDS.find(d => d.id === selectedId) ?? null;

  const iframeUrl = useMemo(() => {
    if (!selectedId) return null;
    const params = new URLSearchParams({
      orgId: '1',
      kiosk: '',
      theme: 'dark',
      from: timeRange,
      to: 'now',
    });
    if (autoRefresh) {
      params.set('refresh', '30s');
    }
    return `${GRAFANA_BASE_URL}/d/${selectedId}?${params.toString()}`;
  }, [selectedId, timeRange, autoRefresh]);

  const handleOpenFullscreen = useCallback(() => {
    if (!iframeUrl) return;
    window.open(iframeUrl, '_blank', 'noopener,noreferrer');
  }, [iframeUrl]);

  return (
    <div className="space-y-5" data-testid="grafana-dashboard">
      {/* Dashboard Selector */}
      <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-4 space-y-4">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium text-foreground">Select Dashboard</p>
          {selectedDashboard && (
            <Badge className={cn('text-xs', CATEGORY_BADGE_COLORS[selectedDashboard.category])}>
              {selectedDashboard.category}
            </Badge>
          )}
        </div>

        <div className="space-y-4">
          {grouped.map(({ category, dashboards }) => (
            <CategorySection
              key={category}
              category={category}
              dashboards={dashboards}
              selectedId={selectedId}
              onSelect={setSelectedId}
            />
          ))}
        </div>
      </div>

      {/* Controls bar — shown only when a dashboard is selected */}
      {selectedId && (
        <div
          className="flex flex-wrap items-center gap-3 rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md px-4 py-3"
          data-testid="grafana-controls"
        >
          {/* Time range */}
          <div className="flex items-center gap-1.5" data-testid="time-range-selector">
            <Clock className="h-3.5 w-3.5 text-muted-foreground" />
            {TIME_RANGES.map(tr => (
              <Button
                key={tr.value}
                variant={timeRange === tr.value ? 'default' : 'outline'}
                size="sm"
                className="h-7 px-2.5 text-xs"
                onClick={() => setTimeRange(tr.value)}
                data-testid={`time-range-${tr.label}`}
              >
                {tr.label}
              </Button>
            ))}
          </div>

          {/* Auto-refresh toggle */}
          <Button
            variant={autoRefresh ? 'default' : 'outline'}
            size="sm"
            className="gap-1.5 h-7 text-xs"
            onClick={() => setAutoRefresh(prev => !prev)}
            data-testid="auto-refresh-toggle"
          >
            <RefreshCw className={cn('h-3.5 w-3.5', autoRefresh && 'animate-spin')} />
            {autoRefresh ? 'Auto (30s)' : 'Refresh'}
          </Button>

          {/* Fullscreen */}
          <Button
            variant="outline"
            size="sm"
            className="gap-1.5 h-7 text-xs ml-auto"
            onClick={handleOpenFullscreen}
            data-testid="fullscreen-toggle"
          >
            <Maximize2 className="h-3.5 w-3.5" />
            Open
            <ExternalLink className="h-3 w-3" />
          </Button>
        </div>
      )}

      {/* Iframe / Placeholder */}
      {selectedId ? (
        isConfigured ? (
          <div
            className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-hidden"
            data-testid="grafana-iframe-container"
          >
            <iframe
              key={iframeUrl}
              src={iframeUrl ?? ''}
              title={selectedDashboard?.name ?? 'Grafana Dashboard'}
              className="w-full border-0"
              style={{ height: 'calc(100vh - 320px)', minHeight: '500px' }}
              loading="lazy"
              data-testid="grafana-iframe"
            />
          </div>
        ) : (
          <GrafanaNotConfigured />
        )
      ) : (
        <div
          className="rounded-xl border border-dashed border-muted-foreground/20 bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-10 text-center"
          data-testid="grafana-empty-state"
        >
          <BarChart3 className="mx-auto h-8 w-8 text-muted-foreground/40 mb-2" />
          <p className="text-sm text-muted-foreground">Select a dashboard above to view metrics.</p>
        </div>
      )}
    </div>
  );
}
