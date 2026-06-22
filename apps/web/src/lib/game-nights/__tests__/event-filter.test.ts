/**
 * Tests for filterEvents + isFilterKey (Stage 3 /game-nights index v2 — Foundation).
 */

import { describe, expect, it } from 'vitest';

import { FILTER_KEYS, filterEvents, isFilterKey } from '../event-filter';
import type { GameNightVM } from '../view-model';

function vm(overrides: Partial<GameNightVM> = {}): GameNightVM {
  return {
    id: 'a',
    title: 'Test',
    scheduledAtIso: '2026-06-12T19:00:00',
    day: 12,
    month: 5,
    year: 2026,
    timeLabel: '19:00',
    durationLabel: '',
    location: '',
    gameIds: [],
    playerIds: [],
    role: 'organizer',
    statusKey: 'planned',
    ...overrides,
  };
}

describe('isFilterKey', () => {
  it('accepts each canonical key', () => {
    for (const k of FILTER_KEYS) {
      expect(isFilterKey(k)).toBe(true);
    }
  });

  it('rejects unknown string', () => {
    expect(isFilterKey('bogus')).toBe(false);
  });

  it('rejects non-strings', () => {
    expect(isFilterKey(null)).toBe(false);
    expect(isFilterKey(undefined)).toBe(false);
    expect(isFilterKey(123)).toBe(false);
    expect(isFilterKey({})).toBe(false);
  });
});

describe('filterEvents', () => {
  const a = vm({ id: 'a', role: 'organizer', statusKey: 'planned' });
  const b = vm({ id: 'b', role: 'invited', statusKey: 'confirmed' });
  const c = vm({ id: 'c', role: 'organizer', statusKey: 'completed' });
  const d = vm({ id: 'd', role: 'invited', statusKey: 'completed' });
  const all = [a, b, c, d];

  it('all → returns all events (shallow copy, not same reference)', () => {
    const out = filterEvents(all, 'all');
    expect(out).toEqual(all);
    expect(out).not.toBe(all);
  });

  it('organizing → only role=organizer', () => {
    expect(filterEvents(all, 'organizing')).toEqual([a, c]);
  });

  it('invited → only role=invited', () => {
    expect(filterEvents(all, 'invited')).toEqual([b, d]);
  });

  it('completed → only statusKey=completed', () => {
    expect(filterEvents(all, 'completed')).toEqual([c, d]);
  });
});
