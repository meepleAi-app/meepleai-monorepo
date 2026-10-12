/**
 * #4187 — il wizard di /upload offriva la prima pagina del catalogo (20 giochi, il default di
 * `api.games.getAll`) e non aveva ricerca: un gioco oltre quella pagina non si poteva scegliere.
 * Qui il catalogo finto vive "sul server": `getAll` filtra per titolo e restituisce una pagina.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useGames } from '../useGames';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockCreate = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { games: { getAll: mockGetAll, create: mockCreate } } }));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: (value: string) => value,
}));

const CATALOG = [
  ...Array.from({ length: 60 }, (_, i) => ({
    id: `game-${i}`,
    title: `Agricola ${String(i).padStart(2, '0')}`,
    createdAt: '2026-01-01',
  })),
  { id: 'game-wingspan', title: 'Wingspan', createdAt: '2026-01-01' },
];

function serveCatalog(filters?: { search?: string }, page = 1, pageSize = 20) {
  const pool = filters?.search
    ? CATALOG.filter(g => g.title.toLowerCase().includes(filters.search!.toLowerCase()))
    : CATALOG;
  return Promise.resolve({
    games: pool.slice((page - 1) * pageSize, page * pageSize),
    total: pool.length,
    page,
    pageSize,
    totalPages: Math.ceil(pool.length / pageSize),
  });
}

describe('useGames (wizard) — ricerca sul server (#4187)', () => {
  beforeEach(() => {
    mockGetAll.mockReset();
    mockCreate.mockReset();
    mockGetAll.mockImplementation(serveCatalog);
  });

  it('con un testo chiede al server e trova «Wingspan», oltre la prima pagina', async () => {
    const { result } = renderHook(() => useGames(undefined, 'Wingspan'));

    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));
    expect(mockGetAll).toHaveBeenLastCalledWith({ search: 'Wingspan' }, 1, 50);
  });

  it('un gioco visto prima resta nominabile quando la ricerca cambia', async () => {
    const { result, rerender } = renderHook(({ search }) => useGames(undefined, search), {
      initialProps: { search: 'Wingspan' },
    });
    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Wingspan']));

    rerender({ search: 'Agricola 01' });

    await waitFor(() => expect(result.current.games.map(g => g.title)).toEqual(['Agricola 01']));
    expect(result.current.gamesById['game-wingspan']?.title).toBe('Wingspan');
  });

  it('cambiando ricerca non torna in caricamento (il Select resta usabile)', async () => {
    const { result, rerender } = renderHook(({ search }) => useGames(undefined, search), {
      initialProps: { search: '' },
    });
    await waitFor(() => expect(result.current.loading).toBe(false));

    mockGetAll.mockImplementation(() => new Promise(() => {}));
    rerender({ search: 'Wing' });

    expect(result.current.loading).toBe(false);
  });

  it('un gioco creato entra anche fra quelli nominabili', async () => {
    mockCreate.mockResolvedValue({ id: 'new-1', title: 'Nuovo', createdAt: '2026-10-12' });
    const { result } = renderHook(() => useGames());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await result.current.createGame('Nuovo');

    await waitFor(() => expect(result.current.gamesById['new-1']?.title).toBe('Nuovo'));
  });
});
