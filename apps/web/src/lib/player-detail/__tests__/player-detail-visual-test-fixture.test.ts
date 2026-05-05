import { describe, expect, it } from 'vitest';

import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '../player-detail-visual-test-fixture';

// ---------------------------------------------------------------------------
// Production safety gate
// ---------------------------------------------------------------------------

describe('IS_VISUAL_TEST_BUILD', () => {
  it('is false in the normal test environment (NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED not set to "1")', () => {
    // vitest does NOT set NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 by default,
    // so this constant evaluates to `false` at module evaluation time.
    expect(IS_VISUAL_TEST_BUILD).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// tryLoadVisualTestFixture — production mode (fixture disabled)
// ---------------------------------------------------------------------------

describe('tryLoadVisualTestFixture — IS_VISUAL_TEST_BUILD=false', () => {
  it('returns null when not a visual-test build (default state)', () => {
    const result = tryLoadVisualTestFixture('default');
    expect(result).toBeNull();
  });

  it('returns null when not a visual-test build (not-found state)', () => {
    const result = tryLoadVisualTestFixture('not-found');
    expect(result).toBeNull();
  });

  it('returns null when called with no argument (default omitted)', () => {
    const result = tryLoadVisualTestFixture();
    expect(result).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// tryLoadVisualTestFixture — structural contract (state shapes)
// ---------------------------------------------------------------------------

describe('tryLoadVisualTestFixture — fixture data contract', () => {
  it('fixture module exports IS_VISUAL_TEST_BUILD as a boolean', () => {
    expect(typeof IS_VISUAL_TEST_BUILD).toBe('boolean');
  });

  it('tryLoadVisualTestFixture is a function accepting optional PlayerDetailFixtureState', () => {
    expect(typeof tryLoadVisualTestFixture).toBe('function');
    // No throw on valid inputs
    expect(() => tryLoadVisualTestFixture('default')).not.toThrow();
    expect(() => tryLoadVisualTestFixture('not-found')).not.toThrow();
    expect(() => tryLoadVisualTestFixture()).not.toThrow();
  });
});
