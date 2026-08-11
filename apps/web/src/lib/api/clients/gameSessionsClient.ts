/**
 * Game Sessions API Client (Game Night Flow)
 *
 * Client for SessionTracking bounded context.
 * Covers: session creation, go-live, finalization.
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

export interface GoLiveResponse {
  sessionId: string;
  gameNightId: string;
  gameNightSessionId: string;
  playOrder: number;
  status: string;
}

/**
 * Create a new game session for a game night
 * POST /api/v1/game-sessions
 *
 * Epic #3188: this now creates a DRAFT (Pending) session. Taking it live is a separate
 * explicit step — see {@link goLive}.
 */
export async function createSession(payload: CreateSessionPayload): Promise<CreateSessionResponse> {
  return apiClient.post<CreateSessionResponse>('/api/v1/game-sessions', payload);
}

/**
 * Take an existing draft game-night session live (Pending → InProgress).
 * POST /api/v1/sessions/{id}/go-live
 *
 * Epic #3188 Slice 2/3 (D5): the explicit go-live sub-resource. Organizer-only; enforces
 * at most one live session per game night (409 on a concurrent go-live or non-draft session).
 */
export async function goLive(sessionId: string): Promise<GoLiveResponse> {
  return apiClient.post<GoLiveResponse>(
    `/api/v1/sessions/${encodeURIComponent(sessionId)}/go-live`,
    {}
  );
}

/**
 * Finalize (close) an active game session
 * POST /api/v1/game-sessions/{sessionId}/finalize
 */
export async function finalizeSession(sessionId: string): Promise<void> {
  await apiClient.post<void>(`/api/v1/game-sessions/${encodeURIComponent(sessionId)}/finalize`, {});
}
