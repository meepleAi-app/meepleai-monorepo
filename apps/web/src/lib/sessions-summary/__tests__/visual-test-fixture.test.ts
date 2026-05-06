/**
 * Unit tests for `parseStateOverride` + `sessionSummaryFixtures` (Wave D.3).
 *
 * Coverage targets (≥10 tests):
 *   - All 6 fixture kinds expose internally consistent data
 *   - Tied fixture has 2+ participants with identical totalScore at the top
 *   - Solo fixture has exactly 1 participant
 *   - empty-achievements has achievements:[] but everything else populated
 *   - empty-photos has snapshots:[] but everything else populated
 *   - parseStateOverride accepts string, URLSearchParams, null, undefined
 *   - parseStateOverride rejects unknown kinds
 *   - STATE_OVERRIDE_ENABLED gates the behaviour (NODE_ENV=test allows override)
 */

import { describe, expect, it } from 'vitest';

import {
  SESSION_SUMMARY_VISUAL_TEST_SENTINEL,
  STATE_OVERRIDE_ENABLED,
  parseStateOverride,
  sessionSummaryFixtures,
} from '../visual-test-fixture';

describe('sessionSummaryFixtures', () => {
  it('exposes exactly 6 fixture kinds', () => {
    const keys = Object.keys(sessionSummaryFixtures).sort();
    expect(keys).toEqual(
      ['abandoned', 'default', 'empty-achievements', 'empty-photos', 'solo', 'tied'].sort()
    );
  });

  it('every fixture targets the same sentinel session id', () => {
    for (const f of Object.values(sessionSummaryFixtures)) {
      expect(f.session.id).toBe(SESSION_SUMMARY_VISUAL_TEST_SENTINEL);
    }
  });

  it('default fixture has 4 participants with strictly distinct scores', () => {
    const { participants } = sessionSummaryFixtures.default.session;
    expect(participants).toHaveLength(4);
    const scores = participants.map(p => p.totalScore);
    expect(new Set(scores).size).toBe(scores.length);
  });

  it('tied fixture has 2 participants sharing the top score', () => {
    const { participants } = sessionSummaryFixtures.tied.session;
    expect(participants.length).toBeGreaterThanOrEqual(2);
    const sortedDesc = [...participants].sort((a, b) => b.totalScore - a.totalScore);
    expect(sortedDesc[0].totalScore).toBe(sortedDesc[1].totalScore);
  });

  it('abandoned fixture has status === "Abandoned"', () => {
    expect(sessionSummaryFixtures.abandoned.session.status).toBe('Abandoned');
  });

  it('completed fixtures (default/tied/solo) all have status === "Completed"', () => {
    expect(sessionSummaryFixtures.default.session.status).toBe('Completed');
    expect(sessionSummaryFixtures.tied.session.status).toBe('Completed');
    expect(sessionSummaryFixtures.solo.session.status).toBe('Completed');
  });

  it('solo fixture has exactly 1 participant', () => {
    expect(sessionSummaryFixtures.solo.session.participants).toHaveLength(1);
  });

  it('empty-achievements has achievements=[] but other sections populated', () => {
    const f = sessionSummaryFixtures['empty-achievements'];
    expect(f.achievements).toEqual([]);
    expect(f.diary.length).toBeGreaterThan(0);
    expect(f.snapshots.length).toBeGreaterThan(0);
    expect(f.session.participants.length).toBeGreaterThan(0);
  });

  it('empty-photos has snapshots=[] but other sections populated', () => {
    const f = sessionSummaryFixtures['empty-photos'];
    expect(f.snapshots).toEqual([]);
    expect(f.diary.length).toBeGreaterThan(0);
    expect(f.achievements.length).toBeGreaterThan(0);
  });

  it('every fixture has a valid finalizedAt for completed/abandoned status', () => {
    for (const f of Object.values(sessionSummaryFixtures)) {
      expect(f.session.finalizedAt).toBeTruthy();
      expect(() => new Date(f.session.finalizedAt!)).not.toThrow();
    }
  });
});

describe('parseStateOverride', () => {
  it('STATE_OVERRIDE_ENABLED is true in test env (NODE_ENV !== production)', () => {
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
  });

  it('returns null for null input', () => {
    expect(parseStateOverride(null)).toBeNull();
  });

  it('returns null for undefined input', () => {
    expect(parseStateOverride(undefined)).toBeNull();
  });

  it('returns null for empty string', () => {
    expect(parseStateOverride('')).toBeNull();
  });

  it('returns the kind for valid string input', () => {
    expect(parseStateOverride('default')).toBe('default');
    expect(parseStateOverride('tied')).toBe('tied');
    expect(parseStateOverride('abandoned')).toBe('abandoned');
    expect(parseStateOverride('solo')).toBe('solo');
    expect(parseStateOverride('empty-achievements')).toBe('empty-achievements');
    expect(parseStateOverride('empty-photos')).toBe('empty-photos');
  });

  it('returns null for an unknown kind', () => {
    expect(parseStateOverride('totally-not-a-kind')).toBeNull();
  });

  it('reads the "fixture" param from URLSearchParams', () => {
    const sp = new URLSearchParams('fixture=tied&other=ignored');
    expect(parseStateOverride(sp)).toBe('tied');
  });

  it('returns null when URLSearchParams has no fixture param', () => {
    const sp = new URLSearchParams('?other=value');
    expect(parseStateOverride(sp)).toBeNull();
  });
});
