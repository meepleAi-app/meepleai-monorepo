import { apiClient } from './client';
import { NotFoundError } from './core/errors';

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
  isActive: boolean;
  isTwoFactorEnabled: boolean;
  emailVerified: boolean;
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

/** Empty paged result for fallback on auth failure */
function emptyPagedResult<T>(): PagedResult<T> {
  return { items: [], totalCount: 0, page: 1, pageSize: 0, totalPages: 0 };
}

export const adminClient = {
  // Stats - Issue #4198: Uses dedicated overview-stats endpoint
  async getStats(): Promise<AdminStats> {
    const response = await apiClient.get<AdminStats>(
      '/api/v1/admin/overview-stats'
    );

    // GET returns null on 401 - return zeroed stats
    if (!response) {
      return {
        totalGames: 0, publishedGames: 0, pendingGames: 0,
        totalUsers: 0, activeUsers: 0, newUsers: 0,
        approvalRate: 0, pendingApprovals: 0, recentSubmissions: 0,
      };
    }

    return {
      totalGames: response.totalGames ?? 0,
      publishedGames: response.publishedGames ?? 0,
      pendingGames: 0,
      totalUsers: response.totalUsers ?? 0,
      activeUsers: response.activeUsers ?? 0,
      newUsers: 0,
      approvalRate: response.approvalRate ?? 0,
      pendingApprovals: response.pendingApprovals ?? 0,
      recentSubmissions: response.recentSubmissions ?? 0,
    };
  },

  // Shared Games Management
  async getApprovalQueue(params?: {
    page?: number;
    pageSize?: number;
    status?: string;
    search?: string;
  }): Promise<PagedResult<ApprovalQueueItem>> {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.status) query.set('status', params.status);
    if (params?.search) query.set('search', params.search);

    const result = await apiClient.get<PagedResult<ApprovalQueueItem>>(
      `/api/v1/admin/shared-games/approval-queue?${query.toString()}`
    );
    return result ?? emptyPagedResult<ApprovalQueueItem>();
  },

  async batchApproveGames(gameIds: string[]): Promise<void> {
    await apiClient.post('/api/v1/admin/shared-games/batch-approve', { gameIds });
  },

  async batchRejectGames(gameIds: string[]): Promise<void> {
    await apiClient.post('/api/v1/admin/shared-games/batch-reject', { gameIds });
  },

  // User Management
  async getUsers(params?: {
    page?: number;
    pageSize?: number;
    role?: string;
    tier?: string;
    search?: string;
  }): Promise<PagedResult<User>> {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.role) query.set('role', params.role);
    if (params?.tier) query.set('tier', params.tier);
    if (params?.search) query.set('search', params.search);

    const result = await apiClient.get<PagedResult<User>>(
      `/api/v1/admin/users?${query.toString()}`
    );
    return result ?? emptyPagedResult<User>();
  },

  async getUserDetail(userId: string): Promise<User> {
    const result = await apiClient.get<User>(`/api/v1/admin/users/${userId}`);
    if (!result) throw new NotFoundError({ message: `User ${userId} not found` });
    return result;
  },

  async getUserLibraryStats(userId: string): Promise<UserLibraryStats> {
    const result = await apiClient.get<UserLibraryStats>(
      `/api/v1/admin/users/${userId}/library/stats`
    );
    if (!result) throw new NotFoundError({ message: `Library stats for user ${userId} not found` });
    return result;
  },

  async getUserBadges(userId: string): Promise<UserBadge[]> {
    const result = await apiClient.get<UserBadge[]>(
      `/api/v1/admin/users/${userId}/badges`
    );
    return result ?? [];
  },

  async suspendUser(userId: string): Promise<void> {
    await apiClient.post('/api/v1/admin/users/' + userId + '/suspend');
  },

  async unsuspendUser(userId: string): Promise<void> {
    await apiClient.post('/api/v1/admin/users/' + userId + '/unsuspend');
  },

  async updateUserTier(userId: string, tier: string): Promise<void> {
    await apiClient.put('/api/v1/admin/users/' + userId + '/tier', { tier });
  },

  async resetUserPassword(userId: string): Promise<void> {
    await apiClient.post('/api/v1/admin/users/' + userId + '/reset-password');
  },

  async impersonateUser(userId: string): Promise<{ sessionToken: string }> {
    return await apiClient.post<{ sessionToken: string }>(
      '/api/v1/admin/users/' + userId + '/impersonate'
    );
  },
};
