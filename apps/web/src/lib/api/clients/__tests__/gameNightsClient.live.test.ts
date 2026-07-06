/**
 * gameNightsClient.getLive tests — #2633 Slice B (LD-10).
 *
 * The night-live read (GET /game-nights/{id}/live) must NOT feed a 401→null
 * response into GameNightLiveDtoSchema.parse() (which would surface a
 * SchemaValidationError indistinguishable from a corrupt 500). Instead null is
 * detected and rethrown as a typed UnauthorizedError.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { UnauthorizedError } from '@/lib/api/core/errors';

import { createGameNightsClient } from '../gameNightsClient';

const mockGet = vi.fn();
const mockPost = vi.fn();
const mockHttpClient = {
  get: mockGet,
  post: mockPost,
  put: vi.fn(),
  delete: vi.fn(),
};

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const LIVE = {
  id: GN_ID,
  title: 'Serata Eldoria',
  status: 'Published',
  sessions: [
    {
      sessionId: '11111111-1111-4111-8111-111111111111',
      gameId: '22222222-2222-4222-8222-222222222222',
      gameTitle: 'Eldoria',
      playOrder: 1,
      status: 'InProgress',
      winnerId: null,
      winnerName: null,
      startedAt: '2026-07-04T20:00:00Z',
      completedAt: null,
    },
  ],
  isViewerOrganizer: true,
  plannedLineup: [],
  currentSessionRoster: [],
};

describe('gameNightsClient.getLive', () => {
  let client: ReturnType<typeof createGameNightsClient>;

  beforeEach(() => {
    vi.clearAllMocks();
    client = createGameNightsClient({ httpClient: mockHttpClient as never });
  });

  it('GETs /game-nights/{id}/live and returns the parsed live dto', async () => {
    mockGet.mockResolvedValue(LIVE);

    const result = await client.getLive(GN_ID);

    expect(mockGet).toHaveBeenCalledWith(`/api/v1/game-nights/${GN_ID}/live`);
    expect(result.title).toBe('Serata Eldoria');
    expect(result.sessions).toHaveLength(1);
    expect(result.sessions[0].status).toBe('InProgress');
  });

  it('throws UnauthorizedError when the client returns null (401 path) — never parses null', async () => {
    mockGet.mockResolvedValue(null);

    await expect(client.getLive(GN_ID)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('rejects an unknown session status at the parse boundary — not the mapper (AC5)', async () => {
    const poison = {
      ...LIVE,
      sessions: [{ ...LIVE.sessions[0], status: 'NewFutureStatus' }],
    };
    mockGet.mockResolvedValue(poison);

    await expect(client.getLive(GN_ID)).rejects.toThrow();
  });
});

describe('gameNightsClient.startNextGame (WS1 DEC-10)', () => {
  let client: ReturnType<typeof createGameNightsClient>;

  beforeEach(() => {
    vi.clearAllMocks();
    client = createGameNightsClient({ httpClient: mockHttpClient as never });
  });

  it('POSTs /game-nights/{id}/sessions with gameId+gameTitle and returns the parsed result', async () => {
    const gameId = '22222222-2222-4222-8222-222222222222';
    mockPost.mockResolvedValue({
      sessionId: '33333333-3333-4333-8333-333333333333',
      gameNightSessionId: '44444444-4444-4444-8444-444444444444',
      sessionCode: 'ABC123',
      playOrder: 2,
    });

    const result = await client.startNextGame(GN_ID, gameId, 'Catan');

    expect(mockPost).toHaveBeenCalledWith(`/api/v1/game-nights/${GN_ID}/sessions`, {
      gameId,
      gameTitle: 'Catan',
    });
    expect(result.sessionId).toBe('33333333-3333-4333-8333-333333333333');
    expect(result.playOrder).toBe(2);
  });

  it('throws UnauthorizedError when the client returns null (401 path)', async () => {
    mockPost.mockResolvedValue(null);
    await expect(
      client.startNextGame(GN_ID, '22222222-2222-4222-8222-222222222222', 'Catan')
    ).rejects.toBeInstanceOf(UnauthorizedError);
  });
});
