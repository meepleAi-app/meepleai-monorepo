/**
 * AI Usage API Client
 * Issue #94: C3 Editor Self-Service AI Usage Page
 * Issue #5484: Editor self-service AI usage
 *
 * TypeScript client for user-facing AI usage endpoints.
 * Extracted from dashboard-client.ts to respect bounded-context SRP.
 */

import { HttpClient } from '../core/httpClient';
import {
  UserAiUsageDtoSchema,
  type UserAiUsageDto,
  AiUsageSummaryDtoSchema,
  type AiUsageSummaryDto,
  AiUsageDistributionsDtoSchema,
  type AiUsageDistributionsDto,
  AiUsageRecentDtoSchema,
  type AiUsageRecentDto,
} from '../schemas/ai-usage.schemas';

const httpClient = new HttpClient();

/**
 * AI Usage API client for editor self-service usage tracking
 * Issue #94, Issue #5484
 */
export const aiUsageClient = {
  /**
   * Get current user's AI usage statistics
   * Issue #5484: Editor self-service AI usage
   * @param days Lookback period (default: 30)
   */
  async getMyAiUsage(days = 30): Promise<UserAiUsageDto> {
    const response = await httpClient.get<UserAiUsageDto>(
      `/api/v1/users/me/ai-usage?days=${days}`,
      UserAiUsageDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage');
    return response;
  },

  /**
   * Get multi-period AI usage summary (today/7d/30d)
   * Issue #94: C3 Editor Self-Service AI Usage Page
   */
  async getMyAiUsageSummary(): Promise<AiUsageSummaryDto> {
    const response = await httpClient.get<AiUsageSummaryDto>(
      '/api/v1/users/me/ai-usage/summary',
      AiUsageSummaryDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage summary');
    return response;
  },

  /**
   * Get AI usage distributions (model, provider, operation)
   * Issue #94: C3 Editor Self-Service AI Usage Page
   */
  async getMyAiUsageDistributions(days = 30): Promise<AiUsageDistributionsDto> {
    const response = await httpClient.get<AiUsageDistributionsDto>(
      `/api/v1/users/me/ai-usage/distributions?days=${days}`,
      AiUsageDistributionsDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage distributions');
    return response;
  },

  /**
   * Get recent AI requests (last 7 days, paginated)
   * Issue #94: C3 Editor Self-Service AI Usage Page
   */
  async getMyAiUsageRecent(page = 1, pageSize = 20): Promise<AiUsageRecentDto> {
    const response = await httpClient.get<AiUsageRecentDto>(
      `/api/v1/users/me/ai-usage/recent?page=${page}&pageSize=${pageSize}`,
      AiUsageRecentDtoSchema
    );
    if (!response) throw new Error('Failed to fetch recent AI requests');
    return response;
  },
};
