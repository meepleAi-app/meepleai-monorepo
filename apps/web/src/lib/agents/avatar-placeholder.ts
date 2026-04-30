/**
 * Deterministic emoji avatar picker for agents (Wave B.2, Issue #634).
 *
 * AgentDto has no avatar field (backend gap). Wave B.2 ships emoji-based
 * placeholder derived from `agent.id` so the same agent always gets the same
 * face across renders and clients.
 *
 * Pool intentionally small (~10) to keep visual variety meaningful at typical
 * library size (5-30 agents). Replace with proper avatar URLs in a follow-up.
 */

const AGENT_EMOJI_POOL = ['🤖', '🎮', '🎯', '🧠', '🎲', '🃏', '📚', '🎭', '⚔️', '🛡️'] as const;

export const AVATAR_EMOJI_POOL: readonly string[] = AGENT_EMOJI_POOL;

export function pickAvatarEmoji(agentId: string): string {
  if (agentId.length === 0) return AGENT_EMOJI_POOL[0];
  let hash = 0;
  for (let i = 0; i < agentId.length; i++) {
    hash = (hash * 31 + agentId.charCodeAt(i)) >>> 0;
  }
  return AGENT_EMOJI_POOL[hash % AGENT_EMOJI_POOL.length];
}
