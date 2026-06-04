'use client';

/**
 * useInfrastructureKpis — combina 3 endpoint admin in 4 KPI shapes
 * per il componente KPISparklineStrip (#1837 SP5 F4-C1).
 *
 * Endpoint riusati (zero modifiche BE):
 *   - GET /api/v1/admin/docker/containers
 *   - GET /api/v1/admin/infrastructure/metrics/timeseries?range=1h
 *   - GET /api/v1/admin/operations/batch-jobs?status={queued|running}
 *
 * Memory.total è hardcoded a 32 GB perché BE non espone il limite del host;
 * follow-up: estendere /admin/infrastructure/details con `hostMemoryTotalGb`.
 */

import { useEffect, useState } from 'react';

import { api } from '@/lib/api';

export type Trend = 'up' | 'down' | 'flat';

export interface TimeSeriesPoint {
  timestamp: string;
  value: number;
}

interface ContainerKpi {
  active: number;
  total: number;
  stopped: number;
  loading: boolean;
}

interface MetricKpi {
  value: number;
  series: TimeSeriesPoint[];
  trend: Trend;
  trendPct: number;
  loading: boolean;
}

interface MemoryKpi extends MetricKpi {
  total: number;
}

interface BatchJobsKpi {
  queued: number;
  running: number;
  loading: boolean;
}

export interface InfrastructureKpis {
  containers: ContainerKpi;
  cpu: MetricKpi;
  memory: MemoryKpi;
  batchJobs: BatchJobsKpi;
}

const FLAT_THRESHOLD_PCT = 0.5;
const HOST_MEMORY_TOTAL_GB = 32; // see file-level comment

function computeTrend(series: TimeSeriesPoint[]): { trend: Trend; pct: number } {
  if (series.length < 2) return { trend: 'flat', pct: 0 };
  const first = series[0].value;
  const last = series[series.length - 1].value;
  if (first === 0) return { trend: last > 0 ? 'up' : 'flat', pct: 0 };
  const pct = ((last - first) / first) * 100;
  if (Math.abs(pct) < FLAT_THRESHOLD_PCT) return { trend: 'flat', pct: 0 };
  return { trend: pct > 0 ? 'up' : 'down', pct: Math.round(pct * 10) / 10 };
}

const EMPTY_METRIC: MetricKpi = {
  value: 0,
  series: [],
  trend: 'flat',
  trendPct: 0,
  loading: true,
};
const EMPTY_MEMORY: MemoryKpi = { ...EMPTY_METRIC, total: HOST_MEMORY_TOTAL_GB };

export function useInfrastructureKpis(): InfrastructureKpis {
  const [containers, setContainers] = useState<ContainerKpi>({
    active: 0,
    total: 0,
    stopped: 0,
    loading: true,
  });
  const [cpu, setCpu] = useState<MetricKpi>(EMPTY_METRIC);
  const [memory, setMemory] = useState<MemoryKpi>(EMPTY_MEMORY);
  const [batchJobs, setBatchJobs] = useState<BatchJobsKpi>({
    queued: 0,
    running: 0,
    loading: true,
  });

  useEffect(() => {
    let cancelled = false;

    void api.admin
      .getDockerContainers()
      .then(list => {
        if (cancelled) return;
        const active = list.filter(c => c.state === 'running').length;
        setContainers({
          active,
          total: list.length,
          stopped: list.length - active,
          loading: false,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setContainers({ active: 0, total: 0, stopped: 0, loading: false });
      });

    void api.admin
      .getMetricsTimeSeries('1h')
      .then(resp => {
        if (cancelled) return;
        const cpuSeries = resp?.cpu ?? [];
        const memSeries = resp?.memory ?? [];
        const cpuValue = cpuSeries.length ? cpuSeries[cpuSeries.length - 1].value : 0;
        const memValue = memSeries.length ? memSeries[memSeries.length - 1].value : 0;
        const cpuTrend = computeTrend(cpuSeries);
        const memTrend = computeTrend(memSeries);
        setCpu({
          value: cpuValue,
          series: cpuSeries,
          trend: cpuTrend.trend,
          trendPct: cpuTrend.pct,
          loading: false,
        });
        setMemory({
          value: memValue,
          series: memSeries,
          trend: memTrend.trend,
          trendPct: memTrend.pct,
          total: HOST_MEMORY_TOTAL_GB,
          loading: false,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setCpu({ ...EMPTY_METRIC, loading: false });
        setMemory({ ...EMPTY_MEMORY, loading: false });
      });

    Promise.all([
      api.admin.getAllBatchJobs({ status: 'queued', pageSize: 1 }),
      api.admin.getAllBatchJobs({ status: 'running', pageSize: 1 }),
    ])
      .then(([queued, running]) => {
        if (cancelled) return;
        setBatchJobs({
          queued: queued?.total ?? 0,
          running: running?.total ?? 0,
          loading: false,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setBatchJobs({ queued: 0, running: 0, loading: false });
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return { containers, cpu, memory, batchJobs };
}
