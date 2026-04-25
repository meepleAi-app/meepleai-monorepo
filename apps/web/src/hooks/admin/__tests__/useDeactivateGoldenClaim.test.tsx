/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useDeactivateGoldenClaim } from '../useDeactivateGoldenClaim';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      deactivateClaim: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockDeactivateClaim = api.admin.deactivateClaim as ReturnType<typeof vi.fn>;

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

const SHARED_GAME_ID = '11111111-1111-1111-1111-111111111111';
const CLAIM_ID = '33333333-3333-3333-3333-333333333333';

describe('useDeactivateGoldenClaim', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.deactivateClaim with claimId', async () => {
    mockDeactivateClaim.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDeactivateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockDeactivateClaim).toHaveBeenCalledWith(CLAIM_ID);
  });

  it('invalidates golden.byGame and dashboard.all on success', async () => {
    mockDeactivateClaim.mockResolvedValue(undefined);

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useDeactivateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['golden', SHARED_GAME_ID] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['validation-dashboard'] });
  });

  it('fires toast.success on success', async () => {
    mockDeactivateClaim.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDeactivateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Golden claim deactivated successfully');
  });

  it('fires toast.error with message on failure', async () => {
    mockDeactivateClaim.mockRejectedValue(new Error('not found'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDeactivateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to deactivate golden claim: not found');
  });
});
