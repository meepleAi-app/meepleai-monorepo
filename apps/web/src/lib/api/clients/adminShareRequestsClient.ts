import {
  type ShareRequestDetailsDto,
  type PaginatedAdminShareRequestsResponse,
  type GetAdminShareRequestsParams,
  type ApproveRequestData,
  type RejectRequestData,
  type RequestChangesData,
  type StartReviewResponse,
  type ActiveReviewDto,
  PaginatedAdminShareRequestsResponseSchema,
  ShareRequestDetailsDtoSchema,
  StartReviewResponseSchema,
  ActiveReviewDtoSchema,
} from '../schemas/admin-share-requests.schemas';

import type { HttpClient } from '../core/httpClient';

/**
 * Admin Share Requests API Client
 *
 * Provides methods for admin review and management of user share requests.
 *
 * Endpoints:
 * - GET /api/v1/admin/share-requests - List pending requests with filters
 * - GET /api/v1/admin/share-requests/{id} - Get review details
 * - POST /api/v1/admin/share-requests/{id}/start-review - Acquire lock
 * - POST /api/v1/admin/share-requests/{id}/release - Release lock
 * - GET /api/v1/admin/share-requests/my-reviews - Get active reviews
 * - POST /api/v1/admin/share-requests/{id}/approve - Approve request
 * - POST /api/v1/admin/share-requests/{id}/reject - Reject with reason
 * - POST /api/v1/admin/share-requests/{id}/request-changes - Request modifications
 *
 * Issue #2745: Frontend - Admin Review Interface
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

export interface AdminShareRequestsClient {
  /**
   * Get paginated list of share requests with filters.
   *
   * Query parameters:
   * - page: Page number (default: 1)
   * - pageSize: Items per page (default: 20, max: 100)
   * - status: Filter by status (Pending, InReview, etc.)
   * - contributionType: Filter by type (NewGame, AdditionalContent)
   * - searchTerm: Search game title or contributor name
   * - sortBy: Sort field (CreatedAt, GameTitle, ContributorName, Status)
   * - sortDirection: Ascending or Descending
   */
  getAll(params?: GetAdminShareRequestsParams): Promise<PaginatedAdminShareRequestsResponse>;

  /**
   * Get detailed information about a specific share request.
   * Includes full game details, contributor profile, documents, and history.
   */
  getById(id: string): Promise<ShareRequestDetailsDto>;

  /**
   * Acquire exclusive review lock on a share request.
   * Returns full request details with lock expiration time.
   *
   * Throws:
   * - 409 Conflict if already locked by another admin
   * - 403 Forbidden if not authorized
   */
  startReview(shareRequestId: string): Promise<StartReviewResponse>;

  /**
   * Release review lock on a share request.
   * Allows other admins to review the request.
   */
  releaseReview(shareRequestId: string): Promise<void>;

  /**
   * Get list of share requests currently being reviewed by the admin.
   */
  getMyReviews(): Promise<ActiveReviewDto[]>;

  /**
   * Approve a share request and optionally modify game details.
   *
   * Optional parameters:
   * - modifiedTitle: Override game title
   * - modifiedDescription: Override game description
   * - documentsToInclude: Select specific documents to include
   * - adminNotes: Internal notes about approval
   *
   * Throws:
   * - 409 Conflict if not locked by current admin
   * - 403 Forbidden if not authorized
   */
  approve(shareRequestId: string, data?: ApproveRequestData): Promise<void>;

  /**
   * Reject a share request with a required reason.
   *
   * Required:
   * - reason: Rejection explanation (min 10 characters)
   *
   * Throws:
   * - 409 Conflict if not locked by current admin
   * - 403 Forbidden if not authorized
   */
  reject(shareRequestId: string, data: RejectRequestData): Promise<void>;

  /**
   * Request changes from the user before approval.
   *
   * Required:
   * - feedback: Detailed feedback for user (min 10 characters)
   *
   * Throws:
   * - 409 Conflict if not locked by current admin
   * - 403 Forbidden if not authorized
   */
  requestChanges(shareRequestId: string, data: RequestChangesData): Promise<void>;
}

/**
 * Create an instance of the Admin Share Requests API client.
 */
export function createAdminShareRequestsClient(httpClient: HttpClient): AdminShareRequestsClient {
  const baseUrl = '/api/v1/admin/share-requests';

  return {
    async getAll(params: Partial<GetAdminShareRequestsParams> = {}): Promise<PaginatedAdminShareRequestsResponse> {
      const queryParams = new URLSearchParams();

      if (params.page) queryParams.set('page', params.page.toString());
      if (params.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params.status) queryParams.set('status', params.status);
      if (params.contributionType) queryParams.set('contributionType', params.contributionType);
      if (params.searchTerm) queryParams.set('searchTerm', params.searchTerm);
      if (params.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params.sortDirection) queryParams.set('sortDirection', params.sortDirection);

      const result = await httpClient.get(
        `${baseUrl}?${queryParams}`,
        PaginatedAdminShareRequestsResponseSchema
      );

      // Return empty result if API returns null
      return (
        result ?? {
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        }
      );
    },

    async getById(id: string): Promise<ShareRequestDetailsDto> {
      const result = await httpClient.get(`${baseUrl}/${id}`, ShareRequestDetailsDtoSchema);

      if (!result) {
        throw new Error(`Share request ${id} not found`);
      }

      return result;
    },

    async startReview(shareRequestId: string): Promise<StartReviewResponse> {
      const result = await httpClient.post(
        `${baseUrl}/${shareRequestId}/start-review`,
        null,
        StartReviewResponseSchema
      );

      if (!result) {
        throw new Error('Failed to start review');
      }

      return result;
    },

    async releaseReview(shareRequestId: string): Promise<void> {
      await httpClient.post(`${baseUrl}/${shareRequestId}/release`, null);
    },

    async getMyReviews(): Promise<ActiveReviewDto[]> {
      const result = await httpClient.get(
        `${baseUrl}/my-reviews`,
        ActiveReviewDtoSchema.array()
      );

      return result ?? [];
    },

    async approve(shareRequestId: string, data?: ApproveRequestData): Promise<void> {
      await httpClient.post(`${baseUrl}/${shareRequestId}/approve`, data ?? {});
    },

    async reject(shareRequestId: string, data: RejectRequestData): Promise<void> {
      await httpClient.post(`${baseUrl}/${shareRequestId}/reject`, data);
    },

    async requestChanges(shareRequestId: string, data: RequestChangesData): Promise<void> {
      await httpClient.post(`${baseUrl}/${shareRequestId}/request-changes`, data);
    },
  };
}
