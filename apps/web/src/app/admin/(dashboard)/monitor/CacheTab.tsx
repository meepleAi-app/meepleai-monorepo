'use client';

import { useState, useEffect, useCallback } from 'react';

import { Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

interface CacheMetrics {
  avgLatencyMs: number;
  apiRequests24h: number;
  errorRate: number;
  llmCost24h: number;
}

function MetricCard({
  label,
  value,
  unit,
}: {
  label: string;
  value: string | number;
  unit?: string;
}) {
  return (
    <div
      className={cn(
        'rounded-xl border border-border/60 p-5',
        'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
      )}
    >
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-1 font-quicksand text-2xl font-bold tracking-tight text-foreground">
        {value}
        {unit && <span className="ml-1 text-sm font-normal text-muted-foreground">{unit}</span>}
      </p>
    </div>
  );
}

export function CacheTab() {
  const [metrics, setMetrics] = useState<CacheMetrics | null>(null);
  const [clearing, setClearing] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getInfrastructureDetails()
      .then(details => {
        const prometheus = details?.prometheusMetrics;
        setMetrics({
          avgLatencyMs: prometheus?.avgLatencyMs ?? 0,
          apiRequests24h: prometheus?.apiRequestsLast24h ?? 0,
          errorRate: prometheus?.errorRate ?? 0,
          llmCost24h: prometheus?.llmCostLast24h ?? 0,
        });
      })
      .catch(() => {
        setMetrics(null);
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  const handleClearCache = useCallback(async () => {
    setClearing(true);
    try {
      await api.admin.clearKBCache();
    } catch {
      // Silently handle — toast could be added later
    } finally {
      setClearing(false);
    }
  }, []);

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-24 rounded-xl bg-muted/50" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Cache & System Metrics
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            System performance metrics and cache operations.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void handleClearCache()}
          disabled={clearing}
          className="gap-2 border-red-200 text-red-600 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-900/20"
        >
          <Trash2 className="h-4 w-4" />
          {clearing ? 'Clearing...' : 'Clear Cache'}
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricCard
          label="Avg Latency"
          value={metrics ? metrics.avgLatencyMs.toFixed(1) : '--'}
          unit="ms"
        />
        <MetricCard
          label="API Requests (24h)"
          value={metrics ? metrics.apiRequests24h.toLocaleString() : '--'}
        />
        <MetricCard
          label="Error Rate"
          value={metrics ? `${(metrics.errorRate * 100).toFixed(1)}` : '--'}
          unit="%"
        />
        <MetricCard
          label="LLM Cost (24h)"
          value={metrics ? `$${metrics.llmCost24h.toFixed(2)}` : '--'}
        />
      </div>
    </div>
  );
}
