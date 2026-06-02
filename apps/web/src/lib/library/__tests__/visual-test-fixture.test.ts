/**
 * Unit tests for `library/visual-test-fixture` URL-override hatch (issue #1714).
 *
 * Covers the `parseLibraryStateOverride` + `libraryFixtures` additions made
 * in PR follow-up #1700. The existing `tryLoadVisualTestFixture` /
 * `IS_VISUAL_TEST_BUILD` constant are NOT re-tested here — those were
 * shipped in Wave B.3 (PR #638) and have not changed.
 */

import { describe, expect, it } from 'vitest';

import {
  libraryFixtureKindSchema,
  libraryFixtures,
  parseLibraryStateOverride,
  STATE_OVERRIDE_ENABLED,
} from '../visual-test-fixture';

describe('STATE_OVERRIDE_ENABLED', () => {
  it('is true in the unit-test environment (NODE_ENV !== "production")', () => {
    // Vitest sets NODE_ENV=test by default. The constant is computed at
    // module-load time so a single boolean read is sufficient.
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
  });
});

describe('libraryFixtureKindSchema', () => {
  it('accepts "default"', () => {
    expect(libraryFixtureKindSchema.safeParse('default').success).toBe(true);
  });

  it('accepts "empty"', () => {
    expect(libraryFixtureKindSchema.safeParse('empty').success).toBe(true);
  });

  it('rejects unknown values', () => {
    expect(libraryFixtureKindSchema.safeParse('loading').success).toBe(false);
    expect(libraryFixtureKindSchema.safeParse('filtered-empty').success).toBe(false);
    expect(libraryFixtureKindSchema.safeParse('').success).toBe(false);
  });
});

describe('libraryFixtures', () => {
  it('has the 2 expected kinds', () => {
    expect(Object.keys(libraryFixtures).sort()).toEqual(['default', 'empty']);
  });

  it('default has >= 3 entries (covers grid 3-col layout + reduced-motion hover)', () => {
    expect(libraryFixtures.default.items.length).toBeGreaterThanOrEqual(3);
  });

  it('default totalCount matches items.length', () => {
    expect(libraryFixtures.default.totalCount).toBe(libraryFixtures.default.items.length);
  });

  it('default has totalPages=1 (single page response)', () => {
    expect(libraryFixtures.default.totalPages).toBe(1);
    expect(libraryFixtures.default.hasNextPage).toBe(false);
    expect(libraryFixtures.default.hasPreviousPage).toBe(false);
  });

  it('empty has zero entries', () => {
    expect(libraryFixtures.empty.items).toHaveLength(0);
    expect(libraryFixtures.empty.totalCount).toBe(0);
    expect(libraryFixtures.empty.totalPages).toBe(0);
  });

  it('default entries provide all fields MeepleCard grid render needs', () => {
    for (const entry of libraryFixtures.default.items) {
      expect(entry.gameId).toEqual(expect.any(String));
      expect(entry.gameTitle).toEqual(expect.any(String));
      expect(entry.gameTitle.length).toBeGreaterThan(0);
      // gameImageUrl + gamePublisher MAY be null but the field must exist.
      expect(entry).toHaveProperty('gameImageUrl');
      expect(entry).toHaveProperty('gamePublisher');
      expect(entry).toHaveProperty('currentState');
      expect(entry.addedAt).toEqual(expect.any(String));
    }
  });
});

describe('parseLibraryStateOverride', () => {
  it('returns null when input is null/undefined', () => {
    expect(parseLibraryStateOverride(null)).toBeNull();
    expect(parseLibraryStateOverride(undefined)).toBeNull();
  });

  it('returns null when input is empty string', () => {
    expect(parseLibraryStateOverride('')).toBeNull();
  });

  it('parses raw string "default"', () => {
    expect(parseLibraryStateOverride('default')).toBe('default');
  });

  it('parses raw string "empty"', () => {
    expect(parseLibraryStateOverride('empty')).toBe('empty');
  });

  it('returns null on unknown string', () => {
    expect(parseLibraryStateOverride('loading')).toBeNull();
    expect(parseLibraryStateOverride('garbage')).toBeNull();
  });

  it('extracts the fixture key from a URLSearchParams', () => {
    const params = new URLSearchParams('fixture=default&other=ignored');
    expect(parseLibraryStateOverride(params)).toBe('default');
  });

  it('returns null when URLSearchParams has no `fixture` key', () => {
    const params = new URLSearchParams('state=filtered-empty');
    expect(parseLibraryStateOverride(params)).toBeNull();
  });

  it('respects empty URLSearchParams', () => {
    expect(parseLibraryStateOverride(new URLSearchParams())).toBeNull();
  });
});
