/**
 * #4244 — il thread su un gioco privato.
 *
 * Il backend manda in `gameId` il gioco del thread, del catalogo o privato che sia, e in
 * `privateGameId` lo stesso id quando il gioco è privato. Lo schema deve conservarlo: `z.object`
 * scarta i campi che non conosce, e senza di esso chi mostra il thread non sa a quale rotta
 * chiedere il titolo del gioco (`/games/{id}` risolve solo il catalogo).
 */

import { describe, expect, it } from 'vitest';

import { ChatThreadDtoSchema } from '../chat.schemas';

const GAME_ID = '2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e';

function thread(extra: Record<string, unknown>) {
  return {
    id: '1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d',
    gameId: GAME_ID,
    title: 'Chat: Il mio prototipo',
    createdAt: '2026-10-13T08:00:00Z',
    lastMessageAt: '2026-10-13T08:00:00Z',
    messageCount: 0,
    messages: [],
    ...extra,
  };
}

describe('ChatThreadDtoSchema — private game', () => {
  it('keeps privateGameId when the thread is on a private game', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({ privateGameId: GAME_ID }));

    expect(parsed.privateGameId).toBe(GAME_ID);
    expect(parsed.gameId).toBe(GAME_ID);
  });

  it('accepts a thread on a catalog game, with privateGameId null', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({ privateGameId: null }));

    expect(parsed.privateGameId).toBeNull();
  });

  // Un backend più vecchio del frontend non manda il campo: il thread resta leggibile.
  it('accepts a thread without the field', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({}));

    expect(parsed.privateGameId).toBeUndefined();
  });
});
