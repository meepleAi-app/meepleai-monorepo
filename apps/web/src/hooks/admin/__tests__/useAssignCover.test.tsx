/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: { admin: { assignCover: vi.fn() } },
}));

import { api } from '@/lib/api';
import type { CoverCandidates } from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from '../coverEditorKeys';
import { useAssignCover } from '../useAssignCover';

const mockAssign = api.admin.assignCover as ReturnType<typeof vi.fn>;
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
  candidates: [{ source: 'Pdf', previewUrl: 'https://r2.example/p.webp' }],
  assignments: { card: null, hero: null, social: null },
};

describe('useAssignCover', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls api.admin.assignCover with gameId, context, and body', async () => {
    mockAssign.mockResolvedValue({ context: 'Hero', source: 'Pdf', focalX: 0.5, focalY: 0.5 });
    const body = { source: 'Pdf' as const, focalX: 0.5, focalY: 0.5 };

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useAssignCover(), { wrapper });

    result.current.mutate({ gameId: GAME_ID, context: 'Hero', body });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockAssign).toHaveBeenCalledWith(GAME_ID, 'Hero', body);
  });

  it('invalidates the candidates query and the shared-game detail on settle', async () => {
    mockAssign.mockResolvedValue({ context: 'Hero', source: 'Pdf', focalX: 0.5, focalY: 0.5 });

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useAssignCover(), { wrapper });

    result.current.mutate({
      gameId: GAME_ID,
      context: 'Hero',
      body: { source: 'Pdf', focalX: 0.5, focalY: 0.5 },
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: coverEditorKeys.candidates(GAME_ID) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'shared-games', GAME_ID] });
  });

  it('optimistically updates the cached assignment for the pinned context', async () => {
    const { wrapper, queryClient } = createWrapper();
    queryClient.setQueryData(coverEditorKeys.candidates(GAME_ID), seededCandidates);

    // Keep the mutation pending so we can observe the optimistic cache state.
    let resolveAssign: (v: unknown) => void = () => {};
    mockAssign.mockReturnValue(
      new Promise(resolve => {
        resolveAssign = resolve;
      })
    );

    const { result } = renderHook(() => useAssignCover(), { wrapper });

    result.current.mutate({
      gameId: GAME_ID,
      context: 'Hero',
      body: { source: 'Wikidata', focalX: 0.5, focalY: 0.5 },
    });

    await waitFor(() => {
      const cached = queryClient.getQueryData<CoverCandidates>(coverEditorKeys.candidates(GAME_ID));
      expect(cached?.assignments.hero).toBe('Wikidata');
    });

    resolveAssign({ context: 'Hero', source: 'Wikidata', focalX: 0.5, focalY: 0.5 });
  });

  it('rolls back the optimistic update when the pin fails', async () => {
    const { wrapper, queryClient } = createWrapper();
    queryClient.setQueryData(coverEditorKeys.candidates(GAME_ID), seededCandidates);
    mockAssign.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useAssignCover(), { wrapper });

    result.current.mutate({
      gameId: GAME_ID,
      context: 'Hero',
      body: { source: 'Wikidata', focalX: 0.5, focalY: 0.5 },
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    const cached = queryClient.getQueryData<CoverCandidates>(coverEditorKeys.candidates(GAME_ID));
    expect(cached?.assignments.hero).toBeNull();
  });
});
