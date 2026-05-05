/**
 * Derives a UI-facing status from the canonical AgentDto fields (Wave B.2, Issue #634).
 *
 * Maps `(isActive, invocationCount)` → discriminated UI status:
 *   - `'attivo'`     : isActive && invocationCount > 0  (operational, has been used)
 *   - `'in-setup'`   : isActive && invocationCount === 0 (created, never invoked)
 *   - `'archiviato'` : !isActive (soft-archived; ignores invocation count)
 *
 * Backend gap: AgentDto has no `status` field. Wave B.2 ships derivation in
 * frontend; follow-up issue may promote this to a server-side computed field.
 *
 * `isIdle` does NOT influence status — that flag is a recency hint only.
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

export type AgentDerivedStatus = 'attivo' | 'in-setup' | 'archiviato';

export function deriveStatus(
  agent: Pick<AgentDto, 'isActive' | 'invocationCount'>
): AgentDerivedStatus {
  if (!agent.isActive) return 'archiviato';
  if (agent.invocationCount > 0) return 'attivo';
  return 'in-setup';
}
