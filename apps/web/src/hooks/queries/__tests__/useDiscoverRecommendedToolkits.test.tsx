/**
 * @vitest-environment jsdom
 *
 * useDiscoverRecommendedToolkits unit tests — Wave 3 Phase 4a
 * (Issue #805 / PR #732 §4.3.4).
 *
 * Coverage:
 *   - Default limit (10) + clamp (1..50)
 *   - Empty-state contract (200 with { items: [] } → [] from hook)
 *   - Schema validates Gate-B-aware DTO (installCount: 0, ratingAverage: null)
 *   - `enabled: false` defers the request
 *   - Error path surfaces the rejection
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  DISCOVER_RECOMMENDED_TOOLKITS_DEFAULT_LIMIT,
  DISCOVER_RECOMMENDED_TOOLKITS_STALE_TIME_MS,
  useDiscoverRecommendedToolkits,
} from '../useDiscoverRecommendedToolkits';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

// Mocks
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

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SAMPLE_RECOMMENDED = {
  items: [
    {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      name: 'Catan Strategy Toolkit',
      authorName: 'Alice',
      // Gate B v1: backend always returns 0 / null until rating + install entities ship.
      installCount: 0,
      ratingAverage: null,
      ratingCount: 0,
      coverImageUrl: null,
    },
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'Wingspan Helper',
      authorName: 'Bob',
      installCount: 0,
      ratingAverage: null,
      ratingCount: 0,
      coverImageUrl: null,
    },
  ],
};

describe('useDiscoverRecommendedToolkits — constants', () => {
  it('exports a default limit of 10', () => {
    expect(DISCOVER_RECOMMENDED_TOOLKITS_DEFAULT_LIMIT).toBe(10);
  });

  it('exports a 30min staleTime to mirror the backend HybridCache TTL', () => {
    expect(DISCOVER_RECOMMENDED_TOOLKITS_STALE_TIME_MS).toBe(30 * 60 * 1000);
  });
});

describe('useDiscoverRecommendedToolkits — limit handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_RECOMMENDED);
  });

  it('uses default limit=10 when no option is passed', async () => {
    const { result } = renderHook(() => useDiscoverRecommendedToolkits(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/toolkits/recommended?limit=10',
      expect.anything()
    );
  });

  it('clamps an oversize limit to 50', async () => {
    const { result } = renderHook(() => useDiscoverRecommendedToolkits({ limit: 200 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/toolkits/recommended?limit=50',
      expect.anything()
    );
  });

  it('clamps a sub-1 limit to default 10', async () => {
    const { result } = renderHook(() => useDiscoverRecommendedToolkits({ limit: 0 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/toolkits/recommended?limit=10',
      expect.anything()
    );
  });
});

describe('useDiscoverRecommendedToolkits — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the items array on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_RECOMMENDED);

    const { result } = renderHook(() => useDiscoverRecommendedToolkits(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_RECOMMENDED.items);
    // Gate B sanity: all items have installCount=0 and ratingAverage=null in v1.
    expect(result.current.data!.every(t => t.installCount === 0)).toBe(true);
    expect(result.current.data!.every(t => t.ratingAverage === null)).toBe(true);
  });

  it('returns an empty array on the empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useDiscoverRecommendedToolkits(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useDiscoverRecommendedToolkits({ enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('Toolkits service unavailable'));

    const { result } = renderHook(() => useDiscoverRecommendedToolkits(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Toolkits service unavailable');
  });
});
