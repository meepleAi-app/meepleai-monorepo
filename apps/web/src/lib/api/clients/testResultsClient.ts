/**
 * Test Results Client (Issue #3379)
 *
 * API client for admin test results history & persistence endpoints.
 */

import { z } from 'zod';

import {
  testResultSchema,
  testResultListSchema,
  type TestResult,
  type TestResultList,
  type SaveTestResultRequest,
  type TestResultsQuery,
} from '../schemas/test-results.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateTestResultsClientParams {
  httpClient: HttpClient;
}

export interface TestResultsClient {
  /**
   * Save a new test result
   */
  save(request: SaveTestResultRequest): Promise<{ id: string }>;

  /**
   * Get test results with optional filters
   */
  getAll(query?: TestResultsQuery): Promise<TestResultList>;

  /**
   * Get a single test result by ID
   */
  getById(id: string): Promise<TestResult | null>;

  /**
   * Delete a test result
   */
  delete(id: string): Promise<void>;
}
const saveResponseSchema = z.object({
  id: z.string().uuid(),
});

/**
 * Create Test Results API client with Zod validation
 */
export function createTestResultsClient({
  httpClient,
}: CreateTestResultsClientParams): TestResultsClient {
  const client: TestResultsClient = {
    async save(request: SaveTestResultRequest): Promise<{ id: string }> {
      return httpClient.post('/api/v1/admin/test-results', request, saveResponseSchema);
    },

    async getAll(query?: TestResultsQuery): Promise<TestResultList> {
      const params = new URLSearchParams();

      if (query?.typologyId) params.append('typologyId', query.typologyId);
      if (query?.from) params.append('from', query.from);
      if (query?.to) params.append('to', query.to);
      if (query?.savedOnly !== undefined) params.append('savedOnly', String(query.savedOnly));
      if (query?.skip !== undefined) params.append('skip', String(query.skip));
      if (query?.take !== undefined) params.append('take', String(query.take));

      const queryString = params.toString();
      const url = `/api/v1/admin/test-results${queryString ? `?${queryString}` : ''}`;

      const response = await httpClient.get(url, testResultListSchema);
      return response ?? { items: [], totalCount: 0, skip: 0, take: 50 };
    },

    async getById(id: string): Promise<TestResult | null> {
      return httpClient.get(
        `/api/v1/admin/test-results/${encodeURIComponent(id)}`,
        testResultSchema
      );
    },

    async delete(id: string): Promise<void> {
      return httpClient.delete(`/api/v1/admin/test-results/${encodeURIComponent(id)}`);
    },
  };

  return client;
}
