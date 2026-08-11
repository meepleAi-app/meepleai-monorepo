/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: { admin: { removeCoverAssignment: vi.fn() } },
}));

import { api } from '@/lib/api';
import type { CoverCandidates } from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from '../coverEditorKeys';
import { useRemoveCoverAssignment } from '../useRemoveCoverAssignment';

const mockRemove = api.admin.removeCoverAssignment as ReturnType<typeof vi.fn>;
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

const seededCandidates: CoverCandidates = {
  gameId: GAME_ID,
  candidates: [{ source: 'Wikidata', previewUrl: 'https://r2.example/w.webp' }],
  assignments: { card: { source: 'Wikidata', focalX: 0.5, focalY: 0.5 }, hero: null, social: null },
};

describe('useRemoveCoverAssignment', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls api.admin.removeCoverAssignment with gameId and context', async () => {
    mockRemove.mockResolvedValue(undefined);

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useRemoveCoverAssignment(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, context: 'Card' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockRemove).toHaveBeenCalledWith(GAME_ID, 'Card');
  });

  it('invalidates the candidates query and the shared-game detail on settle', async () => {
    mockRemove.mockResolvedValue(undefined);

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useRemoveCoverAssignment(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, context: 'Card' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: coverEditorKeys.candidates(GAME_ID) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'shared-games', GAME_ID] });
  });

  it('optimistically clears the cached assignment for the context', async () => {
    const { wrapper, queryClient } = createWrapper();
    queryClient.setQueryData(coverEditorKeys.candidates(GAME_ID), seededCandidates);

    let resolveRemove: (v: unknown) => void = () => {};
    mockRemove.mockReturnValue(
      new Promise(resolve => {
        resolveRemove = resolve;
      })
    );

    const { result } = renderHook(() => useRemoveCoverAssignment(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, context: 'Card' });

    await waitFor(() => {
      const cached = queryClient.getQueryData<CoverCandidates>(coverEditorKeys.candidates(GAME_ID));
      expect(cached?.assignments.card).toBeNull();
    });

    resolveRemove(undefined);
  });

  it('rolls back the optimistic clear when the remove fails', async () => {
    const { wrapper, queryClient } = createWrapper();
    queryClient.setQueryData(coverEditorKeys.candidates(GAME_ID), seededCandidates);
    mockRemove.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useRemoveCoverAssignment(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, context: 'Card' });

    await waitFor(() => expect(result.current.isError).toBe(true));

    const cached = queryClient.getQueryData<CoverCandidates>(coverEditorKeys.candidates(GAME_ID));
    expect(cached?.assignments.card).toEqual({ source: 'Wikidata', focalX: 0.5, focalY: 0.5 });
  });
});
