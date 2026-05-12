/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useLibrary, useLibraryStats, useLibraryGameDetail, libraryKeys } from '../useLibrary';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
      getStats: vi.fn(),
      getGameDetail: vi.fn(),
      getPrivateGame: vi.fn(),
    },
    sharedGames: {
      getById: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(() => ({ data: { id: 'u1' } })),
}));

import { api } from '@/lib/api';

const mockGetLibrary = api.library.getLibrary as ReturnType<typeof vi.fn>;
const mockGetStats = api.library.getStats as ReturnType<typeof vi.fn>;
const mockGetGameDetail = api.library.getGameDetail as ReturnType<typeof vi.fn>;
const mockGetPrivateGame = api.library.getPrivateGame as ReturnType<typeof vi.fn>;
const mockGetSharedGameById = api.sharedGames.getById as ReturnType<typeof vi.fn>;

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

describe('useLibraryGameDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Issue #1068 (WS-B Mockup Conformity): the unified `/library/[gameId]` route after
  // PR #1037 IA consolidation must serve PrivateGame entries (e.g. Nanolith dogfood
  // game) the same way it serves SharedGame entries — neither getGameDetail (library
  // shared entry) nor sharedGames.getById (catalog) returns data for a private game
  // UUID, so the hook MUST also probe getPrivateGame and map its DTO to
  // LibraryGameDetail. Without this fallback, page.tsx:76 collapses `!gameDetail`
  // into a misleading "Gioco non trovato" state for a game Aaron actually owns.
  it('returns mapped LibraryGameDetail when only getPrivateGame resolves (private game fallback)', async () => {
    const privateGameId = '11111111-1111-4111-8111-111111111111';
    const privateGame = {
      id: privateGameId,
      ownerId: '22222222-2222-4222-8222-222222222222',
      source: 'Manual' as const,
      bggId: 462362,
      title: 'Nanolith',
      minPlayers: 1,
      maxPlayers: 4,
      yearPublished: 2025,
      description: 'A libro game adventure',
      playingTimeMinutes: 60,
      minAge: 12,
      complexityRating: 3.5,
      imageUrl: 'https://example.com/nanolith.png',
      thumbnailUrl: 'https://example.com/nanolith-thumb.png',
      createdAt: '2026-01-15T10:30:00Z',
      updatedAt: null,
      bggSyncedAt: null,
    };

    mockGetGameDetail.mockResolvedValue(null);
    mockGetSharedGameById.mockResolvedValue(null);
    mockGetPrivateGame.mockResolvedValue(privateGame);

    const { result } = renderHook(() => useLibraryGameDetail(privateGameId), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).not.toBeNull();
    expect(result.current.data?.gameId).toBe(privateGameId);
    expect(result.current.data?.gameTitle).toBe('Nanolith');
    expect(result.current.data?.libraryEntryId).toBe('');
    expect(result.current.data?.minPlayers).toBe(1);
    expect(result.current.data?.maxPlayers).toBe(4);
    expect(result.current.data?.gameImageUrl).toBe('https://example.com/nanolith.png');
    expect(result.current.data?.bggId).toBe(462362);
    expect(result.current.data?.playingTimeMinutes).toBe(60);
    expect(result.current.data?.complexityRating).toBe(3.5);
  });
});
