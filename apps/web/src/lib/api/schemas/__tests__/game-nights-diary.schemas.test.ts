import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { parseGameNightDiaryResilient } from '@/lib/api/schemas/game-nights.schemas';

const GN = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
const validEntry = (over: Record<string, unknown> = {}) => ({
  id: '11111111-1111-4111-8111-111111111111',
  sessionId: '22222222-2222-4222-8222-222222222222',
  eventType: 'score_updated',
  description: '📊 Punteggio aggiornato',
  payload: null,
  actorId: null,
  timestamp: '2026-07-04T20:00:00',
  ...over,
});

describe('parseGameNightDiaryResilient (panel D8)', () => {
  beforeEach(() => vi.spyOn(console, 'warn').mockImplementation(() => undefined));
  afterEach(() => vi.restoreAllMocks());

  it('parses a well-formed diary', () => {
    const result = parseGameNightDiaryResilient({ gameNightId: GN, entries: [validEntry()] });
    expect(result.gameNightId).toBe(GN);
    expect(result.entries).toHaveLength(1);
  });

  it('accepts an OPEN eventType (a new BE type is NOT rejected)', () => {
    const result = parseGameNightDiaryResilient({
      gameNightId: GN,
      entries: [validEntry({ eventType: 'brand_new_event_type' })],
    });
    expect(result.entries).toHaveLength(1);
    expect(result.entries[0].eventType).toBe('brand_new_event_type');
  });

  it('skips ONE malformed entry without dropping the whole array', () => {
    const result = parseGameNightDiaryResilient({
      gameNightId: GN,
      entries: [
        validEntry(),
        { id: 'not-a-uuid', garbage: true },
        validEntry({ id: '33333333-3333-4333-8333-333333333333' }),
      ],
    });
    expect(result.entries).toHaveLength(2);
    expect(console.warn).toHaveBeenCalled();
  });

  it('degrades a malformed envelope to an empty diary (no throw)', () => {
    expect(() => parseGameNightDiaryResilient({ nope: 1 })).not.toThrow();
    const result = parseGameNightDiaryResilient({ nope: 1 });
    expect(result.entries).toEqual([]);
  });

  it('degrades null/undefined to an empty diary (no throw)', () => {
    expect(parseGameNightDiaryResilient(null).entries).toEqual([]);
    expect(parseGameNightDiaryResilient(undefined).entries).toEqual([]);
  });
});
