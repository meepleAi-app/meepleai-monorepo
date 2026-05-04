/**
 * Pure helpers for the `/games/[id]` orchestrator FSM (Wave C.1, Issue #581).
 *
 * Phase 0.5 contract sez. 3.2 — state derivation function.
 *
 * 4-state FSM:
 *   - `loading`   → query in flight (gameId valid but result not yet arrived)
 *   - `error`     → query failed (libraryQuery.isError === true)
 *   - `not-found` → gameId is null (Cell 1) OR query resolved with null (Cell 4)
 *   - `default`   → query resolved with a LibraryGameDetail (game data available)
 *
 * CRITICAL contract (Phase 0.5 sez. 3 — Cell 1):
 *   `gameId === null` short-circuits FIRST before any other check.
 *   This was missing in the closed PR #697 implementation and is the root cause
 *   of the `/api/v1/agents/undefined` cascade failure.
 *
 * FSM precedence: null gameId > loading > error > no-data > default
 *
 * The `error` state is covered by unit tests (deterministic via URL override)
 * but excluded from visual-baseline coverage because reproducing TanStack
 * Query `isError` deterministically via URL override is impractical (mirror
 * Wave B.2/B.3 exclusion rationale).
 */

export type GameDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export interface DeriveGameDetailUiStateInput {
  /**
   * Normalized gameId from URL params.
   * MUST be string | null — NEVER undefined or the string 'undefined'.
   * See Phase 0.5 contract sez. 2.1 for normalization rules.
   */
  readonly gameId: string | null;
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly hasData: boolean;
}

/**
 * Pure FSM resolver for the `/games/[id]` orchestrator.
 *
 * Precedence order (per Phase 0.5 contract sez. 3):
 *   1. gameId null   → not-found (Cell 1: no valid id, no fetch should happen)
 *   2. isLoading     → loading   (Cell 2: fetch in flight)
 *   3. isError       → error     (Cell 3: fetch failed)
 *   4. !hasData      → not-found (Cell 4: success(null), game not in library)
 *   5. default       → default   (Cell 5: success(data), render game)
 *
 * @param input - FSM input (gameId must be string|null)
 * @returns GameDetailUiState
 */
export function deriveGameDetailUiState(input: DeriveGameDetailUiStateInput): GameDetailUiState {
  // ⚠️ CRITICAL: gameId === null short-circuits FIRST (Phase 0.5 Cell 1 contract)
  // Using == to catch both null and undefined defensively, though input contract
  // mandates string|null (never undefined per sez. 2.1)
  if (input.gameId == null) return 'not-found';

  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';

  // Cell 4: success(null) — game not found in library/catalog
  if (!input.hasData) return 'not-found';

  // Cell 5: success(data) — full game detail available
  return 'default';
}
