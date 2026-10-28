/**
 * useDocumentsByGame — il polling mentre un documento è in elaborazione (#4360).
 *
 * `KnowledgeBaseTab` mostra lo stato di ciascun documento. Prima lo stato del gioco arrivava da
 * `useEmbeddingStatus`, che interrogava il backend ogni due secondi; ora viene dal `processingState` di
 * ciascun file, e questo hook ha `staleTime` di 30 s: senza polling un documento che finisce con la
 * scheda aperta resterebbe «in elaborazione» fino al refetch successivo.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { useDocumentsByGame } from '../useDocumentsByGame';

const GAME_ID = '99999999-9999-9999-9999-999999999999';

function doc(processingState: string): PdfDocumentDto {
  return {
    id: '33333333-3333-3333-3333-333333333333',
    gameId: GAME_ID,
    fileName: 'rulebook.pdf',
    processingState,
  } as PdfDocumentDto;
}

function createWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

const getDocumentsByGame = vi.mocked(api.documents.getDocumentsByGame);

describe('useDocumentsByGame — pollWhileProcessing', () => {
  beforeEach(() => {
    getDocumentsByGame.mockReset();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reads again every few seconds while a document is in progress, then stops on its own', async () => {
    getDocumentsByGame.mockResolvedValueOnce([doc('Extracting')]).mockResolvedValue([doc('Ready')]);

    const { result } = renderHook(
      () => useDocumentsByGame({ gameId: GAME_ID, pollWhileProcessing: true }),
      { wrapper: createWrapper() }
    );
    await waitFor(() => expect(result.current.data?.[0].processingState).toBe('Extracting'));
    expect(getDocumentsByGame).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3_000);
    });
    await waitFor(() => expect(result.current.data?.[0].processingState).toBe('Ready'));
    const callsOnceReady = getDocumentsByGame.mock.calls.length;

    await act(async () => {
      await vi.advanceTimersByTimeAsync(20_000);
    });
    expect(getDocumentsByGame).toHaveBeenCalledTimes(callsOnceReady);
  });

  it('does not poll without the flag, even with a document in progress', async () => {
    getDocumentsByGame.mockResolvedValue([doc('Extracting')]);

    const { result } = renderHook(() => useDocumentsByGame({ gameId: GAME_ID }), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.data).toBeDefined());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(20_000);
    });

    expect(getDocumentsByGame).toHaveBeenCalledTimes(1);
  });

  it('does not poll when every document is already in a final state', async () => {
    getDocumentsByGame.mockResolvedValue([doc('Ready'), doc('Failed')]);

    const { result } = renderHook(
      () => useDocumentsByGame({ gameId: GAME_ID, pollWhileProcessing: true }),
      { wrapper: createWrapper() }
    );
    await waitFor(() => expect(result.current.data).toBeDefined());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(20_000);
    });

    expect(getDocumentsByGame).toHaveBeenCalledTimes(1);
  });
});
