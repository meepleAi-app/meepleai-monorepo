/**
 * FSM state derivation for the /players/[id] v2 surface (Wave 3).
 *
 * Tier M route — single hook (usePlayerStatistics) + linear 4-state FSM.
 * Pattern blueprint: Wave 4 D1 (PR #717) `/players` index.
 *
 * Schema reality: /players/[id] page is mostly decorative — playerId from URL
 * slug is decoded as displayName. Hook used: usePlayerStatistics() returns
 * CURRENT USER's aggregated stats (not other-player data). The page is a
 * "self profile" pretending to be an other-player profile.
 *
 * Wave 3 = visual upgrade only. Maintain v1 data shape.
 * Filed followup post-merge for true player profile API.
 *
 * No React, no API client — pure function for unit-testability.
 */

/**
 * The 4 mutually-exclusive UI states for the /players/[id] view.
 *
 * Precedence (highest → lowest):
 *   null playerId → loading → error → !hasData (not-found) → default
 */
export type PlayerDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

/** Input for FSM derivation — all fields are read-only scalars. */
export interface DerivePlayerDetailUiStateInput {
  /**
   * The player ID decoded from the URL slug.
   * `null` when the URL param is absent or invalid — immediately maps to `not-found`.
   *
   * @note Use `string | null`, NEVER `string | undefined` — the FSM contract
   * uses `== null` null-coalescing check which covers both, but the type
   * contract is explicit for callers.
   */
  readonly playerId: string | null;
  /** True while the TanStack Query fetch is in-flight. */
  readonly isLoading: boolean;
  /** True when the most recent fetch resolved with an error. */
  readonly isError: boolean;
  /** True when the statistics query returned meaningful data. */
  readonly hasData: boolean;
}

/**
 * Derives the current UI state from playerId presence, TanStack Query state.
 *
 * Precedence:
 *   1. `not-found`  — playerId is null (no valid URL param to work with)
 *   2. `loading`    — fetch in-flight
 *   3. `error`      — fetch failed
 *   4. `not-found`  — data resolved but no content (e.g. backend 404)
 *   5. `default`    — healthy, data present
 */
export function derivePlayerDetailUiState(
  input: DerivePlayerDetailUiStateInput
): PlayerDetailUiState {
  if (input.playerId == null) return 'not-found';
  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';
  if (!input.hasData) return 'not-found';
  return 'default';
}
