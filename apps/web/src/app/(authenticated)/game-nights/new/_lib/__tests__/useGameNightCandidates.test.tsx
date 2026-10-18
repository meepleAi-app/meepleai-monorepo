/**
 * @vitest-environment jsdom
 */

/**
 * #4187 — i candidati del passo 4 di `game-nights/new`.
 *
 * Il wizard chiedeva i primi 50 giochi della libreria e li offriva come scelta completa:
 * un gioco oltre quella pagina non si poteva proporre. Qui la libreria finta vive "sul
 * server": `getLibrary` filtra per titolo come `ILike` in UserLibraryRepository e
 * restituisce una pagina. Un gioco scelto che la ricerca corrente non mostra va letto
 * per id, altrimenti sparisce dalla griglia e l'anteprima RSVP stampa il suo UUID.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

import { useGameNightCandidates } from '../useGameNightCandidates';

const mockGetLibrary = vi.hoisted(() => vi.fn());
const mockGetSharedGame = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    library: { getLibrary: mockGetLibrary },
    sharedGames: { getById: mockGetSharedGame },
  },
}));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: (value: string) => value,
}));

interface FakeEntry {
  gameId: string;
  gameTitle: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
}

const LIBRARY: FakeEntry[] = [
  {
    gameId: 'game-catan',
    gameTitle: 'Catan',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
  },
  ...Array.from({ length: 60 }, (_, i) => ({
    gameId: `game-filler-${i}`,
    gameTitle: `Agricola ${String(i).padStart(2, '0')}`,
    minPlayers: 1,
    maxPlayers: 4,
    playingTimeMinutes: 120,
  })),
  {
    gameId: 'game-wingspan',
    gameTitle: 'Wingspan',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
  },
];

function serveLibrary(params?: { search?: string; pageSize?: number }) {
  const pool = params?.search
    ? LIBRARY.filter(e => e.gameTitle.toLowerCase().includes(params.search!.toLowerCase()))
    : LIBRARY;
  return Promise.resolve({
    items: pool.slice(0, params?.pageSize ?? 20),
    page: 1,
    pageSize: params?.pageSize ?? 20,
    totalCount: pool.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  });
}

function serveSharedGame(id: string) {
  const entry = LIBRARY.find(e => e.gameId === id);
  if (!entry) return Promise.resolve(null);
  return Promise.resolve({
    id,
    title: entry.gameTitle,
    minPlayers: entry.minPlayers,
    maxPlayers: entry.maxPlayers,
    playingTimeMinutes: entry.playingTimeMinutes,
    coverUrl: null,
  });
}

function wrapper() {
  const client = createTestQueryClient();
  return ({ children }: { children: ReactNode }) =>
    createElement(QueryClientProvider, { client }, children);
}

describe('useGameNightCandidates (#4187)', () => {
  beforeEach(() => {
    mockGetLibrary.mockReset();
    mockGetSharedGame.mockReset();
    mockGetLibrary.mockImplementation(serveLibrary);
    mockGetSharedGame.mockImplementation(serveSharedGame);
  });

  it('cerca sul server: «Wingspan», oltre la prima pagina della libreria, si trova', async () => {
    const { result } = renderHook(
      () => useGameNightCandidates({ search: 'Wingspan', selectedIds: [], enabled: true }),
      { wrapper: wrapper() }
    );

    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));
    expect(mockGetLibrary).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'Wingspan', sortBy: 'title', sortDescending: false })
    );
    expect(result.current.games[0]).toEqual(
      expect.objectContaining({ id: 'game-wingspan', minPlayers: 1, maxPlayers: 5 })
    );
  });

  it('senza testo mostra la prima pagina in ordine crescente di titolo', async () => {
    const { result } = renderHook(
      () => useGameNightCandidates({ search: '', selectedIds: [], enabled: true }),
      { wrapper: wrapper() }
    );

    await waitFor(() => expect(result.current.games.length).toBeGreaterThan(0));
    const params = mockGetLibrary.mock.calls[0][0];
    expect(params.search).toBeUndefined();
    expect(params).toEqual(expect.objectContaining({ sortBy: 'title', sortDescending: false }));
  });

  it('un gioco scelto che la ricerca non mostra resta in lista, in testa, col titolo letto per id', async () => {
    const { result } = renderHook(
      () => useGameNightCandidates({ search: 'Wing', selectedIds: ['game-catan'], enabled: true }),
      { wrapper: wrapper() }
    );

    await waitFor(() =>
      expect(result.current.games.map(g => g.title)).toEqual(['Catan', 'Wingspan'])
    );
    expect(mockGetSharedGame).toHaveBeenCalledWith('game-catan');
    expect(result.current.games[0]).toEqual(
      expect.objectContaining({ id: 'game-catan', minPlayers: 3, maxPlayers: 4 })
    );
  });

  it('un gioco scelto presente nei risultati non viene letto per id né duplicato', async () => {
    const { result } = renderHook(
      () =>
        useGameNightCandidates({
          search: 'Wing',
          selectedIds: ['game-wingspan'],
          enabled: true,
        }),
      { wrapper: wrapper() }
    );

    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));
    expect(mockGetSharedGame).not.toHaveBeenCalled();
  });

  it('cambiando ricerca, un gioco scelto visto nei risultati non sparisce mai (nessun lampo)', async () => {
    // La lettura per id non risponde: il gioco deve restare grazie a quanto già visto.
    mockGetSharedGame.mockImplementation(() => new Promise(() => {}));
    const { result, rerender } = renderHook(
      ({ search }) =>
        useGameNightCandidates({ search, selectedIds: ['game-catan'], enabled: true }),
      { wrapper: wrapper(), initialProps: { search: '' } }
    );
    await waitFor(() => expect(result.current.games.map(g => g.title)).toContain('Catan'));

    rerender({ search: 'Wing' });

    await waitFor(() => expect(result.current.games.map(g => g.title)).toContain('Wingspan'));
    expect(result.current.games.map(g => g.title)).toEqual(['Catan', 'Wingspan']);
  });

  it('entrando nel passo 4 i giochi scelti già nominati restano mentre la ricerca carica', async () => {
    const { result, rerender } = renderHook(
      ({ enabled }) =>
        useGameNightCandidates({ search: '', selectedIds: ['game-wingspan'], enabled }),
      { wrapper: wrapper(), initialProps: { enabled: false } }
    );
    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));

    // Passo 4: la libreria non risponde ancora.
    mockGetLibrary.mockImplementation(() => new Promise(() => {}));
    rerender({ enabled: true });

    await waitFor(() => expect(mockGetLibrary).toHaveBeenCalled());
    expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']);
  });

  it('fuori dal passo 4 non cerca nella libreria, ma nomina ancora i giochi scelti (anteprima)', async () => {
    const { result } = renderHook(
      () => useGameNightCandidates({ search: '', selectedIds: ['game-wingspan'], enabled: false }),
      { wrapper: wrapper() }
    );

    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });
});
