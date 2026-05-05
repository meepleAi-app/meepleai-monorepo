/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useOverrideCertification } from '../useOverrideCertification';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      overrideCertification: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockOverride = api.admin.overrideCertification as ReturnType<typeof vi.fn>;

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
const REASON = 'Manual override due to ambiguous source documentation.';

describe('useOverrideCertification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.overrideCertification with analysisId and reason', async () => {
    mockOverride.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useOverrideCertification(), { wrapper });

    result.current.mutate({ analysisId: ANALYSIS_ID, reason: REASON });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockOverride).toHaveBeenCalledWith(ANALYSIS_ID, REASON);
  });

  it('invalidates dashboard.all and mechanicAnalysis.detail on success', async () => {
    mockOverride.mockResolvedValue(undefined);

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useOverrideCertification(), { wrapper });

    result.current.mutate({ analysisId: ANALYSIS_ID, reason: REASON });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['validation-dashboard'] });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: ['mechanic-analysis', ANALYSIS_ID],
    });
  });

  it('fires toast.success on success', async () => {
    mockOverride.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useOverrideCertification(), { wrapper });

    result.current.mutate({ analysisId: ANALYSIS_ID, reason: REASON });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Certification overridden successfully');
  });

  it('fires toast.error with message on failure', async () => {
    mockOverride.mockRejectedValue(new Error('forbidden'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useOverrideCertification(), { wrapper });

    result.current.mutate({ analysisId: ANALYSIS_ID, reason: REASON });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to override certification: forbidden');
  });
});
