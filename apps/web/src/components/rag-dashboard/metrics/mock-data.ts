/**
 * Mock data for RAG Performance Metrics
 *
 * Provides realistic mock data for development and testing.
 */

import type { RagMetrics } from './types';

/**
 * Default mock metrics data.
 */
export const MOCK_RAG_METRICS: RagMetrics = {
  timestamp: new Date().toISOString(),
  latency: {
    p50: 120,
    p95: 450,
    p99: 890,
    avg: 185,
    trend: -12, // 12% improvement
  },
  tokenUsage: {
    input: 12500,
    output: 4800,
    context: 8200,
    total: 25500,
    costEstimate: 0.038,
  },
  cache: {
    hitRate: 78,
    missRate: 22,
    totalHits: 1245,
    totalMisses: 355,
    cacheSize: 512, // MB
    ttlSeconds: 3600,
  },
  accuracy: {
    overallScore: 92,
    byStrategy: {
      Hybrid: 94,
      Semantic: 88,
      Keyword: 82,
      Contextual: 91,
      MultiQuery: 95,
      Agentic: 97,
    },
    userFeedbackScore: 4.3,
    citationAccuracy: 89,
  },
  cost: {
    currentSession: 0.24,
    projectedMonthly: 156.80,
    avgPerQuery: 0.0048,
    byStrategy: {
      Hybrid: 0.003,
      Semantic: 0.001,
      Keyword: 0.0005,
      Contextual: 0.005,
      MultiQuery: 0.012,
      Agentic: 0.08,
    },
    budgetUsed: 42,
    budgetLimit: 500,
  },
};

/**
 * Generate mock metrics with some randomization.
 */
export function generateMockMetrics(): RagMetrics {
  const randomVariation = (base: number, variance: number): number => {
    return Math.round(base * (1 + (Math.random() - 0.5) * variance * 2));
  };

  return {
    ...MOCK_RAG_METRICS,
    timestamp: new Date().toISOString(),
    latency: {
      p50: randomVariation(120, 0.15),
      p95: randomVariation(450, 0.2),
      p99: randomVariation(890, 0.25),
      avg: randomVariation(185, 0.15),
      trend: Math.round((Math.random() - 0.5) * 30),
    },
    cache: {
      ...MOCK_RAG_METRICS.cache,
      hitRate: Math.round(75 + Math.random() * 10),
      missRate: Math.round(15 + Math.random() * 10),
    },
    accuracy: {
      ...MOCK_RAG_METRICS.accuracy,
      overallScore: Math.round(88 + Math.random() * 8),
    },
  };
}
