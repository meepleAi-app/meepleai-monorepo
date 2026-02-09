/**
 * Dashboard Data Adapters - Issue #3975
 *
 * Converts API data format to component-specific formats
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 */

import type {
  DashboardData,
  ActivityEvent as ApiActivityEvent,
  ChatThread as ApiChatThread,
  ActiveSession as ApiActiveSession,
} from '@/types/dashboard';
import type { DashboardStats as ComponentDashboardStats } from '@/components/dashboard/HeroStats';
import type { ActiveSession as ComponentActiveSession } from '@/components/dashboard/ActiveSessionsWidget';
import type { ChatThread as ComponentChatThread } from '@/components/dashboard/ChatHistorySection';

// ============================================================================
// Helpers
// ============================================================================

/**
 * Parse duration string to minutes. Handles "45min", "1h30min", "90", or numeric values.
 * Returns 0 for unparseable input.
 */
function parseDuration(duration: string | number): number {
  if (typeof duration === 'number') return duration;
  const num = parseInt(duration, 10);
  return isNaN(num) ? 0 : num;
}

// ============================================================================
// HeroStats Adapter
// ============================================================================

/**
 * Convert API stats to HeroStats component format
 */
export function adaptStatsForHeroStats(
  data: DashboardData
): ComponentDashboardStats {
  return {
    collection: {
      total: data.stats.libraryCount,
      trend: 0, // TODO: Backend should provide trend data
    },
    played: {
      total: data.stats.playedLast30Days,
      streak: data.stats.currentStreak,
    },
    chats: {
      total: data.stats.chatCount,
    },
    wishlist: {
      total: data.stats.wishlistCount,
      trend: 0, // TODO: Backend should provide trend data
    },
    lastAccess: new Date().toISOString(), // TODO: Backend should provide lastAccess
    userName: data.user.username,
  };
}

// ============================================================================
// ActiveSessions Adapter
// ============================================================================

/**
 * Convert API active sessions to ActiveSessionsWidget format
 */
export function adaptActiveSessions(
  sessions: ApiActiveSession[]
): ComponentActiveSession[] {
  return sessions.map((session) => ({
    id: session.id,
    gameName: session.gameName,
    gameId: session.gameId,
    startDate: session.lastActivity.toISOString(),
    players: {
      current: session.players.current,
      max: session.players.total, // API uses "total", component uses "max"
    },
    turn: session.progress.turn,
    duration: parseDuration(session.progress.duration),
  }));
}

// ============================================================================
// ChatHistory Adapter
// ============================================================================

/**
 * Convert API chat threads to ChatHistorySection format
 */
export function adaptChatThreads(
  threads: ApiChatThread[]
): ComponentChatThread[] {
  return threads.map((thread) => ({
    id: thread.id,
    title: thread.topic, // API uses "topic", component uses "title"
    lastMessageAt: thread.timestamp.toISOString(), // API uses "timestamp", component uses "lastMessageAt"
    messageCount: thread.messageCount || 0,
    gameId: undefined, // Optional field, not provided by API yet
  }));
}
