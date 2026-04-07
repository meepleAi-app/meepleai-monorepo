/**
 * Game Night Session API Client (Game Night Experience v2)
 *
 * Client for multi-session game night flow: start sessions,
 * complete sessions, finalize nights, and retrieve diary.
 */

import { apiClient } from '@/lib/api/client';

const BASE = '/api/v1/game-nights';

export interface StartSessionResponse {
  sessionId: string;
  gameNightSessionId: string;
  sessionCode: string;
  playOrder: number;
}

export interface DiaryEntryDto {
  id: string;
  sessionId: string;
  eventType: string;
  description: string;
  payload?: string;
  actorId?: string;
  timestamp: string;
}

export interface DiaryResponse {
  gameNightId: string;
  entries: DiaryEntryDto[];
}

export const gameNightSessionClient = {
  startSession: (gameNightId: string, gameId: string, gameTitle: string) =>
    apiClient.post<StartSessionResponse>(`${BASE}/${encodeURIComponent(gameNightId)}/sessions`, {
      gameId,
      gameTitle,
    }),

  completeSession: (gameNightId: string, winnerId?: string) =>
    apiClient.post<void>(`${BASE}/${encodeURIComponent(gameNightId)}/sessions/complete`, {
      winnerId: winnerId ?? null,
    }),

  finalizeNight: (gameNightId: string) =>
    apiClient.post<void>(`${BASE}/${encodeURIComponent(gameNightId)}/finalize`, {}),

  getDiary: (gameNightId: string) =>
    apiClient.get<DiaryResponse>(`${BASE}/${encodeURIComponent(gameNightId)}/diary`),
};
