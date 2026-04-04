/**
 * Admin Users Sub-Client
 *
 * User CRUD, sessions, impersonation, API keys, and data export.
 */

import { z } from 'zod';

import { getApiBase } from '../../core/httpClient';
import {
  AdminUserSchema,
  AdminUserResponseSchema,
  PagedResultSchema,
  AuditLogListResultSchema,
  GetUserActivityResultSchema,
  AdminSessionInfoSchema,
  GetAllApiKeysWithStatsResponseSchema,
  BulkImportApiKeysResultSchema,
  type CreateUserRequest,
  type UpdateUserRequest,
  type AdminUser,
  type PagedResult,
  type AuditLogListResult,
  type GetUserActivityResult,
  type UserActivityFilters,
  type AdminSessionInfo,
  type GetAdminSessionsParams,
  type GetAllApiKeysWithStatsResponse,
  type BulkImportApiKeysResult,
} from '../../schemas';

import type { HttpClient } from '../../core/httpClient';

// ========== Types defined locally (previously in adminClient.ts) ==========

export type UserBadge = {
  id: string;
  code: string;
  name: string;
  description: string;
  iconUrl: string | null;
  tier: string;
  earnedAt: string;
  isDisplayed: boolean;
};

export type UserLibraryStats = {
  totalGames: number;
  sessionsPlayed: number;
  avgSessionDuration: number | null;
};

export type RoleChangeHistory = {
  changedAt: string;
  oldRole: string;
  newRole: string;
  changedBy: string;
  changedByDisplayName: string;
  ipAddress: string | null;
};

export type ImpersonateUserResponse = {
  sessionToken: string;
  impersonatedUserId: string;
  expiresAt: string;
};

export type EndImpersonationResponse = {
  success: boolean;
  message: string;
};

// ========== Invitation Schemas (local to this sub-client) ==========

const InvitationStatusSchema = z.enum(['Pending', 'Accepted', 'Expired', 'Revoked']);

const InvitationSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  role: z.string(),
  status: InvitationStatusSchema,
  sentAt: z.string(),
  expiresAt: z.string(),
  acceptedAt: z.string().nullable().optional(),
});

const InvitationListResponseSchema = z.object({
  invitations: z.array(InvitationSchema),
  total: z.number(),
});

const InvitationStatsSchema = z.object({
  total: z.number(),
  pending: z.number(),
  accepted: z.number(),
  expired: z.number(),
  revoked: z.number(),
});

export type Invitation = z.infer<typeof InvitationSchema>;
export type InvitationStats = z.infer<typeof InvitationStatsSchema>;

export function createAdminUsersClient(http: HttpClient) {
  return {
    // ========== User Management ==========

    async createUser(request: CreateUserRequest): Promise<AdminUser> {
      const response = await http.post('/api/v1/admin/users', request, AdminUserResponseSchema);
      return response.user;
    },

    async updateUser(userId: string, updates: UpdateUserRequest): Promise<void> {
      await http.put(`/api/v1/admin/users/${userId}`, updates);
    },

    async deleteUser(userId: string): Promise<void> {
      await http.delete(`/api/v1/admin/users/${userId}`);
    },

    async updateUserTier(userId: string, tier: string): Promise<AdminUser> {
      const result = await http.put<AdminUser>(
        `/api/v1/admin/users/${userId}/tier`,
        { tier },
        AdminUserSchema
      );
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      return result!;
    },

    async changeUserRole(userId: string, newRole: string, reason?: string): Promise<AdminUser> {
      const result = await http.put<AdminUser>(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/role`,
        { newRole, reason },
        AdminUserSchema
      );
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      return result!;
    },

    async getAllUsers(params?: {
      search?: string;
      role?: string;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
      page?: number;
      limit?: number;
    }): Promise<{ items: AdminUser[]; total: number; page: number; pageSize: number }> {
      const queryParams = new URLSearchParams();
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role) queryParams.set('role', params.role);
      if (params?.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params?.sortOrder) queryParams.set('sortOrder', params.sortOrder);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.limit) queryParams.set('limit', params.limit.toString());

      const query = queryParams.toString();
      const result = await http.get(
        `/api/v1/admin/users${query ? `?${query}` : ''}`,
        z.object({
          items: z.array(AdminUserSchema),
          total: z.number(),
          page: z.number(),
          pageSize: z.number(),
        })
      );

      return result || { items: [], total: 0, page: 1, pageSize: 20 };
    },

    async getUsers(params?: {
      page?: number;
      pageSize?: number;
      search?: string;
      role?: string;
      status?: string;
      tier?: string;
    }): Promise<PagedResult<AdminUser>> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role && params.role !== 'all') queryParams.set('role', params.role);
      if (params?.status && params.status !== 'all') queryParams.set('status', params.status);
      if (params?.tier && params.tier !== 'all') queryParams.set('tier', params.tier);

      const query = queryParams.toString();
      const result = await http.get<PagedResult<AdminUser>>(
        `/api/v1/admin/users${query ? `?${query}` : ''}`,
        PagedResultSchema(AdminUserSchema)
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    async suspendUser(userId: string, reason?: string): Promise<AdminUser> {
      const response = await http.post(
        `/api/v1/admin/users/${userId}/suspend`,
        { reason },
        AdminUserResponseSchema
      );
      return response.user;
    },

    async unsuspendUser(userId: string): Promise<AdminUser> {
      const response = await http.post(
        `/api/v1/admin/users/${userId}/unsuspend`,
        {},
        AdminUserResponseSchema
      );
      return response.user;
    },

    async resetUserPassword(userId: string, newPassword: string): Promise<void> {
      await http.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/reset-password`,
        { newPassword },
        z.object({ message: z.string() })
      );
    },

    // ========== User Detail Endpoints (Issue #2890) ==========

    async getUserDetail(userId: string): Promise<AdminUser> {
      const result = await http.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}`,
        AdminUserSchema
      );
      if (!result) {
        throw new Error('User not found');
      }
      return result;
    },

    async getUserActivity(
      userId: string,
      filters?: UserActivityFilters
    ): Promise<GetUserActivityResult> {
      const params = new URLSearchParams();
      if (filters?.actionFilter) params.append('actionFilter', filters.actionFilter);
      if (filters?.resourceFilter) params.append('resourceFilter', filters.resourceFilter);
      if (filters?.startDate) params.append('startDate', filters.startDate.toISOString());
      if (filters?.endDate) params.append('endDate', filters.endDate.toISOString());
      if (filters?.limit !== undefined) params.append('limit', filters.limit.toString());

      const query = params.toString();
      const result = await http.get(
        `/api/v1/admin/users/${userId}/activity${query ? `?${query}` : ''}`,
        GetUserActivityResultSchema
      );
      return result ?? { activities: [], totalCount: 0 };
    },

    async getUserAuditLog(
      userId: string,
      params?: {
        limit?: number;
        offset?: number;
      }
    ): Promise<AuditLogListResult> {
      const searchParams = new URLSearchParams();
      if (params?.limit != null) searchParams.set('limit', String(params.limit));
      if (params?.offset != null) searchParams.set('offset', String(params.offset));
      const qs = searchParams.toString();
      const url = `/api/v1/admin/users/${encodeURIComponent(userId)}/audit-log${qs ? `?${qs}` : ''}`;
      const result = await http.get<AuditLogListResult>(url, AuditLogListResultSchema);
      if (!result) {
        return {
          entries: [],
          totalCount: 0,
          limit: params?.limit ?? 50,
          offset: params?.offset ?? 0,
        };
      }
      return result;
    },

    async getUserBadges(userId: string): Promise<UserBadge[]> {
      const UserBadgeSchema = z.object({
        id: z.string(),
        code: z.string(),
        name: z.string(),
        description: z.string(),
        iconUrl: z.string().nullable(),
        tier: z.string(),
        earnedAt: z.string(),
        isDisplayed: z.boolean(),
      });
      const result = await http.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/badges`,
        z.array(UserBadgeSchema)
      );
      return result || [];
    },

    async getUserLibraryStats(userId: string): Promise<UserLibraryStats> {
      const UserLibraryStatsSchema = z.object({
        totalGames: z.number(),
        sessionsPlayed: z.number(),
        avgSessionDuration: z.number().nullable(),
      });
      const result = await http.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/library/stats`,
        UserLibraryStatsSchema
      );
      if (!result) {
        throw new Error('Library stats not found');
      }
      return result;
    },

    async getUserRoleHistory(userId: string): Promise<RoleChangeHistory[]> {
      const RoleChangeHistorySchema = z.object({
        changedAt: z.string(),
        oldRole: z.string(),
        newRole: z.string(),
        changedBy: z.string(),
        changedByDisplayName: z.string(),
        ipAddress: z.string().nullable(),
      });
      const result = await http.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/role-history`,
        z.array(RoleChangeHistorySchema)
      );
      return result || [];
    },

    // ========== Admin Sessions Management ==========

    async getAdminSessions(params?: GetAdminSessionsParams): Promise<AdminSessionInfo[]> {
      const queryParams = new URLSearchParams();
      if (params?.limit !== undefined) queryParams.append('limit', params.limit.toString());
      if (params?.userId) queryParams.append('userId', params.userId);

      const query = queryParams.toString();
      const result = await http.get(
        `/api/v1/admin/sessions${query ? `?${query}` : ''}`,
        z.array(AdminSessionInfoSchema)
      );
      return result ?? [];
    },

    async revokeSession(sessionId: string): Promise<void> {
      await http.delete(`/api/v1/admin/sessions/${sessionId}`);
    },

    async revokeAllUserSessions(userId: string): Promise<void> {
      await http.delete(`/api/v1/admin/users/${userId}/sessions`);
    },

    // ========== Impersonation ==========

    async impersonateUser(userId: string): Promise<ImpersonateUserResponse> {
      const ImpersonateUserResponseSchema = z.object({
        sessionToken: z.string(),
        impersonatedUserId: z.string(),
        expiresAt: z.string(),
      });
      const result = await http.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/impersonate`,
        {},
        ImpersonateUserResponseSchema
      );
      if (!result) {
        throw new Error('Failed to impersonate user');
      }
      return result;
    },

    async endImpersonation(sessionId: string): Promise<EndImpersonationResponse> {
      const EndImpersonationResponseSchema = z.object({
        success: z.boolean(),
        message: z.string(),
      });
      const result = await http.post(
        '/api/v1/admin/impersonation/end',
        { sessionId },
        EndImpersonationResponseSchema
      );
      if (!result) {
        throw new Error('Failed to end impersonation');
      }
      return result;
    },

    // ========== API Key Management (Issue #908) ==========

    async getApiKeysWithStats(params?: {
      userId?: string;
      includeRevoked?: boolean;
    }): Promise<GetAllApiKeysWithStatsResponse> {
      const queryParams = new URLSearchParams();
      if (params?.userId) queryParams.set('userId', params.userId);
      if (params?.includeRevoked !== undefined)
        queryParams.set('includeRevoked', params.includeRevoked.toString());

      const query = queryParams.toString();
      const result = await http.get(
        `/api/v1/admin/api-keys/stats${query ? `?${query}` : ''}`,
        GetAllApiKeysWithStatsResponseSchema
      );

      if (!result) {
        throw new Error('Failed to fetch API keys');
      }

      return result;
    },

    async deleteApiKey(keyId: string): Promise<void> {
      await http.delete(`/api/v1/admin/api-keys/${keyId}`);
    },

    async bulkImportApiKeysFromCSV(csvContent: string): Promise<BulkImportApiKeysResult> {
      return http.post(
        '/api/v1/admin/api-keys/bulk/import',
        csvContent,
        BulkImportApiKeysResultSchema,
        {
          headers: {
            'Content-Type': 'text/csv',
          },
        }
      );
    },

    async importApiKeysFromCSV(csvContent: string): Promise<BulkImportApiKeysResult> {
      return http.post(
        '/api/v1/admin/api-keys/bulk/import',
        csvContent,
        BulkImportApiKeysResultSchema,
        {
          headers: {
            'Content-Type': 'text/csv',
          },
        }
      );
    },

    async exportApiKeysToCSV(params?: {
      userId?: string;
      isActive?: boolean;
      searchTerm?: string;
    }): Promise<Blob> {
      const queryParams = new URLSearchParams();
      if (params?.userId) queryParams.set('userId', params.userId);
      if (params?.isActive !== undefined) queryParams.set('isActive', params.isActive.toString());
      if (params?.searchTerm) queryParams.set('searchTerm', params.searchTerm);

      const query = queryParams.toString();
      const response = await fetch(
        `${getApiBase()}/api/v1/admin/api-keys/bulk/export${query ? `?${query}` : ''}`,
        {
          method: 'GET',
          credentials: 'include',
        }
      );

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      return response.blob();
    },

    // ========== Data Export ==========

    async exportUsersToCSV(params?: { role?: string; search?: string }): Promise<Blob> {
      const queryParams = new URLSearchParams();
      if (params?.role) queryParams.set('role', params.role);
      if (params?.search) queryParams.set('search', params.search);

      const query = queryParams.toString();
      const response = await fetch(
        `${getApiBase()}/api/v1/admin/users/bulk/export${query ? `?${query}` : ''}`,
        {
          method: 'GET',
          credentials: 'include',
        }
      );

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      return response.blob();
    },

    async importUsersFromCSV(
      csvContent: string
    ): Promise<{ successCount: number; failureCount: number; errors: string[] }> {
      return http.post(
        '/api/v1/admin/users/bulk/import',
        csvContent,
        z.object({
          successCount: z.number(),
          failureCount: z.number(),
          errors: z.array(z.string()),
        }),
        {
          headers: {
            'Content-Type': 'text/csv',
          },
        }
      );
    },

    async exportAuditLogs(params?: {
      adminUserId?: string;
      action?: string;
      resource?: string;
      result?: string;
      startDate?: string;
      endDate?: string;
    }): Promise<Blob> {
      const searchParams = new URLSearchParams();
      if (params?.adminUserId) searchParams.set('adminUserId', params.adminUserId);
      if (params?.action) searchParams.set('action', params.action);
      if (params?.resource) searchParams.set('resource', params.resource);
      if (params?.result) searchParams.set('result', params.result);
      if (params?.startDate) searchParams.set('startDate', params.startDate);
      if (params?.endDate) searchParams.set('endDate', params.endDate);
      const qs = searchParams.toString();
      const url = `${getApiBase()}/api/v1/admin/audit-log/export${qs ? `?${qs}` : ''}`;
      const response = await fetch(url, { credentials: 'include' });
      if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
      return response.blob();
    },

    // ========== Invitation Management ==========

    async sendInvitation(request: { email: string; role: string }): Promise<{ id: string }> {
      return http.post('/api/v1/admin/users/invite', request);
    },

    async sendUserEmail(userId: string, subject: string, body: string): Promise<void> {
      await http.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/send-email`,
        { subject, body },
        z.object({ message: z.string() })
      );
    },

    async bulkSendInvitations(
      emails: string[],
      role: string
    ): Promise<{ sent: number; failed: number }> {
      const csvContent = emails.join('\n');
      const blob = new Blob([csvContent], { type: 'text/csv' });
      const formData = new FormData();
      formData.append('file', blob, 'invitations.csv');
      formData.append('role', role);
      const response = await fetch(`${getApiBase()}/api/v1/admin/users/bulk/invite`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });
      if (!response.ok) throw new Error('Bulk invite failed');
      return response.json();
    },

    async getInvitations(params?: {
      status?: string;
      page?: number;
      pageSize?: number;
    }): Promise<{ invitations: Invitation[]; total: number }> {
      const query = new URLSearchParams();
      if (params?.status) query.set('status', params.status);
      if (params?.page) query.set('page', params.page.toString());
      if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
      const url = `/api/v1/admin/users/invitations${query.toString() ? `?${query}` : ''}`;
      const result = await http.get(url, InvitationListResponseSchema);
      return result ?? { invitations: [], total: 0 };
    },

    async getInvitationStats(): Promise<InvitationStats> {
      const result = await http.get('/api/v1/admin/users/invitations/stats', InvitationStatsSchema);
      return result ?? { total: 0, pending: 0, accepted: 0, expired: 0, revoked: 0 };
    },

    async resendInvitation(invitationId: string): Promise<void> {
      await http.post(`/api/v1/admin/users/invitations/${invitationId}/resend`, {});
    },

    async revokeInvitation(invitationId: string): Promise<void> {
      await http.delete(`/api/v1/admin/users/invitations/${invitationId}`);
    },
  };
}

export type AdminUsersClient = ReturnType<typeof createAdminUsersClient>;
