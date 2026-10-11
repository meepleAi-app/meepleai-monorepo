/**
 * #4187 — i giochi del catalogo per id, indipendenti dalla pagina della lista.
 *
 * Il dettaglio della serata ricostruiva i suoi giochi cercandoli nella prima pagina del catalogo
 * (20 giochi): quelli oltre la pagina sparivano dalla serata e dal pannello di voto.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

const api = vi.hoisted(() => ({ getById: vi.fn() }));

vi.mock('@/lib/api', () => ({
  api: { sharedGames: { getById: (...a: unknown[]) => api.getById(...a) } },
}));

import { useSharedGamesByIds } from '../useSharedGames';

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createTestQueryClient()}>{children}</QueryClientProvider>;
}

const detail = (id: string, title: string) => ({
  id,
  title,
  minPlayers: 2,
  maxPlayers: 4,
  thumbnailUrl: '',
});

beforeEach(() => {
  vi.clearAllMocks();
  api.getById.mockImplementation(async (id: string) =>
    id === 'missing' ? null : detail(id, `Game ${id}`)
  );
});

describe('useSharedGamesByIds (#4187)', () => {
  it('resolves every id, whatever page of the catalog it would be on', async () => {
    const { result } = renderHook(() => useSharedGamesByIds(['g1', 'g42']), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.gamesById.g1?.title).toBe('Game g1');
    expect(result.current.gamesById.g42?.title).toBe('Game g42');
    expect(api.getById).toHaveBeenCalledTimes(2);
  });

  it('leaves out ids the catalog does not know', async () => {
    const { result } = renderHook(() => useSharedGamesByIds(['g1', 'missing']), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Object.keys(result.current.gamesById)).toEqual(['g1']);
  });

  it('keeps the same map across renders while the data does not change', async () => {
    // Il dettaglio della serata usa la mappa come dipendenza dell'effetto che riempie la
    // pianificazione: una mappa nuova a ogni render rifarebbe reset() a ogni render.
    const { result, rerender } = renderHook(() => useSharedGamesByIds(['g1']), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const first = result.current.gamesById;

    rerender();

    expect(result.current.gamesById).toBe(first);
  });

  it('does not fetch when disabled', async () => {
    const { result } = renderHook(() => useSharedGamesByIds(['g1'], false), { wrapper });

    await new Promise(r => setTimeout(r, 20));
    expect(api.getById).not.toHaveBeenCalled();
    expect(result.current.isLoading).toBe(false);
  });
});
