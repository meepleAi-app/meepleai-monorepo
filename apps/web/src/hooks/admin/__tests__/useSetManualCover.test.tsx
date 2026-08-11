/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: { admin: { setManualCover: vi.fn() } },
}));

import { api } from '@/lib/api';

import { coverEditorKeys } from '../coverEditorKeys';
import { useSetManualCover } from '../useSetManualCover';

const mockSet = api.admin.setManualCover as ReturnType<typeof vi.fn>;
const GAME_ID = '550e8400-e29b-41d4-a716-446655440000';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');
  return {
    queryClient,
    invalidateSpy,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

describe('useSetManualCover', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls api.admin.setManualCover with gameId and body', async () => {
    mockSet.mockResolvedValue({
      dbKey: 'covers/manual/x/cover',
      presignedUrl: 'https://r2/m.webp',
    });
    const body = {
      sourceUrl: 'https://commons.example.org/c.png',
      license: 'CC0',
      attribution: null,
    };

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useSetManualCover(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, body });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockSet).toHaveBeenCalledWith(GAME_ID, body);
  });

  it('invalidates the candidates query and the shared-game detail on settle', async () => {
    // A fresh Manual candidate only appears after the candidates query is busted, so the
    // hook MUST invalidate it (mirrors useAssignCover) — else the picker looks like a no-op.
    mockSet.mockResolvedValue({ dbKey: 'k', presignedUrl: '' });

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useSetManualCover(), { wrapper });

    result.current.mutate({
      gameId: GAME_ID,
      body: { sourceUrl: 'https://commons.example.org/c.png', license: 'CC0', attribution: null },
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: coverEditorKeys.candidates(GAME_ID) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'shared-games', GAME_ID] });
  });
});
