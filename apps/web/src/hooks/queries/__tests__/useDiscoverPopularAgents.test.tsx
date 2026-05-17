/**
 * @vitest-environment jsdom
 *
 * useDiscoverPopularAgents unit tests — Wave 3 Phase 1
 * (Issue #805 / PR #732 §4.3.3).
 *
 * Coverage:
 *   - Default limit (10) + clamp (1..50)
 *   - Empty-state contract (200 with { items: [] } → [] from hook)
 *   - Schema validates Gate-B-aware DTO (installCount: 0)
 *   - `enabled: false` defers the request
 *   - Error path surfaces the rejection
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  DISCOVER_POPULAR_AGENTS_DEFAULT_LIMIT,
  DISCOVER_POPULAR_AGENTS_STALE_TIME_MS,
  useDiscoverPopularAgents,
} from '../useDiscoverPopularAgents';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

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

const SAMPLE_POPULAR_AGENTS = {
  items: [
    {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      name: 'Catan tutor',
      gameId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      gameName: 'Catan',
      // Gate B v1: backend always returns 0 until AgentInstallation lands.
      installCount: 0,
      invocationCount: 1283,
    },
    {
      id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'Generic Q&A',
      gameId: null,
      gameName: null,
      installCount: 0,
      invocationCount: 472,
    },
  ],
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useDiscoverPopularAgents — constants', () => {
  it('exports a default limit of 10', () => {
    expect(DISCOVER_POPULAR_AGENTS_DEFAULT_LIMIT).toBe(10);
  });

  it('exports a 15min staleTime to mirror the backend HybridCache TTL', () => {
    expect(DISCOVER_POPULAR_AGENTS_STALE_TIME_MS).toBe(15 * 60 * 1000);
  });
});

describe('useDiscoverPopularAgents — limit handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_POPULAR_AGENTS);
  });

  it('uses default limit=10 when no option is passed', async () => {
    const { result } = renderHook(() => useDiscoverPopularAgents(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/api/v1/agents/popular?limit=10', expect.anything());
  });

  it('clamps an oversize limit to 50', async () => {
    const { result } = renderHook(() => useDiscoverPopularAgents({ limit: 75 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/api/v1/agents/popular?limit=50', expect.anything());
  });
});

describe('useDiscoverPopularAgents — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the items array on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_POPULAR_AGENTS);

    const { result } = renderHook(() => useDiscoverPopularAgents(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_POPULAR_AGENTS.items);
    // Gate B sanity check — every emitted agent has installCount = 0 in v1.
    expect(result.current.data!.every(a => a.installCount === 0)).toBe(true);
  });

  it('returns an empty array on the empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useDiscoverPopularAgents(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useDiscoverPopularAgents({ enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('Agents service unavailable'));

    const { result } = renderHook(() => useDiscoverPopularAgents(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Agents service unavailable');
  });
});
