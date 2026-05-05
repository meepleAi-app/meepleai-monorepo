/**
 * @vitest-environment jsdom
 *
 * Tests for the recalc-job cancel hook (ADR-051 Sprint 2 / Task 21).
 *
 * The hook fires from the `RecalcProgressDrawer`'s "Cancel" button. The
 * server-side semantics are cooperative: the endpoint just sets the
 * `cancellationRequested` flag on the aggregate; the worker observes it
 * on its next iteration and transitions the job to a terminal status.
 *
 * Behaviour under test:
 *  - Calls `api.admin.cancelRecalcJob(jobId)` with the supplied id.
 *  - Invalidates the recalc-job status query so the next poll picks up
 *    `cancellationRequested=true` immediately (snappy UI feedback).
 *  - Fires a neutral info-style toast on success (no green "all done"
 *    affordance — the actual cancellation is async, so calling it a
 *    success is misleading; the drawer reports the terminal status).
 *  - Fires an error toast on failure (e.g., 409 if job already terminal).
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCancelRecalcJob } from '../useCancelRecalcJob';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      cancelRecalcJob: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), info: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockCancel = api.admin.cancelRecalcJob as ReturnType<typeof vi.fn>;

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

const JOB_ID = '66666666-6666-6666-6666-666666666666';

describe('useCancelRecalcJob', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.cancelRecalcJob with the supplied jobId', async () => {
    mockCancel.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCancelRecalcJob(), { wrapper });

    result.current.mutate(JOB_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockCancel).toHaveBeenCalledWith(JOB_ID);
  });

  it('invalidates the recalc-job status query on success', async () => {
    mockCancel.mockResolvedValue(undefined);

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useCancelRecalcJob(), { wrapper });

    result.current.mutate(JOB_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['mechanic-recalc-job', JOB_ID] });
  });

  it('fires toast.info on success (cancellation is cooperative, not done yet)', async () => {
    mockCancel.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCancelRecalcJob(), { wrapper });

    result.current.mutate(JOB_ID);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.info).toHaveBeenCalledWith(expect.stringMatching(/cancellation requested/i));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('fires toast.error with the message on failure', async () => {
    mockCancel.mockRejectedValue(new Error('already terminal'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCancelRecalcJob(), { wrapper });

    result.current.mutate(JOB_ID);

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to cancel recalc: already terminal');
  });
});
