/**
 * Variant derivation for `/agents/[id]` v2 view (Wave C.2, Issue #581).
 *
 * Phase 0.5 contract sez. 3.3 — variant resolver.
 *
 * 3-state variant matrix:
 *   - `active`   → agent is operational and ready to use
 *   - `draft`    → agent created but never invoked (not yet set up)
 *   - `archived` → agent is inactive / soft-archived
 *
 * Backend schema note (Wave C.2 implementation decision):
 *   AgentDto (apps/web/src/lib/api/schemas/agents.schemas.ts) does NOT expose
 *   `archivedAt` or `systemPrompt` fields. The Phase 0.5 contract sez. 3.3
 *   specifies these as possible sources but defers to implementation:
 *     "Decision rinviata a implementation Task 1: leggere AgentDto."
 *
 *   Resolution: derive from available canonical fields:
 *     - `isActive === false` → 'archived'   (matches contract intent for archivedAt != null)
 *     - `isActive === true && invocationCount === 0` → 'draft'
 *       (mirrors derive-status.ts 'in-setup' logic — agent never invoked)
 *     - `isActive === true && invocationCount > 0` → 'active'
 *
 *   Optional extension: if future backend adds `archivedAt` or `systemPrompt`,
 *   the function accepts a broader interface via optional fields.
 *
 * Render rules per Phase 0.5 contract sez. 4.2:
 *   - `active`   → CTA Play visible; no banner
 *   - `draft`    → CTA Setup + setup banner; Performance/History tabs LOCKED
 *   - `archived` → CTA Unarchive + archived banner; all tabs READ-ONLY
 */

/**
 * UI variant for an agent detail page view.
 * Controls CTA labels, banners, and tab availability.
 */
export type AgentVariant = 'active' | 'draft' | 'archived';

/**
 * Minimal agent shape required for variant derivation.
 * Using a structural interface allows the function to work with AgentDto,
 * AgentDetailDto, or any superset without tight coupling to a single schema.
 *
 * Optional fields (`archivedAt`, `systemPrompt`) are present for forward-
 * compatibility if the backend adds them — they take precedence when present.
 */
export interface AgentVariantInput {
  /** Whether the agent is currently active (not soft-archived). Required. */
  readonly isActive: boolean;
  /** Number of invocations (0 → agent in draft/setup state). Required. */
  readonly invocationCount: number;
  /**
   * ISO-8601 timestamp of when the agent was archived, or null.
   * Optional — takes precedence over `isActive` when provided.
   * Maps to contract field `agent.archivedAt`.
   */
  readonly archivedAt?: string | null;
  /**
   * The agent's system prompt text, or null/empty string if not configured.
   * Optional — used as secondary draft signal when present.
   * Maps to contract field `agent.systemPrompt`.
   */
  readonly systemPrompt?: string | null;
}

/**
 * Derives the UI variant for an agent based on its current state.
 *
 * Derivation precedence:
 *   1. `archivedAt != null` → 'archived'  (explicit archive timestamp wins)
 *   2. `!isActive`          → 'archived'  (isActive === false = soft-archived)
 *   3. `systemPrompt == null || ''` → 'draft' (if systemPrompt available)
 *   4. `invocationCount === 0`      → 'draft' (never invoked = in-setup)
 *   5. default → 'active'
 *
 * @param agent - Partial agent data sufficient for variant derivation
 * @returns AgentVariant — the UI render variant
 */
export function deriveAgentVariant(agent: AgentVariantInput): AgentVariant {
  // Step 1: archivedAt takes precedence when backend provides it
  if (agent.archivedAt != null) return 'archived';

  // Step 2: isActive === false → archived (canonical field always available)
  if (!agent.isActive) return 'archived';

  // Step 3: if systemPrompt is present and empty/null → draft
  if (
    agent.systemPrompt !== undefined &&
    (agent.systemPrompt == null || agent.systemPrompt === '')
  ) {
    return 'draft';
  }

  // Step 4: fallback to invocationCount — mirrors derive-status.ts logic
  // invocationCount === 0 → agent in setup state (created but never run)
  if (agent.invocationCount === 0) return 'draft';

  // Step 5: active and has been invoked
  return 'active';
}
