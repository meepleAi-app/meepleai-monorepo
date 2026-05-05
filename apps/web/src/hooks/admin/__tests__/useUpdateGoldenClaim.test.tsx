/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { UpdateGoldenClaimRequest } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { useUpdateGoldenClaim } from '../useUpdateGoldenClaim';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      updateClaim: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockUpdateClaim = api.admin.updateClaim as ReturnType<typeof vi.fn>;

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

const request: UpdateGoldenClaimRequest = {
  statement: 'Players take three actions per turn.',
  expectedPage: 4,
  sourceQuote: 'On each turn, players take three actions.',
};

describe('useUpdateGoldenClaim', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.updateClaim with claimId and request', async () => {
    mockUpdateClaim.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useUpdateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockUpdateClaim).toHaveBeenCalledWith(CLAIM_ID, request);
  });

  it('invalidates golden.byGame on success', async () => {
    mockUpdateClaim.mockResolvedValue(undefined);

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useUpdateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['golden', SHARED_GAME_ID] });
  });

  it('fires toast.success on success', async () => {
    mockUpdateClaim.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useUpdateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Golden claim updated successfully');
  });

  it('fires toast.error with message on failure', async () => {
    mockUpdateClaim.mockRejectedValue(new Error('conflict'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useUpdateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, claimId: CLAIM_ID, request });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to update golden claim: conflict');
  });
});
