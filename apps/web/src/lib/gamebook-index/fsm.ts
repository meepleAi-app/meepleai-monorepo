/**
 * 6-cell FSM derivation for `/gamebook` index (SP6 Phase B Task 1).
 *
 * Pure function — no React, no API client. Composes 2 query inputs (gamebooks,
 * quota) into a single discriminated union output cell. The orchestrator
 * (Task 3) renders the appropriate UI per cell kind.
 *
 * Schema reality v1 carryover (Gate B): see `schemas.ts` header. Both
 * `useGamebooks` and `useQuotaInfo` hooks are aspirational — the orchestrator
 * (Task 3) is responsible for adapting whatever the backend exposes (or stub
 * via fixture) into the canonical shapes consumed here.
 *
 * Cell precedence (highest → lowest):
 *   1. loading      — ANY of the 2 queries pending
 *   2. error        — ANY query errored (gamebooks error preferred for messaging)
 *   3. quota-hard   — quota.used >= quota.total (≥1 gamebook)
 *   4. quota-soft   — quota.used / total ≥ 0.9 (≥1 gamebook, < 100%)
 *   5. empty        — gamebooks.length === 0 (data resolved, no error)
 *   6. default      — has ≥1 gamebook AND quota < 90%
 *
 * Edge cases:
 *   - When gamebooks=[] AND quota at hard/soft limit, the user has used their
 *     translation budget without any gamebook in their library. We still
 *     surface the `empty` cell — the quota banner is contextually irrelevant
 *     when there are no gamebooks to query against. Cell precedence: quota
 *     warnings only fire when ≥1 gamebook exists.
 *
 *   - The `error` cell prefers `gamebooksQuery.error` over `quotaQuery.error`
 *     because the gamebooks list is the primary content (quota is auxiliary).
 *     If the gamebooks query succeeded but quota errored, we still treat it
 *     as a global error (the QuotaWidget can't be rendered without data).
 */

import type { GamebookCardData, QuotaInfo } from './schemas';

// ---------------------------------------------------------------------------
// Cell discriminated union
// ---------------------------------------------------------------------------

export type GamebookIndexFSMCell =
  | { kind: 'loading' }
  | { kind: 'error'; error: Error }
  | { kind: 'empty'; quota: QuotaInfo }
  | {
      kind: 'default';
      gamebooks: readonly GamebookCardData[];
      quota: QuotaInfo;
    }
  | {
      kind: 'quota-soft';
      gamebooks: readonly GamebookCardData[];
      quota: QuotaInfo;
    }
  | {
      kind: 'quota-hard';
      gamebooks: readonly GamebookCardData[];
      quota: QuotaInfo;
    };

// ---------------------------------------------------------------------------
// Input shapes (mirror TanStack Query's UseQueryResult slice)
// ---------------------------------------------------------------------------

/**
 * Minimal slice of TanStack `UseQueryResult` we actually need. Independent
 * type so this module stays decoupled from `@tanstack/react-query`.
 */
export interface QueryLike<TData> {
  readonly isPending: boolean;
  readonly isError: boolean;
  readonly error?: Error | null;
  readonly data?: TData | null;
}

export interface DeriveStateInput {
  readonly gamebooksQuery: QueryLike<readonly GamebookCardData[]>;
  readonly quotaQuery: QueryLike<QuotaInfo>;
}

// ---------------------------------------------------------------------------
// Threshold constants
// ---------------------------------------------------------------------------

/**
 * Soft-warning threshold for quota. When `used / total >= QUOTA_SOFT_RATIO`
 * (and < 1.0), the FSM emits the `quota-soft` cell which renders the
 * QuotaWidget in warning state and surfaces the upgrade CTA.
 */
export const QUOTA_SOFT_RATIO = 0.9;

// ---------------------------------------------------------------------------
// Derivation
// ---------------------------------------------------------------------------

/**
 * Derive the 6-cell FSM cell from query state.
 *
 * Cell kinds are mutually exclusive — the function returns exactly one cell.
 * The discriminant `kind` is narrow-typeable in an exhaustive switch.
 */
export function deriveGamebookIndexState(input: DeriveStateInput): GamebookIndexFSMCell {
  const { gamebooksQuery, quotaQuery } = input;

  // Cell 1: loading — ANY query pending
  if (gamebooksQuery.isPending || quotaQuery.isPending) {
    return { kind: 'loading' };
  }

  // Cell 2: error — ANY query errored (prefer gamebooks error for surfaced messaging)
  if (gamebooksQuery.isError) {
    return {
      kind: 'error',
      error: gamebooksQuery.error ?? new Error('Gamebooks query failed'),
    };
  }
  if (quotaQuery.isError) {
    return {
      kind: 'error',
      error: quotaQuery.error ?? new Error('Quota query failed'),
    };
  }

  // From here on, both queries succeeded. Default to defensive fallbacks
  // (TanStack returns `data: undefined` when query disabled).
  const gamebooks = gamebooksQuery.data ?? [];
  const quota = quotaQuery.data;

  // Defensive: if quota data is null/undefined despite no error, treat as
  // an error rather than rendering with garbage. This guards against query
  // disabled state that didn't propagate isPending correctly.
  if (quota === null || quota === undefined) {
    return {
      kind: 'error',
      error: new Error('Quota data missing'),
    };
  }

  // Cell 5: empty — no gamebooks, regardless of quota state
  // Rationale: quota warnings are contextually irrelevant without any
  // gamebook content. Show empty state CTA (camera shutter) instead.
  if (gamebooks.length === 0) {
    return { kind: 'empty', quota };
  }

  // Compute quota ratio (guard total > 0 enforced by Zod schema, but
  // belt-and-suspenders for runtime safety).
  const ratio = quota.total > 0 ? quota.used / quota.total : 0;

  // Cell 3: quota-hard — used >= total (100%+)
  if (quota.used >= quota.total) {
    return { kind: 'quota-hard', gamebooks, quota };
  }

  // Cell 4: quota-soft — ratio >= 0.9 AND < 1.0
  if (ratio >= QUOTA_SOFT_RATIO) {
    return { kind: 'quota-soft', gamebooks, quota };
  }

  // Cell 6: default — has gamebooks, quota healthy
  return { kind: 'default', gamebooks, quota };
}
