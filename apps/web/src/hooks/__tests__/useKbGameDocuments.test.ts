/**
 * useKbGameDocuments Hook - Unit Tests
 *
 * Tests the KB game documents React Query hook.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: { getGameDocuments: vi.fn() },
  },
}));

// Mock TanStack Query to avoid needing a QueryClientProvider wrapper
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useQuery: vi.fn(),
  };
});

const { useQuery } = await import('@tanstack/react-query');

import { api } from '@/lib/api';
import { useKbGameDocuments, kbGameDocumentKeys } from '../queries/useGameDocuments';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const MOCK_DOCUMENTS: GameDocument[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Core Rulebook',
    status: 'indexed',
    pageCount: 24,
    createdAt: '2026-04-01T10:00:00Z',
    category: 'Rulebook',
    versionLabel: 'v2.0',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Quick Start Guide',
    status: 'indexed',
    pageCount: 4,
    createdAt: '2026-04-02T12:00:00Z',
    category: 'QuickStart',
    versionLabel: null,
  },
];

describe('useKbGameDocuments', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Query Key Factory', () => {
    it('generates correct query keys', () => {
      expect(kbGameDocumentKeys.all).toEqual(['kb-game-documents']);
      expect(kbGameDocumentKeys.byGame('game-abc')).toEqual(['kb-game-documents', 'game-abc']);
    });
  });

  describe('Hook behavior', () => {
    it('should call useQuery with correct config for a valid gameId', () => {
      vi.mocked(useQuery).mockReturnValue({
        data: MOCK_DOCUMENTS,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useQuery>);

      const { result } = renderHook(() => useKbGameDocuments('game-abc'));

      expect(useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          queryKey: ['kb-game-documents', 'game-abc'],
          enabled: true,
          staleTime: 5 * 60_000,
          gcTime: 10 * 60_000,
        })
      );
      expect(result.current.data).toEqual(MOCK_DOCUMENTS);
    });

    it('should be disabled when gameId is undefined', () => {
      vi.mocked(useQuery).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useQuery>);

      renderHook(() => useKbGameDocuments(undefined));

      expect(useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
        })
      );
    });

    it('should be disabled when enabled param is false', () => {
      vi.mocked(useQuery).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useQuery>);

      renderHook(() => useKbGameDocuments('game-abc', false));

      expect(useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
        })
      );
    });

    it('should use empty string in queryKey when gameId is undefined', () => {
      vi.mocked(useQuery).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useQuery>);

      renderHook(() => useKbGameDocuments(undefined));

      expect(useQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          queryKey: ['kb-game-documents', ''],
        })
      );
    });

    it('queryFn should call api.knowledgeBase.getGameDocuments', () => {
      let capturedQueryFn: (() => Promise<GameDocument[]>) | undefined;
      vi.mocked(useQuery).mockImplementation((opts: Record<string, unknown>) => {
        capturedQueryFn = opts.queryFn as () => Promise<GameDocument[]>;
        return { data: undefined, isLoading: true, error: null } as ReturnType<typeof useQuery>;
      });

      renderHook(() => useKbGameDocuments('game-abc'));

      // Execute the captured queryFn
      expect(capturedQueryFn).toBeDefined();
      capturedQueryFn!();
      expect(api.knowledgeBase.getGameDocuments).toHaveBeenCalledWith('game-abc');
    });
  });
});
