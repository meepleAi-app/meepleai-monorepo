'use client';

import { useEffect, useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { ActiveSessions } from '@/components/dashboard/v2/ActiveSessions';
import { HeroZone } from '@/components/dashboard/v2/HeroZone';
import { QuickStats } from '@/components/dashboard/v2/QuickStats';
import { RecentActivitySidebar } from '@/components/dashboard/v2/RecentActivitySidebar';
import { RecentAgentsSidebar } from '@/components/dashboard/v2/RecentAgentsSidebar';
import { RecentChatsSidebar } from '@/components/dashboard/v2/RecentChatsSidebar';
import { RecentGames } from '@/components/dashboard/v2/RecentGames';
import type { GameItem } from '@/components/dashboard/v2/RecentGames';
import { YourAgents } from '@/components/dashboard/v2/YourAgents';
import type { AgentItem } from '@/components/dashboard/v2/YourAgents';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useRecentAgents } from '@/hooks/queries/useRecentAgents';
import type { UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

// ============================================================================
// DTO → Component Props Mappers
// ============================================================================

function mapGameToCard(game: UserGameDto): GameItem {
  const state =
    game.playCount > 0
      ? { text: 'Giocato', variant: 'success' as const }
      : { text: 'Nuovo', variant: 'info' as const };

  const linkedEntities: GameItem['linkedEntities'] = [];
  // Note: UserGameDto doesn't currently expose agent/kb/session/chat counts.
  // These will be populated when the backend enriches the DTO.

  return {
    id: game.id,
    title: game.title,
    subtitle: [
      game.publisher,
      game.minPlayers && game.maxPlayers ? `${game.minPlayers}-${game.maxPlayers} giocatori` : null,
    ]
      .filter(Boolean)
      .join(' · '),
    imageUrl: game.imageUrl ?? game.thumbnailUrl,
    state,
    linkedEntities,
    metadata: [
      ...(game.minPlayers && game.maxPlayers
        ? [{ label: `👥 ${game.minPlayers}-${game.maxPlayers}` }]
        : []),
      ...(game.playingTimeMinutes ? [{ label: `⏱ ${game.playingTimeMinutes}min` }] : []),
      ...(game.averageRating ? [{ label: `⭐ ${game.averageRating.toFixed(1)}` }] : []),
    ],
    rating: game.averageRating,
  };
}

function mapAgentToCard(agent: Record<string, unknown>): AgentItem {
  return {
    id: String(agent.id ?? ''),
    name: String(agent.name ?? agent.agentName ?? 'Agent'),
    imageUrl: agent.imageUrl as string | undefined,
    status: (agent.status as AgentItem['status']) ?? 'idle',
    stats:
      agent.invocationCount != null
        ? {
            invocationCount: Number(agent.invocationCount),
            lastExecutedAt: agent.lastExecutedAt as string | undefined,
          }
        : undefined,
    gameTitle: agent.gameName as string | undefined,
  };
}

// ============================================================================
// Dashboard Client Component
// ============================================================================

export function DashboardClient() {
  const router = useRouter();
  const { user } = useAuth();

  // Zustand store — stats, sessions, games
  const {
    stats,
    isLoadingStats,
    statsError,
    fetchStats,
    recentSessions,
    isLoadingSessions,
    fetchRecentSessions,
    games,
    isLoadingGames,
    gamesError,
    fetchGames,
    updateFilters,
  } = useDashboardStore();

  // React Query — agents, chat sessions
  const { data: agentsRaw, isLoading: agentsLoading } = useRecentAgents(5);
  const { data: chatSessionsRaw, isLoading: chatsLoading } = useRecentChatSessions(4);

  // Fetch on mount
  useEffect(() => {
    fetchStats();
    fetchRecentSessions(5);
    updateFilters({ sort: 'playCount', pageSize: 6, page: 1 });
    fetchGames();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Map data
  const gameCards = useMemo(() => games.map(mapGameToCard), [games]);
  const agentCards = useMemo(() => (agentsRaw ?? []).map(mapAgentToCard), [agentsRaw]);

  const sidebarAgents = useMemo(
    () =>
      (agentsRaw ?? []).slice(0, 3).map((a: Record<string, unknown>) => ({
        id: String(a.id ?? ''),
        name: String(a.name ?? a.agentName ?? 'Agent'),
        lastUsedAt: String(a.lastExecutedAt ?? a.updatedAt ?? new Date().toISOString()),
        isReady: a.status === 'active' || a.status === 'idle',
      })),
    [agentsRaw]
  );

  // chatSessionsRaw can be { sessions: [...], totalCount } or an array
  const chatSessionsList = useMemo(() => {
    if (!chatSessionsRaw) return [];
    if (Array.isArray(chatSessionsRaw)) return chatSessionsRaw;
    if ('sessions' in chatSessionsRaw && Array.isArray(chatSessionsRaw.sessions))
      return chatSessionsRaw.sessions;
    return [];
  }, [chatSessionsRaw]);

  const sidebarChats = useMemo(
    () =>
      chatSessionsList.slice(0, 4).map(t => ({
        id: String(t.id ?? ''),
        title: String(t.title ?? t.gameTitle ?? 'Chat'),
        messageCount: Number(t.messageCount ?? 0),
        agentName: String(t.agentName ?? 'Agent'),
        lastMessageAt: String(t.lastMessageAt ?? t.createdAt ?? new Date().toISOString()),
      })),
    [chatSessionsList]
  );

  const quickStatsData = stats
    ? {
        totalGames: stats.totalGames,
        monthlyPlays: stats.monthlyPlays,
        weeklyPlaytime: stats.weeklyPlayTime ?? '0h',
        favorites: stats.monthlyFavorites,
      }
    : null;

  const userName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';

  return (
    <div className="flex flex-col lg:flex-row gap-6 w-full max-w-7xl mx-auto px-4 py-6">
      {/* Tavolo — main column */}
      <main className="flex-1 min-w-0 flex flex-col gap-6">
        <HeroZone userName={userName} />

        <QuickStats stats={quickStatsData} loading={isLoadingStats} error={!!statsError} />

        <ActiveSessions sessions={recentSessions} loading={isLoadingSessions} />

        <RecentGames games={gameCards} loading={isLoadingGames} />

        <YourAgents
          agents={agentCards}
          loading={agentsLoading}
          onCreateAgent={() => router.push('/agents')}
        />
      </main>

      {/* Sidebar */}
      <aside className="flex flex-col gap-6 w-full lg:w-[280px] lg:flex-shrink-0">
        <RecentAgentsSidebar agents={sidebarAgents} loading={agentsLoading} />

        <RecentChatsSidebar threads={sidebarChats} loading={chatsLoading} />

        <RecentActivitySidebar activities={[]} loading={false} />
      </aside>
    </div>
  );
}
