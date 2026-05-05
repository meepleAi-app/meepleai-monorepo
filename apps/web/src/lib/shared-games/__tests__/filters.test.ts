import { describe, expect, it } from 'vitest';

import {
  FILTER_CHIPS,
  GENRES,
  SORT_OPTIONS,
  buildCategoryNameMap,
  genreKeyToCategoryIds,
  sortKeyToBackendParams,
} from '../filters';

describe('FILTER_CHIPS', () => {
  it('exposes the 4 chips required by the sp3-shared-games mockup', () => {
    expect(FILTER_CHIPS.map(c => c.key)).toEqual([
      'with-toolkit',
      'with-agent',
      'top-rated',
      'new',
    ]);
  });
});

describe('SORT_OPTIONS', () => {
  it('exposes the 4 sort keys required by the spec §3.2', () => {
    expect(SORT_OPTIONS.map(s => s.key)).toEqual(['rating', 'contrib', 'new', 'title']);
  });
});

describe('sortKeyToBackendParams', () => {
  it('maps rating → AverageRating desc', () => {
    expect(sortKeyToBackendParams('rating')).toEqual({
      sortBy: 'AverageRating',
      sortDescending: true,
    });
  });

  it('maps contrib → Contrib desc (most contributors first)', () => {
    expect(sortKeyToBackendParams('contrib')).toEqual({
      sortBy: 'Contrib',
      sortDescending: true,
    });
  });

  it('maps new → New desc (most recent first)', () => {
    expect(sortKeyToBackendParams('new')).toEqual({
      sortBy: 'New',
      sortDescending: true,
    });
  });

  it('maps title → Title asc (alphabetical default)', () => {
    expect(sortKeyToBackendParams('title')).toEqual({
      sortBy: 'Title',
      sortDescending: false,
    });
  });
});

describe('GENRES', () => {
  it('starts with the "all" sentinel having no matchNames (resolves to []) ', () => {
    const all = GENRES.find(g => g.key === 'all');
    expect(all).toBeDefined();
    expect(all!.matchNames).toEqual([]);
  });

  it('covers strategy, family, party, cooperative, thematic, wargame', () => {
    const keys = GENRES.map(g => g.key);
    expect(keys).toContain('strategy');
    expect(keys).toContain('family');
    expect(keys).toContain('party');
    expect(keys).toContain('cooperative');
    expect(keys).toContain('thematic');
    expect(keys).toContain('wargame');
  });
});

describe('buildCategoryNameMap', () => {
  it('builds a Map from id+name pairs', () => {
    const map = buildCategoryNameMap([
      { id: 'aaaa', name: 'Strategy' },
      { id: 'bbbb', name: 'Family Game' },
    ]);
    expect(map.get('Strategy')).toBe('aaaa');
    expect(map.get('Family Game')).toBe('bbbb');
    expect(map.get('Unknown')).toBeUndefined();
  });
});

describe('genreKeyToCategoryIds', () => {
  const map = buildCategoryNameMap([
    { id: 'g-strategy-1', name: 'Strategy' },
    { id: 'g-strategy-2', name: 'Strategy Game' },
    { id: 'g-family', name: 'Family' },
    { id: 'g-coop', name: 'Cooperative' },
  ]);

  it('returns [] for the "all" sentinel', () => {
    expect(genreKeyToCategoryIds('all', map)).toEqual([]);
  });

  it('returns [] for empty key', () => {
    expect(genreKeyToCategoryIds('', map)).toEqual([]);
  });

  it('returns [] for unknown key (no entry in GENRES)', () => {
    expect(genreKeyToCategoryIds('not-a-genre', map)).toEqual([]);
  });

  it('resolves multiple matchNames to multiple categoryIds (strategy)', () => {
    const result = genreKeyToCategoryIds('strategy', map);
    expect(result).toContain('g-strategy-1');
    expect(result).toContain('g-strategy-2');
    expect(result).toHaveLength(2);
  });

  it('skips matchNames not present in the lookup map (cooperative also matches "Co-op")', () => {
    // Only "Cooperative" present; "Co-op" missing.
    const result = genreKeyToCategoryIds('cooperative', map);
    expect(result).toEqual(['g-coop']);
  });

  it('returns [] when the genre exists but no names match (thematic with empty map)', () => {
    expect(genreKeyToCategoryIds('thematic', map)).toEqual([]);
  });
});
