'use client';

/**
 * ServicesDashboard — Enhanced service health matrix with auto-refresh,
 * uptime badges, response time trends, and category grouping.
 * Issue #132 — Phase 2A: Enhanced ServiceHealthMatrix
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import {
  Activity,
  ChevronDown,
  ChevronRight,
  Clock,
  Loader2,
  Maximize2,
  Minimize2,
  Pause,
  Play,
  TrendingDown,
  TrendingUp,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';
import { api } from '@/lib/api';
import type {
  EnhancedServiceDashboard,
  EnhancedServiceHealth,
  OverallHealthStatus,
  ServiceCategory,
} from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

const REFRESH_OPTIONS = [
  { value: 15, label: '15s' },
  { value: 30, label: '30s' },
  { value: 60, label: '60s' },
] as const;

const CATEGORY_ORDER: ServiceCategory[] = [
  'Core Infrastructure',
  'AI Services',
  'External APIs',
  'Monitoring',
];

const CATEGORY_COLORS: Record<ServiceCategory, string> = {
  'Core Infrastructure': 'text-blue-600',
  'AI Services': 'text-purple-600',
  'External APIs': 'text-amber-600',
  Monitoring: 'text-green-600',
};

function UptimeBadge({ percent }: { percent: number }) {
  const color =
    percent >= 99
      ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
      : percent >= 95
        ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
        : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';

  return (
    <span
      className={cn('inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium', color)}
      data-testid="uptime-badge"
    >
      {percent.toFixed(1)}%
    </span>
  );
}

function TrendIndicator({
  trend,
  currentMs,
  previousMs,
}: {
  trend: 'up' | 'down' | 'stable';
  currentMs: number;
  previousMs?: number;
}) {
  if (trend === 'up') {
    return (
      <span
        className="inline-flex items-center gap-0.5 text-red-600"
        title={previousMs != null ? `Previous: ${previousMs}ms` : undefined}
        data-testid="trend-up"
      >
        <TrendingUp className="h-3 w-3" />
      </span>
    );
  }
  if (trend === 'down') {
    return (
      <span
        className="inline-flex items-center gap-0.5 text-green-600"
        title={previousMs != null ? `Previous: ${previousMs}ms` : undefined}
        data-testid="trend-down"
      >
        <TrendingDown className="h-3 w-3" />
      </span>
    );
  }
  return (
    <span className="text-muted-foreground text-xs" data-testid="trend-stable">
      —
    </span>
  );
}

function formatResponseTime(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
  return `${ms}ms`;
}

function ServiceRow({ service, compact }: { service: EnhancedServiceHealth; compact: boolean }) {
  const stateColor =
    service.state === 'Healthy'
      ? 'bg-green-500'
      : service.state === 'Degraded'
        ? 'bg-yellow-500'
        : 'bg-red-500';

  const stateBadge =
    service.state === 'Healthy'
      ? 'secondary'
      : service.state === 'Degraded'
        ? 'outline'
        : 'destructive';

  return (
    <tr className="border-b last:border-0" data-testid={`service-row-${service.serviceName}`}>
      <td className="py-3 px-4">
        <div className="flex items-center gap-2">
          <span
            className={cn('h-2 w-2 rounded-full flex-shrink-0 transition-colors duration-500', stateColor)}
            data-testid="status-dot"
          />
          <span className="font-medium text-sm">{service.serviceName}</span>
        </div>
      </td>
      <td className="py-3 px-3">
        <Badge variant={stateBadge} className="text-xs">
          {service.state}
        </Badge>
      </td>
      <td className="py-3 px-3">
        <UptimeBadge percent={service.uptimePercent24h} />
      </td>
      <td className="py-3 px-3">
        <div className="flex items-center gap-1.5">
          <span className="text-sm font-mono">{formatResponseTime(service.responseTimeMs)}</span>
          <TrendIndicator
            trend={service.responseTimeTrend}
            currentMs={service.responseTimeMs}
            previousMs={service.previousResponseTimeMs}
          />
        </div>
      </td>
      {!compact && (
        <>
          <td className="py-3 px-3 text-xs text-muted-foreground">
            {service.lastIncidentAt
              ? new Date(service.lastIncidentAt).toLocaleString()
              : 'None'}
          </td>
          <td className="py-3 px-3 text-xs text-muted-foreground">
            {service.errorMessage ?? '—'}
          </td>
        </>
      )}
    </tr>
  );
}

function OverallHealthBanner({ overall }: { overall: OverallHealthStatus }) {
  const color =
    overall.state === 'Healthy'
      ? 'border-green-300 bg-green-50 dark:bg-green-950/20'
      : overall.state === 'Degraded'
        ? 'border-amber-300 bg-amber-50 dark:bg-amber-950/20'
        : 'border-red-300 bg-red-50 dark:bg-red-950/20';

  return (
    <div
      className={cn('rounded-lg border p-3 flex items-center gap-3', color)}
      data-testid="overall-health-banner"
    >
      <Activity
        className={cn(
          'h-5 w-5',
          overall.state === 'Healthy'
            ? 'text-green-600'
            : overall.state === 'Degraded'
              ? 'text-amber-600'
              : 'text-red-600'
        )}
      />
      <div className="flex-1">
        <p className="text-sm font-medium">
          System {overall.state === 'Healthy' ? 'Healthy' : overall.state}
        </p>
        <p className="text-xs text-muted-foreground">
          {overall.healthyServices}/{overall.totalServices} services healthy
          {overall.degradedServices > 0 && ` · ${overall.degradedServices} degraded`}
          {overall.unhealthyServices > 0 && ` · ${overall.unhealthyServices} unhealthy`}
        </p>
      </div>
    </div>
  );
}

function CategoryGroup({
  category,
  services,
  compact,
  defaultOpen,
}: {
  category: ServiceCategory;
  services: EnhancedServiceHealth[];
  compact: boolean;
  defaultOpen: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const unhealthyCount = services.filter(s => s.state !== 'Healthy').length;

  return (
    <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-hidden">
      <button
        className="w-full flex items-center justify-between px-4 py-3 hover:bg-slate-50/50 dark:hover:bg-zinc-800/30 transition-colors"
        onClick={() => setOpen(!open)}
        data-testid={`category-toggle-${category.replace(/\s+/g, '-').toLowerCase()}`}
      >
        <div className="flex items-center gap-2">
          {open ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
          <span className={cn('font-quicksand font-semibold text-sm', CATEGORY_COLORS[category])}>
            {category}
          </span>
          <span className="text-xs text-muted-foreground">({services.length})</span>
          {unhealthyCount > 0 && (
            <Badge variant="destructive" className="text-xs ml-1">
              {unhealthyCount} issue{unhealthyCount > 1 ? 's' : ''}
            </Badge>
          )}
        </div>
      </button>
      {open && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-t border-b text-xs text-muted-foreground">
                <th className="text-left py-2 px-4">Service</th>
                <th className="text-left py-2 px-3">Status</th>
                <th className="text-left py-2 px-3">Uptime (24h)</th>
                <th className="text-left py-2 px-3">Response Time</th>
                {!compact && (
                  <>
                    <th className="text-left py-2 px-3">Last Incident</th>
                    <th className="text-left py-2 px-3">Error</th>
                  </>
                )}
              </tr>
            </thead>
            <tbody>
              {services.map(service => (
                <ServiceRow key={service.serviceName} service={service} compact={compact} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function ServicesDashboard() {
  const [data, setData] = useState<EnhancedServiceDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState(30);
  const [countdown, setCountdown] = useState(30);
  const [compact, setCompact] = useState(false);

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const { toast } = useToast();

  const fetchData = useCallback(async () => {
    try {
      const result = await api.admin.getServiceDashboard();
      if (result) {
        setData(result);
      }
    } catch {
      // On first load show toast, on refresh silently fail
      if (!data) {
        toast({ title: 'Failed to load service health data', variant: 'destructive' });
      }
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [toast]);

  // Initial fetch
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Auto-refresh interval
  useEffect(() => {
    if (intervalRef.current) clearInterval(intervalRef.current);
    if (countdownRef.current) clearInterval(countdownRef.current);

    if (autoRefresh) {
      setCountdown(refreshInterval);

      countdownRef.current = setInterval(() => {
        setCountdown(prev => {
          if (prev <= 1) return refreshInterval;
          return prev - 1;
        });
      }, 1000);

      intervalRef.current = setInterval(() => {
        fetchData();
        setCountdown(refreshInterval);
      }, refreshInterval * 1000);
    }

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
      if (countdownRef.current) clearInterval(countdownRef.current);
    };
  }, [autoRefresh, refreshInterval, fetchData]);

  // Group services by category
  const grouped = data
    ? CATEGORY_ORDER.reduce(
        (acc, cat) => {
          const services = data.services.filter(s => s.category === cat);
          if (services.length > 0) acc.push({ category: cat, services });
          return acc;
        },
        [] as { category: ServiceCategory; services: EnhancedServiceHealth[] }[]
      )
    : [];

  return (
    <div className="space-y-6" data-testid="services-dashboard">
      {/* Header Controls */}
      <div className="flex flex-wrap items-center gap-3">
        {/* Auto-refresh toggle */}
        <div className="flex items-center gap-2" data-testid="auto-refresh-controls">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAutoRefresh(!autoRefresh)}
            className="gap-1.5"
            data-testid="auto-refresh-toggle"
          >
            {autoRefresh ? <Pause className="h-3.5 w-3.5" /> : <Play className="h-3.5 w-3.5" />}
            {autoRefresh ? 'Pause' : 'Resume'}
          </Button>

          {autoRefresh && (
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Clock className="h-3 w-3" />
              <span data-testid="countdown">{countdown}s</span>
            </div>
          )}

          <select
            value={refreshInterval}
            onChange={e => {
              setRefreshInterval(Number(e.target.value));
              setCountdown(Number(e.target.value));
            }}
            className="rounded-md border bg-background px-2 py-1 text-xs"
            data-testid="refresh-interval-select"
          >
            {REFRESH_OPTIONS.map(opt => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Compact/Expanded toggle */}
        <Button
          variant="outline"
          size="sm"
          onClick={() => setCompact(!compact)}
          className="gap-1.5 ml-auto"
          data-testid="compact-toggle"
        >
          {compact ? <Maximize2 className="h-3.5 w-3.5" /> : <Minimize2 className="h-3.5 w-3.5" />}
          {compact ? 'Expanded' : 'Compact'}
        </Button>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <>
          {/* Overall Health Banner */}
          {data && <OverallHealthBanner overall={data.overall} />}

          {/* Category Groups */}
          {grouped.length === 0 ? (
            <div className="text-center py-10 text-sm text-muted-foreground">
              No service health data available.
            </div>
          ) : (
            <div className="space-y-4">
              {grouped.map(({ category, services }) => (
                <CategoryGroup
                  key={category}
                  category={category}
                  services={services}
                  compact={compact}
                  defaultOpen={true}
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
