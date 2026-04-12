/**
 * Knowledge Base Client (Issue #4065)
 *
 * Client for Knowledge Base bounded context status endpoint.
 * Used by useEmbeddingStatus hook to poll RAG readiness.
 */

import { z } from 'zod';

import { GameDocumentSchema, type GameDocument } from '../schemas/game-documents.schemas';
import {
  KnowledgeBaseStatusSchema,
  RagConfigSchema,
  UserGameKbStatusSchema,
  AdminGameKbDocumentsSchema,
  AdminKbFeedbackSchema,
  GameKbSettingsSchema,
  type KnowledgeBaseStatus,
  type RagConfigResponse,
  type UserGameKbStatus,
  type AdminGameKbDocuments,
  type AdminKbFeedback,
  type GameKbSettings,
} from '../schemas/knowledge-base.schemas';

// ========== KB Management DTOs ==========

export type { UserGameKbStatus, AdminGameKbDocuments, AdminKbFeedback, GameKbSettings };

export interface SubmitKbFeedbackBody {
  chatSessionId: string;
  messageId: string;
  outcome: 'helpful' | 'not_helpful';
  comment?: string;
}

export interface AdminKbFeedbackParams {
  outcome?: 'helpful' | 'not_helpful';
  from?: string;
  page?: number;
  pageSize?: number;
}

export type GameKbSettingsPayload = Omit<GameKbSettings, 'gameId'>;

import type { HttpClient } from '../core/httpClient';

// ── Quick Links (S4) ────────────────────────────────────────────────────────

export const QuickLinkSchema = z.object({
  id: z.string(),
  title: z.string(),
  snippet: z.string(),
});
export const QuickLinksSchema = z.array(QuickLinkSchema);
export type QuickLink = z.infer<typeof QuickLinkSchema>;

export interface CreateKnowledgeBaseClientParams {
  httpClient: HttpClient;
}

export type KnowledgeBaseClient = ReturnType<typeof createKnowledgeBaseClient>;

/**
 * Knowledge Base API client with Zod validation
 */
export function createKnowledgeBaseClient({ httpClient }: CreateKnowledgeBaseClientParams) {
  return {
    /**
     * Get embedding status for a game's knowledge base
     * @param gameId Game ID (GUID format)
     * @returns Current embedding status with chunk progress
     */
    async getEmbeddingStatus(gameId: string): Promise<KnowledgeBaseStatus | null> {
      return httpClient.get(
        `/api/v1/knowledge-base/${encodeURIComponent(gameId)}/status`,
        KnowledgeBaseStatusSchema
      );
    },

    /**
     * Get KB (RAG) status for a private game
     * Polls /api/v1/private-games/{id}/kb-status
     * Issue #3664: Private game PDF support — KB readiness polling.
     * @param privateGameId Private game UUID
     * @returns Current KB status with chunk progress, or null if not found
     */
    async getPrivateGameKbStatus(privateGameId: string): Promise<KnowledgeBaseStatus | null> {
      return httpClient.get(
        `/api/v1/private-games/${encodeURIComponent(privateGameId)}/kb-status`,
        KnowledgeBaseStatusSchema
      );
    },

    /**
     * Get user's RAG configuration (Issue #5311)
     * @param strategy Optional strategy filter
     * @returns Current RAG config or defaults
     */
    async getRagConfig(strategy?: string): Promise<RagConfigResponse | null> {
      const query = strategy ? `?strategy=${encodeURIComponent(strategy)}` : '';
      return httpClient.get(`/api/v1/rag-dashboard/config${query}`, RagConfigSchema);
    },

    /**
     * Save user's RAG configuration (Issue #5311)
     * @param config Full RAG config to persist
     * @returns Saved config
     */
    async saveRagConfig(config: RagConfigResponse): Promise<RagConfigResponse> {
      return httpClient.put(`/api/v1/rag-dashboard/config`, config, RagConfigSchema);
    },

    /**
     * Reset user's RAG configuration to defaults (Issue #5311)
     * @param strategy Optional strategy to reset
     * @returns Default config
     */
    async resetRagConfig(strategy?: string): Promise<RagConfigResponse> {
      const body = strategy ? { strategy } : {};
      return httpClient.post(`/api/v1/rag-dashboard/config/reset`, body, RagConfigSchema);
    },

    // ========== KB Management Methods (KB-01/02/03/06/08/10) ==========

    /**
     * KB-03: Get user-facing KB status for a game
     * @param gameId Game ID (GUID format)
     * @returns KB coverage status with suggested questions
     */
    async getUserGameKbStatus(gameId: string): Promise<UserGameKbStatus | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/knowledge-base`,
        UserGameKbStatusSchema
      );
    },

    /**
     * KB-06: Submit user feedback on a KB-powered chat message
     * @param gameId Game ID (GUID format)
     * @param body Feedback payload
     */
    async submitKbFeedback(gameId: string, body: SubmitKbFeedbackBody): Promise<void> {
      await httpClient.post(
        `/api/v1/games/${encodeURIComponent(gameId)}/knowledge-base/feedback`,
        body
      );
    },

    /**
     * KB-01: Get admin list of indexed documents for a game's KB
     * @param gameId Game ID (GUID format)
     * @returns Indexed document list with chunk counts and status
     */
    async getAdminGameKbDocuments(gameId: string): Promise<AdminGameKbDocuments | null> {
      return httpClient.get(
        `/api/v1/admin/kb/games/${encodeURIComponent(gameId)}/documents`,
        AdminGameKbDocumentsSchema
      );
    },

    /**
     * KB-02: Remove an indexed document from a game's KB
     * @param gameId Game ID (GUID format)
     * @param vectorDocId Vector document ID to remove
     */
    async removeKbDocument(gameId: string, vectorDocId: string): Promise<void> {
      return httpClient.delete(
        `/api/v1/admin/kb/games/${encodeURIComponent(gameId)}/documents/${encodeURIComponent(vectorDocId)}`
      );
    },

    /**
     * KB-08: Get admin feedback list for a game's KB
     * @param gameId Game ID (GUID format)
     * @param params Optional filters (outcome, date range, pagination)
     * @returns Paginated feedback items
     */
    async getAdminKbFeedback(
      gameId: string,
      params?: AdminKbFeedbackParams
    ): Promise<AdminKbFeedback | null> {
      const searchParams = new URLSearchParams();
      if (params?.outcome) searchParams.set('outcome', params.outcome);
      if (params?.from) searchParams.set('from', params.from);
      if (params?.page !== undefined) searchParams.set('page', String(params.page));
      if (params?.pageSize !== undefined) searchParams.set('pageSize', String(params.pageSize));
      const query = searchParams.toString() ? `?${searchParams.toString()}` : '';
      return httpClient.get(
        `/api/v1/admin/kb/games/${encodeURIComponent(gameId)}/feedback${query}`,
        AdminKbFeedbackSchema
      );
    },

    /**
     * KB-10: Get per-game KB settings (admin)
     * @param gameId Game ID (GUID format)
     * @returns Current KB settings overrides
     */
    async getGameKbSettings(gameId: string): Promise<GameKbSettings | null> {
      return httpClient.get(
        `/api/v1/admin/kb/games/${encodeURIComponent(gameId)}/settings`,
        GameKbSettingsSchema
      );
    },

    /**
     * KB-10: Update per-game KB settings (admin)
     * @param gameId Game ID (GUID format)
     * @param settings Settings overrides to apply
     * @returns Updated KB settings
     */
    async setGameKbSettings(
      gameId: string,
      settings: GameKbSettingsPayload
    ): Promise<GameKbSettings> {
      return httpClient.put(
        `/api/v1/admin/kb/games/${encodeURIComponent(gameId)}/settings`,
        settings,
        GameKbSettingsSchema
      );
    },

    /**
     * Get user-facing KB documents for a game
     * @param gameId Game ID (GUID format)
     * @returns List of indexed documents with category and version info
     */
    async getGameDocuments(gameId: string): Promise<GameDocument[]> {
      const result = await httpClient.get(
        `/api/v1/knowledge-base/${encodeURIComponent(gameId)}/documents`,
        z.array(GameDocumentSchema)
      );
      return result ?? [];
    },

    /**
     * S4: Get RAG quick links for a game (max 4 from the game's KB)
     * @param gameId Game ID (GUID format)
     * @returns Array of quick link items (id, title, snippet)
     */
    async getQuickLinks(gameId: string): Promise<QuickLink[]> {
      // Route: GET /api/v1/knowledge-base/{gameId}/quick-links (path param, consistent with other KB endpoints)
      const result = await httpClient.get(
        `/api/v1/knowledge-base/${encodeURIComponent(gameId)}/quick-links`,
        QuickLinksSchema
      );
      return result ?? [];
    },
  };
}
