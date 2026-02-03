/**
 * ChartsSection Component - Issue #2790, #2850
 *
 * Analytics charts section using real data from useDashboardData hook.
 * Replaces mock API endpoints with existing trend data from DashboardStats.
 *
 * MeepleAI Design System (Issue #2850):
 * - Section title: 'Quicksand' font, 1.75rem, bold, #2d2d2d
 * - Warm color palette integration
 *
 * Data Sources:
 * - API Requests: apiRequestTrend from DashboardStats
 * - AI Usage: Calculated breakdown from DashboardMetrics
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import { useMemo } from 'react';

import { useDashboardData } from '@/hooks/queries/useDashboardData';
import type { DashboardMetrics } from '@/lib/api';

import { AIUsageDonut, AiUsageStats } from './AIUsageDonut';
import { APIRequestsChart, ApiRequestByDay } from './APIRequestsChart';

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

export function ChartsSection({ className }: ChartsSectionProps){
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
    <section className={`mb-12 ${className ?? ''}`}>
      {/* Issue #2850: MeepleAI section title styling */}
      <h2 className="mb-7 font-['Quicksand',sans-serif] text-[1.75rem] font-bold text-foreground">
        Analytics
      </h2>

      {/* Issue #2850: Gap 1.5rem (6) */}
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
