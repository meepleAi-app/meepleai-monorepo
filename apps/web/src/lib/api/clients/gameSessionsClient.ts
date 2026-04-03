/**
 * Game Sessions API Client (Game Night Flow)
 *
 * Client for SessionTracking bounded context.
 * Covers: session creation, finalization, score updates.
 */

import { apiClient } from '@/lib/api/client';

export interface CreateSessionPayload {
  gameNightId: string;
  gameId: string;
  participants: Array<{
    displayName: string;
    userId?: string;
    isGuest: boolean;
  }>;
}

export interface CreateSessionResponse {
  sessionId: string;
  code: string;
}

/**
 * Create a new game session for a game night
 * POST /api/v1/game-sessions
 */
export async function createSession(payload: CreateSessionPayload): Promise<CreateSessionResponse> {
  return apiClient.post<CreateSessionResponse>('/api/v1/game-sessions', payload);
}

/**
 * Finalize (close) an active game session
 * POST /api/v1/game-sessions/{sessionId}/finalize
 */
export async function finalizeSession(sessionId: string): Promise<void> {
  await apiClient.post<void>(`/api/v1/game-sessions/${encodeURIComponent(sessionId)}/finalize`, {});
}

/**
 * Update a participant's score in a session
 * PUT /api/v1/game-sessions/{sessionId}/scores
 */
export async function updateScore(
  sessionId: string,
  participantId: string,
  score: number
): Promise<void> {
  await apiClient.put<void>(`/api/v1/game-sessions/${encodeURIComponent(sessionId)}/scores`, {
    participantId,
    score,
  });
}
