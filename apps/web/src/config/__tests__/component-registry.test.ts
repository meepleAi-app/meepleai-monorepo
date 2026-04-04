import { describe, it, expect } from 'vitest';

import {
  COMPONENT_REGISTRY,
  getRegistryEntry,
  filterRegistry,
  getCategories,
  getAreas,
} from '../component-registry';

// ─── COMPONENT_REGISTRY validation ──────────────────────────────────────────

describe('COMPONENT_REGISTRY', () => {
  it('has at least 150 entries', () => {
    expect(COMPONENT_REGISTRY.length).toBeGreaterThanOrEqual(150);
  });

  it('every entry has a non-empty id', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(typeof entry.id).toBe('string');
      expect(entry.id.trim().length).toBeGreaterThan(0);
    }
  });

  it('every entry has a non-empty name', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(typeof entry.name).toBe('string');
      expect(entry.name.trim().length).toBeGreaterThan(0);
    }
  });

  it('every entry has a non-empty importPath', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(typeof entry.importPath).toBe('string');
      expect(entry.importPath.trim().length).toBeGreaterThan(0);
    }
  });

  it('every entry has a valid category', () => {
    const validCategories = [
      'Data Display',
      'Navigation',
      'Feedback',
      'Tags',
      'Animations',
      'Gates',
      'Forms',
      'Charts',
      'Layout',
      'Overlays',
      'Meeple',
      'Agent',
    ];
    for (const entry of COMPONENT_REGISTRY) {
      expect(validCategories).toContain(entry.category);
    }
  });

  it('every entry has a non-empty areas array', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(Array.isArray(entry.areas)).toBe(true);
      expect(entry.areas.length).toBeGreaterThan(0);
    }
  });

  it('every entry has tier set to "interactive" or "static"', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(['interactive', 'static']).toContain(entry.tier);
    }
  });

  it('every entry has a non-empty description', () => {
    for (const entry of COMPONENT_REGISTRY) {
      expect(typeof entry.description).toBe('string');
      expect(entry.description.trim().length).toBeGreaterThan(0);
    }
  });

  it('has no duplicate ids', () => {
    const ids = COMPONENT_REGISTRY.map(e => e.id);
    const uniqueIds = new Set(ids);
    expect(uniqueIds.size).toBe(ids.length);
  });
});

// ─── getRegistryEntry ────────────────────────────────────────────────────────

describe('getRegistryEntry', () => {
  it('finds an existing entry by id (meeple-card)', () => {
    const entry = getRegistryEntry('meeple-card');
    expect(entry).toBeDefined();
    expect(entry!.id).toBe('meeple-card');
    expect(entry!.name).toBe('MeepleCard');
  });

  it('returns undefined for a nonexistent id', () => {
    const entry = getRegistryEntry('__this-id-does-not-exist__');
    expect(entry).toBeUndefined();
  });
});

// ─── filterRegistry ──────────────────────────────────────────────────────────

describe('filterRegistry', () => {
  it('filters by category "Data Display"', () => {
    const results = filterRegistry({ category: 'Data Display' });
    expect(results.length).toBeGreaterThan(0);
    for (const entry of results) {
      expect(entry.category).toBe('Data Display');
    }
  });

  it('filters by area "admin"', () => {
    const results = filterRegistry({ area: 'admin' });
    expect(results.length).toBeGreaterThan(0);
    for (const entry of results) {
      expect(entry.areas).toContain('admin');
    }
  });

  it('filters by tier "interactive"', () => {
    const results = filterRegistry({ tier: 'interactive' });
    expect(results.length).toBeGreaterThan(0);
    for (const entry of results) {
      expect(entry.tier).toBe('interactive');
    }
  });

  it('filters by search term "card"', () => {
    const results = filterRegistry({ search: 'card' });
    expect(results.length).toBeGreaterThan(0);
    // Every result should match 'card' in name, description, or tags
    for (const entry of results) {
      const matchesName = entry.name.toLowerCase().includes('card');
      const matchesDescription = entry.description.toLowerCase().includes('card');
      const matchesTags = entry.tags?.some(t => t.includes('card')) ?? false;
      expect(matchesName || matchesDescription || matchesTags).toBe(true);
    }
  });

  it('combines category and tier filters', () => {
    const results = filterRegistry({ category: 'Data Display', tier: 'interactive' });
    expect(results.length).toBeGreaterThan(0);
    for (const entry of results) {
      expect(entry.category).toBe('Data Display');
      expect(entry.tier).toBe('interactive');
    }
  });

  it('combines area and search filters', () => {
    const results = filterRegistry({ area: 'shared', search: 'card' });
    expect(results.length).toBeGreaterThan(0);
    for (const entry of results) {
      expect(entry.areas).toContain('shared');
    }
  });

  it('returns empty array when no entries match', () => {
    const results = filterRegistry({ search: '__zzz-no-match-xyz__' });
    expect(results).toHaveLength(0);
  });

  it('returns all entries when no filters are provided', () => {
    const results = filterRegistry({});
    expect(results.length).toBe(COMPONENT_REGISTRY.length);
  });
});

// ─── getCategories ───────────────────────────────────────────────────────────

describe('getCategories', () => {
  it('returns all used categories with counts greater than 0', () => {
    const categories = getCategories();
    expect(categories.length).toBeGreaterThan(0);
    for (const item of categories) {
      expect(item.count).toBeGreaterThan(0);
    }
  });

  it('every returned category exists in the registry', () => {
    const categories = getCategories();
    const registryCategories = new Set(COMPONENT_REGISTRY.map(e => e.category));
    for (const item of categories) {
      expect(registryCategories.has(item.category)).toBe(true);
    }
  });

  it('counts sum to at least the registry length', () => {
    const categories = getCategories();
    const total = categories.reduce((sum, item) => sum + item.count, 0);
    expect(total).toBe(COMPONENT_REGISTRY.length);
  });

  it('includes "Data Display" category', () => {
    const categories = getCategories();
    const dataDisplay = categories.find(c => c.category === 'Data Display');
    expect(dataDisplay).toBeDefined();
    expect(dataDisplay!.count).toBeGreaterThan(0);
  });
});

// ─── getAreas ────────────────────────────────────────────────────────────────

describe('getAreas', () => {
  it('returns areas including "shared"', () => {
    const areas = getAreas();
    const areaNames = areas.map(a => a.area);
    expect(areaNames).toContain('shared');
  });

  it('returns areas including "admin"', () => {
    const areas = getAreas();
    const areaNames = areas.map(a => a.area);
    expect(areaNames).toContain('admin');
  });

  it('every returned area has count greater than 0', () => {
    const areas = getAreas();
    expect(areas.length).toBeGreaterThan(0);
    for (const item of areas) {
      expect(item.count).toBeGreaterThan(0);
    }
  });

  it('every returned area exists in at least one registry entry', () => {
    const areas = getAreas();
    for (const item of areas) {
      const hasArea = COMPONENT_REGISTRY.some(e => e.areas.includes(item.area));
      expect(hasArea).toBe(true);
    }
  });
});
