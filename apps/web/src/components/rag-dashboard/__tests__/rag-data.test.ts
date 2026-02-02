/**
 * Tests for rag-data utility functions
 * Issue #3005: Frontend coverage improvements - Function coverage
 */

import { describe, it, expect } from 'vitest';
import {
  calculateQueryCost,
  getStrategy,
  getLayer,
  formatLatency,
  calculateMonthlyCost,
} from '../rag-data';

describe('rag-data utilities', () => {
  // =========================================================================
  // calculateQueryCost Tests
  // =========================================================================

  describe('calculateQueryCost', () => {
    it('should calculate cost for Claude Sonnet 4.5', () => {
      const cost = calculateQueryCost(1000, 'claude-sonnet-4.5', 0.7);

      // 1000 tokens = 0.001M tokens
      // Input: 700 tokens * $3.00/1M = $0.0021
      // Output: 300 tokens * $15.00/1M = $0.0045
      // Total: $0.0066
      expect(cost).toBeCloseTo(0.0066, 4);
    });

    it('should calculate cost for free models', () => {
      const cost = calculateQueryCost(1000, 'llama-3.3-70b', 0.7);

      // Free model should have 0 cost
      expect(cost).toBe(0);
    });

    it('should handle different input ratios', () => {
      const cost50 = calculateQueryCost(1000, 'claude-sonnet-4.5', 0.5);
      const cost80 = calculateQueryCost(1000, 'claude-sonnet-4.5', 0.8);

      // Higher input ratio = LOWER cost (input $3/1M vs output $15/1M)
      // 50% input = more expensive output tokens
      // 80% input = cheaper input tokens
      expect(cost50).toBeGreaterThan(cost80);
    });

    it('should return 0 for unknown models', () => {
      const cost = calculateQueryCost(1000, 'unknown-model-xyz', 0.7);
      expect(cost).toBe(0);
    });
  });

  // =========================================================================
  // getStrategy Tests
  // =========================================================================

  describe('getStrategy', () => {
    it('should return FAST strategy', () => {
      const strategy = getStrategy('FAST');

      expect(strategy).toBeDefined();
      expect(strategy?.id).toBe('FAST');
      expect(strategy?.name).toBe('FAST');
      expect(strategy?.tokens).toBe(2060);
    });

    it('should return BALANCED strategy', () => {
      const strategy = getStrategy('BALANCED');

      expect(strategy).toBeDefined();
      expect(strategy?.id).toBe('BALANCED');
      expect(strategy?.cost).toBe(0.01);
    });

    it('should return PRECISE strategy', () => {
      const strategy = getStrategy('PRECISE');

      expect(strategy).toBeDefined();
      expect(strategy?.tokens).toBe(22396);
    });

    it('should return undefined for unknown strategy', () => {
      const strategy = getStrategy('UNKNOWN');
      expect(strategy).toBeUndefined();
    });

    it('should return all 6 strategies', () => {
      const fast = getStrategy('FAST');
      const balanced = getStrategy('BALANCED');
      const precise = getStrategy('PRECISE');
      const expert = getStrategy('EXPERT');
      const consensus = getStrategy('CONSENSUS');
      const custom = getStrategy('CUSTOM');

      expect(fast).toBeDefined();
      expect(balanced).toBeDefined();
      expect(precise).toBeDefined();
      expect(expert).toBeDefined();
      expect(consensus).toBeDefined();
      expect(custom).toBeDefined();
    });
  });

  // =========================================================================
  // getLayer Tests
  // =========================================================================

  describe('getLayer', () => {
    it('should return routing layer', () => {
      const layer = getLayer('routing');

      expect(layer).toBeDefined();
      expect(layer?.id).toBe('routing');
      expect(layer?.name).toBe('Intelligent Routing');
      expect(layer?.shortName).toBe('L1');
    });

    it('should return cache layer', () => {
      const layer = getLayer('cache');

      expect(layer).toBeDefined();
      expect(layer?.id).toBe('cache');
      expect(layer?.shortName).toBe('L2');
    });

    it('should return validation layer', () => {
      const layer = getLayer('validation');

      expect(layer).toBeDefined();
      expect(layer?.id).toBe('validation');
      expect(layer?.shortName).toBe('L6');
    });

    it('should return undefined for unknown layer', () => {
      const layer = getLayer('unknown-layer');
      expect(layer).toBeUndefined();
    });

    it('should return all 6 layers', () => {
      const routing = getLayer('routing');
      const cache = getLayer('cache');
      const retrieval = getLayer('retrieval');
      const crag = getLayer('crag');
      const generation = getLayer('generation');
      const validation = getLayer('validation');

      expect(routing).toBeDefined();
      expect(cache).toBeDefined();
      expect(retrieval).toBeDefined();
      expect(crag).toBeDefined();
      expect(generation).toBeDefined();
      expect(validation).toBeDefined();
    });
  });

  // =========================================================================
  // formatLatency Tests
  // =========================================================================

  describe('formatLatency', () => {
    it('should format milliseconds for values < 1000ms', () => {
      expect(formatLatency(50, 200)).toBe('50-200ms');
      expect(formatLatency(100, 500)).toBe('100-500ms');
      expect(formatLatency(0, 999)).toBe('0-999ms');
    });

    it('should format seconds for values >= 1000ms', () => {
      expect(formatLatency(1000, 2000)).toBe('1-2s');
      expect(formatLatency(5000, 10000)).toBe('5-10s');
      expect(formatLatency(8000, 15000)).toBe('8-15s');
    });

    it('should handle edge case at 1000ms boundary', () => {
      expect(formatLatency(500, 1000)).toBe('1-1s');
      expect(formatLatency(999, 1000)).toBe('1-1s');
    });

    it('should round seconds correctly', () => {
      expect(formatLatency(1500, 2500)).toBe('2-3s'); // 1.5s -> 2s, 2.5s -> 3s
      expect(formatLatency(7800, 12300)).toBe('8-12s');
    });
  });

  // =========================================================================
  // calculateMonthlyCost Tests
  // =========================================================================

  describe('calculateMonthlyCost', () => {
    it('should calculate cost with default distribution and cache', () => {
      const cost = calculateMonthlyCost(10000);

      // With 80% cache hit rate (default):
      // Cache miss (20% of queries) = full cost
      // Cache hit (80% of queries) = $0.0001 minimal lookup
      // Default distribution: FAST 60%, BALANCED 25%, PRECISE 10%, EXPERT 3%, CONSENSUS 2%
      expect(cost).toBeGreaterThan(40);
      expect(cost).toBeLessThan(60);
    });

    it('should calculate cost with custom distribution', () => {
      const distribution = {
        FAST: 0.8,
        BALANCED: 0.15,
        PRECISE: 0.05,
        EXPERT: 0,
        CONSENSUS: 0,
        CUSTOM: 0,
      };

      const cost = calculateMonthlyCost(10000, distribution, 0.8);

      // With cache, cost is much lower than without
      expect(cost).toBeGreaterThan(10);
      expect(cost).toBeLessThan(30);
    });

    it('should handle 100% FAST queries with cache', () => {
      const distribution = {
        FAST: 1.0,
        BALANCED: 0,
        PRECISE: 0,
        EXPERT: 0,
        CONSENSUS: 0,
        CUSTOM: 0,
      };

      const cost = calculateMonthlyCost(10000, distribution, 0.8);

      // FAST cost = $0.0001 per query
      // Cache miss (20%): 2000 * 0.0001 = $0.20
      // Cache hit (80%): 8000 * 0.0001 = $0.80
      // Total = $1.00
      expect(cost).toBeCloseTo(1.0, 1);
    });

    it('should handle 100% CONSENSUS queries with cache', () => {
      const distribution = {
        FAST: 0,
        BALANCED: 0,
        PRECISE: 0,
        EXPERT: 0,
        CONSENSUS: 1.0,
        CUSTOM: 0,
      };

      const cost = calculateMonthlyCost(10000, distribution, 0.8);

      // CONSENSUS cost = $0.09 per query
      // Cache miss (20%): 2000 * 0.09 = $180
      // Cache hit (80%): 8000 * 0.0001 = $0.80
      // Total ≈ $180.80
      expect(cost).toBeGreaterThan(180);
      expect(cost).toBeLessThan(182);
    });

    it('should handle 0% cache hit rate (no caching)', () => {
      const cost = calculateMonthlyCost(10000, undefined, 0);

      // Without cache, costs are much higher
      expect(cost).toBeGreaterThan(200);
      expect(cost).toBeLessThan(300);
    });

    it('should scale linearly with query volume', () => {
      const cost1000 = calculateMonthlyCost(1000);
      const cost10000 = calculateMonthlyCost(10000);

      // 10x queries should be ~10x cost
      expect(cost10000 / cost1000).toBeCloseTo(10, 0);
    });
  });
});
