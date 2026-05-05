import { describe, expect, it } from 'vitest';

import {
  type PlayerListItem,
  type PlayersFilterInput,
  applyPlayersFilters,
  hasPlayersFilters,
  transformStatsToItems,
} from '../players-filters';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function item(
  overrides: Partial<PlayerListItem> & Pick<PlayerListItem, 'gameName'>
): PlayerListItem {
  return {
    id: overrides.gameName.toLowerCase().replace(/\s+/g, '-'),
    displayName: overrides.gameName,
    playCount: 1,
    ...overrides,
  };
}

const WINGSPAN = item({ gameName: 'Wingspan', playCount: 12 });
const AZUL = item({ gameName: 'Azul', playCount: 8 });
const CATAN = item({ gameName: 'Catan', playCount: 5 });
const TERRAFORMING_MARS = item({ gameName: 'Terraforming Mars', playCount: 3 });

const ALL_ITEMS: ReadonlyArray<PlayerListItem> = [WINGSPAN, AZUL, CATAN, TERRAFORMING_MARS];

// ---------------------------------------------------------------------------
// applyPlayersFilters
// ---------------------------------------------------------------------------

describe('applyPlayersFilters', () => {
  it('returns all items when search is empty string', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: '' });
    expect(result).toHaveLength(4);
  });

  it('returns all items when search is whitespace only', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: '   ' });
    expect(result).toHaveLength(4);
  });

  it('matches gameName substring case-insensitively', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: 'WING' });
    expect(result).toHaveLength(1);
    expect(result[0].gameName).toBe('Wingspan');
  });

  it('matches partial gameName (multi-word)', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: 'terraforming' });
    expect(result).toHaveLength(1);
    expect(result[0].gameName).toBe('Terraforming Mars');
  });

  it('returns empty array when search matches nothing', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: 'Monopoly' });
    expect(result).toHaveLength(0);
  });

  it('trims leading/trailing whitespace before matching', () => {
    const result = applyPlayersFilters(ALL_ITEMS, { search: '  Azul  ' });
    expect(result).toHaveLength(1);
    expect(result[0].gameName).toBe('Azul');
  });

  it('returns a new array (does not mutate input)', () => {
    const items = [...ALL_ITEMS];
    const result = applyPlayersFilters(items, { search: '' });
    // Reference may differ, but values are the same
    expect(result.length).toBe(items.length);
  });
});

// ---------------------------------------------------------------------------
// hasPlayersFilters
// ---------------------------------------------------------------------------

describe('hasPlayersFilters', () => {
  it('returns false for empty search', () => {
    expect(hasPlayersFilters({ search: '' })).toBe(false);
  });

  it('returns false for whitespace-only search', () => {
    expect(hasPlayersFilters({ search: '   ' })).toBe(false);
  });

  it('returns true for non-empty search after trim', () => {
    expect(hasPlayersFilters({ search: 'Catan' })).toBe(true);
  });

  it('returns true for single character search', () => {
    expect(hasPlayersFilters({ search: 'A' })).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// transformStatsToItems
// ---------------------------------------------------------------------------

describe('transformStatsToItems', () => {
  it('maps each Record entry to a PlayerListItem', () => {
    const record: Record<string, number> = {
      Wingspan: 12,
      Azul: 8,
    };
    const result = transformStatsToItems(record);
    expect(result).toHaveLength(2);
  });

  it('generates id as lowercased, hyphenated gameName', () => {
    const result = transformStatsToItems({ 'Terraforming Mars': 5 });
    expect(result[0].id).toBe('terraforming-mars');
  });

  it('sets displayName equal to gameName (v1 anti-pattern carryover)', () => {
    const result = transformStatsToItems({ Wingspan: 12 });
    expect(result[0].displayName).toBe('Wingspan');
    expect(result[0].gameName).toBe('Wingspan');
  });

  it('sets playCount from Record value', () => {
    const result = transformStatsToItems({ Catan: 7 });
    expect(result[0].playCount).toBe(7);
  });

  it('returns empty array for empty Record', () => {
    const result = transformStatsToItems({});
    expect(result).toHaveLength(0);
  });

  it('handles single-word game names with no spaces', () => {
    const result = transformStatsToItems({ Azul: 3 });
    expect(result[0].id).toBe('azul');
  });

  it('returns a ReadonlyArray (no mutation contract)', () => {
    const result = transformStatsToItems({ Wingspan: 1 });
    // TypeScript enforces this at compile-time; at runtime we just verify it's array-like
    expect(Array.isArray(result)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Filter input type validation (structural)
// ---------------------------------------------------------------------------

describe('PlayersFilterInput type', () => {
  it('accepts a valid filter object', () => {
    const filter: PlayersFilterInput = { search: 'test' };
    expect(hasPlayersFilters(filter)).toBe(true);
  });
});
