/**
 * @vitest-environment jsdom
 *
 * Tests for the recalc-all enqueue hook (ADR-051 Sprint 2 / Task 21).
 *
 * The hook is the entry point for the async mass-recalculation flow:
 * the dashboard's "Recalculate all" button fires it, captures the returned
 * `jobId`, and then mounts the `RecalcProgressDrawer` to poll status.
 *
 * Behaviour under test:
 *  - Calls `api.admin.enqueueRecalcAll()` with no args (the endpoint takes
 *    none — actor id is sourced from the session on the server).
 *  - Surfaces the new job id in `data.jobId` so callers can stash it.
 *  - On error fires a toast — the dashboard never sees the raw exception.
 *  - Does NOT fire a "started" toast on success: the drawer owns terminal
 *    Completed/Failed/Cancelled toasts so we don't double-notify.
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useEnqueueRecalcAll } from '../useEnqueueRecalcAll';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      enqueueRecalcAll: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockEnqueue = api.admin.enqueueRecalcAll as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

const JOB_ID = '33333333-3333-3333-3333-333333333333';

describe('useEnqueueRecalcAll', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.enqueueRecalcAll() with no arguments', async () => {
    mockEnqueue.mockResolvedValue({ jobId: JOB_ID });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useEnqueueRecalcAll(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockEnqueue).toHaveBeenCalledTimes(1);
    expect(mockEnqueue).toHaveBeenCalledWith();
  });

  it('exposes the jobId on `data` so the caller can stash it', async () => {
    mockEnqueue.mockResolvedValue({ jobId: JOB_ID });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useEnqueueRecalcAll(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual({ jobId: JOB_ID });
  });

  it('does NOT fire a success toast on enqueue (drawer owns terminal toasts)', async () => {
    mockEnqueue.mockResolvedValue({ jobId: JOB_ID });

    const wrapper = createWrapper();
    const { result } = renderHook(() => useEnqueueRecalcAll(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).not.toHaveBeenCalled();
  });

  it('fires toast.error with the message on failure', async () => {
    mockEnqueue.mockRejectedValue(new Error('boom'));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useEnqueueRecalcAll(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to enqueue recalc: boom');
  });
});
