/**
 * Unit tests for `deriveSessionSummaryState` (Wave D.3 Phase 0.5 §2).
 *
 * Coverage targets (≥30 tests):
 *   - 6 cells × precedence rules
 *   - Each cell's edge cases
 *   - Discriminant narrowing (TypeScript exhaustive switch)
 *   - Cell precedence: loading > error > not-found > not-completed > partial > default
 */

import { describe, expect, it } from 'vitest';

import type { SessionDetailsDto } from '@/lib/api/schemas/session-tracking.schemas';
import type { DiaryEntryDto } from '@/lib/api/session-flow/types';
import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';

import { deriveSessionSummaryState, type DeriveStateInput, type QueryLike } from '../fsm';
import type { AchievementDto } from '../schemas';

// ---------------------------------------------------------------------------
// Test factories
// ---------------------------------------------------------------------------

const SESSION_ID = '00000000-0000-4000-8000-000000000756';

function makeSession(status: string): SessionDetailsDto {
  return {
    id: SESSION_ID,
    userId: '00000000-0000-4000-8000-000000000a01',
    gameId: '00000000-0000-4000-8000-00000000c001',
    sessionCode: 'TEST-CODE',
    sessionType: 'Standard',
    status,
    sessionDate: '2026-05-04T19:00:00.000Z',
    location: null,
    finalizedAt:
      status === 'Completed' || status === 'Abandoned' ? '2026-05-04T20:30:00.000Z' : null,
    participants: [],
    scores: [],
    notes: [],
  };
}

function makeDiary(): readonly DiaryEntryDto[] {
  return [
    {
      id: '00000000-0000-4000-8000-0000000d1000',
      sessionId: SESSION_ID,
      gameNightId: null,
      eventType: 'score_updated',
      timestamp: '2026-05-04T19:30:00.000Z',
      payload: null,
      createdBy: null,
      source: 'system',
    },
  ];
}

function makeSnapshots(): readonly SessionSnapshotDto[] {
  return [
    {
      id: '00000000-0000-4000-8000-0000000e2000',
      sessionId: SESSION_ID,
      turnNumber: 1,
      caption: 'Test snapshot',
      hasGameState: false,
      createdAt: '2026-05-04T19:35:00.000Z',
      images: [],
    },
  ];
}

function makeAchievements(): readonly AchievementDto[] {
  return [
    {
      id: '00000000-0000-4000-8000-0000000ach100',
      code: 'firstWin',
      titleKey: 'pages.sessionSummary.achievements.firstWin.title',
      descriptionKey: 'pages.sessionSummary.achievements.firstWin.description',
      iconEmoji: '🏆',
      unlockedAt: '2026-05-04T20:30:00.000Z',
    },
  ];
}

function ok<T>(data: T | null): QueryLike<T> {
  return { isPending: false, isError: false, error: null, data };
}

function pending<T>(): QueryLike<T> {
  return { isPending: true, isError: false, error: null, data: undefined };
}

function fail<T>(err = new Error('boom')): QueryLike<T> {
  return { isPending: false, isError: true, error: err, data: undefined };
}

function happyInput(overrides: Partial<DeriveStateInput> = {}): DeriveStateInput {
  return {
    sessionQuery: ok(makeSession('Completed')),
    diaryQuery: ok(makeDiary()),
    snapshotsQuery: ok(makeSnapshots()),
    achievementsQuery: ok(makeAchievements()),
    sessionId: SESSION_ID,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Cell 1 — loading
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 1 loading', () => {
  it('returns loading when sessionQuery is pending', () => {
    const state = deriveSessionSummaryState(happyInput({ sessionQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('returns loading when diaryQuery is pending', () => {
    const state = deriveSessionSummaryState(happyInput({ diaryQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('returns loading when snapshotsQuery is pending', () => {
    const state = deriveSessionSummaryState(happyInput({ snapshotsQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('returns loading when achievementsQuery is pending', () => {
    const state = deriveSessionSummaryState(happyInput({ achievementsQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('loading takes precedence over error (if both isPending and isError set)', () => {
    // Defensive: TanStack should never produce this combo, but we still check.
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: { isPending: true, isError: true, error: new Error('x'), data: undefined },
      })
    );
    expect(state).toEqual({ kind: 'loading' });
  });
});

// ---------------------------------------------------------------------------
// Cell 2 — error
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 2 error', () => {
  it('returns error when session query failed', () => {
    const err = new Error('session 500');
    const state = deriveSessionSummaryState(happyInput({ sessionQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('returns error when diary query failed and session is ok', () => {
    const err = new Error('diary 502');
    const state = deriveSessionSummaryState(happyInput({ diaryQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('returns error when snapshots query failed', () => {
    const err = new Error('snapshots 503');
    const state = deriveSessionSummaryState(happyInput({ snapshotsQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('returns error when achievements query failed', () => {
    const err = new Error('achievements 504');
    const state = deriveSessionSummaryState(happyInput({ achievementsQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('prefers session error over sub-query errors when both fail', () => {
    const sessErr = new Error('session');
    const diaryErr = new Error('diary');
    const state = deriveSessionSummaryState(
      happyInput({ sessionQuery: fail(sessErr), diaryQuery: fail(diaryErr) })
    );
    expect(state).toEqual({ kind: 'error', error: sessErr });
  });

  it('synthesizes a default Error when error field is null', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: { isPending: false, isError: true, error: null, data: undefined },
      })
    );
    expect(state.kind).toBe('error');
    if (state.kind === 'error') {
      expect(state.error).toBeInstanceOf(Error);
      expect(state.error.message).toBe('Session query failed');
    }
  });
});

// ---------------------------------------------------------------------------
// Cell 3 — not-found
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 3 not-found', () => {
  it('returns not-found when sessionQuery resolves with null', () => {
    const state = deriveSessionSummaryState(happyInput({ sessionQuery: ok(null) }));
    expect(state).toEqual({ kind: 'not-found' });
  });

  it('returns not-found when sessionQuery data is undefined', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: { isPending: false, isError: false, error: null, data: undefined },
      })
    );
    expect(state).toEqual({ kind: 'not-found' });
  });
});

// ---------------------------------------------------------------------------
// Cell 4 — not-completed
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 4 not-completed', () => {
  it.each(['InProgress', 'Paused', 'Setup'])('returns not-completed when status is %s', status => {
    const state = deriveSessionSummaryState(happyInput({ sessionQuery: ok(makeSession(status)) }));
    expect(state).toEqual({ kind: 'not-completed', status, sessionId: SESSION_ID });
  });

  it('not-completed includes the original sessionId for redirect target', () => {
    const customId = '11111111-1111-4111-8111-111111111111';
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: ok({ ...makeSession('InProgress'), id: customId }),
        sessionId: customId,
      })
    );
    if (state.kind === 'not-completed') {
      expect(state.sessionId).toBe(customId);
    } else {
      throw new Error(`Expected not-completed, got ${state.kind}`);
    }
  });
});

// ---------------------------------------------------------------------------
// Cell 5 — default (happy path)
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 5 default', () => {
  it('returns default when all queries succeed with non-empty data', () => {
    const state = deriveSessionSummaryState(happyInput());
    expect(state.kind).toBe('default');
    if (state.kind === 'default') {
      expect(state.session.status).toBe('Completed');
      expect(state.diary).toHaveLength(1);
      expect(state.snapshots).toHaveLength(1);
      expect(state.achievements).toHaveLength(1);
    }
  });

  it('returns default for Abandoned status (still considered "completed" for summary)', () => {
    const state = deriveSessionSummaryState(
      happyInput({ sessionQuery: ok(makeSession('Abandoned')) })
    );
    expect(state.kind).toBe('default');
    if (state.kind === 'default') {
      expect(state.session.status).toBe('Abandoned');
    }
  });
});

// ---------------------------------------------------------------------------
// Cell 6 — partial
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — Cell 6 partial', () => {
  it('returns partial with missing=["diary"] when diary array is empty', () => {
    const state = deriveSessionSummaryState(happyInput({ diaryQuery: ok([]) }));
    expect(state.kind).toBe('partial');
    if (state.kind === 'partial') {
      expect(state.missing).toContain('diary');
      expect(state.missing).not.toContain('snapshots');
      expect(state.missing).not.toContain('achievements');
    }
  });

  it('returns partial with missing=["snapshots"] when snapshots empty', () => {
    const state = deriveSessionSummaryState(happyInput({ snapshotsQuery: ok([]) }));
    if (state.kind === 'partial') {
      expect(state.missing).toEqual(['snapshots']);
    } else {
      throw new Error('expected partial');
    }
  });

  it('returns partial with missing=["achievements"] when achievements empty', () => {
    const state = deriveSessionSummaryState(happyInput({ achievementsQuery: ok([]) }));
    if (state.kind === 'partial') {
      expect(state.missing).toEqual(['achievements']);
    } else {
      throw new Error('expected partial');
    }
  });

  it('returns partial with multiple missing entries when several sub-arrays are empty', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        diaryQuery: ok([]),
        snapshotsQuery: ok([]),
      })
    );
    if (state.kind === 'partial') {
      expect(state.missing).toEqual(['diary', 'snapshots']);
    } else {
      throw new Error('expected partial');
    }
  });

  it('returns partial when all three sub-arrays are empty', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        diaryQuery: ok([]),
        snapshotsQuery: ok([]),
        achievementsQuery: ok([]),
      })
    );
    if (state.kind === 'partial') {
      expect(state.missing).toEqual(['diary', 'snapshots', 'achievements']);
    } else {
      throw new Error('expected partial');
    }
  });

  it('partial cell still carries the resolved data for non-empty sections', () => {
    const state = deriveSessionSummaryState(happyInput({ achievementsQuery: ok([]) }));
    if (state.kind === 'partial') {
      expect(state.diary).toHaveLength(1);
      expect(state.snapshots).toHaveLength(1);
      expect(state.achievements).toHaveLength(0);
    } else {
      throw new Error('expected partial');
    }
  });
});

// ---------------------------------------------------------------------------
// Precedence rules
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — precedence', () => {
  it('loading > error', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: pending(),
        diaryQuery: fail(),
      })
    );
    expect(state).toEqual({ kind: 'loading' });
  });

  it('error > not-found', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: fail(new Error('boom')),
        // not-found would also be a contender if data were null, but error wins.
      })
    );
    expect(state.kind).toBe('error');
  });

  it('not-found > not-completed', () => {
    const state = deriveSessionSummaryState(happyInput({ sessionQuery: ok(null) }));
    expect(state).toEqual({ kind: 'not-found' });
  });

  it('not-completed > partial', () => {
    const state = deriveSessionSummaryState(
      happyInput({
        sessionQuery: ok(makeSession('InProgress')),
        diaryQuery: ok([]),
      })
    );
    expect(state.kind).toBe('not-completed');
  });

  it('partial > default when any sub-array empty', () => {
    const state = deriveSessionSummaryState(happyInput({ snapshotsQuery: ok([]) }));
    expect(state.kind).toBe('partial');
  });
});

// ---------------------------------------------------------------------------
// Discriminant narrowing
// ---------------------------------------------------------------------------

describe('deriveSessionSummaryState — type narrowing', () => {
  it('exhaustive switch covers all cells without falling through', () => {
    // This test guards against future cell additions that forget to update
    // every consumer. If a new cell is added without an arm here, TypeScript
    // would flag the `never` assertion at compile time.
    const variants: Array<DeriveStateInput> = [
      happyInput({ sessionQuery: pending() }),
      happyInput({ sessionQuery: fail() }),
      happyInput({ sessionQuery: ok(null) }),
      happyInput({ sessionQuery: ok(makeSession('InProgress')) }),
      happyInput(),
      happyInput({ achievementsQuery: ok([]) }),
    ];
    for (const v of variants) {
      const cell = deriveSessionSummaryState(v);
      switch (cell.kind) {
        case 'loading':
        case 'error':
        case 'not-found':
        case 'not-completed':
        case 'default':
        case 'partial':
          expect(typeof cell.kind).toBe('string');
          break;
        default: {
          const _exhaustive: never = cell;
          throw new Error(`Unhandled cell ${JSON.stringify(_exhaustive)}`);
        }
      }
    }
  });
});
