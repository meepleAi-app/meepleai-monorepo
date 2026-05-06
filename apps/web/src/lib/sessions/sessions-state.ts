/**
 * FSM state derivation for the /sessions v2 surface (Wave D.1).
 *
 * Tier S route — single hook (useActiveSessions) + linear 5-state FSM.
 * Pattern blueprint: Wave 4 D1 (PR #717) `/players` route.
 *
 * Schema reality: /sessions displays ALL user sessions (active + history).
 * Wave D.1 = visual upgrade only; the page currently shows only active sessions
 * via useActiveSessions. A session-history hook is a separate followup.
 * Foundation declares the FSM shape; orchestrator (Task 3) composes data sources.
 *
 * No React, no API client — pure function for unit-testability.
 */

/**
 * The 5 mutually-exclusive UI states for the /sessions view.
 *
 * Precedence (highest → lowest):
 *   loading → error → empty → filtered-empty → default
 */
export type SessionsUiState = 'loading' | 'error' | 'empty' | 'filtered-empty' | 'default';

/** Input for FSM derivation — all fields are read-only scalars. */
export interface DeriveSessionsUiStateInput {
  /** True while the TanStack Query fetch is in-flight. */
  readonly isLoading: boolean;
  /** True when the most recent fetch resolved with an error. */
  readonly isError: boolean;
  /** Total count of sessions from backend (before client-side filtering). */
  readonly totalCount: number;
  /** Count of sessions remaining after all active filters are applied. */
  readonly filteredCount: number;
}

/**
 * Derives the current UI state from TanStack Query and filter state.
 *
 * Precedence:
 *   1. `loading`       — fetch in-flight (beats everything)
 *   2. `error`         — fetch failed
 *   3. `empty`         — no data at all from backend (totalCount === 0)
 *   4. `filtered-empty`— data exists but active filter eliminated all rows
 *   5. `default`       — healthy, data present (with or without visible filter)
 */
export function deriveSessionsUiState(input: DeriveSessionsUiStateInput): SessionsUiState {
  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';
  if (input.totalCount === 0) return 'empty';
  if (input.filteredCount === 0) return 'filtered-empty';
  return 'default';
}

// ---------------------------------------------------------------------------
// State override re-export (for orchestrator import convenience)
// ---------------------------------------------------------------------------

/**
 * Re-exported from sessions-visual-test-fixture so orchestrator (Task 3) can
 * import `parseStateOverride` from the same barrel as `deriveSessionsUiState`
 * without coupling to the fixture module directly.
 */
export { parseStateOverride } from './sessions-visual-test-fixture';
