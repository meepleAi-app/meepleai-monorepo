/**
 * @vitest-environment jsdom
 *
 * useDiscoverRecentKbDocs unit tests — Wave 3 Phase 1
 * (Issue #805 / PR #732 §4.3.5).
 *
 * Coverage:
 *   - Default limit (10) + clamp (1..50)
 *   - Empty-state contract (200 with { items: [] } → [] from hook)
 *   - Schema validates the 4-value docType wire vocabulary
 *   - `enabled: false` defers the request
 *   - Error path surfaces the rejection
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  DISCOVER_RECENT_KB_DOCS_DEFAULT_LIMIT,
  DISCOVER_RECENT_KB_DOCS_STALE_TIME_MS,
  useDiscoverRecentKbDocs,
} from '../useDiscoverRecentKbDocs';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

import { apiClient } from '@/lib/api/client';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SAMPLE_RECENT_KB_DOCS = {
  items: [
    {
      id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
      title: 'Wingspan rulebook',
      gameId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
      gameName: 'Wingspan',
      docType: 'rulebook' as const,
      lastIngestedAt: '2026-05-01T10:00:00Z',
      chunkCount: 47,
    },
    {
      id: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
      title: 'Catan errata',
      gameId: null,
      gameName: null,
      docType: 'errata' as const,
      lastIngestedAt: '2026-04-29T08:30:00Z',
      chunkCount: 5,
    },
  ],
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useDiscoverRecentKbDocs — constants', () => {
  it('exports a default limit of 10', () => {
    expect(DISCOVER_RECENT_KB_DOCS_DEFAULT_LIMIT).toBe(10);
  });

  it('exports a 5min staleTime to mirror the backend HybridCache TTL', () => {
    expect(DISCOVER_RECENT_KB_DOCS_STALE_TIME_MS).toBe(5 * 60 * 1000);
  });
});

describe('useDiscoverRecentKbDocs — limit handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_RECENT_KB_DOCS);
  });

  it('uses default limit=10 when no option is passed', async () => {
    const { result } = renderHook(() => useDiscoverRecentKbDocs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/kb-docs/recent?limit=10', expect.anything());
  });

  it('clamps a fractional limit downward to integer', async () => {
    // Math.floor(7.9) = 7
    const { result } = renderHook(() => useDiscoverRecentKbDocs({ limit: 7.9 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/kb-docs/recent?limit=7', expect.anything());
  });

  it('clamps a NaN limit to the default', async () => {
    const { result } = renderHook(() => useDiscoverRecentKbDocs({ limit: Number.NaN }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith('/kb-docs/recent?limit=10', expect.anything());
  });
});

describe('useDiscoverRecentKbDocs — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the items array on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_RECENT_KB_DOCS);

    const { result } = renderHook(() => useDiscoverRecentKbDocs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_RECENT_KB_DOCS.items);
    // Surface vocabulary check — backend collapses DocumentCategory into
    // a 4-value enum: rulebook | faq | errata | guide.
    expect(['rulebook', 'faq', 'errata', 'guide']).toContain(result.current.data![0].docType);
  });

  it('returns an empty array on the empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useDiscoverRecentKbDocs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useDiscoverRecentKbDocs({ enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('KB service unavailable'));

    const { result } = renderHook(() => useDiscoverRecentKbDocs(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('KB service unavailable');
  });
});
