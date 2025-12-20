/**
 * ShareLinks Client (ISSUE-2052)
 *
 * Modular client for shareable chat thread links.
 * Covers: Create links, revoke links, access shared threads, add comments
 */

import {
  CreateShareLinkResponseSchema,
  GetSharedThreadResponseSchema,
  AddCommentToSharedThreadResponseSchema,
  type CreateShareLinkResponse,
  type RevokeShareLinkResponse,
  type GetSharedThreadResponse,
  type AddCommentToSharedThreadResponse,
} from '../schemas/share-links.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateShareLinksClientParams {
  httpClient: HttpClient;
}

export interface CreateShareLinkRequest {
  threadId: string;
  role: 'view' | 'comment';
  expiryDays?: number;
  label?: string;
}

export interface AddCommentRequest {
  token: string;
  content: string;
}

/**
 * ShareLinks API client with Zod validation
 */
export function createShareLinksClient({ httpClient }: CreateShareLinksClientParams) {
  return {
    // ========== Authenticated Operations ==========

    /**
     * Create a shareable link for a chat thread
     * POST /api/v1/share-links
     * Requires: Session authentication
     */
    async createShareLink(request: CreateShareLinkRequest): Promise<CreateShareLinkResponse> {
      return await httpClient.post('/api/v1/share-links', request, CreateShareLinkResponseSchema);
    },

    /**
     * Revoke a shareable link
     * DELETE /api/v1/share-links/{id}
     * Requires: Session authentication (must be creator)
     */
    async revokeShareLink(shareLinkId: string): Promise<RevokeShareLinkResponse> {
      await httpClient.delete(`/api/v1/share-links/${shareLinkId}`);
      return { success: true };
    },

    // ========== Public Operations (via JWT token) ==========

    /**
     * Get a shared chat thread via share link token
     * GET /api/v1/shared/thread?token={token}
     * Public: No session required, uses JWT token from URL
     * @throws NotFoundError if token is invalid or thread not found
     */
    async getSharedThread(token: string): Promise<GetSharedThreadResponse> {
      const result = await httpClient.get(
        `/api/v1/shared/thread?token=${encodeURIComponent(token)}`,
        GetSharedThreadResponseSchema
      );
      if (!result) {
        throw new Error('Thread not found or access denied');
      }
      return result;
    },

    /**
     * Add a comment to a shared chat thread
     * POST /api/v1/shared/thread/comment
     * Public: No session required, uses JWT token in request
     * Requires: Share link with 'comment' role
     * Rate-limited: 10 comments/hour per share link
     */
    async addCommentToSharedThread(
      request: AddCommentRequest
    ): Promise<AddCommentToSharedThreadResponse> {
      return await httpClient.post(
        '/api/v1/shared/thread/comment',
        request,
        AddCommentToSharedThreadResponseSchema
      );
    },
  };
}

export type ShareLinksClient = ReturnType<typeof createShareLinksClient>;
