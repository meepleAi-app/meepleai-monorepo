'use client';

import { useMemo } from 'react';

import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import type { SessionSummaryDto, UserGameDto } from '@/lib/api/dashboard-client';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// ── Types ────────────────────────────────────────────────────────────────────

export type DashboardContextType =
  | 'active-session'
  | 'upcoming-game-night'
  | 'incomplete-session'
  | 'last-played'
  | 'welcome';

export interface HeroPriority {
  type: DashboardContextType;
  priority: number;
  data: unknown;
}

export interface DashboardContextInput {
  activeSession:
    | (Pick<GameSessionDto, 'id' | 'gameId'> & {
        gameName?: string;
        playerCount: number;
        durationMinutes: number;
      })
    | null;
  upcomingGameNight: { id: string; title: string; scheduledAt: string } | null;
  incompleteSessions: Array<{ id: string; gameName: string; sessionDate: string }>;
  lastPlayed: (Pick<UserGameDto, 'id' | 'title' | 'imageUrl'> & { lastPlayedAt: string }) | null;
}

// ── Pure function (testable) ────────────────────────────────────────────────

export function computeHeroPriority(input: DashboardContextInput): HeroPriority {
  if (input.activeSession) {
    return { type: 'active-session', priority: 100, data: input.activeSession };
  }

  if (input.upcomingGameNight) {
    const hoursUntil =
      (new Date(input.upcomingGameNight.scheduledAt).getTime() - Date.now()) / 3_600_000;
    if (hoursUntil <= 24 && hoursUntil > 0) {
      return { type: 'upcoming-game-night', priority: 90, data: input.upcomingGameNight };
    }
  }

  if (input.incompleteSessions.length > 0) {
    return { type: 'incomplete-session', priority: 80, data: input.incompleteSessions[0] };
  }

  if (input.lastPlayed) {
    return { type: 'last-played', priority: 50, data: input.lastPlayed };
  }

  return { type: 'welcome', priority: 10, data: null };
}

// ── Hook ─────────────────────────────────────────────────────────────────────

export interface DashboardContext {
  hero: HeroPriority;
  isSessionMode: boolean;
  isLoading: boolean;
}

export function useDashboardContext(deps: {
  recentSessions: SessionSummaryDto[];
  games: UserGameDto[];
  upcomingGameNight: { id: string; title: string; scheduledAt: string } | null;
  incompleteSessions: Array<{ id: string; gameName: string; sessionDate: string }>;
}): DashboardContext {
  const { data: activeSessions, isLoading } = useActiveSessions(1);
  const activeSession = activeSessions?.sessions?.[0] ?? null;

  const hero = useMemo(() => {
    const lastGame =
      [...deps.games]
        .filter(g => g.lastPlayed)
        .sort((a, b) => new Date(b.lastPlayed!).getTime() - new Date(a.lastPlayed!).getTime())[0] ??
      null;
    const lastPlayed = lastGame
      ? {
          id: lastGame.id,
          title: lastGame.title,
          imageUrl: lastGame.imageUrl,
          lastPlayedAt: lastGame.lastPlayed!,
        }
      : null;

    return computeHeroPriority({
      activeSession: activeSession
        ? {
            id: activeSession.id,
            gameId: activeSession.gameId,
            playerCount: activeSession.playerCount,
            durationMinutes: activeSession.durationMinutes,
          }
        : null,
      upcomingGameNight: deps.upcomingGameNight,
      incompleteSessions: deps.incompleteSessions,
      lastPlayed,
    });
  }, [activeSession, deps.upcomingGameNight, deps.incompleteSessions, deps.games]);

  return {
    hero,
    isSessionMode: hero.type === 'active-session',
    isLoading,
  };
}
