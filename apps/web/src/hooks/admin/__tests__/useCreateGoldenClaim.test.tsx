/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { CreateGoldenClaimRequest } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { useCreateGoldenClaim } from '../useCreateGoldenClaim';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      createClaim: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockCreateClaim = api.admin.createClaim as ReturnType<typeof vi.fn>;

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

const request: CreateGoldenClaimRequest = {
  sharedGameId: SHARED_GAME_ID,
  section: 'Mechanics',
  statement: 'Players take two actions per turn.',
  expectedPage: 3,
  sourceQuote: 'Each turn, a player takes two actions.',
};

describe('useCreateGoldenClaim', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.createClaim with the request body', async () => {
    mockCreateClaim.mockResolvedValue({ id: '22222222-2222-2222-2222-222222222222' });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCreateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockCreateClaim).toHaveBeenCalledWith(request);
  });

  it('invalidates golden.byGame and dashboard.all on success', async () => {
    mockCreateClaim.mockResolvedValue({ id: '22222222-2222-2222-2222-222222222222' });

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useCreateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['golden', SHARED_GAME_ID] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['validation-dashboard'] });
  });

  it('fires toast.success on success', async () => {
    mockCreateClaim.mockResolvedValue({ id: '22222222-2222-2222-2222-222222222222' });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCreateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, request });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Golden claim created successfully');
  });

  it('fires toast.error with message on failure', async () => {
    mockCreateClaim.mockRejectedValue(new Error('boom'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCreateGoldenClaim(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, request });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to create golden claim: boom');
  });
});
