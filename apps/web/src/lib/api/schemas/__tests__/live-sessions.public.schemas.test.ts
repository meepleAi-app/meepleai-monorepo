import { describe, it, expect } from 'vitest';

import { PublicLiveSessionDtoSchema } from '../live-sessions.schemas';

/**
 * #2590 — the public lobby projection must be a NARROW contract: it accepts the
 * whitelisted scoreboard fields and never surfaces a sensitive field that the
 * server must not expose to an anonymous caller (userId, createdByUserId, notes, ...).
 */
describe('PublicLiveSessionDtoSchema (#2590 public lobby)', () => {
  const valid = {
    id: '11111111-1111-4111-8111-111111111111',
    sessionCode: 'ABC234',
    gameName: 'Catan',
    gameSlug: 'catan',
    status: 'InProgress',
    players: [
      {
        id: '22222222-2222-4222-8222-222222222222',
        displayName: 'Alice',
        color: 'Red',
        totalScore: 5,
        currentRank: 1,
        isActive: true,
      },
    ],
  };

  it('accepts the narrow lobby payload', () => {
    const parsed = PublicLiveSessionDtoSchema.parse(valid);
    expect(parsed.gameName).toBe('Catan');
    expect(parsed.gameSlug).toBe('catan');
    expect(parsed.players).toHaveLength(1);
    expect(parsed.players[0].displayName).toBe('Alice');
    expect(parsed.players[0].color).toBe('Red');
  });

  it('never surfaces sensitive fields even if the server includes them', () => {
    const withSensitive = {
      ...valid,
      createdByUserId: '33333333-3333-4333-8333-333333333333',
      notes: 'host private notes',
      visibility: 'Private',
      groupId: '44444444-4444-4444-8444-444444444444',
      players: [{ ...valid.players[0], userId: '55555555-5555-4555-8555-555555555555' }],
    };

    // Zod strips unknown keys (a strict schema would reject them — both are safe);
    // either way the parsed value must never expose the sensitive fields.
    const parsed = PublicLiveSessionDtoSchema.parse(withSensitive) as Record<string, unknown>;

    expect('createdByUserId' in parsed).toBe(false);
    expect('notes' in parsed).toBe(false);
    expect('visibility' in parsed).toBe(false);
    expect('groupId' in parsed).toBe(false);
    expect('userId' in (parsed.players as Record<string, unknown>[])[0]).toBe(false);
  });

  it('rejects a payload missing required fields', () => {
    expect(() => PublicLiveSessionDtoSchema.parse({ id: valid.id })).toThrow();
  });
});
