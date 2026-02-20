/**
 * Shared Game Catalog API Client (Issue #2372)
 *
 * API client for SharedGameCatalog bounded context.
 * Covers: Games search/CRUD, Categories, Mechanics, FAQs, Errata, Delete Workflow
 */

import { z } from 'zod';

import { type HttpClient } from '../core/httpClient';
import {
  agentDefinitionDtoSchema,
  kbCardDtoSchema,
  type AgentDefinitionDto,
  type KbCardDto,
} from '../schemas/agent-definitions.schemas';
import {
  SharedGameDetailSchema,
  PagedSharedGamesSchema,
  GameCategorySchema,
  GameMechanicSchema,
  PagedDeleteRequestsSchema,
  CreatedResponseSchema,
  DeleteRequestAcceptedSchema,
  SharedGameDocumentSchema,
  BulkImportResultSchema,
  BggSearchResultSchema,
  BggDuplicateCheckResultSchema,
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
  type BulkGameImportDto,
  type BulkImportResult,
  type BggSearchResult,
  type BggDuplicateCheckResult,
  type UpdateFromBggRequest,
  type BatchApprovalResult,
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

    /**
     * Bulk import games from JSON (ADMIN ONLY)
     *
     * Batch import multiple games in a single request.
     * More efficient than individual create calls.
     * Max 100 games per request.
     *
     * @param games - Array of game data to import
     * @returns Import result with success/failure counts and imported game IDs
     */
    async bulkImport(games: BulkGameImportDto[]): Promise<BulkImportResult> {
      return httpClient.post<BulkImportResult>(
        '/api/v1/admin/shared-games/bulk-import',
        { games },
        BulkImportResultSchema
      );
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
     * Batch approve multiple games (ADMIN ONLY)
     * Issue #3350: Batch Approval/Rejection for Games
     *
     * Approves multiple games in a single transaction.
     *
     * @param gameIds - Array of game UUIDs to approve
     * @param note - Optional approval note
     * @returns Result with success/failure counts and errors
     */
    async batchApprove(
      gameIds: string[],
      note?: string
    ): Promise<BatchApprovalResult> {
      return httpClient.post(`/api/v1/admin/shared-games/batch-approve`, {
        gameIds,
        note,
      });
    },

    /**
     * Batch reject multiple games (ADMIN ONLY)
     * Issue #3350: Batch Approval/Rejection for Games
     *
     * Rejects multiple games in a single transaction with a common reason.
     *
     * @param gameIds - Array of game UUIDs to reject
     * @param reason - Reason for rejection (required)
     * @returns Result with success/failure counts and errors
     */
    async batchReject(
      gameIds: string[],
      reason: string
    ): Promise<BatchApprovalResult> {
      return httpClient.post(`/api/v1/admin/shared-games/batch-reject`, {
        gameIds,
        reason,
      });
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

    // ========== BGG Import Flow (Admin Add from BGG) ==========

    /**
     * Search BoardGameGeek for games (ADMIN/EDITOR)
     *
     * Used for autocomplete in the add-from-BGG flow.
     *
     * @param query - Search query (game name)
     * @param exact - Whether to search for exact match only
     * @returns List of BGG search results
     */
    async searchBgg(query: string, exact = false): Promise<BggSearchResult[]> {
      const queryParams = new URLSearchParams();
      queryParams.set('query', query);
      if (exact) queryParams.set('exact', 'true');

      const result = await httpClient.get(
        `/api/v1/admin/shared-games/bgg/search?${queryParams.toString()}`,
        z.array(BggSearchResultSchema)
      );
      return result ?? [];
    },

    /**
     * Check if a game with given BGG ID already exists (ADMIN/EDITOR)
     *
     * Returns both existing game data and fresh BGG data for diff comparison.
     * Used to detect duplicates before import and propose updates.
     *
     * @param bggId - BoardGameGeek game ID
     * @returns Duplicate check result with existing and BGG data
     */
    async checkBggDuplicate(bggId: number): Promise<BggDuplicateCheckResult> {
      const result = await httpClient.get(
        `/api/v1/admin/shared-games/bgg/check-duplicate/${bggId}`,
        BggDuplicateCheckResultSchema
      );
      return result ?? { isDuplicate: false, existingGameId: null, existingGame: null, bggData: null };
    },

    /**
     * Import a new game from BoardGameGeek (ADMIN/EDITOR)
     *
     * Creates a new SharedGame in Draft status with data from BGG.
     *
     * @param bggId - BoardGameGeek game ID
     * @returns Created game ID
     */
    async importFromBgg(bggId: number): Promise<string> {
      const result = await httpClient.post<CreatedResponse>(
        '/api/v1/admin/shared-games/import-bgg',
        { bggId },
        CreatedResponseSchema
      );
      return result.id;
    },

    /**
     * Update existing game from BGG data (ADMIN/EDITOR)
     *
     * Updates an existing game with fresh data from BGG.
     * Supports selective field updates via fieldsToUpdate parameter.
     *
     * @param gameId - Existing game UUID
     * @param request - BGG ID and optional fields to update
     * @returns Updated game ID
     */
    async updateFromBgg(gameId: string, request: UpdateFromBggRequest): Promise<string> {
      const result = await httpClient.put<string>(
        `/api/v1/admin/shared-games/${gameId}/update-from-bgg`,
        request,
        z.string().uuid()
      );
      return result;
    },

    // ========== PDF Wizard Upload (Issue #4168) ==========

    /**
     * Upload PDF for wizard import workflow (ADMIN/EDITOR)
     * Issue #4168: Wizard-specific upload endpoint
     *
     * Uploads PDF to temporary storage for wizard processing.
     * Does not require gameId (wizard-specific endpoint).
     *
     * @param file - PDF file to upload
     * @param onProgress - Optional progress callback (0-100)
     * @returns Upload result with documentId
     */
    async wizardUploadPdf(
      file: File,
      onProgress?: (percent: number) => void
    ): Promise<{ documentId: string; fileName: string }> {
      const formData = new FormData();
      formData.append('file', file);

      const WizardUploadResultSchema = z.object({
        documentId: z.string(),
        fileName: z.string(),
      });

      // Use XMLHttpRequest for progress tracking
      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        xhr.upload.addEventListener('progress', e => {
          if (e.lengthComputable && onProgress) {
            const percent = Math.round((e.loaded / e.total) * 100);
            onProgress(percent);
          }
        });

        xhr.addEventListener('load', () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const response = JSON.parse(xhr.responseText);
              const validated = WizardUploadResultSchema.parse(response);
              resolve(validated);
            } catch (_error) {
              reject(new Error('Invalid response format'));
            }
          } else {
            reject(new Error(`Upload failed: ${xhr.status} ${xhr.statusText}`));
          }
        });

        xhr.addEventListener('error', () => reject(new Error('Network error')));
        xhr.addEventListener('abort', () => reject(new Error('Upload cancelled')));

        xhr.open('POST', '/api/v1/admin/games/wizard/upload-pdf');
        xhr.send(formData);
      });
    },

    // ========== AI Agent Linking (Issue #4924 + #4926) ==========

    /**
     * Get linked AI agent for a shared game (ADMIN/EDITOR)
     * GET /api/v1/admin/shared-games/{gameId}/linked-agent
     * Returns null (204 NoContent) if no agent is linked.
     *
     * @param gameId - Game UUID
     * @returns Linked agent or null if no agent linked
     */
    async getLinkedAgent(gameId: string): Promise<AgentDefinitionDto | null> {
      return httpClient.get(
        `/api/v1/admin/shared-games/${gameId}/linked-agent`,
        agentDefinitionDtoSchema
      );
    },

    /**
     * Link an AI agent to a shared game (ADMIN/EDITOR)
     * POST /api/v1/admin/shared-games/{gameId}/link-agent/{agentId}
     *
     * @param gameId - Game UUID
     * @param agentId - Agent UUID
     */
    async linkAgent(gameId: string, agentId: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/shared-games/${gameId}/link-agent/${agentId}`, {});
    },

    /**
     * Unlink AI agent from a shared game (ADMIN/EDITOR)
     * DELETE /api/v1/admin/shared-games/{gameId}/unlink-agent
     *
     * @param gameId - Game UUID
     */
    async unlinkAgent(gameId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/shared-games/${gameId}/unlink-agent`);
    },

    // ========== KB Cards (Issue #4925 + #4926) ==========

    /**
     * Get KB cards (indexed vector documents) for a shared game (ADMIN/EDITOR)
     * GET /api/v1/admin/shared-games/{gameId}/kb-cards?status=completed
     *
     * @param gameId - Game UUID
     * @param status - Optional filter: pending|processing|completed|failed
     * @returns Array of KB card DTOs
     */
    async getKbCards(gameId: string, status?: string): Promise<KbCardDto[]> {
      const path = status
        ? `/api/v1/admin/shared-games/${gameId}/kb-cards?status=${encodeURIComponent(status)}`
        : `/api/v1/admin/shared-games/${gameId}/kb-cards`;
      const result = await httpClient.get(path, kbCardDtoSchema.array());
      return result ?? [];
    },

    // ========== Admin PDF Upload (Issue #4922 + #4926) ==========

    /**
     * Upload a PDF for a shared game as admin (ADMIN/EDITOR)
     * POST /api/v1/admin/shared-games/{gameId}/documents/upload (multipart/form-data)
     *
     * @param gameId - Game UUID
     * @param formData - FormData with fields: file, documentType, version, setAsActive, tags
     * @param onProgress - Optional XHR progress callback (0-100)
     * @returns Upload result with pdfDocumentId and processingStatus
     */
    async uploadDocument(
      gameId: string,
      formData: FormData,
      onProgress?: (percent: number) => void
    ): Promise<{ pdfDocumentId: string; sharedGameDocumentId: string; processingStatus: string }> {
      const apiBase = typeof window !== 'undefined'
        ? (process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:8080')
        : 'http://localhost:8080';

      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        if (onProgress) {
          xhr.upload.addEventListener('progress', (e) => {
            if (e.lengthComputable) onProgress(Math.round((e.loaded / e.total) * 100));
          });
        }

        xhr.addEventListener('load', () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              resolve(JSON.parse(xhr.responseText));
            } catch {
              reject(new Error('Invalid response format'));
            }
          } else {
            try {
              const err = JSON.parse(xhr.responseText);
              reject(new Error(err.error ?? `Upload failed: ${xhr.status}`));
            } catch {
              reject(new Error(`Upload failed: ${xhr.status} ${xhr.statusText}`));
            }
          }
        });

        xhr.addEventListener('error', () => reject(new Error('Network error during upload')));
        xhr.addEventListener('abort', () => reject(new Error('Upload cancelled')));

        xhr.open('POST', `${apiBase}/api/v1/admin/shared-games/${gameId}/documents/upload`);
        xhr.withCredentials = true;
        xhr.send(formData);
      });
    },
  };
}

/**
 * Type for the shared games client instance
 */
export type SharedGamesClient = ReturnType<typeof createSharedGamesClient>;
