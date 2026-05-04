/**
 * Pure helpers for the `/games/[id]` orchestrator FSM (Wave C.1, Issue #581).
 *
 * Mirror of `library/library-filters.deriveLibraryUiState` (Wave B.3) — kept
 * as a tiny pure function so the orchestrator can stay declarative and the
 * unit tests can exercise the FSM matrix without rendering.
 *
 * 5-state FSM:
 *   - `loading`   → query in flight, no data, no error, no fixture
 *   - `error`     → query failed (libraryQuery.isError === true)
 *   - `not-found` → query resolved with `null` (game not in user library)
 *   - `default`   → query resolved with a `LibraryGameDetail`
 *   - `empty`     → reserved for future use; today reuses `not-found` shape
 *                   so the orchestrator only needs 4 real surfaces but the
 *                   visual-state matrix (state coverage spec) keeps 4
 *                   distinct screens (default, loading, empty=not-found,
 *                   not-found alias).
 *
 * The `error` surface is covered by unit tests (deterministic via override)
 * but excluded from visual-baseline coverage because reproducing TanStack
 * Query `isError` deterministically via URL override is impractical (mirror
 * Wave B.2/B.3 exclusion rationale).
 */

export type GameDetailUiState = 'default' | 'loading' | 'empty' | 'not-found' | 'error';

export interface DeriveGameDetailUiStateInput {
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly hasData: boolean;
}

/**
 * Pure FSM resolver. Order of precedence: error > loading > not-found > default.
 */
export function deriveGameDetailUiState(input: DeriveGameDetailUiStateInput): GameDetailUiState {
  if (input.isError) return 'error';
  if (input.isLoading) return 'loading';
  if (!input.hasData) return 'not-found';
  return 'default';
}
