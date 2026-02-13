import { apiClient } from './client';

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
  // Stats
  async getStats(params?: { days?: number }): Promise<AdminStats> {
    const query = new URLSearchParams();
    if (params?.days) query.set('days', params.days.toString());

    const response = await apiClient.get<AdminStats>(
      `/admin/stats?${query.toString()}`
    );
    if (!response) throw new Error('API request failed');
  return response;
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

    const response = await apiClient.get<PagedResult<ApprovalQueueItem>>(
      `/admin/shared-games/approval-queue?${query.toString()}`
    );
    if (!response) throw new Error('API request failed');
  return response;
  },

  async batchApproveGames(gameIds: string[]): Promise<void> {
    await apiClient.post('/admin/shared-games/batch-approve', { gameIds });
  },

  async batchRejectGames(gameIds: string[]): Promise<void> {
    await apiClient.post('/admin/shared-games/batch-reject', { gameIds });
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

    const response = await apiClient.get<PagedResult<User>>(
      `/admin/users?${query.toString()}`
    );
    if (!response) throw new Error('API request failed');
  return response;
  },

  async getUserDetail(userId: string): Promise<User> {
    const response = await apiClient.get<User>(`/admin/users/${userId}`);
    if (!response) throw new Error('API request failed');
  return response;
  },

  async getUserLibraryStats(userId: string): Promise<UserLibraryStats> {
    const response = await apiClient.get<UserLibraryStats>(
      `/admin/users/${userId}/library/stats`
    );
    if (!response) throw new Error('API request failed');
  return response;
  },

  async getUserBadges(userId: string): Promise<UserBadge[]> {
    const response = await apiClient.get<UserBadge[]>(
      `/admin/users/${userId}/badges`
    );
    if (!response) throw new Error('API request failed');
  return response;
  },

  async suspendUser(userId: string): Promise<void> {
    await apiClient.post(`/admin/users/${userId}/suspend`);
  },

  async unsuspendUser(userId: string): Promise<void> {
    await apiClient.post(`/admin/users/${userId}/unsuspend`);
  },

  async updateUserTier(userId: string, tier: string): Promise<void> {
    await apiClient.put(`/admin/users/${userId}/tier`, { tier });
  },

  async resetUserPassword(userId: string): Promise<void> {
    await apiClient.post(`/admin/users/${userId}/reset-password`);
  },

  async impersonateUser(userId: string): Promise<{ sessionToken: string }> {
    const response = await apiClient.post<{ sessionToken: string }>(
      `/admin/users/${userId}/impersonate`
    );
    if (!response) throw new Error('API request failed');
  return response;
  },
};
