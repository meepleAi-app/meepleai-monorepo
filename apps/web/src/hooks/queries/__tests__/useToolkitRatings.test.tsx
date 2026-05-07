/**
 * @vitest-environment jsdom
 *
 * useToolkitRatings unit tests — Wave 3 Phase 4b
 * (Issue #805 / PR #732 §5.3.3).
 *
 * Coverage:
 *   - Default limit (20) + clamp (1..50)
 *   - Empty-state contract (Gate B v1 stub: items=[], breakdown all 0)
 *   - Cursor passed through to URL
 *   - `enabled: false` and missing toolkitId defer the request
 *   - Error path surfaces the rejection
 *   - Schema validates the v1 stub shape
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  TOOLKIT_RATINGS_DEFAULT_LIMIT,
  TOOLKIT_RATINGS_STALE_TIME_MS,
  useToolkitRatings,
} from '../useToolkitRatings';

vi.mock('@/lib/api/client', () => ({
  apiClient: { get: vi.fn() },
}));

import { apiClient } from '@/lib/api/client';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const TOOLKIT_ID = '11111111-1111-1111-1111-111111111111';

const STUB_EMPTY_RESPONSE = {
  items: [],
  nextCursor: null,
  breakdown: { star1: 0, star2: 0, star3: 0, star4: 0, star5: 0 },
  averageStars: 0,
  totalCount: 0,
};

const STUB_WITH_RATINGS = {
  items: [
    {
      id: '22222222-2222-2222-2222-222222222222',
      raterDisplayName: 'Alice',
      raterAvatarUrl: null,
      stars: 5,
      comment: 'Great toolkit!',
      createdAt: '2026-05-01T12:00:00Z',
    },
  ],
  nextCursor: 'cursor-page-2',
  breakdown: { star1: 0, star2: 0, star3: 0, star4: 0, star5: 1 },
  averageStars: 5,
  totalCount: 1,
};

describe('useToolkitRatings', () => {
  beforeEach(() => {
    mockGet.mockReset();
  });

  it('exports default limit + stale time constants matching backend cache', () => {
    expect(TOOLKIT_RATINGS_DEFAULT_LIMIT).toBe(20);
    expect(TOOLKIT_RATINGS_STALE_TIME_MS).toBe(5 * 60 * 1000);
  });

  it('fetches with default limit when none is supplied', async () => {
    mockGet.mockResolvedValueOnce(STUB_EMPTY_RESPONSE);

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet.mock.calls[0]![0]).toBe(`/toolkits/${TOOLKIT_ID}/ratings?limit=20`);
  });

  it('returns the v1 empty stub envelope unchanged', async () => {
    mockGet.mockResolvedValueOnce(STUB_EMPTY_RESPONSE);

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(STUB_EMPTY_RESPONSE);
  });

  it('passes cursor + limit through to the URL', async () => {
    mockGet.mockResolvedValueOnce(STUB_WITH_RATINGS);

    const { result } = renderHook(
      () => useToolkitRatings({ toolkitId: TOOLKIT_ID, cursor: 'cursor-page-1', limit: 5 }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const url = mockGet.mock.calls[0]![0] as string;
    expect(url).toContain('limit=5');
    expect(url).toContain('cursor=cursor-page-1');
  });

  it('clamps limit values outside [1, 50]', async () => {
    mockGet.mockResolvedValueOnce(STUB_EMPTY_RESPONSE);

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID, limit: 999 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGet.mock.calls[0]![0]).toContain('limit=50');
  });

  it('uses default limit when limit is zero or negative', async () => {
    mockGet.mockResolvedValueOnce(STUB_EMPTY_RESPONSE);

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID, limit: 0 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGet.mock.calls[0]![0]).toContain('limit=20');
  });

  it('does not fire the request when toolkitId is null', () => {
    renderHook(() => useToolkitRatings({ toolkitId: null }), { wrapper: createWrapper() });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire the request when enabled is false', () => {
    renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID, enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('falls back to a stub envelope when the client returns null (204 safety)', async () => {
    mockGet.mockResolvedValueOnce(null);

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(STUB_EMPTY_RESPONSE);
  });

  it('surfaces upstream errors via the query result', async () => {
    mockGet.mockRejectedValueOnce(new Error('API failure'));

    const { result } = renderHook(() => useToolkitRatings({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('API failure');
  });
});
