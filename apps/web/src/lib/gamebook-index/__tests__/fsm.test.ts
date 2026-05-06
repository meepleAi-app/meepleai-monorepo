/**
 * Unit tests for `deriveGamebookIndexState` (SP6 Phase B Task 1).
 *
 * Coverage targets (≥25 tests):
 *   - 6 cells × edge cases
 *   - Cell precedence: loading > error > quota-hard > quota-soft > empty > default
 *   - Quota threshold edge cases: 89% → default; 90% → quota-soft; 100% → quota-hard
 *   - Discriminant narrowing exhaustive switch (TypeScript)
 */

import { describe, expect, it } from 'vitest';

import {
  deriveGamebookIndexState,
  QUOTA_SOFT_RATIO,
  type DeriveStateInput,
  type QueryLike,
} from '../fsm';
import type { GamebookCardData, QuotaInfo } from '../schemas';

// ---------------------------------------------------------------------------
// Test factories
// ---------------------------------------------------------------------------

function makeGamebook(overrides: Partial<GamebookCardData> = {}): GamebookCardData {
  return {
    id: '00000000-0000-4000-8000-000000000001',
    gameId: '00000000-0000-4000-8000-0000000c0001',
    title: 'Test Gamebook',
    publisher: 'Test',
    year: 2024,
    pages: 50,
    totalPages: 50,
    chunks: 100,
    status: 'ready',
    cover: null,
    emoji: '📕',
    qaCount: 0,
    sessionsCount: 0,
    errorMsg: null,
    ...overrides,
  };
}

function makeQuota(overrides: Partial<QuotaInfo> = {}): QuotaInfo {
  return {
    used: 12,
    total: 50,
    resetDate: '2026-06-01T00:00:00.000Z',
    tier: 'free',
    ...overrides,
  };
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
    gamebooksQuery: ok([makeGamebook()]),
    quotaQuery: ok(makeQuota()),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Cell 1 — loading
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 1 loading', () => {
  it('returns loading when gamebooksQuery is pending', () => {
    const state = deriveGamebookIndexState(happyInput({ gamebooksQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('returns loading when quotaQuery is pending', () => {
    const state = deriveGamebookIndexState(happyInput({ quotaQuery: pending() }));
    expect(state).toEqual({ kind: 'loading' });
  });

  it('returns loading when both queries pending', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: pending(),
      quotaQuery: pending(),
    });
    expect(state).toEqual({ kind: 'loading' });
  });

  it('loading takes precedence over error (defensive)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        gamebooksQuery: { isPending: true, isError: true, error: new Error('x'), data: undefined },
      })
    );
    expect(state).toEqual({ kind: 'loading' });
  });
});

// ---------------------------------------------------------------------------
// Cell 2 — error
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 2 error', () => {
  it('returns error when gamebooks query failed', () => {
    const err = new Error('gamebooks 500');
    const state = deriveGamebookIndexState(happyInput({ gamebooksQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('returns error when quota query failed and gamebooks is ok', () => {
    const err = new Error('quota 502');
    const state = deriveGamebookIndexState(happyInput({ quotaQuery: fail(err) }));
    expect(state).toEqual({ kind: 'error', error: err });
  });

  it('prefers gamebooks error over quota error when both fail', () => {
    const gbErr = new Error('gamebooks');
    const quotaErr = new Error('quota');
    const state = deriveGamebookIndexState({
      gamebooksQuery: fail(gbErr),
      quotaQuery: fail(quotaErr),
    });
    expect(state).toEqual({ kind: 'error', error: gbErr });
  });

  it('synthesizes a default Error when error field is null', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        gamebooksQuery: { isPending: false, isError: true, error: null, data: undefined },
      })
    );
    expect(state.kind).toBe('error');
    if (state.kind === 'error') {
      expect(state.error).toBeInstanceOf(Error);
      expect(state.error.message).toBe('Gamebooks query failed');
    }
  });

  it('synthesizes default Error for quota when null', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: { isPending: false, isError: true, error: null, data: undefined },
      })
    );
    expect(state.kind).toBe('error');
    if (state.kind === 'error') {
      expect(state.error.message).toBe('Quota query failed');
    }
  });

  it('returns error when quota data is null but query succeeded', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([makeGamebook()]),
      quotaQuery: ok(null),
    });
    expect(state.kind).toBe('error');
    if (state.kind === 'error') {
      expect(state.error.message).toBe('Quota data missing');
    }
  });
});

// ---------------------------------------------------------------------------
// Cell 5 — empty
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 5 empty', () => {
  it('returns empty when gamebooks=[] and quota healthy', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota()),
    });
    expect(state.kind).toBe('empty');
    if (state.kind === 'empty') {
      expect(state.quota).toEqual(makeQuota());
    }
  });

  it('returns empty even when quota is at hard limit (no gamebooks dominates)', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
    });
    expect(state.kind).toBe('empty');
  });

  it('returns empty even when quota is at soft limit', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota({ used: 47, total: 50 })),
    });
    expect(state.kind).toBe('empty');
  });

  it('returns empty when gamebooks data is undefined (defensive null coalesce)', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: { isPending: false, isError: false, error: null, data: undefined },
      quotaQuery: ok(makeQuota()),
    });
    expect(state.kind).toBe('empty');
  });
});

// ---------------------------------------------------------------------------
// Cell 6 — default
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 6 default', () => {
  it('returns default when has gamebooks and quota healthy', () => {
    const state = deriveGamebookIndexState(happyInput());
    expect(state.kind).toBe('default');
    if (state.kind === 'default') {
      expect(state.gamebooks).toHaveLength(1);
      expect(state.quota.used).toBe(12);
    }
  });

  it('returns default when ratio is exactly 89% (just under soft threshold)', () => {
    // 44/50 = 0.88 — clearly below 0.9 threshold
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 44, total: 50 })),
      })
    );
    expect(state.kind).toBe('default');
  });

  it('returns default with multiple gamebooks', () => {
    const gbs = [makeGamebook(), makeGamebook({ id: '00000000-0000-4000-8000-000000000002' })];
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok(gbs),
      quotaQuery: ok(makeQuota()),
    });
    expect(state.kind).toBe('default');
    if (state.kind === 'default') {
      expect(state.gamebooks).toHaveLength(2);
    }
  });

  it('returns default with mixed-status gamebooks', () => {
    const gbs = [
      makeGamebook({ status: 'ready' }),
      makeGamebook({ id: '00000000-0000-4000-8000-000000000002', status: 'indexing' }),
      makeGamebook({ id: '00000000-0000-4000-8000-000000000003', status: 'error' }),
    ];
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok(gbs),
      quotaQuery: ok(makeQuota()),
    });
    expect(state.kind).toBe('default');
  });
});

// ---------------------------------------------------------------------------
// Cell 4 — quota-soft
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 4 quota-soft', () => {
  it('returns quota-soft at exactly 90% (45/50)', () => {
    // 45/50 = 0.9 (exact threshold)
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 45, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-soft');
  });

  it('returns quota-soft at 94% (47/50, mockup canonical)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 47, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-soft');
    if (state.kind === 'quota-soft') {
      expect(state.quota.used).toBe(47);
    }
  });

  it('returns quota-soft at 99% (49/50)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 49, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-soft');
  });

  it('quota-soft requires ≥1 gamebook (empty wins otherwise)', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota({ used: 47, total: 50 })),
    });
    expect(state.kind).toBe('empty');
  });

  it('quota-soft preserves gamebooks list', () => {
    const gbs = [makeGamebook(), makeGamebook({ id: '00000000-0000-4000-8000-000000000002' })];
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok(gbs),
      quotaQuery: ok(makeQuota({ used: 47, total: 50 })),
    });
    if (state.kind === 'quota-soft') {
      expect(state.gamebooks).toHaveLength(2);
    } else {
      throw new Error(`expected quota-soft, got ${state.kind}`);
    }
  });
});

// ---------------------------------------------------------------------------
// Cell 3 — quota-hard
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — Cell 3 quota-hard', () => {
  it('returns quota-hard at exactly 100% (50/50)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-hard');
  });

  it('returns quota-hard when used > total (overflow)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 51, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-hard');
  });

  it('quota-hard requires ≥1 gamebook (empty wins otherwise)', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
    });
    expect(state.kind).toBe('empty');
  });

  it('quota-hard preserves gamebooks list', () => {
    const gbs = [makeGamebook()];
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok(gbs),
      quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
    });
    if (state.kind === 'quota-hard') {
      expect(state.gamebooks).toHaveLength(1);
    } else {
      throw new Error(`expected quota-hard, got ${state.kind}`);
    }
  });
});

// ---------------------------------------------------------------------------
// Precedence rules
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — precedence', () => {
  it('loading > error', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: pending(),
      quotaQuery: fail(),
    });
    expect(state).toEqual({ kind: 'loading' });
  });

  it('error > quota-hard', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: fail(),
      quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
    });
    expect(state.kind).toBe('error');
  });

  it('quota-hard > quota-soft (despite both threshold-positive)', () => {
    // used=50/50=100% which is BOTH ≥0.9 AND ==1.0 — hard wins
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 50, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-hard');
  });

  it('quota-soft > default (when threshold met)', () => {
    const state = deriveGamebookIndexState(
      happyInput({
        quotaQuery: ok(makeQuota({ used: 47, total: 50 })),
      })
    );
    expect(state.kind).toBe('quota-soft');
  });

  it('empty > default (when gamebooks=[])', () => {
    const state = deriveGamebookIndexState({
      gamebooksQuery: ok([]),
      quotaQuery: ok(makeQuota()),
    });
    expect(state.kind).toBe('empty');
  });
});

// ---------------------------------------------------------------------------
// Threshold constant
// ---------------------------------------------------------------------------

describe('QUOTA_SOFT_RATIO constant', () => {
  it('is exactly 0.9 (90%)', () => {
    expect(QUOTA_SOFT_RATIO).toBe(0.9);
  });
});

// ---------------------------------------------------------------------------
// Discriminant narrowing
// ---------------------------------------------------------------------------

describe('deriveGamebookIndexState — type narrowing', () => {
  it('exhaustive switch covers all cells without falling through', () => {
    // This test guards against future cell additions that forget to update
    // every consumer. If a new cell is added without an arm here, TypeScript
    // would flag the `never` assertion at compile time.
    const variants: Array<DeriveStateInput> = [
      happyInput({ gamebooksQuery: pending() }),
      happyInput({ gamebooksQuery: fail() }),
      { gamebooksQuery: ok([]), quotaQuery: ok(makeQuota()) },
      happyInput(),
      happyInput({ quotaQuery: ok(makeQuota({ used: 47, total: 50 })) }),
      happyInput({ quotaQuery: ok(makeQuota({ used: 50, total: 50 })) }),
    ];
    for (const v of variants) {
      const cell = deriveGamebookIndexState(v);
      switch (cell.kind) {
        case 'loading':
        case 'error':
        case 'empty':
        case 'default':
        case 'quota-soft':
        case 'quota-hard':
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
