/**
 * Offline-budget reducer for `/gamebook/upload` Step 3 batch upload retries
 * (SP6 Phase C.2 Task A — Interactions sub-PR).
 *
 * Implements the retry budget contract from §10:
 *
 *   - Exponential delays: [1s, 2s, 4s, 8s, 16s] = 31s total
 *   - 5 retry attempts max, then `isExhausted: true`
 *   - Cancel-during-retry support via `AbortController` thread-through
 *   - Pure reducer (no side effects) — host hook owns timer + abort
 *
 * Re-uses canonical `RETRY_DELAYS_MS` + `RETRY_BUDGET_TOTAL_MS` from
 * `schemas.ts` (defined in Foundation Task A) so values stay symmetric across
 * the codebase. Re-exported here for ergonomic single-import-site usage.
 *
 * Used by:
 *   - `lib/gamebook-upload/hooks/useOfflineBudget.ts` (Phase C.2 Task B/host-hook)
 *   - Orchestrator step3 cell derivation (Phase C.2 Task C)
 *   - E2E spec `e2e/v2-states/gamebook-upload-offline.spec.ts`
 *
 * Schema reality v1 carryover (Gate B): backend currently exposes only
 * batch-level `averageConfidence` — per-page conf tracking is aspirational and
 * deferred per §12. Reducer state shape is independent of that decision.
 */

import { RETRY_BUDGET_TOTAL_MS, RETRY_DELAYS_MS } from './schemas';

// Re-export canonical timing constants for ergonomic single-import-site
export { RETRY_BUDGET_TOTAL_MS, RETRY_DELAYS_MS } from './schemas';

/**
 * Pure state shape for the offline-budget reducer (per contract §10).
 *
 *   - `attemptCount`     0..5; how many retries fired so far
 *   - `nextRetryInMs`    ms remaining until next fire; null when idle/exhausted
 *   - `isExhausted`      true once all 5 retries fired; UI shows manual recovery
 *   - `abortController`  in-flight HTTP abort handle; null when idle
 */
export interface OfflineBudgetState {
  readonly attemptCount: number;
  readonly nextRetryInMs: number | null;
  readonly isExhausted: boolean;
  readonly abortController: AbortController | null;
}

/**
 * Initial state — idle, no retries pending, no abort handle wired.
 *
 * Frozen via `Object.freeze` is NOT applied so reducer action callers can
 * destructure without TS readonly friction. Reducer itself never mutates the
 * input state — only returns new objects.
 */
export const initialOfflineBudgetState: OfflineBudgetState = {
  attemptCount: 0,
  nextRetryInMs: null,
  isExhausted: false,
  abortController: null,
};

/**
 * Discriminated union of retry-budget transitions (per contract §10).
 *
 *   - `NETWORK_ERROR`   batch upload mutation failed → schedule next retry
 *   - `RETRY_TICK`      timer pulse (every 100ms by host hook) → decrement counter
 *   - `RETRY_FIRE`      timer reached 0 → host hook re-issues HTTP request
 *   - `RETRY_SUCCESS`   batch upload settled successfully → reset to initial
 *   - `CANCEL`          user confirmed cancel-modal → abort + reset to initial
 */
export type OfflineBudgetAction =
  | { type: 'NETWORK_ERROR'; abortController: AbortController }
  | { type: 'RETRY_TICK'; deltaMs: number }
  | { type: 'RETRY_FIRE' }
  | { type: 'RETRY_SUCCESS' }
  | { type: 'CANCEL' };

/**
 * Pure reducer — no side effects, no I/O, no `Date.now()`.
 *
 * Transitions:
 *
 *   - `NETWORK_ERROR` from idle / mid-retry:
 *     * If `attemptCount === 5` → mark `isExhausted: true`, clear timer
 *     * Else → bump `attemptCount`, schedule `nextRetryInMs` to
 *       `RETRY_DELAYS_MS[attemptCount]`, store `abortController`
 *
 *   - `RETRY_TICK` decrements `nextRetryInMs` by `deltaMs` (clamped to 0).
 *     No-op when `nextRetryInMs` is null (idle).
 *
 *   - `RETRY_FIRE` zeroes `nextRetryInMs` (host hook re-issues HTTP). Reducer
 *     does NOT bump `attemptCount` here — `attemptCount` only bumps on
 *     `NETWORK_ERROR`. (Distinction matters: a fire-then-success path leaves
 *     `attemptCount` at the value reached during the prior NETWORK_ERROR.)
 *
 *   - `RETRY_SUCCESS` resets to `initialOfflineBudgetState`.
 *
 *   - `CANCEL` resets to `initialOfflineBudgetState`. Caller is responsible
 *     for invoking `state.abortController?.abort()` BEFORE dispatching CANCEL
 *     (reducer cannot perform side effects).
 *
 * Programmer-error sanity: unknown action `type` returns the input state
 * unchanged (defensive default — same pattern as React's `useReducer`).
 */
export function offlineBudgetReducer(
  state: OfflineBudgetState,
  action: OfflineBudgetAction
): OfflineBudgetState {
  switch (action.type) {
    case 'NETWORK_ERROR': {
      // attemptCount represents number of retries already fired; bump to next
      const nextAttempt = state.attemptCount + 1;
      if (nextAttempt > RETRY_DELAYS_MS.length) {
        // All 5 retries already fired — mark exhausted
        return {
          attemptCount: state.attemptCount,
          nextRetryInMs: null,
          isExhausted: true,
          abortController: null,
        };
      }
      // Schedule next retry — RETRY_DELAYS_MS is 0-indexed, so attempt N uses
      // delay [N-1]. With nextAttempt = state.attemptCount + 1, the new attempt
      // index is `state.attemptCount`.
      return {
        attemptCount: nextAttempt,
        nextRetryInMs: RETRY_DELAYS_MS[state.attemptCount],
        isExhausted: false,
        abortController: action.abortController,
      };
    }

    case 'RETRY_TICK': {
      if (state.nextRetryInMs === null) {
        return state; // idle / exhausted — no-op
      }
      const remaining = Math.max(0, state.nextRetryInMs - action.deltaMs);
      return {
        ...state,
        nextRetryInMs: remaining,
      };
    }

    case 'RETRY_FIRE': {
      if (state.nextRetryInMs === null) {
        return state; // defensive — fire dispatched while idle
      }
      return {
        ...state,
        nextRetryInMs: 0,
      };
    }

    case 'RETRY_SUCCESS':
      return initialOfflineBudgetState;

    case 'CANCEL':
      return initialOfflineBudgetState;

    default:
      return state;
  }
}

/**
 * Convenience helper — total ms elapsed across completed delay segments
 * (useful for `<progress role="progressbar" aria-valuenow={...}>` rendering
 * per §10 "31s total budget visibility").
 *
 * Computation: sum of all completed delay segments + (current segment delay
 * minus `nextRetryInMs` remaining). `null` `nextRetryInMs` is treated as 0.
 *
 * @example
 *   // After NETWORK_ERROR (attempt 1) + 600ms ticked
 *   elapsedBudgetMs({ attemptCount: 1, nextRetryInMs: 400, ... })
 *   // → 600   (1000ms delay - 400ms remaining = 600ms elapsed in current segment)
 *
 *   // After NETWORK_ERROR (attempt 2) + 1500ms ticked into 2s delay
 *   elapsedBudgetMs({ attemptCount: 2, nextRetryInMs: 500, ... })
 *   // → 1000 (prev 1s segment) + 1500 (current) = 2500
 */
export function elapsedBudgetMs(state: OfflineBudgetState): number {
  if (state.attemptCount === 0) return 0;

  // Sum delays from completed prior segments
  let elapsed = 0;
  for (let i = 0; i < state.attemptCount - 1; i += 1) {
    elapsed += RETRY_DELAYS_MS[i];
  }

  // Add elapsed portion of current segment
  const currentSegmentDelay = RETRY_DELAYS_MS[state.attemptCount - 1];
  const remaining = state.nextRetryInMs ?? 0;
  elapsed += Math.max(0, currentSegmentDelay - remaining);

  // Clamp to total budget (defensive — cannot exceed 31s)
  return Math.min(elapsed, RETRY_BUDGET_TOTAL_MS);
}
