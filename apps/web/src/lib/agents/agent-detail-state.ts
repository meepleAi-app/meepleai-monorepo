/**
 * Pure helpers for the `/agents/[id]` orchestrator FSM (Wave C.2, Issue #581).
 *
 * Phase 0.5 contract sez. 3.2 — state derivation function.
 *
 * 4-state FSM:
 *   - `loading`   → query in flight (agentId valid but result not yet arrived)
 *   - `error`     → query failed (agentQuery.isError === true)
 *   - `not-found` → agentId is null (Cell 1) OR query resolved with null (Cell 4)
 *   - `default`   → query resolved with an AgentDto (agent data available)
 *
 * CRITICAL contract (Phase 0.5 sez. 3 — Cell 1):
 *   `agentId === null` short-circuits FIRST before any other check.
 *   This was missing in the closed PR #697 implementation and is the root cause
 *   of the `/api/v1/agents/undefined` cascade failure (mirror Wave C.1 lesson).
 *
 * FSM precedence: null agentId > loading > error > no-data > default
 *
 * The `error` state is covered by unit tests (deterministic via URL override)
 * but excluded from visual-baseline coverage because reproducing TanStack
 * Query `isError` deterministically via URL override is impractical (mirror
 * Wave B.2/B.3 exclusion rationale).
 *
 * Wave C.2 extends Wave C.1 with a variant matrix (active/draft/archived)
 * and a 2-step dependency chain for Knowledge tab gating (Cell 10).
 * All FSM cells documented in docs/frontend/contracts/agents-id-hooks.md sez. 3.
 */

export type AgentDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export interface DeriveAgentDetailUiStateInput {
  /**
   * Normalized agentId from URL params.
   * MUST be string | null — NEVER undefined or the string 'undefined'.
   * See Phase 0.5 contract sez. 2.1 for normalization rules.
   */
  readonly agentId: string | null;
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly hasData: boolean;
}

/**
 * Pure FSM resolver for the `/agents/[id]` orchestrator.
 *
 * Precedence order (per Phase 0.5 contract sez. 3):
 *   1. agentId null  → not-found (Cell 1: no valid id, no fetch should happen)
 *   2. isLoading     → loading   (Cell 2: fetch in flight)
 *   3. isError       → error     (Cell 3: fetch failed)
 *   4. !hasData      → not-found (Cell 4: success(null), agent not found)
 *   5. default       → default   (Cells 5-10: success(data), render agent)
 *
 * @param input - FSM input (agentId must be string|null)
 * @returns AgentDetailUiState
 */
export function deriveAgentDetailUiState(input: DeriveAgentDetailUiStateInput): AgentDetailUiState {
  // ⚠️ CRITICAL: agentId === null short-circuits FIRST (Phase 0.5 Cell 1 contract)
  // Using == to catch both null and undefined defensively, though input contract
  // mandates string|null (never undefined per sez. 2.1)
  if (input.agentId == null) return 'not-found';

  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';

  // Cell 4: success(null) — agent not found
  if (!input.hasData) return 'not-found';

  // Cells 5-10: success(data) — full agent detail available
  // Cell 10 distinction (agent.gameId === null → standalone Knowledge tab)
  // is handled at the orchestrator level, not here — this FSM only tracks
  // the top-level agent query state.
  return 'default';
}
