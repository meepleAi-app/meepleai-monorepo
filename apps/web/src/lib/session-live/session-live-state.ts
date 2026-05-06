/**
 * FSM state derivation for the /sessions/[id]/live v2 surface (Wave D.2).
 *
 * Tier L route — complex cartesian FSM:
 *   5 base states × 5 connection states × 3 dialog states × 3 role variants.
 *
 * This module handles ONLY the 5 base UI states derived from TanStack Query.
 * Connection states, dialog states, and role variants are orchestrated
 * in SessionLiveView (Task 3) using URL state SSOT.
 *
 * Pattern blueprint: Wave D.1 sessions-state.ts (PR #736) adapted for
 * session-live route with sessionId null-guard as Cell 1.
 *
 * No React, no API client — pure functions for unit-testability.
 *
 * Wave D.2 Foundation sub-PR — Issue #746
 */

/**
 * The 4 mutually-exclusive base UI states for the /sessions/[id]/live view.
 *
 * Precedence (highest → lowest):
 *   not-found (null sessionId) → loading → error → default
 *
 * Note: unlike list-view FSMs (Wave D.1), this route has no 'empty' or
 * 'filtered-empty' state. The session is a single entity: either present
 * (default) or absent (not-found).
 *
 * Cell mapping per contract §4.1:
 *   Cell 1: sessionId === null            → 'not-found'
 *   Cell 2: isLoading === true            → 'loading'
 *   Cell 3: isError === true              → 'error'
 *   Cell 4: !hasData                      → 'not-found' (404 from backend)
 *   Cell 5: hasData                       → 'default'
 */
export type SessionLiveUiState = 'loading' | 'error' | 'not-found' | 'default';

/**
 * The 3 dialog states for the session-live route.
 * Driven by ?dialog= URL search param (SSOT — no useState mirror).
 * Serializable for deep-link to confirmation dialogs.
 */
export type SessionLiveDialogState = 'none' | 'pause' | 'endgame';

/** Input for base FSM derivation — all fields are read-only scalars. */
export interface DeriveSessionLiveUiStateInput {
  /**
   * Validated session ID from useParams — null when params?.id is missing
   * or not a valid non-empty string. Never undefined or literal 'undefined'.
   * See contract §2.1 anti-pattern: use rawId && rawId.length > 0 check.
   */
  readonly sessionId: string | null;
  /** True while the TanStack Query fetch is in-flight. */
  readonly isLoading: boolean;
  /** True when the most recent fetch resolved with an error. */
  readonly isError: boolean;
  /** True when session DTO was returned from backend (not null). */
  readonly hasData: boolean;
}

/**
 * Derives the current base UI state from sessionId validation and TanStack Query state.
 *
 * Precedence (contract §4.1):
 *   Cell 1: sessionId === null → 'not-found'  (null param guard, highest priority)
 *   Cell 2: isLoading          → 'loading'    (fetch in-flight)
 *   Cell 3: isError            → 'error'      (fetch failed)
 *   Cell 4: !hasData           → 'not-found'  (404 from backend, session absent)
 *   Cell 5: default            → 'default'    (healthy, data present)
 *
 * Race condition: isLoading + isError — loading wins (Cell 2 before Cell 3).
 * Race condition: !hasData + isLoading — loading wins (Cell 2 before Cell 4).
 */
export function deriveSessionLiveUiState(input: DeriveSessionLiveUiStateInput): SessionLiveUiState {
  if (input.sessionId == null) return 'not-found'; // Cell 1
  if (input.isLoading) return 'loading'; // Cell 2
  if (input.isError) return 'error'; // Cell 3
  if (!input.hasData) return 'not-found'; // Cell 4
  return 'default'; // Cell 5
}

/**
 * Derives the dialog state from URL search params.
 *
 * ?dialog=pause     → 'pause'    (PauseOverlay open)
 * ?dialog=endgame   → 'endgame'  (EndgameDialog open)
 * (absent / other)  → 'none'
 *
 * URL is the SSOT — no useState<DialogKind> + sync hook anti-pattern.
 * Supports deep-linking to "Sessione in pausa" confirmation state.
 */
export function deriveSessionLiveDialogState(
  searchParams: URLSearchParams
): SessionLiveDialogState {
  const raw = searchParams.get('dialog');
  if (raw === 'pause') return 'pause';
  if (raw === 'endgame') return 'endgame';
  return 'none';
}

// ---------------------------------------------------------------------------
// State override re-export (for orchestrator import convenience)
// ---------------------------------------------------------------------------

/**
 * Re-exported from session-live-visual-test-fixture so orchestrator (Task 3) can
 * import `parseStateOverride` from the same barrel as `deriveSessionLiveUiState`
 * without coupling to the fixture module directly.
 *
 * Pattern mirror: Wave D.1 sessions-state.ts re-export.
 *
 * NOTE: ICU plural usage reminder for orchestrator (Gate A compliance):
 * When rendering `pages.sessionLive.topBar.turnLabel`, the orchestrator MUST use:
 *   t('pages.sessionLive.topBar.turnLabel', { count: currentTurn, total: totalTurns })
 * NEVER raw string replace. The `{count, plural, ...}` ICU syntax requires
 * the react-intl / next-intl formatter to handle pluralization correctly.
 */
export { parseStateOverride } from './session-live-visual-test-fixture';
