import { describe, it, expect } from 'vitest';
import { GameSessionDtoSchema } from '../games.schemas';

const base = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T01:00:00Z',
  playerCount: 1,
  players: [{ playerName: 'Alice', playerOrder: 1, color: 'Red' }],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 47,
};

describe('GameSessionDtoSchema #3022', () => {
  it('parses gameSlug/gameName/scorePlayers when present', () => {
    const parsed = GameSessionDtoSchema.parse({
      ...base,
      gameSlug: 'catan',
      gameName: 'Catan',
      scorePlayers: [{ id: 'x', displayName: 'Alice', color: 'Red' }],
    });
    expect(parsed.gameSlug).toBe('catan');
    expect(parsed.gameName).toBe('Catan');
    expect(parsed.scorePlayers?.[0]).toEqual({ id: 'x', displayName: 'Alice', color: 'Red' });
  });

  it('accepts absent and null new fields (back-compat)', () => {
    expect(GameSessionDtoSchema.parse(base).scorePlayers).toBeUndefined();
    expect(
      GameSessionDtoSchema.parse({ ...base, scorePlayers: null, gameSlug: null }).scorePlayers
    ).toBeNull();
  });
});
