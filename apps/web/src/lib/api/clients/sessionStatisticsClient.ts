import {
  SessionStatisticsSchema,
  GameStatisticsSchema,
  type SessionStatistics,
  type GameStatistics,
} from '../schemas/session-statistics.schemas';

import type { HttpClient } from '../core/httpClient';

export interface SessionStatisticsClient {
  getStatistics(monthsBack?: number): Promise<SessionStatistics>;
  getGameStatistics(gameId: string): Promise<GameStatistics>;
}

export function createSessionStatisticsClient(deps: {
  httpClient: HttpClient;
}): SessionStatisticsClient {
  const { httpClient } = deps;

  return {
    async getStatistics(monthsBack?: number): Promise<SessionStatistics> {
      const params = monthsBack ? `?monthsBack=${monthsBack}` : '';
      const data = await httpClient.get(`/game-sessions/session-statistics${params}`);
      return SessionStatisticsSchema.parse(data);
    },

    async getGameStatistics(gameId: string): Promise<GameStatistics> {
      const data = await httpClient.get(`/game-sessions/session-statistics/game/${gameId}`);
      return GameStatisticsSchema.parse(data);
    },
  };
}
