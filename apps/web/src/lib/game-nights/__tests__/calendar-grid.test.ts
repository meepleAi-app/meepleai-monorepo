/**
 * Tests for buildMonthGrid (Stage 3 /game-nights index v2 — Foundation).
 */

import { describe, expect, it } from 'vitest';

import { buildMonthGrid } from '../calendar-grid';

describe('buildMonthGrid', () => {
  it('returns exactly 42 cells (6 rows × 7 cols)', () => {
    const cells = buildMonthGrid(2026, 2); // March 2026
    expect(cells).toHaveLength(42);
  });

  it('March 2026 (Mar 1 = Sunday) — prefixes with prev-month tail', () => {
    const cells = buildMonthGrid(2026, 2);
    // Mar 1 2026 is Sunday; Monday-first ISO offset = (0 + 6) % 7 = 6
    // So cells[0..5] = Feb 23..28 (prev month), cells[6] = Mar 1
    expect(cells[0]).toEqual({ day: 23, otherMonth: true, monthOffset: -1 });
    expect(cells[6]).toEqual({ day: 1, otherMonth: false, monthOffset: 0 });
  });

  it('March 2026 — last cell is from next month', () => {
    const cells = buildMonthGrid(2026, 2);
    const last = cells[41];
    expect(last.otherMonth).toBe(true);
    expect(last.monthOffset).toBe(1);
  });

  it('June 2026 starts on Monday — cells[0] is day 1 of current month', () => {
    const cells = buildMonthGrid(2026, 5); // June 2026
    expect(cells[0]).toEqual({ day: 1, otherMonth: false, monthOffset: 0 });
  });

  it('all current-month cells are otherMonth:false and monthOffset:0', () => {
    const cells = buildMonthGrid(2026, 2);
    const current = cells.filter(c => !c.otherMonth);
    expect(current).toHaveLength(31); // March has 31 days
    for (const c of current) {
      expect(c.monthOffset).toBe(0);
    }
  });

  it('handles January 2026 (Jan 1 = Thursday) — 3 prev-month cells', () => {
    const cells = buildMonthGrid(2026, 0);
    // Jan 1 2026 is Thursday; offset = (4 + 6) % 7 = 3
    // prev tail = Dec 29, 30, 31
    expect(cells[0]).toEqual({ day: 29, otherMonth: true, monthOffset: -1 });
    expect(cells[1]).toEqual({ day: 30, otherMonth: true, monthOffset: -1 });
    expect(cells[2]).toEqual({ day: 31, otherMonth: true, monthOffset: -1 });
    expect(cells[3]).toEqual({ day: 1, otherMonth: false, monthOffset: 0 });
  });
});
