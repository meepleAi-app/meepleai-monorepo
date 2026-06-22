/**
 * KB Quality API Client (Issue #1675)
 *
 * Thin wrapper around the admin per-doc evaluation endpoints. Mirrors the
 * `createXxxClient({ httpClient })` factory pattern used across the codebase
 * (see accessRequestsClient, knowledgeBaseClient, etc.). The returned object
 * is composed into `api.kbQuality` by `createApiClient` in lib/api/index.ts.
 *
 * Endpoints — all under /api/v1/admin/kb/docs/{docId}/evaluations:
 * - POST   start a new evaluation run        → EvaluationStartedResult
 * - GET    paginated history                 → PagedEvaluations
 * - GET    {evaluationId} detail (polled)    → EvaluationDetailDto
 */

import { type HttpClient } from '../core/httpClient';
import {
  EvaluationDetailDtoSchema,
  EvaluationStartedResultSchema,
  PagedEvaluationsSchema,
  type EvaluationDetailDto,
  type EvaluationStartedResult,
  type PagedEvaluations,
  type StartEvaluationRequest,
} from '../schemas/kb-quality.schemas';

export interface CreateKbQualityClientParams {
  httpClient: HttpClient;
}

export function createKbQualityClient({ httpClient }: CreateKbQualityClientParams) {
  const base = (docId: string) => `/api/v1/admin/kb/docs/${encodeURIComponent(docId)}/evaluations`;

  return {
    /**
     * Trigger a new evaluation run for the given doc. The handler runs
     * synchronously end-to-end (goldset gen + metrics) and returns the
     * terminal result; the FE polls `getEvaluation` for the detail view
     * with its richer projection.
     */
    async startEvaluation(
      docId: string,
      body: StartEvaluationRequest
    ): Promise<EvaluationStartedResult> {
      return httpClient.post<EvaluationStartedResult>(
        base(docId),
        body,
        EvaluationStartedResultSchema
      );
    },

    /**
     * Fetch the single-run projection. Returns null when the BE returns 404
     * (e.g. wrong docId+evaluationId combination), preserving the HttpClient
     * contract for GET nullability.
     */
    async getEvaluation(docId: string, evaluationId: string): Promise<EvaluationDetailDto | null> {
      return httpClient.get<EvaluationDetailDto>(
        `${base(docId)}/${encodeURIComponent(evaluationId)}`,
        EvaluationDetailDtoSchema
      );
    },

    /**
     * Paginated list of runs for the doc. Page/pageSize defaults match the
     * BE handler: page ≥ 1, pageSize clamped to [1, 100]. Returns an empty
     * page when the BE returns null.
     */
    async listEvaluations(docId: string, page = 1, pageSize = 20): Promise<PagedEvaluations> {
      const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) }).toString();
      const response = await httpClient.get<PagedEvaluations>(
        `${base(docId)}?${qs}`,
        PagedEvaluationsSchema
      );
      return response ?? { items: [], totalCount: 0, page, pageSize };
    },
  };
}

export type KbQualityClient = ReturnType<typeof createKbQualityClient>;
