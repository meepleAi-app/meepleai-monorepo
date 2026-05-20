/**
 * @vitest-environment jsdom
 *
 * useKbChunksList unit tests — Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.2).
 *
 * Coverage:
 *   - First-page request URL has no `cursor` param, default `limit=50`.
 *   - Custom `limit` reflected in URL.
 *   - `getNextPageParam` follows server-emitted `nextCursor`.
 *   - Last page (`nextCursor === null`) terminates pagination.
 *   - 30min staleTime constant.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KB_CHUNKS_LIST_STALE_TIME_MS, useKbChunksList } from '../useKbChunksList';

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

const DOC_ID = '11111111-1111-1111-1111-111111111111';

function makeItem(idx: number) {
  return {
    id: `00000000-0000-0000-0000-${String(idx).padStart(12, '0')}`,
    position: idx,
    headingPath: [],
    snippet: `chunk ${idx}`,
    pageNumber: 1,
    vectorId: `v${idx}`,
  };
}

describe('useKbChunksList — constants', () => {
  it('exports 30min staleTime to mirror backend HybridCache', () => {
    expect(KB_CHUNKS_LIST_STALE_TIME_MS).toBe(30 * 60 * 1000);
  });
});

describe('useKbChunksList — first page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue({
      items: [makeItem(0), makeItem(1)],
      nextCursor: null,
      totalCount: 2,
    });
  });

  it('requests with default limit 50 and no cursor on initial fetch', async () => {
    const { result } = renderHook(() => useKbChunksList({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledTimes(1);
    const calledPath = mockGet.mock.calls[0][0] as string;
    expect(calledPath).toBe(`/api/v1/kb-docs/${DOC_ID}/chunks?limit=50`);
  });

  it('reflects custom limit in URL', async () => {
    const { result } = renderHook(() => useKbChunksList({ docId: DOC_ID, limit: 25 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const calledPath = mockGet.mock.calls[0][0] as string;
    expect(calledPath).toContain('limit=25');
  });

  it('terminates pagination when nextCursor is null', async () => {
    const { result } = renderHook(() => useKbChunksList({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.hasNextPage).toBe(false);
  });
});

describe('useKbChunksList — pagination', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('forwards nextCursor from previous page on fetchNextPage', async () => {
    mockGet
      .mockResolvedValueOnce({
        items: [makeItem(0)],
        nextCursor: 'opaque-cursor-1',
        totalCount: 2,
      })
      .mockResolvedValueOnce({
        items: [makeItem(1)],
        nextCursor: null,
        totalCount: 2,
      });

    const { result } = renderHook(() => useKbChunksList({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.hasNextPage).toBe(true);

    await result.current.fetchNextPage();
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2));

    const secondCallPath = mockGet.mock.calls[1][0] as string;
    expect(secondCallPath).toContain('cursor=opaque-cursor-1');
  });
});

describe('useKbChunksList — guard rails', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue({ items: [], nextCursor: null, totalCount: 0 });
  });

  it('does not fire when docId is null', () => {
    renderHook(() => useKbChunksList({ docId: null }), { wrapper: createWrapper() });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useKbChunksList({ docId: DOC_ID, enabled: false }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
  });
});
