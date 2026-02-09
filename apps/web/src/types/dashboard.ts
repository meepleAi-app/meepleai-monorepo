/**
 * Dashboard Types - Issue #3975
 *
 * Type definitions for Dashboard Hub aggregated data from `/api/v1/dashboard`
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see docs/07-frontend/DASHBOARD-HUB-INDEX.md
 */

// ============================================================================
// User & Stats
// ============================================================================

export interface DashboardUser {
  id: string;
  username: string;
  email: string;
  avatarUrl?: string;
}

export interface DashboardStats {
  libraryCount: number;
  playedLast30Days: number;
  chatCount: number;
  wishlistCount: number;
  currentStreak: number;
}

// ============================================================================
// Active Sessions
// ============================================================================

export interface ActiveSession {
  id: string;
  gameName: string;
  gameId: string;
  coverUrl?: string;
  players: {
    current: number;
    total: number;
  };
  progress: {
    turn: number;
    duration: string; // e.g., "45min"
  };
  lastActivity: Date;
}

// ============================================================================
// Library Snapshot
// ============================================================================

export interface LibraryQuota {
  used: number;
  total: number;
}

export interface TopGame {
  id: string;
  title: string;
  coverUrl: string;
  rating: number; // 0-5
  playCount: number;
}

export interface LibrarySnapshot {
  quota: LibraryQuota;
  topGames: TopGame[];
}

// ============================================================================
// Activity Feed
// ============================================================================

export type ActivityEventType =
  | 'game_added'
  | 'session_completed'
  | 'chat_saved'
  | 'wishlist_added';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  gameId?: string;
  gameName?: string;
  sessionId?: string;
  chatId?: string;
  topic?: string;
  timestamp: Date;
}

// ============================================================================
// Chat History
// ============================================================================

export interface ChatThread {
  id: string;
  topic: string;
  messageCount?: number;
  timestamp: Date;
}

// ============================================================================
// Main Dashboard Data
// ============================================================================

/**
 * Aggregated dashboard data from `/api/v1/dashboard` endpoint
 *
 * This is the complete response shape from the backend aggregated API.
 */
export interface DashboardData {
  user: DashboardUser;
  stats: DashboardStats;
  activeSessions: ActiveSession[];
  librarySnapshot: LibrarySnapshot;
  recentActivity: ActivityEvent[];
  chatHistory: ChatThread[];
}

// ============================================================================
// API Response Wrapper
// ============================================================================

export interface DashboardApiResponse {
  success: boolean;
  data?: DashboardData;
  error?: {
    code: string;
    message: string;
  };
  timestamp: string;
}
