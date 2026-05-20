/**
 * @vitest-environment jsdom
 *
 * useDiscoverTopContributors unit tests — Wave 3 Phase 4a
 * (Issue #805 / PR #732 §4.3.6).
 *
 * Coverage:
 *   - Default limit (10) + clamp (1..50)
 *   - Empty-state contract (200 with { items: [] } → [] from hook)
 *   - Schema validates Gate-B-aware breakdown (faqsCount: 0,
 *     agentsCreatedCount: 0; only kbUploadsCount carries a real signal)
 *   - `enabled: false` defers the request
 *   - Error path surfaces the rejection
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  DISCOVER_TOP_CONTRIBUTORS_DEFAULT_LIMIT,
  DISCOVER_TOP_CONTRIBUTORS_STALE_TIME_MS,
  useDiscoverTopContributors,
} from '../useDiscoverTopContributors';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

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

const SAMPLE_TOP_CONTRIBUTORS = {
  items: [
    {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      displayName: 'Alice',
      avatarUrl: 'https://example.com/alice.png',
      contributionCount: 7,
      breakdown: {
        // Gate B v1: backend stubs faqsCount + agentsCreatedCount to 0.
        faqsCount: 0,
        kbUploadsCount: 7,
        agentsCreatedCount: 0,
      },
    },
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      displayName: 'Bob',
      avatarUrl: null,
      contributionCount: 3,
      breakdown: {
        faqsCount: 0,
        kbUploadsCount: 3,
        agentsCreatedCount: 0,
      },
    },
  ],
};

describe('useDiscoverTopContributors — constants', () => {
  it('exports a default limit of 10', () => {
    expect(DISCOVER_TOP_CONTRIBUTORS_DEFAULT_LIMIT).toBe(10);
  });

  it('exports a 1h staleTime to mirror the backend HybridCache TTL', () => {
    expect(DISCOVER_TOP_CONTRIBUTORS_STALE_TIME_MS).toBe(60 * 60 * 1000);
  });
});

describe('useDiscoverTopContributors — limit handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_TOP_CONTRIBUTORS);
  });

  it('uses default limit=10 when no option is passed', async () => {
    const { result } = renderHook(() => useDiscoverTopContributors(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/users/top-contributors?limit=10',
      expect.anything()
    );
  });

  it('clamps an oversize limit to 50', async () => {
    const { result } = renderHook(() => useDiscoverTopContributors({ limit: 999 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/users/top-contributors?limit=50',
      expect.anything()
    );
  });

  it('clamps a sub-1 limit to default 10', async () => {
    const { result } = renderHook(() => useDiscoverTopContributors({ limit: -5 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      '/api/v1/users/top-contributors?limit=10',
      expect.anything()
    );
  });
});

describe('useDiscoverTopContributors — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the items array on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_TOP_CONTRIBUTORS);

    const { result } = renderHook(() => useDiscoverTopContributors(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_TOP_CONTRIBUTORS.items);
    // Gate B sanity: faqsCount and agentsCreatedCount are 0 in v1.
    expect(result.current.data!.every(u => u.breakdown.faqsCount === 0)).toBe(true);
    expect(result.current.data!.every(u => u.breakdown.agentsCreatedCount === 0)).toBe(true);
    // contributionCount equals kbUploadsCount in v1.
    expect(
      result.current.data!.every(u => u.contributionCount === u.breakdown.kbUploadsCount)
    ).toBe(true);
  });

  it('returns an empty array on the empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useDiscoverTopContributors(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useDiscoverTopContributors({ enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('Contributors service unavailable'));

    const { result } = renderHook(() => useDiscoverTopContributors(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Contributors service unavailable');
  });
});
