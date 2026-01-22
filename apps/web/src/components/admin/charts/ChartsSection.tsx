/**
 * ChartsSection Component - Issue #2790 (Part of Issue #2792)
 *
 * Analytics charts section using real data from useDashboardData hook.
 * Replaces mock API endpoints with existing trend data from DashboardStats.
 *
 * Data Sources:
 * - API Requests: apiRequestTrend from DashboardStats
 * - AI Usage: Calculated breakdown from DashboardMetrics
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import { useMemo } from 'react';

import type { DashboardMetrics } from '@/lib/api';
import { useDashboardData } from '@/hooks/queries/useDashboardData';

import { APIRequestsChart, ApiRequestByDay } from './APIRequestsChart';
import { AIUsageDonut, AiUsageStats } from './AIUsageDonut';

/**
 * Calculate AI usage breakdown from metrics
 *
 * Approximates usage distribution based on:
 * - RAG requests → Embedding + Completion calls
 * - Chat messages → Completion calls
 * - PDF processing → OCR + Embedding calls
 *
 * @export For unit testing the calculation logic
 */
export function calculateAiUsageBreakdown(metrics: DashboardMetrics | null): AiUsageStats[] {
  if (!metrics) {
    return [];
  }

  const {
    totalRagRequests,
    totalChatMessages,
    totalPdfDocuments,
  } = metrics;

  // Approximate breakdown with realistic ratios
  const embeddingCalls = totalRagRequests + totalPdfDocuments;
  const completionCalls = totalRagRequests + totalChatMessages;
  const ocrCalls = totalPdfDocuments;

  return [
    { category: 'Embeddings', count: embeddingCalls },
    { category: 'Completions', count: completionCalls },
    { category: 'OCR', count: ocrCalls },
  ];
}

export interface ChartsSectionProps {
  /** Optional class name */
  className?: string;
}

export function ChartsSection({ className }: ChartsSectionProps): JSX.Element {
  // Use consolidated dashboard data hook (Issue #2792)
  const { trends, metrics, isLoading } = useDashboardData();

  // Transform apiRequestTrend to chart format
  const apiRequestsData = useMemo<ApiRequestByDay[]>(() => {
    return trends.apiRequest.map(point => ({
      date: point.date,
      count: point.count,
    }));
  }, [trends.apiRequest]);

  // Calculate AI usage breakdown from metrics
  const aiUsageData = useMemo<AiUsageStats[]>(() => {
    return calculateAiUsageBreakdown(metrics);
  }, [metrics]);

  return (
    <section className={`mb-8 ${className ?? ''}`}>
      <h2 className="mb-4 text-2xl font-bold text-gray-900 dark:text-gray-100">Analytics</h2>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* API Requests Chart - Using real apiRequestTrend data */}
        <APIRequestsChart
          data={apiRequestsData}
          isLoading={isLoading}
        />

        {/* AI Usage Chart - Calculated from metrics */}
        <AIUsageDonut
          data={aiUsageData}
          isLoading={isLoading}
        />
      </div>
    </section>
  );
}
