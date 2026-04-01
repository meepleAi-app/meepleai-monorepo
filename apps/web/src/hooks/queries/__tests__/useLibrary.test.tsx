/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useLibrary, useLibraryStats, libraryKeys } from '../useLibrary';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
      getStats: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(() => ({ data: { id: 'u1' } })),
}));

import { api } from '@/lib/api';

const mockGetLibrary = api.library.getLibrary as ReturnType<typeof vi.fn>;
const mockGetStats = api.library.getStats as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('libraryKeys', () => {
  it('returns stable key for list with no params', () => {
    const key = libraryKeys.list(undefined);
    expect(key[0]).toBe('library');
  });

  it('returns stable key for stats', () => {
    expect(libraryKeys.stats()).toContain('library');
  });
});

describe('useLibrary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns paginated library data on success', async () => {
    const mockResponse = {
      items: [{ id: 'g1', title: 'Wingspan' }],
      total: 1,
      page: 1,
      pageSize: 20,
    };
    mockGetLibrary.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useLibrary(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockResponse);
    expect(mockGetLibrary).toHaveBeenCalledTimes(1);
  });

  it('passes params to the API', async () => {
    mockGetLibrary.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 });
    const params = { page: 2, pageSize: 10 };

    const { result } = renderHook(() => useLibrary(params), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGetLibrary).toHaveBeenCalledWith(params);
  });

  it('does not fetch when disabled', () => {
    const { result } = renderHook(() => useLibrary(undefined, false), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  it('enters error state on API failure', async () => {
    mockGetLibrary.mockRejectedValue(new Error('Unauthorized'));

    const { result } = renderHook(() => useLibrary(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Unauthorized');
  });
});

describe('useLibraryStats', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns stats on success', async () => {
    const mockStats = { totalGames: 42, playedGames: 10, wishlistGames: 5 };
    mockGetStats.mockResolvedValue(mockStats);

    const { result } = renderHook(() => useLibraryStats(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockStats);
  });

  it('does not fetch when disabled', () => {
    const { result } = renderHook(() => useLibraryStats(false), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockGetStats).not.toHaveBeenCalled();
  });
});
