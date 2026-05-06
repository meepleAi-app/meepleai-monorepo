import { describe, expect, it } from 'vitest';

import {
  IS_VISUAL_TEST_BUILD,
  STATE_OVERRIDE_ENABLED,
  VISUAL_TEST_FIXTURE_SESSIONS,
  VISUAL_TEST_FIXTURE_SESSIONS_EMPTY,
  VISUAL_TEST_FIXTURE_SESSIONS_ID,
  parseStateOverride,
  tryLoadVisualTestFixture,
} from '../sessions-visual-test-fixture';

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

describe('STATE_OVERRIDE_ENABLED', () => {
  it('is true in test/development environment (NODE_ENV !== "production")', () => {
    // vitest runs with NODE_ENV=test, so STATE_OVERRIDE_ENABLED must be true
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
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

  it('returns null when not a visual-test build (empty state)', () => {
    const result = tryLoadVisualTestFixture('empty');
    expect(result).toBeNull();
  });

  it('returns null when called with no argument (default omitted)', () => {
    const result = tryLoadVisualTestFixture();
    expect(result).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Fixture data contract — shape validation
// ---------------------------------------------------------------------------

describe('VISUAL_TEST_FIXTURE_SESSIONS — data contract', () => {
  it('exports exactly 6 fixture entries', () => {
    expect(VISUAL_TEST_FIXTURE_SESSIONS).toHaveLength(6);
  });

  it('all entries have the required SessionListItem fields', () => {
    VISUAL_TEST_FIXTURE_SESSIONS.forEach((s, i) => {
      expect(s.id, `entry ${i}: id`).toBeTruthy();
      expect(s.gameName, `entry ${i}: gameName`).toBeTruthy();
      expect(s.date, `entry ${i}: date`).toBeTruthy();
      expect(s.when, `entry ${i}: when`).toBeTruthy();
      expect(s.duration, `entry ${i}: duration`).toBeTruthy();
      expect(['inprogress', 'paused', 'completed', 'abandoned']).toContain(s.status);
      expect(typeof s.playerCount).toBe('number');
      expect(Array.isArray(s.scores)).toBe(true);
      expect(typeof s.hasChat).toBe('boolean');
    });
  });

  it('contains exactly 1 inprogress session', () => {
    const count = VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status === 'inprogress').length;
    expect(count).toBe(1);
  });

  it('contains exactly 1 paused session', () => {
    const count = VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status === 'paused').length;
    expect(count).toBe(1);
  });

  it('contains exactly 3 completed sessions', () => {
    const count = VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status === 'completed').length;
    expect(count).toBe(3);
  });

  it('contains exactly 1 abandoned session', () => {
    const count = VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status === 'abandoned').length;
    expect(count).toBe(1);
  });

  it('completed sessions cover all 3 outcomes: won, lost, tie', () => {
    const completed = VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status === 'completed');
    const outcomes = completed.map(s => s.outcome);
    expect(outcomes).toContain('won');
    expect(outcomes).toContain('lost');
    expect(outcomes).toContain('tie');
  });

  it('all entries use the deterministic UUID sentinel format', () => {
    const uuidPattern = /^00000000-0000-4000-8000-\d{12}$/;
    VISUAL_TEST_FIXTURE_SESSIONS.forEach((s, i) => {
      expect(s.id, `entry ${i}: id format`).toMatch(uuidPattern);
    });
  });

  it('the inprogress session has a turn field and hasChat=true', () => {
    const liveSession = VISUAL_TEST_FIXTURE_SESSIONS.find(s => s.status === 'inprogress');
    expect(liveSession).toBeDefined();
    expect(liveSession?.turn).toBeTruthy();
    expect(liveSession?.hasChat).toBe(true);
    expect(liveSession?.chatCount).toBeGreaterThan(0);
  });

  it('the paused session has paused=true and a turn field', () => {
    const pausedSession = VISUAL_TEST_FIXTURE_SESSIONS.find(s => s.status === 'paused');
    expect(pausedSession).toBeDefined();
    expect(pausedSession?.paused).toBe(true);
    expect(pausedSession?.turn).toBeTruthy();
  });

  it('non-completed sessions have outcome=null', () => {
    VISUAL_TEST_FIXTURE_SESSIONS.filter(s => s.status !== 'completed').forEach(s => {
      expect(s.outcome).toBeNull();
    });
  });
});

describe('VISUAL_TEST_FIXTURE_SESSIONS_EMPTY', () => {
  it('is an empty array', () => {
    expect(VISUAL_TEST_FIXTURE_SESSIONS_EMPTY).toHaveLength(0);
    expect(Array.isArray(VISUAL_TEST_FIXTURE_SESSIONS_EMPTY)).toBe(true);
  });
});

describe('VISUAL_TEST_FIXTURE_SESSIONS_ID', () => {
  it('encodes issue #735 in the UUID sentinel', () => {
    expect(VISUAL_TEST_FIXTURE_SESSIONS_ID).toBe('00000000-0000-4000-8000-000000000735');
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride
// ---------------------------------------------------------------------------

describe('parseStateOverride — STATE_OVERRIDE_ENABLED=true (test env)', () => {
  function params(state: string): URLSearchParams {
    return new URLSearchParams({ state });
  }

  it("returns 'loading' for ?state=loading", () => {
    expect(parseStateOverride(params('loading'))).toBe('loading');
  });

  it("returns 'empty' for ?state=empty", () => {
    expect(parseStateOverride(params('empty'))).toBe('empty');
  });

  it("returns 'filtered-empty' for ?state=filtered-empty", () => {
    expect(parseStateOverride(params('filtered-empty'))).toBe('filtered-empty');
  });

  it('returns null for ?state=error (not reproducible via URL deterministically)', () => {
    expect(parseStateOverride(params('error'))).toBeNull();
  });

  it('returns null for unknown state value', () => {
    expect(parseStateOverride(params('unknown'))).toBeNull();
  });

  it('returns null when no state param present', () => {
    expect(parseStateOverride(new URLSearchParams())).toBeNull();
  });

  it('accepts a plain Record<string, string> as searchParams', () => {
    expect(parseStateOverride({ state: 'loading' })).toBe('loading');
    expect(parseStateOverride({ state: 'empty' })).toBe('empty');
    expect(parseStateOverride({})).toBeNull();
  });

  it("returns 'default' is NOT a valid override (returned as null)", () => {
    // 'default' is not in the valid overrides set — it's handled by fixture
    expect(parseStateOverride(params('default'))).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Structural export contracts
// ---------------------------------------------------------------------------

describe('module export contracts', () => {
  it('IS_VISUAL_TEST_BUILD is a boolean', () => {
    expect(typeof IS_VISUAL_TEST_BUILD).toBe('boolean');
  });

  it('STATE_OVERRIDE_ENABLED is a boolean', () => {
    expect(typeof STATE_OVERRIDE_ENABLED).toBe('boolean');
  });

  it('tryLoadVisualTestFixture is a function', () => {
    expect(typeof tryLoadVisualTestFixture).toBe('function');
    expect(() => tryLoadVisualTestFixture('default')).not.toThrow();
    expect(() => tryLoadVisualTestFixture('empty')).not.toThrow();
    expect(() => tryLoadVisualTestFixture()).not.toThrow();
  });

  it('parseStateOverride is a function', () => {
    expect(typeof parseStateOverride).toBe('function');
  });
});
