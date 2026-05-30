/**
 * Admin KB Used-by API client.
 * Issue #1651: F3-FU-2 — agents that consume a PDF document.
 */

import { apiClient } from '@/lib/api/client';
import {
  KbDocConsumingAgentsResponseSchema,
  type KbDocConsumingAgent,
} from '@/lib/api/schemas/kb-consuming-agents.schemas';

/**
 * GET /api/v1/admin/kb/docs/{docId}/agents
 * Returns the agents that explicitly consume the document via KbCardIds.
 * Empty array when no agent consumes it.
 */
export async function fetchKbDocConsumingAgents(docId: string): Promise<KbDocConsumingAgent[]> {
  const result = await apiClient.get<KbDocConsumingAgent[]>(
    `/api/v1/admin/kb/docs/${docId}/agents`,
    KbDocConsumingAgentsResponseSchema
  );
  return result ?? [];
}
