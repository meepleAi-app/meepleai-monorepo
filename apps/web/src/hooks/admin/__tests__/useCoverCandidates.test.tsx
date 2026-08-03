/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: { admin: { getCoverCandidates: vi.fn() } },
}));

import { api } from '@/lib/api';

import { useCoverCandidates } from '../useCoverCandidates';

const mockGet = api.admin.getCoverCandidates as ReturnType<typeof vi.fn>;
const GAME_ID = '550e8400-e29b-41d4-a716-446655440000';

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

const candidates = {
  gameId: GAME_ID,
  candidates: [],
  assignments: { card: null, hero: null, social: null },
};

describe('useCoverCandidates', () => {
  beforeEach(() => vi.clearAllMocks());

  it('fetches the cover candidates for the game', async () => {
    mockGet.mockResolvedValue(candidates);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCoverCandidates(GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(GAME_ID);
    expect(result.current.data).toEqual(candidates);
  });

  it('does not fetch when gameId is empty (disabled query)', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useCoverCandidates(''), { wrapper });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockGet).not.toHaveBeenCalled();
  });
});
