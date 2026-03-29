/**
 * Agent Documents API Client
 *
 * Client for user-scoped agent document selection endpoints.
 * Fetches available documents for a library game's agent and
 * updates the user's document selection.
 */

import {
  AvailableDocumentsSchema,
  type AvailableDocuments,
} from '../schemas/agent-documents.schemas';

import type { HttpClient } from '../core/httpClient';

export interface AgentDocumentsClient {
  getAvailableDocuments(gameId: string): Promise<AvailableDocuments>;
  updateSelectedDocuments(gameId: string, selectedDocumentIds: string[]): Promise<void>;
}

export interface CreateAgentDocumentsClientParams {
  httpClient: HttpClient;
}

export function createAgentDocumentsClient({
  httpClient,
}: CreateAgentDocumentsClientParams): AgentDocumentsClient {
  return {
    async getAvailableDocuments(gameId: string): Promise<AvailableDocuments> {
      const data = await httpClient.get<AvailableDocuments>(
        `/api/v1/library/games/${gameId}/agent/documents`,
        AvailableDocumentsSchema
      );
      if (!data) throw new Error('Failed to fetch agent documents');
      return data;
    },

    async updateSelectedDocuments(gameId: string, selectedDocumentIds: string[]): Promise<void> {
      await httpClient.put(`/api/v1/library/games/${gameId}/agent/documents`, {
        selectedDocumentIds,
      });
    },
  };
}
