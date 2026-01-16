/**
 * Shared Game Catalog API Client (Issue #2372)
 *
 * API client for SharedGameCatalog bounded context.
 * Covers: Games search/CRUD, Categories, Mechanics, FAQs, Errata, Delete Workflow
 */

import { z } from 'zod';

import { type HttpClient } from '../core/httpClient';
import {
  SharedGameDetailSchema,
  PagedSharedGamesSchema,
  GameCategorySchema,
  GameMechanicSchema,
  PagedDeleteRequestsSchema,
  CreatedResponseSchema,
  DeleteRequestAcceptedSchema,
  SharedGameDocumentSchema,
  type SharedGameDetail,
  type PagedSharedGames,
  type GameCategory,
  type GameMechanic,
  type PagedDeleteRequests,
  type CreateSharedGameRequest,
  type UpdateSharedGameRequest,
  type DeleteSharedGameRequestBody,
  type ApproveDeleteRequestBody,
  type RejectDeleteRequestBody,
  type AddFaqRequest,
  type UpdateFaqRequest,
  type AddErrataRequest,
  type UpdateErrataRequest,
  type SearchSharedGamesParams,
  type CreatedResponse,
  type DeleteRequestAccepted,
  type SharedGameDocument,
  type SharedGameDocumentTypeNumeric,
  type AddDocumentRequest,
} from '../schemas/shared-games.schemas';

export interface CreateSharedGamesClientParams {
  httpClient: HttpClient;
}

/**
 * Shared Games API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createSharedGamesClient({ httpClient });
 *
 * // Search games (public)
 * const games = await client.search({ searchTerm: 'catan', page: 1 });
 *
 * // Get game details (public)
 * const game = await client.getById('uuid-here');
 *
 * // Admin: Create new game
 * const newId = await client.create({ title: 'New Game', ... });
 * ```
 */
export function createSharedGamesClient({ httpClient }: CreateSharedGamesClientParams) {
  return {
    // ========== Public Endpoints ==========

    /**
     * Search shared games with filters and pagination (PUBLIC)
     *
     * @param params - Search parameters
     * @returns Paginated list of shared games
     */
    async search(params: SearchSharedGamesParams = {}): Promise<PagedSharedGames> {
      const queryParams = new URLSearchParams();

      if (params.searchTerm) queryParams.set('searchTerm', params.searchTerm);
      if (params.categoryIds) queryParams.set('categoryIds', params.categoryIds);
      if (params.mechanicIds) queryParams.set('mechanicIds', params.mechanicIds);
      if (params.minPlayers !== undefined)
        queryParams.set('minPlayers', params.minPlayers.toString());
      if (params.maxPlayers !== undefined)
        queryParams.set('maxPlayers', params.maxPlayers.toString());
      if (params.maxPlayingTime !== undefined)
        queryParams.set('maxPlayingTime', params.maxPlayingTime.toString());
      if (params.status !== undefined) queryParams.set('status', params.status.toString());
      if (params.page !== undefined) queryParams.set('page', params.page.toString());
      if (params.pageSize !== undefined) queryParams.set('pageSize', params.pageSize.toString());
      if (params.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params.sortDescending !== undefined)
        queryParams.set('sortDescending', params.sortDescending.toString());

      const queryString = queryParams.toString();
      const path = `/api/v1/shared-games${queryString ? `?${queryString}` : ''}`;

      const result = await httpClient.get(path, PagedSharedGamesSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get shared game by ID with full details (PUBLIC)
     *
     * @param id - Game UUID
     * @returns Game details or null if not found
     */
    async getById(id: string): Promise<SharedGameDetail | null> {
      return httpClient.get(`/api/v1/shared-games/${id}`, SharedGameDetailSchema);
    },

    /**
     * Get all game categories (PUBLIC)
     *
     * @returns List of categories
     */
    async getCategories(): Promise<GameCategory[]> {
      const result = await httpClient.get(
        '/api/v1/shared-games/categories',
        z.array(GameCategorySchema)
      );
      return result ?? [];
    },

    /**
     * Get all game mechanics (PUBLIC)
     *
     * @returns List of mechanics
     */
    async getMechanics(): Promise<GameMechanic[]> {
      const result = await httpClient.get(
        '/api/v1/shared-games/mechanics',
        z.array(GameMechanicSchema)
      );
      return result ?? [];
    },

    // ========== Admin CRUD Endpoints ==========

    /**
     * Get all shared games for admin list (ADMIN/EDITOR)
     *
     * @param params - Filter parameters
     * @returns Paginated list of shared games (all statuses)
     */
    async getAll(
      params: { status?: number; page?: number; pageSize?: number } = {}
    ): Promise<PagedSharedGames> {
      const queryParams = new URLSearchParams();

      if (params.status !== undefined) queryParams.set('status', params.status.toString());
      if (params.page !== undefined) queryParams.set('page', params.page.toString());
      if (params.pageSize !== undefined) queryParams.set('pageSize', params.pageSize.toString());

      const queryString = queryParams.toString();
      const path = `/api/v1/admin/shared-games${queryString ? `?${queryString}` : ''}`;

      const result = await httpClient.get(path, PagedSharedGamesSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Create a new shared game (ADMIN/EDITOR)
     * Game starts in Draft status.
     *
     * @param request - Game creation data
     * @returns Created game ID
     */
    async create(request: CreateSharedGameRequest): Promise<string> {
      const result = await httpClient.post<CreatedResponse>(
        '/api/v1/admin/shared-games',
        request,
        CreatedResponseSchema
      );
      return result.id;
    },

    /**
     * Update an existing shared game (ADMIN/EDITOR)
     *
     * @param id - Game UUID
     * @param request - Updated game data
     */
    async update(id: string, request: UpdateSharedGameRequest): Promise<void> {
      await httpClient.put(`/api/v1/admin/shared-games/${id}`, request);
    },

    /**
     * Publish a draft game (ADMIN/EDITOR)
     * Makes the game publicly visible.
     *
     * @param id - Game UUID
     */
    async publish(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${id}/publish`, {});
    },

    /**
     * Archive a game (ADMIN/EDITOR)
     * Removes from public view.
     *
     * @param id - Game UUID
     */
    async archive(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${id}/archive`, {});
    },

    /**
     * Delete a game immediately (ADMIN ONLY)
     *
     * Admins can delete games immediately without approval.
     *
     * @param id - Game UUID
     */
    async delete(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/shared-games/${id}`);
    },

    // ========== Approval Workflow Endpoints (Issue #2514) ==========

    /**
     * Submit a game for approval (ADMIN/EDITOR)
     *
     * Transitions game from Draft (0) to PendingApproval (1).
     * Game must be in Draft status to submit.
     *
     * @param id - Game UUID
     */
    async submitForApproval(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${id}/submit-for-approval`, {});
    },

    /**
     * Approve game publication (ADMIN ONLY)
     *
     * Transitions game from PendingApproval (1) to Published (2).
     * Makes the game publicly visible.
     *
     * @param id - Game UUID
     */
    async approvePublication(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${id}/approve-publication`, {});
    },

    /**
     * Reject game publication (ADMIN ONLY)
     *
     * Transitions game from PendingApproval (1) back to Draft (0).
     * Requires a reason for rejection.
     *
     * @param id - Game UUID
     * @param reason - Reason for rejection (required)
     */
    async rejectPublication(id: string, reason: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${id}/reject-publication`, { reason });
    },

    /**
     * Get games pending approval (ADMIN ONLY)
     *
     * Returns all games in PendingApproval status waiting for admin approval.
     *
     * @param params - Pagination parameters
     * @returns Paginated list of games pending approval
     */
    async getPendingApprovals(
      params: { pageNumber?: number; pageSize?: number } = {}
    ): Promise<PagedSharedGames> {
      const queryParams = new URLSearchParams();

      if (params.pageNumber !== undefined)
        queryParams.set('pageNumber', params.pageNumber.toString());
      if (params.pageSize !== undefined)
        queryParams.set('pageSize', params.pageSize.toString());

      const queryString = queryParams.toString();
      const path = `/api/v1/admin/shared-games/pending-approvals${queryString ? `?${queryString}` : ''}`;

      const result = await httpClient.get(path, PagedSharedGamesSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    // ========== Delete Request Workflow (Issue #2372) ==========

    /**
     * Request deletion of a game (EDITOR)
     *
     * Editors must provide a reason; creates a delete request for admin approval.
     *
     * @param id - Game UUID
     * @param body - Reason for deletion (required)
     * @returns Delete request details with ID
     */
    async requestDelete(
      id: string,
      body: DeleteSharedGameRequestBody
    ): Promise<DeleteRequestAccepted> {
      return httpClient.post<DeleteRequestAccepted>(
        `/api/v1/admin/shared-games/${id}/request-delete`,
        body,
        DeleteRequestAcceptedSchema
      );
    },

    // ========== Delete Workflow Endpoints (ADMIN ONLY) ==========

    /**
     * Get pending delete requests (ADMIN ONLY)
     *
     * @param params - Pagination parameters
     * @returns Paginated list of pending delete requests
     */
    async getPendingDeletes(
      params: { page?: number; pageSize?: number } = {}
    ): Promise<PagedDeleteRequests> {
      const queryParams = new URLSearchParams();

      if (params.page !== undefined) queryParams.set('page', params.page.toString());
      if (params.pageSize !== undefined) queryParams.set('pageSize', params.pageSize.toString());

      const queryString = queryParams.toString();
      const path = `/api/v1/admin/shared-games/pending-deletes${queryString ? `?${queryString}` : ''}`;

      const result = await httpClient.get(path, PagedDeleteRequestsSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Approve a delete request (ADMIN ONLY)
     *
     * @param requestId - Delete request UUID
     * @param body - Optional comment
     */
    async approveDelete(requestId: string, body?: ApproveDeleteRequestBody): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/approve-delete/${requestId}`, body ?? {});
    },

    /**
     * Reject a delete request (ADMIN ONLY)
     *
     * @param requestId - Delete request UUID
     * @param body - Rejection reason (required)
     */
    async rejectDelete(requestId: string, body: RejectDeleteRequestBody): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/reject-delete/${requestId}`, body);
    },

    // ========== FAQ Endpoints (ADMIN/EDITOR) ==========

    /**
     * Add FAQ to a game (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param request - FAQ data
     * @returns Created FAQ ID
     */
    async addFaq(gameId: string, request: AddFaqRequest): Promise<string> {
      const result = await httpClient.post<CreatedResponse>(
        `/api/v1/admin/shared-games/${gameId}/faq`,
        request,
        CreatedResponseSchema
      );
      return result.id;
    },

    /**
     * Update FAQ (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param faqId - FAQ UUID
     * @param request - Updated FAQ data
     */
    async updateFaq(gameId: string, faqId: string, request: UpdateFaqRequest): Promise<void> {
      await httpClient.put(`/api/v1/admin/shared-games/${gameId}/faq/${faqId}`, request);
    },

    /**
     * Delete FAQ (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param faqId - FAQ UUID
     */
    async deleteFaq(gameId: string, faqId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/shared-games/${gameId}/faq/${faqId}`);
    },

    // ========== Errata Endpoints (ADMIN/EDITOR) ==========

    /**
     * Add errata to a game (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param request - Errata data
     * @returns Created errata ID
     */
    async addErrata(gameId: string, request: AddErrataRequest): Promise<string> {
      const result = await httpClient.post<CreatedResponse>(
        `/api/v1/admin/shared-games/${gameId}/errata`,
        request,
        CreatedResponseSchema
      );
      return result.id;
    },

    /**
     * Update errata (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param errataId - Errata UUID
     * @param request - Updated errata data
     */
    async updateErrata(
      gameId: string,
      errataId: string,
      request: UpdateErrataRequest
    ): Promise<void> {
      await httpClient.put(`/api/v1/admin/shared-games/${gameId}/errata/${errataId}`, request);
    },

    /**
     * Delete errata (ADMIN/EDITOR)
     *
     * @param gameId - Game UUID
     * @param errataId - Errata UUID
     */
    async deleteErrata(gameId: string, errataId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/shared-games/${gameId}/errata/${errataId}`);
    },

    // ========== Document Management (Issue #2391 Sprint 1) ==========

    /**
     * Get all documents for a shared game (Admin/Editor)
     *
     * @param gameId - The game UUID
     * @param type - Optional filter by document type
     * @returns List of documents
     */
    async getDocuments(
      gameId: string,
      type?: SharedGameDocumentTypeNumeric
    ): Promise<SharedGameDocument[]> {
      const queryParams = type !== undefined ? `?type=${type}` : '';
      const result = await httpClient.get(
        `/api/v1/admin/shared-games/${gameId}/documents${queryParams}`,
        z.array(SharedGameDocumentSchema)
      );
      return result ?? [];
    },

    /**
     * Get active documents for a shared game (Admin/Editor)
     *
     * @param gameId - The game UUID
     * @returns List of active documents
     */
    async getActiveDocuments(gameId: string): Promise<SharedGameDocument[]> {
      const result = await httpClient.get(
        `/api/v1/admin/shared-games/${gameId}/documents/active`,
        z.array(SharedGameDocumentSchema)
      );
      return result ?? [];
    },

    /**
     * Add a document to a shared game (Admin/Editor)
     *
     * @param gameId - The game UUID
     * @param request - Document data
     * @returns Created document ID
     */
    async addDocument(gameId: string, request: AddDocumentRequest): Promise<CreatedResponse> {
      const result = await httpClient.post(
        `/api/v1/admin/shared-games/${gameId}/documents`,
        request,
        CreatedResponseSchema
      );
      return result;
    },

    /**
     * Set a document version as active (Admin/Editor)
     *
     * @param gameId - The game UUID
     * @param documentId - The document UUID
     */
    async setActiveDocument(gameId: string, documentId: string): Promise<void> {
      await httpClient.post(
        `/api/v1/admin/shared-games/${gameId}/documents/${documentId}/set-active`,
        {}
      );
    },

    /**
     * Remove a document from a shared game (Admin/Editor)
     *
     * @param gameId - The game UUID
     * @param documentId - The document UUID
     */
    async removeDocument(gameId: string, documentId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/shared-games/${gameId}/documents/${documentId}`);
    },
  };
}

/**
 * Type for the shared games client instance
 */
export type SharedGamesClient = ReturnType<typeof createSharedGamesClient>;
