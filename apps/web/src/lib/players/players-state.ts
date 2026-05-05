/**
 * FSM state derivation for the /players v2 surface (Wave 4 D1).
 *
 * Tier S route — single hook (usePlayerStatistics) + linear 5-state FSM.
 * Pattern blueprint: Wave B.1 (PR #635) `/games?tab=library`.
 *
 * Schema reality: /players actually displays *games played by user*,
 * derived from the `gamePlayCounts: Record<gameName, count>` field on
 * PlayerStatistics. The "players" naming is an anti-pattern carryover
 * from v1. Wave 4 D1 = visual upgrade only; backend redesign is a
 * separate followup issue post-merge.
 *
 * No React, no API client — pure function for unit-testability.
 */

/**
 * The 5 mutually-exclusive UI states for the /players view.
 *
 * Precedence (highest → lowest):
 *   loading → error → empty → filtered-empty → default
 */
export type PlayersUiState = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

/** Input for FSM derivation — all fields are read-only scalars. */
export interface DerivePlayersUiStateInput {
  /** True while the TanStack Query fetch is in-flight. */
  readonly isLoading: boolean;
  /** True when the most recent fetch resolved with an error. */
  readonly isError: boolean;
  /** True when `gamePlayCounts` has at least one entry. */
  readonly hasData: boolean;
  /** True when any filter (search) is currently applied. */
  readonly hasFilters: boolean;
  /** Count of items remaining after all active filters are applied. */
  readonly filteredCount: number;
}

/**
 * Derives the current UI state from TanStack Query and filter state.
 *
 * Precedence:
 *   1. `loading`       — fetch in-flight (beats everything)
 *   2. `error`         — fetch failed
 *   3. `empty`         — no data at all from backend
 *   4. `filtered-empty`— data exists but active filter eliminated all rows
 *   5. `default`       — healthy, data present (with or without visible filter)
 */
export function derivePlayersUiState(input: DerivePlayersUiStateInput): PlayersUiState {
  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';
  if (!input.hasData) return 'empty';
  if (input.hasFilters && input.filteredCount === 0) return 'filtered-empty';
  return 'default';
}
