import { describe, it, expect, vi } from 'vitest';

import { createGamesClient } from '../gamesClient';
import { GameLeaderboardResponseSchema } from '../../schemas';

import type { HttpClient } from '../../core/httpClient';

/**
 * Tests for gamesClient.getLeaderboard (#1467).
 * The leaderboard client method must hit the v1-prefixed game-scoped endpoint
 * with optional since/limit query params and validate via the Zod schema.
 */
describe('gamesClient.getLeaderboard (#1467)', () => {
  const mockResponse = {
    gameId: '11111111-1111-1111-1111-111111111111',
    entries: [],
    returnedCount: 0,
    since: null,
  };

  function setup() {
    const httpClient = { get: vi.fn().mockResolvedValue(mockResponse) };
    const client = createGamesClient({ httpClient: httpClient as unknown as HttpClient });
    return { client, httpClient };
  }

  it('calls the v1 leaderboard endpoint with the schema and no query string by default', async () => {
    const { client, httpClient } = setup();

    await client.getLeaderboard('game-1');

    expect(httpClient.get).toHaveBeenCalledWith(
      '/api/v1/games/game-1/leaderboard',
      GameLeaderboardResponseSchema
    );
  });

  it('appends since and limit query params when provided', async () => {
    const { client, httpClient } = setup();

    await client.getLeaderboard('game-1', { since: '2026-01-01T00:00:00.000Z', limit: 5 });

    const url = httpClient.get.mock.calls[0][0] as string;
    expect(url).toContain('/api/v1/games/game-1/leaderboard?');
    expect(url).toContain('since=');
    expect(url).toContain('limit=5');
  });

  it('returns the response from the http client', async () => {
    const { client } = setup();

    const result = await client.getLeaderboard('game-1');

    expect(result).toEqual(mockResponse);
  });
});
