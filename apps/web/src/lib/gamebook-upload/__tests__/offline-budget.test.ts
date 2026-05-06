/**
 * Unit tests for offline-budget reducer (SP6 Phase C.2.A Interactions).
 *
 * Coverage matrix:
 *   - Initial state shape
 *   - NETWORK_ERROR through 5 attempts → progressive scheduling [1s, 2s, 4s, 8s, 16s]
 *   - NETWORK_ERROR after 5 attempts → exhausted terminal
 *   - RETRY_TICK decrement clamped to 0
 *   - RETRY_TICK no-op when idle
 *   - RETRY_FIRE zeroes nextRetryInMs (does NOT bump attemptCount)
 *   - RETRY_FIRE no-op when idle
 *   - RETRY_SUCCESS resets to initial
 *   - CANCEL resets to initial
 *   - Defensive: unknown action returns input state unchanged
 *   - Pure reducer: input state never mutated
 *   - Total budget = 31_000ms (canonical RETRY_BUDGET_TOTAL_MS)
 *   - elapsedBudgetMs computation matrix
 */

import { describe, expect, it } from 'vitest';

import {
  elapsedBudgetMs,
  initialOfflineBudgetState,
  offlineBudgetReducer,
  RETRY_BUDGET_TOTAL_MS,
  RETRY_DELAYS_MS,
  type OfflineBudgetAction,
  type OfflineBudgetState,
} from '../offline-budget';

const ABORT_A = new AbortController();
const ABORT_B = new AbortController();

// ---------------------------------------------------------------------------
// Constants sanity
// ---------------------------------------------------------------------------

describe('retry budget constants', () => {
  it('RETRY_DELAYS_MS = [1_000, 2_000, 4_000, 8_000, 16_000]', () => {
    expect([...RETRY_DELAYS_MS]).toEqual([1_000, 2_000, 4_000, 8_000, 16_000]);
  });

  it('RETRY_BUDGET_TOTAL_MS = 31_000 (sum of delays)', () => {
    expect(RETRY_BUDGET_TOTAL_MS).toBe(31_000);
  });

  it('RETRY_BUDGET_TOTAL_MS equals sum of RETRY_DELAYS_MS', () => {
    const sum = RETRY_DELAYS_MS.reduce((a, b) => a + b, 0);
    expect(sum).toBe(RETRY_BUDGET_TOTAL_MS);
  });
});

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

describe('initialOfflineBudgetState', () => {
  it('starts with zero attempts, no pending retry, not exhausted, no abort handle', () => {
    expect(initialOfflineBudgetState).toEqual({
      attemptCount: 0,
      nextRetryInMs: null,
      isExhausted: false,
      abortController: null,
    });
  });
});

// ---------------------------------------------------------------------------
// NETWORK_ERROR transitions
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — NETWORK_ERROR', () => {
  it('first NETWORK_ERROR schedules attempt 1 with 1000ms delay', () => {
    const next = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    expect(next.attemptCount).toBe(1);
    expect(next.nextRetryInMs).toBe(1_000);
    expect(next.isExhausted).toBe(false);
    expect(next.abortController).toBe(ABORT_A);
  });

  it('progressive NETWORK_ERROR follows exponential schedule [1s, 2s, 4s, 8s, 16s]', () => {
    let state: OfflineBudgetState = initialOfflineBudgetState;
    const expectedDelays = [1_000, 2_000, 4_000, 8_000, 16_000];

    for (let i = 0; i < expectedDelays.length; i += 1) {
      state = offlineBudgetReducer(state, {
        type: 'NETWORK_ERROR',
        abortController: ABORT_A,
      });
      expect(state.attemptCount).toBe(i + 1);
      expect(state.nextRetryInMs).toBe(expectedDelays[i]);
      expect(state.isExhausted).toBe(false);
    }
  });

  it('6th NETWORK_ERROR (after all 5 attempts fired) → isExhausted, nextRetryInMs=null', () => {
    let state: OfflineBudgetState = initialOfflineBudgetState;
    for (let i = 0; i < 5; i += 1) {
      state = offlineBudgetReducer(state, {
        type: 'NETWORK_ERROR',
        abortController: ABORT_A,
      });
    }
    // 6th error — budget exhausted
    state = offlineBudgetReducer(state, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_B,
    });
    expect(state.attemptCount).toBe(5); // capped, did not bump beyond 5
    expect(state.nextRetryInMs).toBeNull();
    expect(state.isExhausted).toBe(true);
    expect(state.abortController).toBeNull(); // cleared on exhaust
  });

  it('overwrites previous abortController with new one on subsequent NETWORK_ERROR', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    const after2 = offlineBudgetReducer(after1, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_B,
    });
    expect(after2.abortController).toBe(ABORT_B);
  });
});

// ---------------------------------------------------------------------------
// RETRY_TICK transitions
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — RETRY_TICK', () => {
  it('decrements nextRetryInMs by deltaMs', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    }); // schedules 1000ms
    const ticked = offlineBudgetReducer(after1, {
      type: 'RETRY_TICK',
      deltaMs: 100,
    });
    expect(ticked.nextRetryInMs).toBe(900);
  });

  it('clamps nextRetryInMs to 0 when delta exceeds remaining', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    const ticked = offlineBudgetReducer(after1, {
      type: 'RETRY_TICK',
      deltaMs: 5_000, // huge delta
    });
    expect(ticked.nextRetryInMs).toBe(0); // not negative
  });

  it('is a no-op when state is idle (nextRetryInMs === null)', () => {
    const ticked = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'RETRY_TICK',
      deltaMs: 100,
    });
    expect(ticked).toBe(initialOfflineBudgetState); // referential equality
  });

  it('is a no-op when state is exhausted', () => {
    const exhausted: OfflineBudgetState = {
      attemptCount: 5,
      nextRetryInMs: null,
      isExhausted: true,
      abortController: null,
    };
    const ticked = offlineBudgetReducer(exhausted, {
      type: 'RETRY_TICK',
      deltaMs: 100,
    });
    expect(ticked).toBe(exhausted);
  });
});

// ---------------------------------------------------------------------------
// RETRY_FIRE transitions
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — RETRY_FIRE', () => {
  it('zeroes nextRetryInMs without bumping attemptCount', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    }); // attemptCount=1, nextRetryInMs=1000
    const fired = offlineBudgetReducer(after1, { type: 'RETRY_FIRE' });
    expect(fired.attemptCount).toBe(1); // unchanged
    expect(fired.nextRetryInMs).toBe(0); // zeroed
    expect(fired.abortController).toBe(ABORT_A); // preserved
  });

  it('is a no-op when state is idle (nextRetryInMs === null)', () => {
    const fired = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'RETRY_FIRE',
    });
    expect(fired).toBe(initialOfflineBudgetState);
  });
});

// ---------------------------------------------------------------------------
// RETRY_SUCCESS transitions
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — RETRY_SUCCESS', () => {
  it('resets state from mid-retry to initial', () => {
    const after2 = offlineBudgetReducer(
      offlineBudgetReducer(initialOfflineBudgetState, {
        type: 'NETWORK_ERROR',
        abortController: ABORT_A,
      }),
      { type: 'NETWORK_ERROR', abortController: ABORT_B }
    );
    expect(after2.attemptCount).toBe(2);

    const reset = offlineBudgetReducer(after2, { type: 'RETRY_SUCCESS' });
    expect(reset).toEqual(initialOfflineBudgetState);
  });

  it('resets exhausted state to initial', () => {
    const exhausted: OfflineBudgetState = {
      attemptCount: 5,
      nextRetryInMs: null,
      isExhausted: true,
      abortController: null,
    };
    const reset = offlineBudgetReducer(exhausted, { type: 'RETRY_SUCCESS' });
    expect(reset).toEqual(initialOfflineBudgetState);
  });
});

// ---------------------------------------------------------------------------
// CANCEL transitions
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — CANCEL', () => {
  it('resets state from mid-retry to initial', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    const cancelled = offlineBudgetReducer(after1, { type: 'CANCEL' });
    expect(cancelled).toEqual(initialOfflineBudgetState);
  });

  it('resets exhausted state to initial', () => {
    const exhausted: OfflineBudgetState = {
      attemptCount: 5,
      nextRetryInMs: null,
      isExhausted: true,
      abortController: null,
    };
    const cancelled = offlineBudgetReducer(exhausted, { type: 'CANCEL' });
    expect(cancelled).toEqual(initialOfflineBudgetState);
  });

  it('is idempotent when state is already initial', () => {
    const cancelled = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'CANCEL',
    });
    expect(cancelled).toEqual(initialOfflineBudgetState);
  });
});

// ---------------------------------------------------------------------------
// Defensive / purity
// ---------------------------------------------------------------------------

describe('offlineBudgetReducer — defensive + purity', () => {
  it('returns input state unchanged on unknown action type', () => {
    const unknown = { type: 'UNKNOWN_ACTION' } as unknown as OfflineBudgetAction;
    const next = offlineBudgetReducer(initialOfflineBudgetState, unknown);
    expect(next).toBe(initialOfflineBudgetState);
  });

  it('does not mutate input state on NETWORK_ERROR', () => {
    const input = { ...initialOfflineBudgetState };
    const snapshot = JSON.stringify({
      attemptCount: input.attemptCount,
      nextRetryInMs: input.nextRetryInMs,
      isExhausted: input.isExhausted,
      abortController: input.abortController,
    });
    offlineBudgetReducer(input, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    const after = JSON.stringify({
      attemptCount: input.attemptCount,
      nextRetryInMs: input.nextRetryInMs,
      isExhausted: input.isExhausted,
      abortController: input.abortController,
    });
    expect(after).toBe(snapshot);
  });

  it('does not mutate input state on RETRY_TICK', () => {
    const seeded = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    });
    const before = seeded.nextRetryInMs;
    offlineBudgetReducer(seeded, { type: 'RETRY_TICK', deltaMs: 100 });
    expect(seeded.nextRetryInMs).toBe(before);
  });
});

// ---------------------------------------------------------------------------
// elapsedBudgetMs
// ---------------------------------------------------------------------------

describe('elapsedBudgetMs', () => {
  it('returns 0 for initial state', () => {
    expect(elapsedBudgetMs(initialOfflineBudgetState)).toBe(0);
  });

  it('returns elapsed time within first segment after NETWORK_ERROR + 600ms ticked', () => {
    const after1 = offlineBudgetReducer(initialOfflineBudgetState, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    }); // attemptCount=1, nextRetryInMs=1000
    const ticked = offlineBudgetReducer(after1, {
      type: 'RETRY_TICK',
      deltaMs: 600,
    }); // nextRetryInMs=400
    expect(elapsedBudgetMs(ticked)).toBe(600); // 1000 - 400
  });

  it('sums prior completed segments + current segment for attempt 2', () => {
    let state: OfflineBudgetState = initialOfflineBudgetState;
    state = offlineBudgetReducer(state, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    }); // attempt 1 (1000ms)
    state = offlineBudgetReducer(state, {
      type: 'NETWORK_ERROR',
      abortController: ABORT_A,
    }); // attempt 2 (2000ms)
    state = offlineBudgetReducer(state, {
      type: 'RETRY_TICK',
      deltaMs: 1500,
    }); // 500ms remaining
    // 1000 (segment 1 fully completed) + 1500 (segment 2 partial) = 2500
    expect(elapsedBudgetMs(state)).toBe(2500);
  });

  it('clamps to RETRY_BUDGET_TOTAL_MS for exhausted state', () => {
    const exhausted: OfflineBudgetState = {
      attemptCount: 5,
      nextRetryInMs: null,
      isExhausted: true,
      abortController: null,
    };
    expect(elapsedBudgetMs(exhausted)).toBe(RETRY_BUDGET_TOTAL_MS);
  });
});
