/**
 * #4333 — la chat incorporata con l'hook VERO.
 *
 * Gli altri test di questa vista sostituiscono `useAgentChatStream` con un doppio, e quelli
 * dell'hook lo provano da solo: nessuno dei due vedeva che, messi insieme, lo stream non partiva.
 * `stopStreaming` era una funzione nuova a ogni render, e la vista la chiama dal cleanup di un
 * effetto che ne dipende: al primo render dopo l'invio lo stream veniva fermato, prima della
 * richiesta.
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { sseEvent, steppedFetch } from '@/test-utils/sse-stepped-stream';

import { EmbeddedChatView } from '../EmbeddedChatView';

vi.mock('sonner', () => ({ toast: { info: vi.fn(), error: vi.fn(), success: vi.fn() } }));

// I tipi numerici di `StreamingEventType`, com'è sul filo.
const STATE_UPDATE = 0;
const COMPLETE = 4;
const TOKEN = 7;

function ask(question: string) {
  fireEvent.change(screen.getByTestId('embedded-chat-input'), { target: { value: question } });
  fireEvent.submit(screen.getByTestId('embedded-chat-input').closest('form')!);
}

/** Consegna un evento e lascia alla vista il tempo di rendere. */
async function deliver(next: () => void) {
  await act(async () => {
    next();
    await new Promise(resolve => setTimeout(resolve, 10));
  });
}

describe("EmbeddedChatView — lo stream, con l'hook vero (#4333)", () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('shows the answer the stream delivers', async () => {
    const wire = steppedFetch([
      sseEvent(STATE_UPDATE, { message: 'Cerco nel manuale...' }),
      sseEvent(TOKEN, { token: 'Cinque ' }),
      sseEvent(TOKEN, { token: 'carte.' }),
      sseEvent(COMPLETE, { totalTokens: 2 }),
    ]);
    globalThis.fetch = wire.fetch;

    render(<EmbeddedChatView threadId="thread-1" gameId="game-1" />);
    ask('Quante carte?');

    // La richiesta parte: prima di #4333 `fetch` non veniva nemmeno chiamata.
    await waitFor(() => expect(wire.calls()).toBe(1));

    // Un evento alla volta, più la chiusura dello stream.
    for (let i = 0; i < wire.steps; i++) await deliver(wire.next);

    await waitFor(() =>
      expect(screen.getAllByTestId('message-assistant').map(n => n.textContent)).toEqual([
        'Cinque carte.',
      ])
    );
    expect(wire.signal()?.aborted).toBe(false);
    expect(screen.getAllByTestId('message-user').map(n => n.textContent)).toEqual([
      'Quante carte?',
    ]);
    expect(wire.calls()).toBe(1);
  });

  /**
   * Lo stream si ferma ancora quando deve: quando la vista se ne va. Lo garantisce già l'automa
   * dello stream, che si ripulisce da solo allo smontaggio; l'effetto della vista è una seconda
   * cintura. Qui conta la coppia: se una correzione futura togliesse entrambe, questo diventa rosso.
   */
  it('stops the stream in flight when the view goes away', async () => {
    const wire = steppedFetch([sseEvent(TOKEN, { token: 'Cinque ' })]);
    globalThis.fetch = wire.fetch;

    const { unmount } = render(<EmbeddedChatView threadId="thread-1" gameId="game-1" />);
    ask('Quante carte?');
    await waitFor(() => expect(wire.calls()).toBe(1));
    expect(wire.signal()?.aborted).toBe(false);

    unmount();

    expect(wire.signal()?.aborted).toBe(true);
  });
});
