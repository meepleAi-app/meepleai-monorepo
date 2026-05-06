/**
 * @vitest-environment jsdom
 *
 * useBggSearch unit tests — Wave 3 Phase 0 (Issue #805).
 *
 * Coverage:
 *   - Min query length gate (≥3 chars; trims whitespace)
 *   - Auto-disabled when query too short → no fetch fired
 *   - Manual `enabled: false` overrides auto-enable
 *   - 24h staleTime default (validated via QueryClient cache)
 *   - Calls `api.bgg.search(trimmed, false, 1, 20)` with the right contract
 *   - Surfaces success / error states from the underlying client
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  BGG_SEARCH_MIN_QUERY_LENGTH,
  BGG_SEARCH_STALE_TIME_MS,
  useBggSearch,
} from '../useBggSearch';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockBggSearch = api.bgg.search as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SAMPLE_RESPONSE = {
  results: [
    {
      bggId: 266192,
      name: 'Wingspan',
      yearPublished: 2019,
      thumbnailUrl: null,
      type: 'boardgame',
    },
  ],
  total: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useBggSearch — constants', () => {
  it('exports a min query length of 3 characters', () => {
    expect(BGG_SEARCH_MIN_QUERY_LENGTH).toBe(3);
  });

  it('exports a 24h staleTime in milliseconds', () => {
    expect(BGG_SEARCH_STALE_TIME_MS).toBe(24 * 60 * 60 * 1000);
  });
});

describe('useBggSearch — auto-enable gate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does NOT fire when query length is below the min', () => {
    renderHook(() => useBggSearch({ query: 'wi' }), { wrapper: createWrapper() });

    expect(mockBggSearch).not.toHaveBeenCalled();
  });

  it('does NOT fire when query is empty', () => {
    renderHook(() => useBggSearch({ query: '' }), { wrapper: createWrapper() });

    expect(mockBggSearch).not.toHaveBeenCalled();
  });

  it('treats whitespace-only queries as empty (trim) and does not fire', () => {
    renderHook(() => useBggSearch({ query: '   ' }), { wrapper: createWrapper() });

    expect(mockBggSearch).not.toHaveBeenCalled();
  });

  it('fires when trimmed query meets the min length', async () => {
    mockBggSearch.mockResolvedValue(SAMPLE_RESPONSE);

    const { result } = renderHook(() => useBggSearch({ query: '  wingspan  ' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockBggSearch).toHaveBeenCalledTimes(1);
    expect(mockBggSearch).toHaveBeenCalledWith('wingspan', false, 1, 20);
  });
});

describe('useBggSearch — manual enabled override', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does NOT fire when enabled=false even with a valid query', () => {
    renderHook(() => useBggSearch({ query: 'wingspan', enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockBggSearch).not.toHaveBeenCalled();
  });

  it('still respects min-length when enabled=true is passed explicitly', () => {
    renderHook(() => useBggSearch({ query: 'a', enabled: true }), {
      wrapper: createWrapper(),
    });

    // explicit enabled=true MUST still pass the length gate (defensive)
    expect(mockBggSearch).not.toHaveBeenCalled();
  });
});

describe('useBggSearch — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns data on success', async () => {
    mockBggSearch.mockResolvedValue(SAMPLE_RESPONSE);

    const { result } = renderHook(() => useBggSearch({ query: 'wingspan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_RESPONSE);
  });

  it('exposes error state when the API rejects', async () => {
    const apiError = new Error('BGG service unavailable');
    mockBggSearch.mockRejectedValue(apiError);

    const { result } = renderHook(() => useBggSearch({ query: 'wingspan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('BGG service unavailable');
  });
});
