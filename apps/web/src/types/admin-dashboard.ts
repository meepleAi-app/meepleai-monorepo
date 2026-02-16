/**
 * Admin Dashboard Types
 * Shared types for admin dashboard components and API calls
 */

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AdminStats {
  totalGames: number;
  publishedGames: number;
  pendingGames: number;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  approvalRate: number;
  pendingApprovals: number;
  recentSubmissions: number;
}

export interface ApprovalQueueItem {
  gameId: string;
  title: string;
  submittedBy: string;
  submittedByName: string;
  submittedByEmail: string;
  submittedAt: string;
  daysPending: number;
  pdfCount: number;
}

export interface User {
  id: string;
  displayName: string;
  email: string;
  role: 'user' | 'admin';
  tier: 'free' | 'normal' | 'premium';
  level: number;
  experiencePoints: number;
  createdAt: string;
  lastSeenAt: string | null;
  isActive: boolean;
  isSuspended: boolean;
  suspendReason: string | null;
}

export interface UserLibraryStats {
  gamesOwned: number;
  totalPlays: number;
  wishlistCount: number;
  averageRating: number;
  favoriteCategory: string;
}

export interface UserBadge {
  id: string;
  name: string;
  description: string;
  icon: string;
  earnedAt: string;
}

export interface UserActivityEvent {
  id: string;
  type: 'game_added' | 'game_played' | 'badge_earned' | 'chat_created';
  description: string;
  timestamp: string;
  metadata?: Record<string, unknown>;
}

export type GameStatus = 'Draft' | 'PendingApproval' | 'Published' | 'Archived';
export type UserRole = 'user' | 'admin';
export type UserTier = 'free' | 'normal' | 'premium';
