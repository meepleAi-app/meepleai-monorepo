/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCalculateMetrics } from '../useCalculateMetrics';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      calculateMetrics: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockCalculateMetrics = api.admin.calculateMetrics as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const spy = vi.spyOn(queryClient, 'invalidateQueries');
  return {
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
    invalidateSpy: spy,
  };
}

const ANALYSIS_ID = '44444444-4444-4444-4444-444444444444';

describe('useCalculateMetrics', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.calculateMetrics with analysisId', async () => {
    mockCalculateMetrics.mockResolvedValue({
      metricsId: '55555555-5555-5555-5555-555555555555',
    });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCalculateMetrics(), { wrapper });

    result.current.mutate(ANALYSIS_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockCalculateMetrics).toHaveBeenCalledWith(ANALYSIS_ID);
  });

  it('invalidates dashboard.all and mechanicAnalysis.detail on success', async () => {
    mockCalculateMetrics.mockResolvedValue({
      metricsId: '55555555-5555-5555-5555-555555555555',
    });

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useCalculateMetrics(), { wrapper });

    result.current.mutate(ANALYSIS_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['validation-dashboard'] });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ['mechanic-analysis', ANALYSIS_ID],
    });
  });

  it('fires toast.success on success', async () => {
    mockCalculateMetrics.mockResolvedValue({
      metricsId: '55555555-5555-5555-5555-555555555555',
    });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCalculateMetrics(), { wrapper });

    result.current.mutate(ANALYSIS_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Metrics calculated successfully');
  });

  it('fires toast.error with message on failure', async () => {
    mockCalculateMetrics.mockRejectedValue(new Error('timeout'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCalculateMetrics(), { wrapper });

    result.current.mutate(ANALYSIS_ID);

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to calculate metrics: timeout');
  });
});
