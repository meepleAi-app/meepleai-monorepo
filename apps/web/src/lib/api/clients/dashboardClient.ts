/**
 * Dashboard API Client (Issue #3316, #3319)
 *
 * Client for dashboard-specific endpoints including AI insights.
 */

import { HttpClient } from '../core/httpClient';

// ============================================================================
// Types
// ============================================================================

export type InsightType = 'backlog' | 'rules_reminder' | 'recommendation' | 'streak' | 'achievement';

export interface AiInsight {
  id: string;
  type: InsightType;
  icon: string;
  title: string;
  description: string;
  actionUrl: string;
  actionLabel: string;
  priority: number;
  metadata?: Record<string, unknown>;
}

export interface InsightsResponse {
  insights: AiInsight[];
  generatedAt: string;
  nextRefresh: string;
}

// ============================================================================
// Client Interface
// ============================================================================

export interface DashboardClient {
  /**
   * Get AI-powered dashboard insights
   *
   * Returns personalized insights based on user's library, play history,
   * and RAG-powered recommendations.
   */
  getInsights(): Promise<InsightsResponse>;
}

// ============================================================================
// Client Factory
// ============================================================================

export interface DashboardClientConfig {
  httpClient: HttpClient;
}

/**
 * Create Dashboard API client
 */
export function createDashboardClient(config: DashboardClientConfig): DashboardClient {
  const { httpClient } = config;

  return {
    async getInsights(): Promise<InsightsResponse> {
      return httpClient.get<InsightsResponse>('/api/v1/dashboard/insights');
    },
  };
}
