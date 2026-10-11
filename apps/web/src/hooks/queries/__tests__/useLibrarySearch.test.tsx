/**
 * @vitest-environment jsdom
 */

/**
 * #4187 — ricerca nella libreria sul server, per i selettori che mostrano le voci intere
 * (copertina, editore, giocatori). Prima ognuno scaricava una pagina e filtrava sul client:
 * un gioco oltre quella pagina non si trovava. Qui la libreria finta vive "sul server":
 * `getLibrary` filtra per titolo come `ILike` in UserLibraryRepository.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import * as useDebounceModule from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';

import { useLibrarySearch } from '../useLibrarySearch';

const mockGetLibrary = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { library: { getLibrary: mockGetLibrary } } }));

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: vi.fn((value: string) => value),
}));

const LIBRARY = [
  ...Array.from({ length: 120 }, (_, i) => ({
    id: `entry-${i}`,
    gameId: `game-${i}`,
    gameTitle: `Agricola ${String(i).padStart(3, '0')}`,
  })),
  { id: 'entry-wingspan', gameId: 'game-wingspan', gameTitle: 'Wingspan' },
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

function wrapper() {
  const client = createTestQueryClient();
  return ({ children }: { children: ReactNode }) =>
    createElement(QueryClientProvider, { client }, children);
}

describe('useLibrarySearch (#4187)', () => {
  beforeEach(() => {
    mockGetLibrary.mockReset();
    mockGetLibrary.mockImplementation(serveLibrary);
    vi.mocked(useDebounceModule.useDebounce).mockImplementation((value: string) => value);
  });

  it('cerca sul server: «Wingspan», oltre la prima pagina, si trova', async () => {
    const { result } = renderHook(() => useLibrarySearch('Wingspan'), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.items.map(e => e.gameTitle)).toEqual(['Wingspan']));
    expect(mockGetLibrary).toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: 'Wingspan',
        page: 1,
        sortBy: 'title',
        sortDescending: false,
      })
    );
    expect(result.current.totalCount).toBe(1);
  });

  it('senza testo restituisce la prima pagina e il totale vero della libreria', async () => {
    const { result } = renderHook(() => useLibrarySearch(''), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.items).toHaveLength(50));
    expect(mockGetLibrary.mock.calls[0][0].search).toBeUndefined();
    expect(result.current.totalCount).toBe(LIBRARY.length);
  });

  it('non chiede al server più di quanto restituisca (pageSize 1..100)', async () => {
    const { result } = renderHook(() => useLibrarySearch('', { pageSize: 200 }), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.items.length).toBeGreaterThan(0));
    expect(mockGetLibrary.mock.calls[0][0].pageSize).toBe(100);
  });

  it('con enabled: false non fa richieste', () => {
    renderHook(() => useLibrarySearch('Wingspan', { enabled: false }), { wrapper: wrapper() });
    expect(mockGetLibrary).not.toHaveBeenCalled();
  });

  it('resultsMatchQuery è falso finché il debounce non ha raggiunto il testo', async () => {
    vi.mocked(useDebounceModule.useDebounce).mockImplementation((value: string) =>
      value.slice(0, -1)
    );
    const { result } = renderHook(() => useLibrarySearch('Wing'), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.items.length).toBeGreaterThan(0));
    expect(result.current.resultsMatchQuery).toBe(false);
  });
});
