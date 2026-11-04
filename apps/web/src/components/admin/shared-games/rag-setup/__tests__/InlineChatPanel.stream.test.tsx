/**
 * #4335 — la chat di prova dell'admin con l'hook VERO dello stream e la rete finta a passi.
 *
 * `InlineChatPanel.test.tsx` sostituisce `useAgentChatStream` con un doppio fermo: lì una risposta
 * interrotta non può esistere.
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { sseEvent, steppedFetch } from '@/test-utils/sse-stepped-stream';

import { InlineChatPanel } from '../InlineChatPanel';

vi.mock('sonner', () => ({ toast: { info: vi.fn(), error: vi.fn(), success: vi.fn() } }));

// I tipi numerici di `StreamingEventType`, com'è sul filo.
const COMPLETE = 4;
const TOKEN = 7;

const INPUT_PLACEHOLDER = 'Scrivi un messaggio per testare il RAG...';

function ask(question: string) {
  const input = screen.getByPlaceholderText(INPUT_PLACEHOLDER);
  fireEvent.change(input, { target: { value: question } });
  fireEvent.keyDown(input, { key: 'Enter' });
}

/** Consegna un evento e lascia alla vista il tempo di rendere. */
async function deliver(next: () => void) {
  await act(async () => {
    next();
    await new Promise(resolve => setTimeout(resolve, 10));
  });
}

describe("InlineChatPanel — lo stream, con l'hook vero (#4335)", () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  /**
   * La risposta si chiude senza `Complete` né `Error`. Prima il campo restava disattivato e la
   * risposta parziale restava con la rotella: l'unica uscita era il tasto Stop, che la cancellava.
   */
  it('says the answer was interrupted, keeps what arrived, and takes the next question', async () => {
    const first = steppedFetch([sseEvent(TOKEN, { token: 'Cinque ' })]);
    globalThis.fetch = first.fetch;

    render(<InlineChatPanel gameId="game-1" chatThreadId={null} />);
    ask('Quante tessere?');
    await waitFor(() => expect(first.calls()).toBe(1));
    for (let i = 0; i < first.steps; i++) await deliver(first.next);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'La risposta si è interrotta prima della fine'
    );
    expect(screen.getByText('Cinque')).toBeInTheDocument();
    expect(screen.getByTestId('inline-chat-interrupted')).toHaveTextContent('Risposta interrotta');
    // Review di #4509: `text-muted-foreground` su `bg-muted` è 4,43:1 in tema scuro, sotto AA.
    // jsdom non calcola i colori: il rapporto è nel corpo della PR, qui si fissa il token.
    expect(screen.getByTestId('inline-chat-interrupted')).toHaveClass('text-foreground');
    expect(screen.getByPlaceholderText(INPUT_PLACEHOLDER)).not.toBeDisabled();

    const second = steppedFetch([
      sseEvent(TOKEN, { token: 'Cinque tessere.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = second.fetch;
    ask('Quante tessere, di preciso?');
    await waitFor(() => expect(second.calls()).toBe(1));
    for (let i = 0; i < second.steps; i++) await deliver(second.next);

    expect(await screen.findByText('Cinque tessere.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getAllByTestId('inline-chat-interrupted')).toHaveLength(1);
  });
});
