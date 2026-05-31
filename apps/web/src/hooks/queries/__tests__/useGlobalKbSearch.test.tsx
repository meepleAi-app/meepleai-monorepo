/**
 * @vitest-environment jsdom
 *
 * useGlobalKbSearch unit tests — Issue #1482 Phase 1 Foundation.
 *
 * Strategy: mock useDebounce to return the input synchronously, enabling
 * real-timer waitFor calls (no fake-timer/waitFor deadlock). Debounce
 * coalescing is tested by watching the debounced value propagation.
 *
 * Coverage:
 *   - enabled=false when query empty or < 2 chars
 *   - fetches on debounced query (query propagated from useDebounce)
 *   - debounce coalescing: hook NOT called when debouncedQuery still short
 *   - fetchNextPage appends results + passes cursor
 *   - query change resets cursor (via queryKey change)
 *   - mode change resets cursor (via queryKey change)
 *   - hasMore reflects last page only
 *   - error surfaces, no throw
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGlobalKbSearch } from '../useGlobalKbSearch';
import type {
  GlobalKbSearchResponse,
  GlobalKbSearchResult,
} from '@/lib/api/schemas/kb-globale.schemas';

// Mock the api client
vi.mock('@/lib/api', () => ({
  api: {
    kbDocs: {
      searchGlobal: vi.fn(),
    },
  },
}));

// Mock useDebounce to return the value synchronously (passthrough) by default.
// Individual tests can override via mockReturnValueOnce for short-circuit scenarios.
vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: vi.fn((value: string) => value),
}));

import { api } from '@/lib/api';
import { useDebounce } from '@/hooks/useDebounce';

const mockSearchGlobal = vi.mocked(api.kbDocs.searchGlobal);
const mockUseDebounce = vi.mocked(useDebounce);

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function makeResult(idx: number): GlobalKbSearchResult {
  return {
    chunkId: `chunk-${idx}`,
    docId: `00000000-0000-0000-0000-${String(idx).padStart(12, '0')}`,
    docTitle: `Doc ${idx}`,
    gameId: `11111111-1111-1111-1111-${String(idx).padStart(12, '0')}`,
    gameName: `Game ${idx}`,
    docType: 'Rulebook',
    headingPath: null,
    snippet: `Snippet ${idx}`,
    pageNumber: idx + 1,
    score: 0.9 - idx * 0.1,
  };
}

function makeResponse(
  results: GlobalKbSearchResult[],
  hasMore: boolean,
  nextCursor: string | null
): GlobalKbSearchResponse {
  return { results, hasMore, nextCursor };
}

describe('useGlobalKbSearch', () => {
  beforeEach(() => {
    mockSearchGlobal.mockReset();
    // Default: useDebounce returns value synchronously (passthrough)
    mockUseDebounce.mockImplementation((value: string) => value);
  });

  it('1: enabled=false when query is empty — isLoading=false, results=[], mock NOT called', () => {
    const { result } = renderHook(() => useGlobalKbSearch({ query: '' }), { wrapper });
    expect(mockSearchGlobal).not.toHaveBeenCalled();
    expect(result.current.isLoading).toBe(false);
    expect(result.current.results).toHaveLength(0);
  });

  it('2: enabled=false when query length < 2 — mock NOT called', () => {
    // useDebounce passthrough returns 'a' (length 1) → effectiveEnabled=false
    const { result } = renderHook(() => useGlobalKbSearch({ query: 'a' }), { wrapper });
    expect(mockSearchGlobal).not.toHaveBeenCalled();
    expect(result.current.isLoading).toBe(false);
  });

  it('3: fetches on debounced query ≥ 2 chars — mock called 1 time with correct args', async () => {
    mockSearchGlobal.mockResolvedValue(makeResponse([makeResult(0)], false, null));

    const { result } = renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });

    await waitFor(() => expect(result.current.results.length).toBeGreaterThan(0));

    expect(mockSearchGlobal).toHaveBeenCalledTimes(1);
    expect(mockSearchGlobal).toHaveBeenCalledWith({
      query: 'azul',
      mode: undefined,
      cursor: null,
    });
    expect(result.current.results).toHaveLength(1);
  });

  it('4: debounce coalescing — hook disabled while debouncedQuery < 2 chars', () => {
    // Simulate useDebounce holding a short value (debounce has not fired yet)
    mockUseDebounce.mockReturnValue('a'); // still short, not fired

    renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });

    // useDebounce returns 'a' → effectiveEnabled=false → no fetch
    expect(mockSearchGlobal).not.toHaveBeenCalled();
  });

  it('5: fetchNextPage appends results + passes cursor to second call', async () => {
    mockSearchGlobal
      .mockResolvedValueOnce(makeResponse([makeResult(0)], true, 'cursor-1'))
      .mockResolvedValueOnce(makeResponse([makeResult(1)], false, null));

    const { result } = renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });

    await waitFor(() => expect(result.current.results).toHaveLength(1));
    expect(result.current.hasMore).toBe(true);

    act(() => {
      result.current.fetchNextPage();
    });

    await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalledTimes(2));
    expect(mockSearchGlobal).toHaveBeenNthCalledWith(2, {
      query: 'azul',
      mode: undefined,
      cursor: 'cursor-1',
    });
    await waitFor(() => expect(result.current.results).toHaveLength(2));
  });

  it('6: query change resets cursor — second fetch uses cursor=null', async () => {
    mockSearchGlobal
      .mockResolvedValueOnce(makeResponse([makeResult(0)], true, 'cursor-1'))
      .mockResolvedValueOnce(makeResponse([makeResult(1)], false, null));

    const { result, rerender } = renderHook(({ q }) => useGlobalKbSearch({ query: q }), {
      wrapper,
      initialProps: { q: 'azul' },
    });

    await waitFor(() => expect(result.current.results).toHaveLength(1));

    // Change query — queryKey changes, cursor auto-resets via useInfiniteQuery
    rerender({ q: 'catan' });

    await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalledTimes(2));
    expect(mockSearchGlobal).toHaveBeenNthCalledWith(2, {
      query: 'catan',
      mode: undefined,
      cursor: null,
    });
  });

  it('7: mode change resets cursor — second fetch uses cursor=null', async () => {
    mockSearchGlobal
      .mockResolvedValueOnce(makeResponse([makeResult(0)], true, 'cursor-1'))
      .mockResolvedValueOnce(makeResponse([makeResult(1)], false, null));

    const { result, rerender } = renderHook(
      ({ mode }) => useGlobalKbSearch({ query: 'azul', mode }),
      { wrapper, initialProps: { mode: undefined as 'Semantic' | undefined } }
    );

    await waitFor(() => expect(result.current.results).toHaveLength(1));

    // Change mode — queryKey changes, cursor auto-resets
    rerender({ mode: 'Semantic' });

    await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalledTimes(2));
    expect(mockSearchGlobal).toHaveBeenNthCalledWith(2, {
      query: 'azul',
      mode: 'Semantic',
      cursor: null,
    });
  });

  it('8: hasMore reflects last page only — false after 2 pages', async () => {
    mockSearchGlobal
      .mockResolvedValueOnce(makeResponse([makeResult(0)], true, 'cursor-1'))
      .mockResolvedValueOnce(makeResponse([makeResult(1)], false, null));

    const { result } = renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });

    await waitFor(() => expect(result.current.hasMore).toBe(true));

    act(() => {
      result.current.fetchNextPage();
    });

    await waitFor(() => expect(result.current.results).toHaveLength(2));
    expect(result.current.hasMore).toBe(false);
  });

  it('9: error surfaces, no exception bubbling', async () => {
    mockSearchGlobal.mockRejectedValue(new Error('Network failure'));

    const { result } = renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });

    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.error?.message).toBe('Network failure');
    expect(result.current.results).toHaveLength(0);
  });

  // Phase 3 #1737: filter propagation + queryKey identity tests
  describe('useGlobalKbSearch — filters (Phase 3 #1737)', () => {
    it('passes docType filter to client call', async () => {
      mockSearchGlobal.mockResolvedValue({
        results: [],
        hasMore: false,
        nextCursor: null,
      });
      renderHook(() => useGlobalKbSearch({ query: 'azul', filters: { docType: ['Rulebook'] } }), {
        wrapper,
      });
      await waitFor(() =>
        expect(mockSearchGlobal).toHaveBeenCalledWith(
          expect.objectContaining({ docType: ['Rulebook'] })
        )
      );
    });

    it('passes all filter fields to client call', async () => {
      mockSearchGlobal.mockResolvedValue({
        results: [],
        hasMore: false,
        nextCursor: null,
      });
      renderHook(
        () =>
          useGlobalKbSearch({
            query: 'azul',
            filters: {
              docType: ['Rulebook'],
              gameId: ['00000000-0000-0000-0000-000000000001'],
              language: 'it',
            },
          }),
        { wrapper }
      );
      await waitFor(() =>
        expect(mockSearchGlobal).toHaveBeenCalledWith(
          expect.objectContaining({
            docType: ['Rulebook'],
            gameId: ['00000000-0000-0000-0000-000000000001'],
            language: 'it',
          })
        )
      );
    });

    it('queryKey includes filters (different filters = different cache entry)', async () => {
      mockSearchGlobal.mockResolvedValue({
        results: [],
        hasMore: false,
        nextCursor: null,
      });
      const { rerender } = renderHook(
        ({ filters }: { filters: { docType?: readonly string[] } }) =>
          useGlobalKbSearch({ query: 'azul', filters }),
        { wrapper, initialProps: { filters: { docType: ['Rulebook'] } } }
      );
      await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalledTimes(1));
      rerender({ filters: { docType: ['Errata'] } });
      await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalledTimes(2));
    });

    it('omitted filters are not sent (backwards-compat with Phase 1+2 callers)', async () => {
      mockSearchGlobal.mockResolvedValue({
        results: [],
        hasMore: false,
        nextCursor: null,
      });
      renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });
      await waitFor(() => expect(mockSearchGlobal).toHaveBeenCalled());
      const call = mockSearchGlobal.mock.calls[0]?.[0];
      expect(call).not.toHaveProperty('docType');
      expect(call).not.toHaveProperty('gameId');
      expect(call).not.toHaveProperty('language');
    });
  });
});
