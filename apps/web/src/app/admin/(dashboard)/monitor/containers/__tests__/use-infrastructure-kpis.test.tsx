import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

const mockGetDockerContainers = vi.hoisted(() => vi.fn());
const mockGetMetricsTimeSeries = vi.hoisted(() => vi.fn());
const mockGetAllBatchJobs = vi.hoisted(() => vi.fn());
const mockGetSystemResources = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDockerContainers: mockGetDockerContainers,
      getMetricsTimeSeries: mockGetMetricsTimeSeries,
      getAllBatchJobs: mockGetAllBatchJobs,
      getSystemResources: mockGetSystemResources,
    },
  },
}));

import { useInfrastructureKpis } from '../hooks/use-infrastructure-kpis';

describe('useInfrastructureKpis', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // default: 32 GiB host memory → memory.total = 32 GB (34359738368 / 1024**3)
    mockGetSystemResources.mockResolvedValue({
      processWorkingSetBytes: 1,
      gcHeapBytes: 1,
      processorCount: 8,
      processCpuPercent: 0,
      processUptimeSeconds: 1,
      hostMemoryTotalBytes: 34359738368,
      measuredAt: '2026-01-01T00:00:00Z',
    });
  });

  it('combines 3 endpoints into 4 KPI shapes (happy path)', async () => {
    mockGetDockerContainers.mockResolvedValue([
      {
        id: '1',
        name: 'api',
        image: 'x',
        state: 'running',
        status: '',
        created: '',
        labels: {},
      },
      {
        id: '2',
        name: 'redis',
        image: 'y',
        state: 'running',
        status: '',
        created: '',
        labels: {},
      },
      {
        id: '3',
        name: 'pg',
        image: 'z',
        state: 'exited',
        status: '',
        created: '',
        labels: {},
      },
    ]);

    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [
        { timestamp: '2026-06-03T14:00:00Z', value: 20 },
        { timestamp: '2026-06-03T14:30:00Z', value: 28 },
        { timestamp: '2026-06-03T15:00:00Z', value: 34 },
      ],
      memory: [
        { timestamp: '2026-06-03T14:00:00Z', value: 12.0 },
        { timestamp: '2026-06-03T14:30:00Z', value: 15.0 },
        { timestamp: '2026-06-03T15:00:00Z', value: 18.4 },
      ],
      requests: [],
    });

    mockGetAllBatchJobs.mockImplementation((p: { status?: string }) =>
      Promise.resolve({
        jobs: [],
        total: p.status === 'queued' ? 4 : p.status === 'running' ? 2 : 0,
        page: 1,
        pageSize: 20,
      })
    );

    const { result } = renderHook(() => useInfrastructureKpis());

    await waitFor(() => expect(result.current.containers.loading).toBe(false));
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));
    await waitFor(() => expect(result.current.batchJobs.loading).toBe(false));
    await waitFor(() => expect(result.current.memory.total).toBe(32));

    expect(result.current.containers).toMatchObject({ active: 2, total: 3, stopped: 1 });
    expect(result.current.cpu).toMatchObject({ value: 34, trend: 'up' });
    expect(result.current.cpu.series).toHaveLength(3);
    expect(result.current.memory).toMatchObject({ value: 18.4, trend: 'up', total: 32 });
    expect(result.current.batchJobs).toMatchObject({ queued: 4, running: 2 });
    expect(result.current.cpu.sourceAvailable).toBe(true);
    expect(result.current.memory.sourceAvailable).toBe(true);
  });

  it('handles failed fetches with safe fallback values', async () => {
    mockGetDockerContainers.mockRejectedValue(new Error('boom'));
    mockGetMetricsTimeSeries.mockRejectedValue(new Error('boom'));
    mockGetAllBatchJobs.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useInfrastructureKpis());

    await waitFor(() => expect(result.current.containers.loading).toBe(false));
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));
    await waitFor(() => expect(result.current.batchJobs.loading).toBe(false));

    expect(result.current.containers).toMatchObject({ active: 0, total: 0, stopped: 0 });
    expect(result.current.cpu).toMatchObject({ value: 0, trend: 'flat' });
    expect(result.current.cpu.series).toEqual([]);
    expect(result.current.batchJobs).toMatchObject({ queued: 0, running: 0 });
    expect(result.current.cpu.sourceAvailable).toBe(false);
    expect(result.current.memory.sourceAvailable).toBe(false);
  });

  it('marks source unavailable when BE flag is false even with empty series (#3045)', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    mockGetAllBatchJobs.mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 20 });
    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [],
      memory: [],
      requests: [],
      cpuAvailable: false,
      memoryAvailable: false,
    });

    const { result } = renderHook(() => useInfrastructureKpis());
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));

    // 0 punti MA flag false = sorgente giù, non zero reale.
    expect(result.current.cpu.sourceAvailable).toBe(false);
    expect(result.current.memory.sourceAvailable).toBe(false);
  });

  it('marks CPU unavailable but memory available on a partial Prometheus failure (#3045)', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    mockGetAllBatchJobs.mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 20 });
    // CPU query failed (empty + cpuAvailable false) while memory succeeded.
    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [],
      memory: [{ timestamp: '2026-06-03T15:00:00Z', value: 12 }],
      requests: [],
      cpuAvailable: false,
      memoryAvailable: true,
    });

    const { result } = renderHook(() => useInfrastructureKpis());
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));

    expect(result.current.cpu.sourceAvailable).toBe(false);
    expect(result.current.memory.sourceAvailable).toBe(true);
    expect(result.current.memory.value).toBe(12);
  });

  it('computes trend "down" when last < first', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    mockGetAllBatchJobs.mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 20 });
    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [
        { timestamp: '2026-06-03T14:00:00Z', value: 50 },
        { timestamp: '2026-06-03T15:00:00Z', value: 25 },
      ],
      memory: [
        { timestamp: '2026-06-03T14:00:00Z', value: 20 },
        { timestamp: '2026-06-03T15:00:00Z', value: 18 },
      ],
      requests: [],
    });

    const { result } = renderHook(() => useInfrastructureKpis());
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));

    expect(result.current.cpu.trend).toBe('down');
    expect(result.current.memory.trend).toBe('down');
  });

  it('computes trend "flat" when difference is below threshold', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    mockGetAllBatchJobs.mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 20 });
    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [
        { timestamp: '2026-06-03T14:00:00Z', value: 50 },
        { timestamp: '2026-06-03T15:00:00Z', value: 50.1 },
      ],
      memory: [],
      requests: [],
    });

    const { result } = renderHook(() => useInfrastructureKpis());
    await waitFor(() => expect(result.current.cpu.loading).toBe(false));

    expect(result.current.cpu.trend).toBe('flat');
  });

  it('falls back to memory.total=0 when system resources fetch fails (#3041)', async () => {
    mockGetDockerContainers.mockResolvedValue([]);
    mockGetAllBatchJobs.mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 20 });
    mockGetMetricsTimeSeries.mockResolvedValue({
      cpu: [{ timestamp: '2026-06-03T15:00:00Z', value: 10 }],
      memory: [{ timestamp: '2026-06-03T15:00:00Z', value: 5 }],
      requests: [],
    });
    mockGetSystemResources.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useInfrastructureKpis());
    await waitFor(() => expect(result.current.memory.loading).toBe(false));

    expect(result.current.memory.total).toBe(0);
    expect(result.current.memory.value).toBe(5);
  });
});
