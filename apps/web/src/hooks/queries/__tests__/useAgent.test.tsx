/**
 * @vitest-environment jsdom
 *
 * #3852 — un agente inesistente non e' un guasto.
 *
 * `/agents/<id-inesistente>` mostrava «qualcosa e' andato storto» invece di «questo agente non
 * esiste». Il backend distingue correttamente i due casi (risponde 404); era il frontend a
 * collassarli, perche' il 404 propagava fino a `isError`, e la macchina a stati controlla
 * `isError` PRIMA di `hasData`.
 *
 * Sono due situazioni diverse per chi legge: nella prima si torna indietro, nella seconda si
 * crede che il sistema sia rotto e magari si segnala un guasto che non c'e'.
 *
 * `null` mappa su `hasData: false`, che la FSM traduce gia' in 'not-found' — nessuna modifica
 * alla macchina a stati. Stesso schema di useLiveSession.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';

import { useAgent } from '../useAgent';

const mockGetById = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { agents: { getById: mockGetById } },
}));

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

const AGENT_ID = '11111111-1111-1111-1111-111111111111';

describe('useAgent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('su 404 restituisce null senza segnalare un errore', async () => {
    mockGetById.mockRejectedValue(new ApiError({ message: 'Agent not found', statusCode: 404 }));

    const { result } = renderHook(() => useAgent(AGENT_ID), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isError).toBe(false);
    expect(result.current.data).toBeNull();
  });

  it('su un errore vero lo lascia emergere', async () => {
    // Il punto della correzione e' distinguere, non silenziare: un 500 deve restare un errore,
    // altrimenti un guasto reale diventerebbe indistinguibile da una risorsa assente.
    mockGetById.mockRejectedValue(new ApiError({ message: 'Boom', statusCode: 500 }));

    const { result } = renderHook(() => useAgent(AGENT_ID), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('con un agente esistente restituisce i dati', async () => {
    mockGetById.mockResolvedValue({ id: AGENT_ID, name: 'Agente' });

    const { result } = renderHook(() => useAgent(AGENT_ID), { wrapper: createWrapper() });

    // Si attende isSuccess, non `data != null`: undefined supera quel controllo e il caso
    // passerebbe prima che la query abbia risolto.
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toMatchObject({ id: AGENT_ID });
  });
});
