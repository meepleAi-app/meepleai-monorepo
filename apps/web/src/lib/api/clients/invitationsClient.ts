/**
 * Invitations API Client (Issue #132)
 *
 * API client for user invitation management (admin) and
 * invitation acceptance/validation (public auth endpoints).
 */

import { type HttpClient } from '../core/httpClient';
import { getApiBase } from '../core/httpClient';
import {
  InvitationDtoSchema,
  BulkInviteResponseSchema,
  GetInvitationsResponseSchema,
  InvitationStatsSchema,
  TokenValidationSchema,
  AcceptInvitationResponseSchema,
  type InvitationDto,
  type BulkInviteResponse,
  type GetInvitationsResponse,
  type InvitationStats,
  type TokenValidation,
  type AcceptInvitationResponse,
} from '../schemas/invitation.schemas';

export interface CreateInvitationsClientParams {
  httpClient: HttpClient;
}

export interface GetInvitationsParams {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

/**
 * Invitations API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createInvitationsClient({ httpClient });
 *
 * // Admin: send invitation
 * const invitation = await client.sendInvitation('user@example.com', 'Player');
 *
 * // Admin: get all invitations
 * const list = await client.getInvitations({ page: 1, pageSize: 20 });
 *
 * // Public: validate invitation token
 * const validation = await client.validateInvitationToken('abc123');
 *
 * // Public: accept invitation
 * const result = await client.acceptInvitation('abc123', 'P@ssw0rd!', 'P@ssw0rd!');
 * ```
 */
export function createInvitationsClient({ httpClient }: CreateInvitationsClientParams) {
  return {
    // ──────────────────────────────────────────────
    // Admin Endpoints
    // ──────────────────────────────────────────────

    /**
     * Send a single invitation email
     * POST /api/v1/admin/users/invite
     *
     * @param email - Recipient email address
     * @param role - Role to assign (e.g. 'Player', 'Admin')
     * @returns Created invitation DTO
     */
    async sendInvitation(email: string, role: string): Promise<InvitationDto> {
      const response = await httpClient.post<InvitationDto>(
        '/api/v1/admin/users/invite',
        { email, role },
        InvitationDtoSchema
      );
      return response;
    },

    /**
     * Bulk send invitations via CSV content
     * POST /api/v1/admin/users/bulk/invite (multipart/form-data)
     *
     * @param csvContent - CSV string with email,role rows
     * @returns Bulk invite response with successful and failed entries
     */
    async bulkSendInvitations(csvContent: string): Promise<BulkInviteResponse> {
      const baseUrl = getApiBase();
      const url = `${baseUrl}/api/v1/admin/users/bulk/invite`;

      const blob = new Blob([csvContent], { type: 'text/csv' });
      const formData = new FormData();
      formData.append('file', blob, 'invitations.csv');

      const response = await fetch(url, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (!response.ok) {
        let message = `Bulk invite failed (${response.status})`;
        try {
          const err = await response.json();
          message = err.detail || err.title || err.message || message;
        } catch {
          /* use default */
        }
        throw new Error(message);
      }

      const data = await response.json();
      return BulkInviteResponseSchema.parse(data);
    },

    /**
     * Resend an existing invitation
     * POST /api/v1/admin/users/invitations/{id}/resend
     *
     * @param id - Invitation UUID
     */
    async resendInvitation(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/users/invitations/${encodeURIComponent(id)}/resend`);
    },

    /**
     * Revoke an existing invitation
     * DELETE /api/v1/admin/users/invitations/{id}
     *
     * @param id - Invitation UUID
     */
    async revokeInvitation(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/users/invitations/${encodeURIComponent(id)}`);
    },

    /**
     * Get paginated list of invitations
     * GET /api/v1/admin/users/invitations
     *
     * @param params - Pagination and filter parameters
     * @returns Paginated invitation list
     */
    async getInvitations(params?: GetInvitationsParams): Promise<GetInvitationsResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', String(params.page));
      if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize));
      if (params?.status) searchParams.set('status', params.status);
      if (params?.search) searchParams.set('search', params.search);

      const qs = searchParams.toString();
      const url = `/api/v1/admin/users/invitations${qs ? `?${qs}` : ''}`;

      const response = await httpClient.get<GetInvitationsResponse>(
        url,
        GetInvitationsResponseSchema
      );
      return response ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get invitation statistics (pending, accepted, expired counts)
     * GET /api/v1/admin/users/invitations/stats
     *
     * @returns Invitation stats
     */
    async getInvitationStats(): Promise<InvitationStats> {
      const response = await httpClient.get<InvitationStats>(
        '/api/v1/admin/users/invitations/stats',
        InvitationStatsSchema
      );
      return response ?? { pending: 0, accepted: 0, expired: 0, revoked: 0, total: 0 };
    },

    // ──────────────────────────────────────────────
    // Public Auth Endpoints
    // ──────────────────────────────────────────────

    /**
     * Validate an invitation token (public, no auth required)
     * POST /api/v1/auth/validate-invitation
     *
     * @param token - Invitation token from the invite link
     * @returns Token validation result
     */
    async validateInvitationToken(token: string): Promise<TokenValidation> {
      const response = await httpClient.post<TokenValidation>(
        '/api/v1/auth/validate-invitation',
        { token },
        TokenValidationSchema
      );
      return response;
    },

    /**
     * Accept an invitation and create an account (public, no auth required)
     * POST /api/v1/auth/accept-invitation
     *
     * @param token - Invitation token
     * @param password - New password
     * @param confirmPassword - Password confirmation
     * @returns New user + session token
     */
    async acceptInvitation(
      token: string,
      password: string,
      confirmPassword: string
    ): Promise<AcceptInvitationResponse> {
      const response = await httpClient.post<AcceptInvitationResponse>(
        '/api/v1/auth/accept-invitation',
        { token, password, confirmPassword },
        AcceptInvitationResponseSchema
      );
      return response;
    },
  };
}

export type InvitationsClient = ReturnType<typeof createInvitationsClient>;
