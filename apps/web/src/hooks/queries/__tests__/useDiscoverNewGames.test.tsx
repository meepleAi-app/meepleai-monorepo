/**
 * @vitest-environment jsdom
 *
 * useDiscoverNewGames unit tests — Wave 3 Phase 1 (Issue #805 / PR #732 §4.3.2).
 *
 * Coverage:
 *   - Default limit (10) when no option passed
 *   - Limit clamping (1..50, NaN/negative → default 10)
 *   - Empty-state contract (200 with { items: [] } → [] from hook)
 *   - Schema validation forwards typed items
 *   - `enabled: false` defers the request
 *   - Error path surfaces the rejection
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

import {
  DISCOVER_NEW_GAMES_DEFAULT_LIMIT,
  DISCOVER_NEW_GAMES_STALE_TIME_MS,
  useDiscoverNewGames,
} from '../useDiscoverNewGames';

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockApi = vi.hoisted<MockedApiClient>(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  head: vi.fn(),
  options: vi.fn(),
}));
vi.mock('@/lib/api/client', () => ({ apiClient: mockApi }));

const mockGet = mockApi.get;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SAMPLE_NEW_GAMES = {
  items: [
    {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Wingspan',
      publisher: 'Stonemaier',
      year: 2019,
      imageUrl: 'https://example.com/wingspan.jpg',
      createdAt: '2026-04-01T12:00:00Z',
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Catan',
      publisher: null,
      year: null,
      imageUrl: null,
      createdAt: '2026-03-30T08:30:00Z',
    },
  ],
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useDiscoverNewGames — constants', () => {
  it('exports a default limit of 10', () => {
    expect(DISCOVER_NEW_GAMES_DEFAULT_LIMIT).toBe(10);
  });

  it('exports a 1h staleTime to mirror the backend HybridCache TTL', () => {
    expect(DISCOVER_NEW_GAMES_STALE_TIME_MS).toBe(60 * 60 * 1000);
  });
});

describe('useDiscoverNewGames — limit handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_NEW_GAMES);
  });

  it('uses default limit=10 when no option is passed', async () => {
    const { result } = renderHook(() => useDiscoverNewGames(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/api/v1/catalog/games/new?limit=10', expect.anything());
  });

  it('clamps a limit > 50 to the validator ceiling', async () => {
    const { result } = renderHook(() => useDiscoverNewGames({ limit: 200 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/api/v1/catalog/games/new?limit=50', expect.anything());
  });

  it('clamps a non-positive limit to the default', async () => {
    const { result } = renderHook(() => useDiscoverNewGames({ limit: -5 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/api/v1/catalog/games/new?limit=10', expect.anything());
  });
});

describe('useDiscoverNewGames — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the items array on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_NEW_GAMES);

    const { result } = renderHook(() => useDiscoverNewGames(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_NEW_GAMES.items);
  });

  it('returns an empty array when the backend returns the empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useDiscoverNewGames(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('returns an empty array when the backend returns null (401/circuit-open)', async () => {
    mockGet.mockResolvedValue(null);

    const { result } = renderHook(() => useDiscoverNewGames(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useDiscoverNewGames({ enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('exposes error state when the API rejects', async () => {
    const apiError = new Error('Catalog unavailable');
    mockGet.mockRejectedValue(apiError);

    const { result } = renderHook(() => useDiscoverNewGames(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Catalog unavailable');
  });
});
