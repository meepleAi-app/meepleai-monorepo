/**
 * Unit tests for gamebook-index visual fixture (SP6 Phase B Task 1).
 */

import { describe, expect, it } from 'vitest';

import {
  gamebookCardDataSchema,
  gamebookIndexFixtureKindSchema,
  quotaInfoSchema,
} from '../schemas';
import {
  STATE_OVERRIDE_ENABLED,
  gamebookIndexFixtures,
  parseStateOverride,
} from '../visual-test-fixture';

// ---------------------------------------------------------------------------
// Fixture record
// ---------------------------------------------------------------------------

describe('gamebookIndexFixtures', () => {
  it('exposes all 6 fixture kinds', () => {
    const kinds = Object.keys(gamebookIndexFixtures).sort();
    expect(kinds).toEqual(
      ['default', 'empty', 'error', 'loading', 'quota-hard', 'quota-soft'].sort()
    );
  });

  it('every fixture validates against the schema', () => {
    for (const [kind, fixture] of Object.entries(gamebookIndexFixtures)) {
      // Validate quota
      const quotaResult = quotaInfoSchema.safeParse(fixture.quota);
      if (!quotaResult.success) {
        throw new Error(`Fixture "${kind}" quota invalid: ${quotaResult.error.message}`);
      }
      // Validate every gamebook
      for (const gb of fixture.gamebooks) {
        const gbResult = gamebookCardDataSchema.safeParse(gb);
        if (!gbResult.success) {
          throw new Error(
            `Fixture "${kind}" gamebook "${gb.title}" invalid: ${gbResult.error.message}`
          );
        }
      }
    }
  });

  it('default fixture has 4 gamebooks (mockup parity)', () => {
    expect(gamebookIndexFixtures.default.gamebooks).toHaveLength(4);
  });

  it('empty fixture has 0 gamebooks', () => {
    expect(gamebookIndexFixtures.empty.gamebooks).toHaveLength(0);
  });

  it('quota-soft fixture has used/total ratio ≥ 0.9', () => {
    const { used, total } = gamebookIndexFixtures['quota-soft'].quota;
    expect(used / total).toBeGreaterThanOrEqual(0.9);
    expect(used).toBeLessThan(total); // strictly under hard limit
  });

  it('quota-hard fixture has used === total', () => {
    const { used, total } = gamebookIndexFixtures['quota-hard'].quota;
    expect(used).toBe(total);
  });

  it('default fixture mixes status states (ready + indexing + error)', () => {
    const statuses = new Set(gamebookIndexFixtures.default.gamebooks.map(g => g.status));
    expect(statuses.has('ready')).toBe(true);
    expect(statuses.has('indexing')).toBe(true);
    expect(statuses.has('error')).toBe(true);
  });

  it('error gamebooks have errorMsg populated', () => {
    for (const fixture of Object.values(gamebookIndexFixtures)) {
      for (const gb of fixture.gamebooks) {
        if (gb.status === 'error') {
          expect(gb.errorMsg).not.toBeNull();
          expect(gb.errorMsg?.length).toBeGreaterThan(0);
        }
      }
    }
  });

  it('quota-soft and quota-hard fixtures both have ≥1 gamebook', () => {
    expect(gamebookIndexFixtures['quota-soft'].gamebooks.length).toBeGreaterThanOrEqual(1);
    expect(gamebookIndexFixtures['quota-hard'].gamebooks.length).toBeGreaterThanOrEqual(1);
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride
// ---------------------------------------------------------------------------

describe('parseStateOverride', () => {
  it('returns null when input is null', () => {
    expect(parseStateOverride(null)).toBeNull();
  });

  it('returns null when input is undefined', () => {
    expect(parseStateOverride(undefined)).toBeNull();
  });

  it('returns null when input is empty string', () => {
    expect(parseStateOverride('')).toBeNull();
  });

  it('returns null when input is unrecognized fixture kind', () => {
    expect(parseStateOverride('partial')).toBeNull();
    expect(parseStateOverride('unknown')).toBeNull();
  });

  it.each(['default', 'empty', 'quota-soft', 'quota-hard', 'loading', 'error'])(
    'returns %s for valid kind via plain string',
    kind => {
      expect(parseStateOverride(kind)).toBe(kind);
    }
  );

  it('reads "fixture" param from URLSearchParams', () => {
    const params = new URLSearchParams('fixture=quota-soft&other=ignored');
    expect(parseStateOverride(params)).toBe('quota-soft');
  });

  it('returns null for URLSearchParams without fixture param', () => {
    const params = new URLSearchParams('other=value');
    expect(parseStateOverride(params)).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// STATE_OVERRIDE_ENABLED
// ---------------------------------------------------------------------------

describe('STATE_OVERRIDE_ENABLED', () => {
  it('is enabled in test environment (NODE_ENV=test)', () => {
    // Vitest sets NODE_ENV=test, so the gate is open here regardless of
    // NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED. This is the correct behavior:
    // tests can exercise parseStateOverride without setting the env var.
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Schema discriminant
// ---------------------------------------------------------------------------

describe('gamebookIndexFixtureKindSchema integration', () => {
  it('schema enum matches fixture record keys', () => {
    const fixtureKeys = Object.keys(gamebookIndexFixtures).sort();
    const schemaValues = gamebookIndexFixtureKindSchema.options.slice().sort();
    expect(fixtureKeys).toEqual(schemaValues);
  });
});
