/**
 * Tests for groupByMonth (Stage 3 /game-nights index v2 — Foundation).
 */

import { describe, expect, it } from 'vitest';

import { groupByMonth } from '../event-grouping';
import type { GameNightVM } from '../view-model';

function vm(overrides: Partial<GameNightVM> = {}): GameNightVM {
  return {
    id: 'x',
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

describe('groupByMonth', () => {
  it('empty input → []', () => {
    expect(groupByMonth([])).toEqual([]);
  });

  it('groups events by (year, month) and sorts newest-first', () => {
    const now = new Date(2026, 5, 15); // June 15 2026 (current)
    const events: GameNightVM[] = [
      vm({ id: 'jan', year: 2026, month: 0, day: 5 }),
      vm({ id: 'jun', year: 2026, month: 5, day: 20 }),
      vm({ id: 'mar', year: 2026, month: 2, day: 10 }),
    ];
    const groups = groupByMonth(events, now);
    expect(groups.map(g => `${g.year}-${g.month}`)).toEqual(['2026-5', '2026-2', '2026-0']);
  });

  it('current/future month items sorted ASC by day', () => {
    const now = new Date(2026, 5, 1);
    const events: GameNightVM[] = [
      vm({ id: 'late', year: 2026, month: 5, day: 28 }),
      vm({ id: 'mid', year: 2026, month: 5, day: 15 }),
      vm({ id: 'early', year: 2026, month: 5, day: 3 }),
    ];
    const [group] = groupByMonth(events, now);
    expect(group.items.map(i => i.id)).toEqual(['early', 'mid', 'late']);
  });

  it('past month items sorted DESC by day', () => {
    const now = new Date(2026, 5, 15);
    const events: GameNightVM[] = [
      vm({ id: 'mar-3', year: 2026, month: 2, day: 3 }),
      vm({ id: 'mar-25', year: 2026, month: 2, day: 25 }),
      vm({ id: 'mar-10', year: 2026, month: 2, day: 10 }),
    ];
    const [group] = groupByMonth(events, now);
    expect(group.items.map(i => i.id)).toEqual(['mar-25', 'mar-10', 'mar-3']);
  });

  it('sorts across year boundaries (newest year first)', () => {
    const now = new Date(2026, 5, 15);
    const events: GameNightVM[] = [
      vm({ id: '2025-dec', year: 2025, month: 11, day: 31 }),
      vm({ id: '2026-jan', year: 2026, month: 0, day: 1 }),
    ];
    const groups = groupByMonth(events, now);
    expect(groups.map(g => `${g.year}-${g.month}`)).toEqual(['2026-0', '2025-11']);
  });

  it('groups items into the right buckets', () => {
    const now = new Date(2026, 5, 15);
    const events: GameNightVM[] = [
      vm({ id: 'a', year: 2026, month: 5, day: 10 }),
      vm({ id: 'b', year: 2026, month: 5, day: 20 }),
      vm({ id: 'c', year: 2026, month: 2, day: 1 }),
    ];
    const groups = groupByMonth(events, now);
    expect(groups).toHaveLength(2);
    const june = groups.find(g => g.month === 5);
    const march = groups.find(g => g.month === 2);
    expect(june?.items.map(i => i.id).sort()).toEqual(['a', 'b']);
    expect(march?.items.map(i => i.id)).toEqual(['c']);
  });
});
