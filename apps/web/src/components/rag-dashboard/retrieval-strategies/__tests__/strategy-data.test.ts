import { describe, it, expect } from 'vitest';

import {
  RETRIEVAL_STRATEGIES,
  RETRIEVAL_STRATEGY_ORDER,
  getAllRetrievalStrategies,
  getRetrievalStrategy,
} from '../strategy-data';
import type { RetrievalStrategyType } from '../types';

describe('strategy-data', () => {
  describe('RETRIEVAL_STRATEGIES', () => {
    it('contains exactly 6 strategies', () => {
      expect(Object.keys(RETRIEVAL_STRATEGIES)).toHaveLength(6);
    });

    it('contains all expected strategy types', () => {
      const expectedStrategies: RetrievalStrategyType[] = [
        'Hybrid',
        'Semantic',
        'Keyword',
        'Contextual',
        'MultiQuery',
        'Agentic',
      ];

      expectedStrategies.forEach((strategyId) => {
        expect(RETRIEVAL_STRATEGIES[strategyId]).toBeDefined();
      });
    });

    describe('strategy structure', () => {
      it.each(Object.entries(RETRIEVAL_STRATEGIES))(
        '%s has all required fields',
        (_, strategy) => {
          expect(strategy).toHaveProperty('id');
          expect(strategy).toHaveProperty('name');
          expect(strategy).toHaveProperty('shortDescription');
          expect(strategy).toHaveProperty('icon');
          expect(strategy).toHaveProperty('color');
          expect(strategy).toHaveProperty('colorHsl');
          expect(strategy).toHaveProperty('glowColor');
          expect(strategy).toHaveProperty('metrics');
          expect(strategy).toHaveProperty('tags');
        }
      );

      it.each(Object.entries(RETRIEVAL_STRATEGIES))(
        '%s has valid metrics structure',
        (_, strategy) => {
          const { metrics } = strategy;

          expect(metrics).toHaveProperty('latency');
          expect(metrics).toHaveProperty('accuracy');
          expect(metrics).toHaveProperty('costTier');
          expect(metrics).toHaveProperty('latencyMs');
          expect(metrics).toHaveProperty('accuracyPercent');
          expect(metrics).toHaveProperty('costPerQuery');

          // Validate metric tiers
          expect(['low', 'medium', 'high', 'variable']).toContain(metrics.latency);
          expect(['low', 'medium', 'high', 'variable']).toContain(metrics.accuracy);
          expect(['low', 'medium', 'high', 'variable']).toContain(metrics.costTier);

          // Validate metric ranges
          expect(metrics.latencyMs.min).toBeLessThan(metrics.latencyMs.max);
          expect(metrics.accuracyPercent.min).toBeLessThan(metrics.accuracyPercent.max);
          expect(metrics.costPerQuery.min).toBeLessThan(metrics.costPerQuery.max);
        }
      );
    });

    describe('strategy-specific validation', () => {
      it('Hybrid strategy is marked as recommended', () => {
        expect(RETRIEVAL_STRATEGIES.Hybrid.tags).toContain('recommended');
      });

      it('Agentic strategy has variable latency', () => {
        expect(RETRIEVAL_STRATEGIES.Agentic.metrics.latency).toBe('variable');
      });

      it('Keyword strategy has low latency', () => {
        expect(RETRIEVAL_STRATEGIES.Keyword.metrics.latency).toBe('low');
      });

      it('each strategy has a unique icon', () => {
        const icons = Object.values(RETRIEVAL_STRATEGIES).map((s) => s.icon);
        const uniqueIcons = new Set(icons);
        expect(uniqueIcons.size).toBe(icons.length);
      });

      it('each strategy has a unique color', () => {
        const colors = Object.values(RETRIEVAL_STRATEGIES).map((s) => s.color);
        const uniqueColors = new Set(colors);
        expect(uniqueColors.size).toBe(colors.length);
      });
    });
  });

  describe('RETRIEVAL_STRATEGY_ORDER', () => {
    it('contains exactly 6 strategies', () => {
      expect(RETRIEVAL_STRATEGY_ORDER).toHaveLength(6);
    });

    it('starts with Hybrid (recommended)', () => {
      expect(RETRIEVAL_STRATEGY_ORDER[0]).toBe('Hybrid');
    });

    it('contains all strategy types', () => {
      const allStrategies = Object.keys(RETRIEVAL_STRATEGIES) as RetrievalStrategyType[];
      allStrategies.forEach((strategy) => {
        expect(RETRIEVAL_STRATEGY_ORDER).toContain(strategy);
      });
    });

    it('has no duplicates', () => {
      const unique = new Set(RETRIEVAL_STRATEGY_ORDER);
      expect(unique.size).toBe(RETRIEVAL_STRATEGY_ORDER.length);
    });
  });

  describe('getAllRetrievalStrategies', () => {
    it('returns an array of all strategies', () => {
      const strategies = getAllRetrievalStrategies();
      expect(Array.isArray(strategies)).toBe(true);
      expect(strategies).toHaveLength(6);
    });

    it('returns strategies with correct structure', () => {
      const strategies = getAllRetrievalStrategies();
      strategies.forEach((strategy) => {
        expect(strategy).toHaveProperty('id');
        expect(strategy).toHaveProperty('name');
        expect(strategy).toHaveProperty('metrics');
      });
    });
  });

  describe('getRetrievalStrategy', () => {
    it('returns correct strategy for valid ID', () => {
      const strategy = getRetrievalStrategy('Hybrid');
      expect(strategy).toBeDefined();
      expect(strategy?.id).toBe('Hybrid');
      expect(strategy?.name).toBe('Hybrid Search');
    });

    it('returns undefined for invalid ID', () => {
      // @ts-expect-error Testing invalid input
      const strategy = getRetrievalStrategy('Invalid');
      expect(strategy).toBeUndefined();
    });

    it.each(['Hybrid', 'Semantic', 'Keyword', 'Contextual', 'MultiQuery', 'Agentic'] as const)(
      'returns correct strategy for %s',
      (strategyId) => {
        const strategy = getRetrievalStrategy(strategyId);
        expect(strategy).toBeDefined();
        expect(strategy?.id).toBe(strategyId);
      }
    );
  });
});
