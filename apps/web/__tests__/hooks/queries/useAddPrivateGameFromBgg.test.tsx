/**
 * useAddPrivateGameFromBgg - Mutation hook tests
 *
 * Tests the two-step BGG → private game → library flow:
 * 1. Creates private game via addPrivateGame
 * 2. Adds it to library via addGame
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

import { useAddPrivateGameFromBgg } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { AddPrivateGameRequest } from '@/lib/api/schemas/private-games.schemas';

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      addPrivateGame: vi.fn(),
      addGame: vi.fn(),
    },
  },
}));

describe('useAddPrivateGameFromBgg', () => {
  let queryClient: QueryClient;
  let wrapper: ({ children }: { children: ReactNode }) => JSX.Element;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    vi.clearAllMocks();
  });

  const validRequest: AddPrivateGameRequest = {
    source: 'BoardGameGeek',
    bggId: 230802,
    title: 'Azul',
    minPlayers: 2,
    maxPlayers: 4,
  };

  it('calls addPrivateGame then addGame in sequence', async () => {
    const mockPrivateGame = {
      id: 'pg-00000000-0000-0000-0000-000000000001',
      ownerId: 'user-1',
      source: 'BoardGameGeek' as const,
      bggId: 230802,
      title: 'Azul',
      minPlayers: 2,
      maxPlayers: 4,
      createdAt: '2026-03-12T00:00:00.000Z',
    };

    vi.mocked(api.library.addPrivateGame).mockResolvedValue(mockPrivateGame as never);
    vi.mocked(api.library.addGame).mockResolvedValue({} as never);

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate(validRequest);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // Step 1: private game created
    expect(api.library.addPrivateGame).toHaveBeenCalledOnce();
    expect(api.library.addPrivateGame).toHaveBeenCalledWith(validRequest);

    // Step 2: game added to library using private game ID
    expect(api.library.addGame).toHaveBeenCalledOnce();
    expect(api.library.addGame).toHaveBeenCalledWith('pg-00000000-0000-0000-0000-000000000001', {
      isFavorite: false,
    });
  });

  it('returns the created private game on success', async () => {
    const mockPrivateGame = {
      id: 'pg-00000000-0000-0000-0000-000000000002',
      ownerId: 'user-1',
      source: 'BoardGameGeek' as const,
      bggId: 230802,
      title: 'Azul',
      minPlayers: 2,
      maxPlayers: 4,
      createdAt: '2026-03-12T00:00:00.000Z',
    };

    vi.mocked(api.library.addPrivateGame).mockResolvedValue(mockPrivateGame as never);
    vi.mocked(api.library.addGame).mockResolvedValue({} as never);

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate(validRequest);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockPrivateGame);
  });

  it('surfaces error when addPrivateGame fails', async () => {
    const error = new Error('Conflict: game already exists');
    vi.mocked(api.library.addPrivateGame).mockRejectedValue(error);

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate(validRequest);

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
    expect(api.library.addGame).not.toHaveBeenCalled();
  });

  it('surfaces error when addGame fails after private game creation', async () => {
    const mockPrivateGame = {
      id: 'pg-00000000-0000-0000-0000-000000000003',
      ownerId: 'user-1',
      source: 'BoardGameGeek' as const,
      bggId: 230802,
      title: 'Azul',
      minPlayers: 2,
      maxPlayers: 4,
      createdAt: '2026-03-12T00:00:00.000Z',
    };

    vi.mocked(api.library.addPrivateGame).mockResolvedValue(mockPrivateGame as never);
    vi.mocked(api.library.addGame).mockRejectedValue(new Error('Library quota exceeded'));

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate(validRequest);

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Library quota exceeded');
  });
});
