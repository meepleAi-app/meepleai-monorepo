/**
 * useDashboardInsights - TanStack Query hooks for AI dashboard insights
 *
 * Issue #3316 - Frontend hook for AiInsightsWidget
 * Issue #3319 - Backend AI Insights RAG endpoint
 *
 * Provides automatic caching and refresh for AI-powered insights.
 */

import {
  useQuery,
  useQueryClient,
  UseQueryResult,
} from '@tanstack/react-query';

import { api } from '@/lib/api';

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
// Query Keys
// ============================================================================

export const insightsKeys = {
  all: ['insights'] as const,
  dashboard: () => [...insightsKeys.all, 'dashboard'] as const,
};

// ============================================================================
// Hooks
// ============================================================================

/**
 * Hook to fetch AI-powered dashboard insights
 *
 * Returns personalized insights based on user's library, play history,
 * and RAG-powered recommendations.
 *
 * Features:
 * - 15-minute cache (matches backend Redis TTL)
 * - Automatic retry with backoff
 * - Graceful degradation on AI service unavailability
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with insights data
 */
export function useDashboardInsights(
  enabled: boolean = true
): UseQueryResult<InsightsResponse, Error> {
  return useQuery({
    queryKey: insightsKeys.dashboard(),
    queryFn: async (): Promise<InsightsResponse> => {
      return api.dashboard.getInsights();
    },
    enabled,
    staleTime: 15 * 60 * 1000, // 15 minutes (matches backend cache)
    gcTime: 30 * 60 * 1000, // 30 minutes garbage collection
    retry: 2,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000),
  });
}

/**
 * Hook to manually refresh dashboard insights
 *
 * @returns Function to trigger refresh
 */
export function useRefreshInsights() {
  const queryClient = useQueryClient();

  return () => {
    queryClient.invalidateQueries({ queryKey: insightsKeys.dashboard() });
  };
}

/**
 * Hook to prefetch dashboard insights
 *
 * Useful for prefetching on dashboard page navigation.
 *
 * @returns Function to prefetch insights
 */
export function usePrefetchInsights() {
  const queryClient = useQueryClient();

  return () => {
    queryClient.prefetchQuery({
      queryKey: insightsKeys.dashboard(),
      queryFn: async (): Promise<InsightsResponse> => {
        return api.dashboard.getInsights();
      },
      staleTime: 15 * 60 * 1000,
    });
  };
}
