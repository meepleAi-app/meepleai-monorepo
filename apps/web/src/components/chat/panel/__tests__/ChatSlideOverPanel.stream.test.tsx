/**
 * #4333 — il pannello a scomparsa con l'hook VERO dello stream.
 *
 * `ChatSlideOverPanel.test.tsx` sostituisce `useAgentChatStream` con un doppio inerte: lì il
 * pannello non può fermare uno stream che non esiste. Qui l'hook è quello vero e la rete è finta:
 * è la strada che fa la chat dei giochi privati (#4244).
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';
import { sseEvent, steppedFetch } from '@/test-utils/sse-stepped-stream';

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: () => ({
    data: { sessions: [], totalCount: 0 },
    isLoading: false,
    error: null,
  }),
}));

vi.mock('@/hooks/queries/useLibrarySearch', () => ({
  useLibrarySearch: (term: unknown) => ({
    items: [],
    totalCount: 0,
    isLoading: false,
    resultsMatchQuery: true,
    term: String(term ?? ''),
  }),
}));

vi.mock('@/hooks/useEmbeddingStatus', () => ({
  useEmbeddingStatus: () => ({ isReady: true, data: { status: 'Completed' } }),
}));

vi.mock('@/components/chat/entry/ThreadCreator', () => ({
  createThread: vi.fn().mockResolvedValue({ threadId: 'thread-1' }),
}));

vi.mock('@/lib/api', () => ({
  api: { chat: { getThreadById: vi.fn().mockResolvedValue(null) } },
}));

import { ChatSlideOverPanel } from '../ChatSlideOverPanel';

// I tipi numerici di `StreamingEventType`, com'è sul filo.
const STATE_UPDATE = 0;
const CITATIONS = 1;
const COMPLETE = 4;
const ERROR = 5;
const TOKEN = 7;

const AZUL = { id: 'azul', name: 'Azul', year: 2017, pdfCount: 3, kbStatus: 'ready' as const };

function ask(question: string) {
  const input = screen.getByTestId('gamebook-chat-input');
  fireEvent.change(input, { target: { value: question } });
  fireEvent.keyDown(input, { key: 'Enter' });
}

/** Consegna un evento e lascia al pannello il tempo di rendere. */
async function deliver(next: () => void) {
  await act(async () => {
    next();
    await new Promise(resolve => setTimeout(resolve, 10));
  });
}

function answers(): Array<string | null> {
  return screen
    .getAllByTestId('chat-message-bubble')
    .filter(node => node.getAttribute('data-role') === 'assistant')
    .map(node => node.textContent);
}

describe("ChatSlideOverPanel — lo stream, con l'hook vero (#4333)", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    useChatPanelStore.getState().close();
    useChatPanelStore.getState().clearGameContext();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('shows the answer the stream delivers', async () => {
    const wire = steppedFetch([
      sseEvent(STATE_UPDATE, { message: 'Cerco nel manuale...' }),
      sseEvent(TOKEN, { token: 'Cinque ' }),
      sseEvent(TOKEN, { token: 'tessere.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = wire.fetch;
    useChatPanelStore.getState().open(AZUL);

    render(<ChatSlideOverPanel />);
    ask('Quante tessere?');

    // La richiesta parte: prima di #4333 `fetch` non veniva nemmeno chiamata.
    await waitFor(() => expect(wire.calls()).toBe(1));

    // Un evento alla volta, più la chiusura dello stream.
    for (let i = 0; i < wire.steps; i++) await deliver(wire.next);

    await waitFor(() =>
      expect(answers().some(text => text?.includes('Cinque tessere.'))).toBe(true)
    );
    expect(answers()).toHaveLength(1);
    expect(wire.signal()?.aborted).toBe(false);
    expect(wire.calls()).toBe(1);
  });

  /**
   * #4244 (consegna D): la risposta dice da dove viene, e la fonte apre il PDF alla pagina citata.
   * Lo stream le manda in un evento a parte, prima dei token: il pannello lo ignorava.
   */
  it('shows the source of the answer, with a link that opens the cited page of the PDF', async () => {
    const wire = steppedFetch([
      sseEvent(CITATIONS, {
        citations: [
          {
            text: 'Deal two to each player.',
            source: 'PDF:pdf-coup',
            page: 3,
            line: 0,
            score: 0.9,
          },
        ],
      }),
      sseEvent(TOKEN, { token: 'Due carte.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = wire.fetch;
    useChatPanelStore.getState().open(AZUL);

    render(<ChatSlideOverPanel />);
    ask('Quante carte?');
    await waitFor(() => expect(wire.calls()).toBe(1));
    for (let i = 0; i < wire.steps; i++) await deliver(wire.next);

    const link = await screen.findByTestId('citation-pdf-link-0');
    expect(link).toHaveAttribute('href', '/api/v1/pdfs/pdf-coup/download#page=3');
    expect(screen.getAllByTestId('citation')).toHaveLength(1);
  });

  /** Le fonti di una risposta restano sue: la risposta dopo non le eredita. */
  it('does not give the sources of one answer to the next', async () => {
    const first = steppedFetch([
      sseEvent(CITATIONS, {
        citations: [
          {
            text: 'Deal two to each player.',
            source: 'PDF:pdf-coup',
            page: 3,
            line: 0,
            score: 0.9,
          },
        ],
      }),
      sseEvent(TOKEN, { token: 'Due carte.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = first.fetch;
    useChatPanelStore.getState().open(AZUL);

    render(<ChatSlideOverPanel />);
    ask('Quante carte?');
    await waitFor(() => expect(first.calls()).toBe(1));
    for (let i = 0; i < first.steps; i++) await deliver(first.next);
    await screen.findByTestId('citation-pdf-link-0');

    const second = steppedFetch([
      sseEvent(TOKEN, { token: 'Due monete.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = second.fetch;
    ask('E le monete?');
    await waitFor(() => expect(second.calls()).toBe(1));
    for (let i = 0; i < second.steps; i++) await deliver(second.next);

    await waitFor(() => expect(answers()).toHaveLength(2));
    expect(screen.getAllByTestId('citation')).toHaveLength(1);
  });

  /** Lo stream si ferma ancora quando deve: quando il pannello viene chiuso. */
  it('stops the stream in flight when the panel is closed', async () => {
    const wire = steppedFetch([sseEvent(TOKEN, { token: 'Cinque ' })]);
    globalThis.fetch = wire.fetch;
    useChatPanelStore.getState().open(AZUL);

    render(<ChatSlideOverPanel />);
    ask('Quante tessere?');
    await waitFor(() => expect(wire.calls()).toBe(1));
    expect(wire.signal()?.aborted).toBe(false);

    act(() => useChatPanelStore.getState().close());

    expect(wire.signal()?.aborted).toBe(true);
  });

  /** E quando il pannello se ne va del tutto. */
  it('stops the stream in flight when the panel goes away', async () => {
    const wire = steppedFetch([sseEvent(TOKEN, { token: 'Cinque ' })]);
    globalThis.fetch = wire.fetch;
    useChatPanelStore.getState().open(AZUL);

    const { unmount } = render(<ChatSlideOverPanel />);
    ask('Quante tessere?');
    await waitFor(() => expect(wire.calls()).toBe(1));

    unmount();

    expect(wire.signal()?.aborted).toBe(true);
  });

  /**
   * #4335: la risposta si chiude senza `Complete` né `Error`. Prima la parte arrivata restava come
   * anteprima «in corso» e `handleSend` rifiutava ogni domanda dopo, senza dire perché: se ne usciva
   * solo con «Nuova chat», cambiando gioco o chiudendo il pannello.
   */
  describe('a stream that closes without Complete or Error (#4335)', () => {
    const INTERRUPTED = 'La risposta si è interrotta prima della fine';

    it('says the answer was interrupted, keeps what arrived, and takes the next question', async () => {
      const first = steppedFetch([sseEvent(TOKEN, { token: 'Cinque ' })]);
      globalThis.fetch = first.fetch;
      useChatPanelStore.getState().open(AZUL);

      render(<ChatSlideOverPanel />);
      ask('Quante tessere?');
      await waitFor(() => expect(first.calls()).toBe(1));
      for (let i = 0; i < first.steps; i++) await deliver(first.next);

      // L'utente legge che la risposta si è interrotta...
      expect(await screen.findByRole('alert')).toHaveTextContent(INTERRUPTED);
      // ...e la parte che aveva già letto resta, segnata come incompleta.
      expect(answers()).toEqual([expect.stringContaining('Cinque')]);
      expect(screen.getByTestId('chat-message-interrupted')).toHaveTextContent(
        'Risposta interrotta'
      );

      // La domanda dopo parte.
      const second = steppedFetch([
        sseEvent(TOKEN, { token: 'Cinque tessere.' }),
        sseEvent(COMPLETE, { totalTokens: 2 }),
      ]);
      globalThis.fetch = second.fetch;
      ask('Quante tessere, di preciso?');
      await waitFor(() => expect(second.calls()).toBe(1));
      for (let i = 0; i < second.steps; i++) await deliver(second.next);

      await waitFor(() => expect(answers()).toHaveLength(2));
      expect(answers()[1]).toContain('Cinque tessere.');
      // L'avviso era della risposta di prima, e solo quella resta segnata.
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.getAllByTestId('chat-message-interrupted')).toHaveLength(1);
    });

    it('adds no empty answer when the stream closes before any word', async () => {
      const first = steppedFetch([sseEvent(STATE_UPDATE, { message: 'Cerco nel manuale...' })]);
      globalThis.fetch = first.fetch;
      useChatPanelStore.getState().open(AZUL);

      render(<ChatSlideOverPanel />);
      ask('Quante tessere?');
      await waitFor(() => expect(first.calls()).toBe(1));
      for (let i = 0; i < first.steps; i++) await deliver(first.next);

      expect(await screen.findByRole('alert')).toHaveTextContent(INTERRUPTED);
      expect(answers()).toEqual([]);

      const second = steppedFetch([sseEvent(COMPLETE, { totalTokens: 0 })]);
      globalThis.fetch = second.fetch;
      ask('Quante tessere?');
      await waitFor(() => expect(second.calls()).toBe(1));
    });
  });

  /**
   * #4335, deciso insieme: un `Error` che arriva dopo qualche parola. La parte scritta spariva
   * (`onComplete` non scatta): ora resta, segnata come incompleta, accanto al motivo.
   */
  it('keeps the words that came before an Error event, marked as incomplete', async () => {
    const wire = steppedFetch([
      sseEvent(TOKEN, { token: 'Cinque ' }),
      sseEvent(ERROR, {
        errorMessage: 'Il servizio AI è temporaneamente non disponibile.',
        errorCode: 'SOMETHING_ELSE',
      }),
    ]);
    globalThis.fetch = wire.fetch;
    useChatPanelStore.getState().open(AZUL);

    render(<ChatSlideOverPanel />);
    ask('Quante tessere?');
    await waitFor(() => expect(wire.calls()).toBe(1));
    for (let i = 0; i < wire.steps; i++) await deliver(wire.next);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Il servizio AI è temporaneamente non disponibile.'
    );
    expect(answers()).toEqual([expect.stringContaining('Cinque')]);
    expect(screen.getByTestId('chat-message-interrupted')).toBeInTheDocument();
  });
});
