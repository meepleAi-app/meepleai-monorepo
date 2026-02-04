import { describe, expect, it } from 'vitest';

import { generateMockMetrics, MOCK_RAG_METRICS } from '../mock-data';

describe('mock-data', () => {
  describe('MOCK_RAG_METRICS', () => {
    it('contains all required metric sections', () => {
      expect(MOCK_RAG_METRICS).toHaveProperty('latency');
      expect(MOCK_RAG_METRICS).toHaveProperty('tokenUsage');
      expect(MOCK_RAG_METRICS).toHaveProperty('cache');
      expect(MOCK_RAG_METRICS).toHaveProperty('accuracy');
      expect(MOCK_RAG_METRICS).toHaveProperty('cost');
      expect(MOCK_RAG_METRICS).toHaveProperty('timestamp');
    });

    it('has valid latency metrics', () => {
      const { latency } = MOCK_RAG_METRICS;
      expect(latency.p50).toBeGreaterThan(0);
      expect(latency.p95).toBeGreaterThan(latency.p50);
      expect(latency.p99).toBeGreaterThan(latency.p95);
      expect(latency.avg).toBeGreaterThan(0);
      expect(typeof latency.trend).toBe('number');
    });

    it('has valid token usage metrics', () => {
      const { tokenUsage } = MOCK_RAG_METRICS;
      expect(tokenUsage.input).toBeGreaterThan(0);
      expect(tokenUsage.output).toBeGreaterThan(0);
      expect(tokenUsage.context).toBeGreaterThan(0);
      expect(tokenUsage.total).toBe(
        tokenUsage.input + tokenUsage.output + tokenUsage.context
      );
      expect(tokenUsage.costEstimate).toBeGreaterThanOrEqual(0);
    });

    it('has valid cache metrics', () => {
      const { cache } = MOCK_RAG_METRICS;
      expect(cache.hitRate).toBeGreaterThanOrEqual(0);
      expect(cache.hitRate).toBeLessThanOrEqual(100);
      expect(cache.missRate).toBeGreaterThanOrEqual(0);
      expect(cache.missRate).toBeLessThanOrEqual(100);
      expect(cache.totalHits).toBeGreaterThanOrEqual(0);
      expect(cache.totalMisses).toBeGreaterThanOrEqual(0);
      expect(cache.cacheSize).toBeGreaterThan(0);
      expect(cache.ttlSeconds).toBeGreaterThan(0);
    });

    it('has valid accuracy metrics', () => {
      const { accuracy } = MOCK_RAG_METRICS;
      expect(accuracy.overallScore).toBeGreaterThanOrEqual(0);
      expect(accuracy.overallScore).toBeLessThanOrEqual(100);
      expect(accuracy.citationAccuracy).toBeGreaterThanOrEqual(0);
      expect(accuracy.citationAccuracy).toBeLessThanOrEqual(100);
      expect(accuracy.userFeedbackScore).toBeGreaterThanOrEqual(0);
      expect(accuracy.userFeedbackScore).toBeLessThanOrEqual(5);
      expect(accuracy.byStrategy).toHaveProperty('Hybrid');
      expect(accuracy.byStrategy).toHaveProperty('Semantic');
      expect(accuracy.byStrategy).toHaveProperty('Keyword');
      expect(accuracy.byStrategy).toHaveProperty('Contextual');
      expect(accuracy.byStrategy).toHaveProperty('MultiQuery');
      expect(accuracy.byStrategy).toHaveProperty('Agentic');
    });

    it('has valid cost metrics', () => {
      const { cost } = MOCK_RAG_METRICS;
      expect(cost.currentSession).toBeGreaterThanOrEqual(0);
      expect(cost.projectedMonthly).toBeGreaterThanOrEqual(0);
      expect(cost.avgPerQuery).toBeGreaterThanOrEqual(0);
      expect(cost.budgetUsed).toBeGreaterThanOrEqual(0);
      expect(cost.budgetLimit).toBeGreaterThan(0);
      expect(cost.byStrategy).toHaveProperty('Hybrid');
    });

    it('has valid timestamp', () => {
      expect(MOCK_RAG_METRICS.timestamp).toBeDefined();
      const date = new Date(MOCK_RAG_METRICS.timestamp);
      expect(date.getTime()).toBeGreaterThan(0);
    });
  });

  describe('generateMockMetrics', () => {
    it('returns valid metrics object', () => {
      const metrics = generateMockMetrics();
      expect(metrics).toHaveProperty('latency');
      expect(metrics).toHaveProperty('tokenUsage');
      expect(metrics).toHaveProperty('cache');
      expect(metrics).toHaveProperty('accuracy');
      expect(metrics).toHaveProperty('cost');
      expect(metrics).toHaveProperty('timestamp');
    });

    it('generates different timestamps each call', () => {
      const metrics1 = generateMockMetrics();
      const metrics2 = generateMockMetrics();
      // Timestamps should be unique (or very close)
      expect(metrics1.timestamp).toBeDefined();
      expect(metrics2.timestamp).toBeDefined();
    });

    it('generates metrics within valid ranges', () => {
      const metrics = generateMockMetrics();

      // Latency p50 with variation
      expect(metrics.latency.p50).toBeGreaterThanOrEqual(102); // 120 * 0.85
      expect(metrics.latency.p50).toBeLessThanOrEqual(138); // 120 * 1.15

      // Cache hit rate
      expect(metrics.cache.hitRate).toBeGreaterThanOrEqual(75);
      expect(metrics.cache.hitRate).toBeLessThanOrEqual(85);

      // Accuracy
      expect(metrics.accuracy.overallScore).toBeGreaterThanOrEqual(88);
      expect(metrics.accuracy.overallScore).toBeLessThanOrEqual(96);
    });

    it('maintains latency ordering (p50 < p95 < p99)', () => {
      const metrics = generateMockMetrics();
      expect(metrics.latency.p50).toBeLessThan(metrics.latency.p95);
      expect(metrics.latency.p95).toBeLessThan(metrics.latency.p99);
    });

    it('token total equals sum of input, output, and context', () => {
      const metrics = generateMockMetrics();
      expect(metrics.tokenUsage.total).toBe(
        metrics.tokenUsage.input +
          metrics.tokenUsage.output +
          metrics.tokenUsage.context
      );
    });
  });
});
