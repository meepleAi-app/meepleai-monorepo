/**
 * #4752 — `createSession` chiamava `POST /api/v1/games/{gameId}/sessions`: il backend registra solo il
 * `GET` su quel percorso (405 a ogni creazione). La creazione sta in `POST /api/v1/game-sessions`, e il
 * gioco viaggia nel corpo (`CreateSessionRequest.GameId`).
 */

import { describe, expect, it, vi } from 'vitest';

import type { HttpClient } from '../../core/httpClient';
import { createSessionFlowClient } from '../client';

describe('sessionFlow.createSession (#4752)', () => {
  it('posts to /api/v1/game-sessions with the game in the body', async () => {
    const result = { sessionId: 's1', sessionCode: 'ABC123' };
    const post = vi.fn().mockResolvedValue(result);
    const client = createSessionFlowClient({ httpClient: { post } as unknown as HttpClient });

    const created = await client.createSession('game-1', {
      sessionType: 'GameSpecific',
      participants: [],
      guestNames: ['Ada'],
      gameNightEventId: 'night-1',
    });

    expect(created).toBe(result);
    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith('/api/v1/game-sessions', {
      gameId: 'game-1',
      sessionType: 'GameSpecific',
      participants: [],
      guestNames: ['Ada'],
      gameNightEventId: 'night-1',
    });
  });

  it('never targets a /games/{id}/sessions path, which has no POST on the backend', async () => {
    const post = vi.fn().mockResolvedValue({ sessionId: 's1' });
    const client = createSessionFlowClient({ httpClient: { post } as unknown as HttpClient });

    await client.createSession('game-1', { sessionType: 'GameSpecific', participants: [] });

    expect(post.mock.calls[0][0]).not.toMatch(/\/games\/.+\/sessions/);
  });
});
