/**
 * RAG Execution Client (Issue #4459)
 *
 * API client for RAG execution replay and comparison endpoints.
 * Note: Replay uses SSE streaming (handled by useRagExecutionReplay hook directly).
 */

import {
  executionComparisonSchema,
  type CompareExecutionsRequest,
  type ExecutionComparison,
} from '../schemas/rag-execution.schemas';
import type { HttpClient } from '../core/httpClient';

export interface CreateRagExecutionClientParams {
  httpClient: HttpClient;
}

export interface RagExecutionClient {
  /**
   * Compare two RAG executions side-by-side.
   * Returns block-level diffs, metrics delta, and document selection changes.
   */
  compare(request: CompareExecutionsRequest): Promise<ExecutionComparison>;
}

/**
 * Create RAG Execution API client with Zod validation.
 * Note: Replay endpoint is SSE-based and handled via useRagExecutionReplay hook.
 */
export function createRagExecutionClient({
  httpClient,
}: CreateRagExecutionClientParams): RagExecutionClient {
  const client: RagExecutionClient = {
    async compare(request: CompareExecutionsRequest): Promise<ExecutionComparison> {
      return httpClient.post(
        '/api/v1/admin/rag-executions/compare',
        request,
        executionComparisonSchema
      );
    },
  };

  return client;
}
