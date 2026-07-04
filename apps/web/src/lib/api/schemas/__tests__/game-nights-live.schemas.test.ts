/**
 * game-nights-live schema tests — #2633 Slice B (LD-4).
 *
 * The live read model (GET /game-nights/{id}/live → GameNightLiveDto) must:
 *  - accept ALL 5 GameNightSessionStatus wire strings (incl. Skipped/Corrupted),
 *    so a single Skipped/Corrupted row never fails the whole array `.parse()`;
 *  - reject an unknown 6th status at the parse boundary (fail-fast, not silent).
 */

import { describe, it, expect } from 'vitest';

import { GameNightLiveDtoSchema, GameNightSessionStatusSchema } from '../game-nights.schemas';

const SESSION = {
  sessionId: '11111111-1111-4111-8111-111111111111',
  gameId: '22222222-2222-4222-8222-222222222222',
  gameTitle: 'Eldoria',
  playOrder: 1,
  status: 'InProgress',
  winnerId: null,
  startedAt: '2026-07-04T20:00:00+00:00',
  completedAt: null,
};

const LIVE = {
  id: '33333333-3333-4333-8333-333333333333',
  title: 'Serata Eldoria',
  status: 'Published',
  sessions: [SESSION],
  isViewerOrganizer: true,
};

describe('GameNightSessionStatusSchema', () => {
  it.each(['Pending', 'InProgress', 'Completed', 'Skipped', 'Corrupted'])(
    'accepts the wire string %s',
    value => {
      expect(GameNightSessionStatusSchema.parse(value)).toBe(value);
    }
  );

  it('rejects an unknown 6th status at the boundary', () => {
    expect(() => GameNightSessionStatusSchema.parse('Abandoned')).toThrow();
  });
});

describe('GameNightLiveDtoSchema', () => {
  it('parses a valid live payload', () => {
    const parsed = GameNightLiveDtoSchema.parse(LIVE);
    expect(parsed.sessions).toHaveLength(1);
    expect(parsed.sessions[0].status).toBe('InProgress');
  });

  it('parses a payload containing a Corrupted session without throwing', () => {
    const corrupted = {
      ...LIVE,
      sessions: [{ ...SESSION, status: 'Corrupted', startedAt: null }],
    };
    expect(() => GameNightLiveDtoSchema.parse(corrupted)).not.toThrow();
  });

  it('parses an empty-sessions Published night (happy path)', () => {
    const empty = { ...LIVE, sessions: [] };
    expect(GameNightLiveDtoSchema.parse(empty).sessions).toEqual([]);
  });

  it('throws when a session carries an unknown status (poison record fails fast)', () => {
    const poison = { ...LIVE, sessions: [{ ...SESSION, status: 'WhoKnows' }] };
    expect(() => GameNightLiveDtoSchema.parse(poison)).toThrow();
  });
});
