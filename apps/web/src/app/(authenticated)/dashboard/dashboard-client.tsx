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
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
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
  // UserGameDto doesn't currently expose agent/kb/session/chat counts.
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

// Fix #1: Use typed AgentDto, derive status from isActive/isIdle, use lastInvokedAt
function mapAgentToCard(agent: AgentDto): AgentItem {
  const status: AgentItem['status'] = agent.isActive
    ? 'active'
    : agent.isIdle
      ? 'idle'
      : 'training';

  return {
    id: agent.id,
    name: agent.name,
    status,
    stats: {
      invocationCount: agent.invocationCount,
      lastExecutedAt: agent.lastInvokedAt ?? undefined,
    },
    gameTitle: agent.gameName ?? undefined,
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
    updateFilters,
    games,
    isLoadingGames,
  } = useDashboardStore();

  // React Query — agents, chat sessions
  const { data: agentsRaw, isLoading: agentsLoading } = useRecentAgents(5);
  const { data: chatSessionsRaw, isLoading: chatsLoading } = useRecentChatSessions(4);

  // Fetch on mount
  useEffect(() => {
    fetchStats();
    fetchRecentSessions(5);
    // Fix #2: updateFilters calls fetchGames() internally — no explicit fetchGames() needed
    updateFilters({ sort: 'playCount', pageSize: 6, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Map data
  const gameCards = useMemo(() => games.map(mapGameToCard), [games]);
  const agentCards = useMemo(() => (agentsRaw ?? []).map(mapAgentToCard), [agentsRaw]);

  // Fix #1: Use typed AgentDto fields for sidebar
  const sidebarAgents = useMemo(
    () =>
      (agentsRaw ?? []).slice(0, 3).map((a: AgentDto) => ({
        id: a.id,
        name: a.name,
        lastUsedAt: a.lastInvokedAt ?? new Date().toISOString(),
        isReady: a.isActive || a.isIdle,
      })),
    [agentsRaw]
  );

  // Fix #3: chatSessionsRaw always returns { sessions, totalCount }
  const sidebarChats = useMemo(
    () =>
      (chatSessionsRaw?.sessions ?? []).slice(0, 4).map(t => ({
        id: t.id,
        title: t.title ?? t.gameTitle ?? 'Chat',
        messageCount: t.messageCount,
        agentName: t.agentName ?? 'Agent',
        lastMessageAt: t.lastMessageAt ?? t.createdAt,
      })),
    [chatSessionsRaw]
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
