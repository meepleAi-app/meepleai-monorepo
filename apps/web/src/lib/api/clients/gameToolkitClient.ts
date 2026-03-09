/**
 * GameToolkit API Client
 *
 * AI-assisted toolkit generation endpoints (Phase 0).
 *
 * Endpoints:
 * - POST /api/v1/game-toolkits/{gameId}/generate-from-kb  — AI suggestion from KB
 * - POST /api/v1/game-toolkits/{gameId}/apply-ai-suggestion — Apply AI suggestion
 */

import {
  AiToolkitSuggestionSchema,
  GameToolkitTemplateDtoSchema,
  type AiToolkitSuggestion,
  type GameToolkitTemplateDto,
} from '../schemas/toolkit.schemas';

import type { GameToolkitDto } from '../../types/gameToolkit';
import type { HttpClient } from '../core/httpClient';

export interface GameToolkitClient {
  /**
   * Generate an AI toolkit suggestion from knowledge-base content.
   *
   * @param gameId - SharedGame UUID
   * @returns AI-generated toolkit suggestion with reasoning
   */
  generateToolkitFromKb(gameId: string): Promise<AiToolkitSuggestion>;

  /**
   * Apply an AI suggestion to create or update a toolkit.
   *
   * @param gameId     - SharedGame UUID
   * @param toolkitId  - Existing toolkit UUID to update, or null to create new
   * @param suggestion - The AI suggestion payload
   * @returns The created/updated GameToolkitDto
   */
  applyAiSuggestion(
    gameId: string,
    toolkitId: string | null,
    suggestion: AiToolkitSuggestion
  ): Promise<GameToolkitDto>;

  // Template marketplace
  getApprovedTemplates(category?: string): Promise<GameToolkitTemplateDto[]>;
  getPendingReviewTemplates(): Promise<GameToolkitTemplateDto[]>;
  submitForReview(toolkitId: string): Promise<GameToolkitTemplateDto>;
  approveTemplate(toolkitId: string, notes?: string): Promise<GameToolkitTemplateDto>;
  rejectTemplate(toolkitId: string, notes: string): Promise<GameToolkitTemplateDto>;
  cloneFromTemplate(templateId: string, gameId: string): Promise<GameToolkitDto>;
}

export function createGameToolkitClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): GameToolkitClient {
  return {
    async generateToolkitFromKb(gameId) {
      const response = await httpClient.post<AiToolkitSuggestion>(
        `/api/v1/game-toolkits/${gameId}/generate-from-kb`,
        {}
      );
      return AiToolkitSuggestionSchema.parse(response);
    },

    async applyAiSuggestion(gameId, toolkitId, suggestion) {
      const response = await httpClient.post<GameToolkitDto>(
        `/api/v1/game-toolkits/${gameId}/apply-ai-suggestion`,
        { toolkitId, suggestion }
      );
      return response;
    },

    // Template marketplace
    async getApprovedTemplates(category) {
      const params = category ? `?category=${encodeURIComponent(category)}` : '';
      const response = await httpClient.get<GameToolkitTemplateDto[]>(
        `/api/v1/game-toolkits/templates${params}`
      );
      return (response ?? []).map(t => GameToolkitTemplateDtoSchema.parse(t));
    },

    async getPendingReviewTemplates() {
      const response = await httpClient.get<GameToolkitTemplateDto[]>(
        `/api/v1/game-toolkits/templates/pending-review`
      );
      return (response ?? []).map(t => GameToolkitTemplateDtoSchema.parse(t));
    },

    async submitForReview(toolkitId) {
      const response = await httpClient.post<GameToolkitTemplateDto>(
        `/api/v1/game-toolkits/${toolkitId}/submit-for-review`,
        {}
      );
      return GameToolkitTemplateDtoSchema.parse(response);
    },

    async approveTemplate(toolkitId, notes) {
      const response = await httpClient.post<GameToolkitTemplateDto>(
        `/api/v1/game-toolkits/${toolkitId}/approve`,
        { notes: notes ?? null }
      );
      return GameToolkitTemplateDtoSchema.parse(response);
    },

    async rejectTemplate(toolkitId, notes) {
      const response = await httpClient.post<GameToolkitTemplateDto>(
        `/api/v1/game-toolkits/${toolkitId}/reject`,
        { notes }
      );
      return GameToolkitTemplateDtoSchema.parse(response);
    },

    async cloneFromTemplate(templateId, gameId) {
      const response = await httpClient.post<GameToolkitDto>(
        `/api/v1/game-toolkits/clone-from-template/${templateId}`,
        { gameId }
      );
      return response;
    },
  };
}
