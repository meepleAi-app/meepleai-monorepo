/**
 * Session Agent Client — SSE streaming helper
 * Game Night Flow feature
 *
 * Provides utilities for the agent chat endpoint:
 * POST /api/v1/game-sessions/{gameSessionId}/agent/chat
 *
 * The endpoint streams SSE events with the following shapes:
 *  - { type: "token", content: string }
 *  - { type: "complete", threadId: string }
 *  - { type: "error", message: string }
 */

import { getApiBase } from '@/lib/api/core/httpClient';

export interface AgentGameContext {
  gameId: string;
  gameTitle: string;
  players: string[];
  currentTurn: number;
  responseLanguage: string;
}

export interface AgentChatPayload {
  /** The UUID of the active agent session (from LaunchSessionAgent response) */
  agentSessionId: string;
  userQuestion: string;
  chatThreadId?: string;
  gameContext?: AgentGameContext;
}

/**
 * Returns the full URL for the agent chat streaming endpoint.
 * Pattern: POST /api/v1/game-sessions/{gameSessionId}/agent/chat
 */
export function getAgentChatUrl(gameSessionId: string): string {
  const base = getApiBase();
  return `${base}/api/v1/game-sessions/${encodeURIComponent(gameSessionId)}/agent/chat`;
}
