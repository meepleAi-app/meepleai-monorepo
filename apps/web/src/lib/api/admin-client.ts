import { httpClient } from './http-client';

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface AdminStats {
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

interface ApprovalQueueItem {
  gameId: string;
  title: string;
  submittedBy: string;
  submittedAt: string;
  daysPending: number;
  pdfCount: number;
}

interface User {
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

interface UserLibraryStats {
  gamesOwned: number;
  totalPlays: number;
  wishlistCount: number;
  averageRating: number;
  favoriteCategory: string;
}

interface UserBadge {
  id: string;
  name: string;
  description: string;
  icon: string;
  earnedAt: string;
}

export const adminClient = {
  // Stats - adapted to use real backend DashboardStatsDto
  async getStats(params?: { days?: number }): Promise<AdminStats> {
    const query = new URLSearchParams();
    if (params?.days) query.set('days', params.days.toString());

    // Backend returns DashboardStatsDto, we extract metrics
    const response = await httpClient.get<any>(
      `/admin/dashboard/stats?${query.toString()}`
    );

    // Map backend DashboardMetrics to frontend AdminStats
    return {
      totalGames: response.metrics?.totalGames ?? 0,
      publishedGames: 0, // TODO: Calculate from SharedGameCatalog
      pendingGames: 0,   // TODO: Get from approval queue count
      totalUsers: response.metrics?.totalUsers ?? 0,
      activeUsers: response.metrics?.totalUsers ?? 0, // Approximate with total
      newUsers: 0, // TODO: Calculate from userTrend
      approvalRate: 90, // TODO: Calculate from approval stats
      pendingApprovals: 0, // TODO: Get from approval queue
      recentSubmissions: 0, // TODO: Calculate from trends
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

    return await httpClient.get<PagedResult<ApprovalQueueItem>>(
      `/admin/shared-games/approval-queue?${query.toString()}`
    );
  },

  async batchApproveGames(gameIds: string[]): Promise<void> {
    await httpClient.post('/admin/shared-games/batch-approve', { gameIds });
  },

  async batchRejectGames(gameIds: string[]): Promise<void> {
    await httpClient.post('/admin/shared-games/batch-reject', { gameIds });
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

    return await httpClient.get<PagedResult<User>>(
      `/admin/users?${query.toString()}`
    );
  },

  async getUserDetail(userId: string): Promise<User> {
    return await httpClient.get<User>(`/admin/users/${userId}`);
  },

  async getUserLibraryStats(userId: string): Promise<UserLibraryStats> {
    return await httpClient.get<UserLibraryStats>(
      `/admin/users/${userId}/library/stats`
    );
  },

  async getUserBadges(userId: string): Promise<UserBadge[]> {
    return await httpClient.get<UserBadge[]>(
      `/admin/users/${userId}/badges`
    );
  },

  async suspendUser(userId: string): Promise<void> {
    await httpClient.post(`/admin/users/${userId}/suspend`);
  },

  async unsuspendUser(userId: string): Promise<void> {
    await httpClient.post(`/admin/users/${userId}/unsuspend`);
  },

  async updateUserTier(userId: string, tier: string): Promise<void> {
    await httpClient.put(`/admin/users/${userId}/tier`, { tier });
  },

  async resetUserPassword(userId: string): Promise<void> {
    await httpClient.post(`/admin/users/${userId}/reset-password`);
  },

  async impersonateUser(userId: string): Promise<{ sessionToken: string }> {
    return await httpClient.post<{ sessionToken: string }>(
      `/admin/users/${userId}/impersonate`
    );
  },
};
