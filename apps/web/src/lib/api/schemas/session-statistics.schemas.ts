import { z } from 'zod';

export const GamePlayFrequencySchema = z.object({
  gameId: z.string().uuid(),
  gameName: z.string(),
  playCount: z.number(),
});

export const ScoreTrendSchema = z.object({
  date: z.string(),
  gameName: z.string(),
  finalScore: z.number(),
});

export const MonthlyPlayCountSchema = z.object({
  month: z.string(),
  sessionCount: z.number(),
});

export const SessionStatisticsSchema = z.object({
  totalSessions: z.number(),
  totalGamesPlayed: z.number(),
  averageSessionDuration: z.string(),
  mostPlayedGames: z.array(GamePlayFrequencySchema),
  recentScoreTrends: z.array(ScoreTrendSchema),
  monthlyActivity: z.array(MonthlyPlayCountSchema),
});

export const GameStatisticsSchema = z.object({
  gameId: z.string().uuid(),
  gameName: z.string(),
  totalPlays: z.number(),
  wins: z.number(),
  winRate: z.number(),
  averageScore: z.number(),
  highScore: z.number(),
  averageSessionDuration: z.string(),
  scoreHistory: z.array(ScoreTrendSchema),
});

export type GamePlayFrequency = z.infer<typeof GamePlayFrequencySchema>;
export type ScoreTrend = z.infer<typeof ScoreTrendSchema>;
export type MonthlyPlayCount = z.infer<typeof MonthlyPlayCountSchema>;
export type SessionStatistics = z.infer<typeof SessionStatisticsSchema>;
export type GameStatistics = z.infer<typeof GameStatisticsSchema>;
