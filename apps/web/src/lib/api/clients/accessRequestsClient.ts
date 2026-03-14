/**
 * Access Requests API Client (Invite-Only Registration)
 *
 * API client for access request management (admin) and
 * registration mode checking / request submission (public).
 */

import { type HttpClient } from '../core/httpClient';

// ============================================================================
// Types
// ============================================================================

export interface AccessRequestDto {
  id: string;
  email: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  requestedAt: string;
  reviewedAt: string | null;
  reviewedBy: string | null;
  rejectionReason: string | null;
  invitationId: string | null;
}

export interface AccessRequestStats {
  pending: number;
  approved: number;
  rejected: number;
  total: number;
}

export interface BulkApproveResult {
  processed: number;
  succeeded: number;
  failed: number;
  results: Array<{ id: string; status: string; error?: string }>;
}

export interface RegistrationMode {
  publicRegistrationEnabled: boolean;
}

export interface GetAccessRequestsParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface GetAccessRequestsResponse {
  items: AccessRequestDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ============================================================================
// Client Factory
// ============================================================================

export interface CreateAccessRequestsClientParams {
  httpClient: HttpClient;
}

export function createAccessRequestsClient({ httpClient }: CreateAccessRequestsClientParams) {
  return {
    // ──────────────────────────────────────────────
    // Public Endpoints
    // ──────────────────────────────────────────────

    /**
     * Get the current registration mode
     * GET /api/v1/auth/registration-mode
     */
    async getRegistrationMode(): Promise<RegistrationMode> {
      const response = await httpClient.get<RegistrationMode>('/api/v1/auth/registration-mode');
      return response ?? { publicRegistrationEnabled: false };
    },

    /**
     * Submit an access request (email enumeration-safe)
     * POST /api/v1/auth/request-access
     *
     * Always returns 202 with identical message regardless of outcome.
     */
    async requestAccess(email: string): Promise<{ message: string }> {
      return httpClient.post<{ message: string }>('/api/v1/auth/request-access', { email });
    },

    // ──────────────────────────────────────────────
    // Admin Endpoints
    // ──────────────────────────────────────────────

    /**
     * Get paginated list of access requests
     * GET /api/v1/admin/access-requests
     */
    async getAccessRequests(params?: GetAccessRequestsParams): Promise<GetAccessRequestsResponse> {
      const searchParams = new URLSearchParams();
      if (params?.status) searchParams.set('status', params.status);
      if (params?.page) searchParams.set('page', String(params.page));
      if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize));

      const qs = searchParams.toString();
      const url = `/api/v1/admin/access-requests${qs ? `?${qs}` : ''}`;
      const response = await httpClient.get<GetAccessRequestsResponse>(url);
      return response ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get access request statistics
     * GET /api/v1/admin/access-requests/stats
     */
    async getAccessRequestStats(): Promise<AccessRequestStats> {
      const response = await httpClient.get<AccessRequestStats>(
        '/api/v1/admin/access-requests/stats'
      );
      return response ?? { pending: 0, approved: 0, rejected: 0, total: 0 };
    },

    /**
     * Approve an access request (triggers invitation creation)
     * POST /api/v1/admin/access-requests/{id}/approve
     */
    async approveAccessRequest(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/access-requests/${encodeURIComponent(id)}/approve`);
    },

    /**
     * Reject an access request with optional reason
     * POST /api/v1/admin/access-requests/{id}/reject
     */
    async rejectAccessRequest(id: string, reason?: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/access-requests/${encodeURIComponent(id)}/reject`, {
        reason,
      });
    },

    /**
     * Bulk approve multiple access requests (max 25)
     * POST /api/v1/admin/access-requests/bulk-approve
     */
    async bulkApproveAccessRequests(ids: string[]): Promise<BulkApproveResult> {
      return httpClient.post<BulkApproveResult>('/api/v1/admin/access-requests/bulk-approve', {
        ids,
      });
    },

    /**
     * Toggle public registration mode
     * PUT /api/v1/admin/settings/registration-mode
     */
    async setRegistrationMode(enabled: boolean): Promise<void> {
      await httpClient.put('/api/v1/admin/settings/registration-mode', { enabled });
    },
  };
}

export type AccessRequestsClient = ReturnType<typeof createAccessRequestsClient>;
