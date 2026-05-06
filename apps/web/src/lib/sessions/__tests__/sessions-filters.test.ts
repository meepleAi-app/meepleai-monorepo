import { describe, expect, it } from 'vitest';

import {
  type SessionListItem,
  type SessionStatusFilter,
  type SessionViewMode,
  applySearchFilter,
  applyStatusFilter,
  formatDuration,
  parseStatusFilter,
  parseViewMode,
} from '../sessions-filters';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeSession(
  overrides: Partial<SessionListItem> & Pick<SessionListItem, 'id' | 'gameName' | 'status'>
): SessionListItem {
  return {
    date: '23 apr 2026',
    when: '2 giorni fa',
    duration: '1h 24m',
    outcome: null,
    playerCount: 2,
    scores: [],
    hasChat: false,
    ...overrides,
  };
}

const WINGSPAN_COMPLETED = makeSession({
  id: 's1',
  gameName: 'Wingspan',
  status: 'completed',
  outcome: 'won',
});
const AZUL_COMPLETED = makeSession({
  id: 's2',
  gameName: 'Azul',
  status: 'completed',
  outcome: 'lost',
});
const BRASS_INPROGRESS = makeSession({
  id: 's3',
  gameName: 'Brass: Birmingham',
  status: 'inprogress',
  outcome: null,
});
const WINGSPAN_PAUSED = makeSession({
  id: 's4',
  gameName: 'Wingspan',
  status: 'paused',
  outcome: null,
  paused: true,
});
const WONDERS_ABANDONED = makeSession({
  id: 's5',
  gameName: '7 Wonders',
  status: 'abandoned',
  outcome: null,
});

const ALL_ITEMS: ReadonlyArray<SessionListItem> = [
  WINGSPAN_COMPLETED,
  AZUL_COMPLETED,
  BRASS_INPROGRESS,
  WINGSPAN_PAUSED,
  WONDERS_ABANDONED,
];

// ---------------------------------------------------------------------------
// applyStatusFilter
// ---------------------------------------------------------------------------

describe('applyStatusFilter', () => {
  it("returns the same reference when filter='all'", () => {
    const result = applyStatusFilter(ALL_ITEMS, 'all');
    expect(result).toBe(ALL_ITEMS);
    expect(result).toHaveLength(5);
  });

  it("filter='active' returns inprogress + paused sessions only", () => {
    const result = applyStatusFilter(ALL_ITEMS, 'active');
    expect(result).toHaveLength(2);
    const statuses = result.map(s => s.status);
    expect(statuses).toContain('inprogress');
    expect(statuses).toContain('paused');
    expect(statuses).not.toContain('completed');
    expect(statuses).not.toContain('abandoned');
  });

  it("filter='completed' returns only completed sessions", () => {
    const result = applyStatusFilter(ALL_ITEMS, 'completed');
    expect(result).toHaveLength(2);
    result.forEach(s => expect(s.status).toBe('completed'));
  });

  it("filter='abandoned' returns only abandoned sessions", () => {
    const result = applyStatusFilter(ALL_ITEMS, 'abandoned');
    expect(result).toHaveLength(1);
    expect(result[0].id).toBe('s5');
  });

  it("filter='active' returns empty when no active sessions exist", () => {
    const completedOnly: ReadonlyArray<SessionListItem> = [WINGSPAN_COMPLETED, AZUL_COMPLETED];
    const result = applyStatusFilter(completedOnly, 'active');
    expect(result).toHaveLength(0);
  });

  it("filter='completed' returns empty when no completed sessions", () => {
    const activeOnly: ReadonlyArray<SessionListItem> = [BRASS_INPROGRESS, WINGSPAN_PAUSED];
    const result = applyStatusFilter(activeOnly, 'completed');
    expect(result).toHaveLength(0);
  });

  it('handles empty input array gracefully', () => {
    const result = applyStatusFilter([], 'active');
    expect(result).toHaveLength(0);
  });

  it("filter='all' on empty array returns empty array", () => {
    const result = applyStatusFilter([], 'all');
    expect(result).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// applySearchFilter
// ---------------------------------------------------------------------------

describe('applySearchFilter', () => {
  it('returns same reference when query is empty string', () => {
    const result = applySearchFilter(ALL_ITEMS, '');
    expect(result).toBe(ALL_ITEMS);
  });

  it('returns same reference when query is whitespace only', () => {
    const result = applySearchFilter(ALL_ITEMS, '   ');
    expect(result).toBe(ALL_ITEMS);
  });

  it('matches gameName substring case-insensitively', () => {
    const result = applySearchFilter(ALL_ITEMS, 'WING');
    expect(result).toHaveLength(2); // Wingspan completed + Wingspan paused
    result.forEach(s => expect(s.gameName).toBe('Wingspan'));
  });

  it('matches partial gameName for multi-word game names', () => {
    const result = applySearchFilter(ALL_ITEMS, 'brass');
    expect(result).toHaveLength(1);
    expect(result[0].gameName).toBe('Brass: Birmingham');
  });

  it('returns empty array when search matches nothing', () => {
    const result = applySearchFilter(ALL_ITEMS, 'Catan');
    expect(result).toHaveLength(0);
  });

  it('trims leading/trailing whitespace before matching', () => {
    const result = applySearchFilter(ALL_ITEMS, '  Azul  ');
    expect(result).toHaveLength(1);
    expect(result[0].gameName).toBe('Azul');
  });
});

// ---------------------------------------------------------------------------
// parseStatusFilter
// ---------------------------------------------------------------------------

describe('parseStatusFilter', () => {
  it("returns 'all' for null input", () => {
    expect(parseStatusFilter(null)).toBe('all');
  });

  it("returns 'all' for undefined input", () => {
    expect(parseStatusFilter(undefined)).toBe('all');
  });

  it("returns 'all' for empty string", () => {
    expect(parseStatusFilter('')).toBe('all');
  });

  it("returns 'all' for invalid value", () => {
    expect(parseStatusFilter('invalid')).toBe('all');
  });

  it("returns 'all' for valid 'all' value", () => {
    expect(parseStatusFilter('all')).toBe('all');
  });

  it("returns 'active' for valid 'active' value", () => {
    expect(parseStatusFilter('active')).toBe('active');
  });

  it("returns 'completed' for valid 'completed' value", () => {
    expect(parseStatusFilter('completed')).toBe('completed');
  });

  it("returns 'abandoned' for valid 'abandoned' value", () => {
    expect(parseStatusFilter('abandoned')).toBe('abandoned');
  });
});

// ---------------------------------------------------------------------------
// parseViewMode
// ---------------------------------------------------------------------------

describe('parseViewMode', () => {
  it("returns 'list' for null input (default)", () => {
    expect(parseViewMode(null)).toBe('list');
  });

  it("returns 'list' for undefined input", () => {
    expect(parseViewMode(undefined)).toBe('list');
  });

  it("returns 'list' for empty string", () => {
    expect(parseViewMode('')).toBe('list');
  });

  it("returns 'list' for invalid value", () => {
    expect(parseViewMode('table')).toBe('list');
  });

  it("returns 'list' for explicit 'list' value", () => {
    expect(parseViewMode('list')).toBe('list');
  });

  it("returns 'grid' for valid 'grid' value", () => {
    expect(parseViewMode('grid')).toBe('grid');
  });
});

// ---------------------------------------------------------------------------
// formatDuration
// ---------------------------------------------------------------------------

describe('formatDuration', () => {
  it('formats 0 minutes as "0m"', () => {
    expect(formatDuration(0)).toBe('0m');
  });

  it('formats sub-hour as minutes only', () => {
    expect(formatDuration(45)).toBe('45m');
  });

  it('formats exact hours with no minutes', () => {
    expect(formatDuration(60)).toBe('1h');
  });

  it('formats hours and minutes together', () => {
    expect(formatDuration(84)).toBe('1h 24m');
  });

  it('formats 2h 30m correctly', () => {
    expect(formatDuration(150)).toBe('2h 30m');
  });

  it('formats negative minutes as "0m"', () => {
    expect(formatDuration(-5)).toBe('0m');
  });
});

// ---------------------------------------------------------------------------
// SessionStatusFilter type validation (structural)
// ---------------------------------------------------------------------------

describe('SessionStatusFilter type', () => {
  it('accepts all valid status filter values', () => {
    const filters: SessionStatusFilter[] = ['all', 'active', 'completed', 'abandoned'];
    filters.forEach(f => {
      expect(() => applyStatusFilter(ALL_ITEMS, f)).not.toThrow();
    });
  });
});

// ---------------------------------------------------------------------------
// SessionViewMode type validation (structural)
// ---------------------------------------------------------------------------

describe('SessionViewMode type', () => {
  it('accepts both valid view mode values', () => {
    const modes: SessionViewMode[] = ['list', 'grid'];
    modes.forEach(m => {
      expect(parseViewMode(m)).toBe(m);
    });
  });
});
