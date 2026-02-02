/**
 * Tests for RAG Dashboard types and constants
 * Issue #3005: Frontend coverage improvements
 */

import { describe, it, expect } from 'vitest';

import {
  STRATEGY_CONFIGS,
  RAG_LAYERS,
  MODELS,
  DEFAULT_STATS,
  hasRagAccess,
  type RagStrategy,
  type ViewMode,
  type UserTier,
  type DashboardStats,
  type RagLayer,
  type StrategyConfig,
  type ModelConfig,
} from '../types';

describe('RAG Dashboard Types', () => {
  // =========================================================================
  // STRATEGY_CONFIGS Tests
  // =========================================================================

  describe('STRATEGY_CONFIGS', () => {
    it('should have all required strategies defined', () => {
      const expectedStrategies: RagStrategy[] = [
        'FAST',
        'BALANCED',
        'PRECISE',
        'EXPERT',
        'CONSENSUS',
        'CUSTOM',
      ];

      expectedStrategies.forEach((strategy) => {
        expect(STRATEGY_CONFIGS[strategy]).toBeDefined();
      });
    });

    it('should have name and description for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.name).toBeDefined();
        expect(typeof config.name).toBe('string');
        expect(config.description).toBeDefined();
        expect(typeof config.description).toBe('string');
      });
    });

    it('should have valid estimatedTokens for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.estimatedTokens).toBeDefined();
        expect(typeof config.estimatedTokens).toBe('number');
        expect(config.estimatedTokens).toBeGreaterThan(0);
      });
    });

    it('should have FAST strategy with lowest tokens', () => {
      expect(STRATEGY_CONFIGS.FAST.estimatedTokens).toBeLessThan(STRATEGY_CONFIGS.BALANCED.estimatedTokens);
      expect(STRATEGY_CONFIGS.FAST.estimatedTokens).toBeLessThan(STRATEGY_CONFIGS.PRECISE.estimatedTokens);
    });

    it('should have PRECISE strategy with highest tokens', () => {
      expect(STRATEGY_CONFIGS.PRECISE.estimatedTokens).toBeGreaterThan(STRATEGY_CONFIGS.FAST.estimatedTokens);
      expect(STRATEGY_CONFIGS.PRECISE.estimatedTokens).toBeGreaterThan(STRATEGY_CONFIGS.BALANCED.estimatedTokens);
    });

    it('should have displayName for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.displayName).toBeDefined();
        expect(typeof config.displayName).toBe('string');
      });
    });

    it('should have requiredPhases array for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.requiredPhases).toBeDefined();
        expect(Array.isArray(config.requiredPhases)).toBe(true);
        expect(config.requiredPhases.length).toBeGreaterThan(0);
      });
    });

    it('should have estimatedCost for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.estimatedCost).toBeDefined();
        expect(typeof config.estimatedCost).toBe('number');
        expect(config.estimatedCost).toBeGreaterThanOrEqual(0);
      });
    });

    it('should have latencyMs for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.latencyMs).toBeDefined();
        expect(typeof config.latencyMs).toBe('string');
      });
    });

    it('should have accuracyRange for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.accuracyRange).toBeDefined();
        expect(typeof config.accuracyRange).toBe('string');
      });
    });

    it('should have useCase for each strategy', () => {
      Object.values(STRATEGY_CONFIGS).forEach((config) => {
        expect(config.useCase).toBeDefined();
        expect(typeof config.useCase).toBe('string');
      });
    });
  });

  // =========================================================================
  // RAG_LAYERS Tests
  // =========================================================================

  describe('RAG_LAYERS', () => {
    it('should have all 6 layers defined', () => {
      expect(RAG_LAYERS).toHaveLength(6);
    });

    it('should have required properties for each layer', () => {
      RAG_LAYERS.forEach((layer) => {
        expect(layer.id).toBeDefined();
        expect(layer.name).toBeDefined();
        expect(layer.shortName).toBeDefined();
        expect(layer.description).toBeDefined();
        expect(layer.icon).toBeDefined();
        expect(layer.color).toBeDefined();
      });
    });

    it('should have tokenRange with min and max for each layer', () => {
      RAG_LAYERS.forEach((layer) => {
        expect(layer.tokenRange).toBeDefined();
        expect(layer.tokenRange.min).toBeDefined();
        expect(layer.tokenRange.max).toBeDefined();
        expect(layer.tokenRange.min).toBeLessThanOrEqual(layer.tokenRange.max);
      });
    });

    it('should have unique layer IDs', () => {
      const ids = RAG_LAYERS.map((layer) => layer.id);
      const uniqueIds = new Set(ids);
      expect(uniqueIds.size).toBe(RAG_LAYERS.length);
    });

    it('should include Intelligent Routing as first layer', () => {
      expect(RAG_LAYERS[0].name).toBe('Intelligent Routing');
      expect(RAG_LAYERS[0].shortName).toBe('L1');
    });

    it('should have correct layer order (L1-L6)', () => {
      const expectedShortNames = ['L1', 'L2', 'L3', 'L4', 'L5', 'L6'];
      RAG_LAYERS.forEach((layer, index) => {
        expect(layer.shortName).toBe(expectedShortNames[index]);
      });
    });

    it('should have valid HSL color strings', () => {
      RAG_LAYERS.forEach((layer) => {
        expect(layer.color).toMatch(/^hsl\(\d+\s+\d+%\s+\d+%\)$/);
      });
    });
  });

  // =========================================================================
  // MODELS Tests
  // =========================================================================

  describe('MODELS', () => {
    it('should have models for all strategies', () => {
      const strategies: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

      strategies.forEach((strategy) => {
        expect(MODELS[strategy]).toBeDefined();
        expect(Array.isArray(MODELS[strategy])).toBe(true);
        expect(MODELS[strategy].length).toBeGreaterThan(0);
      });
    });

    it('should have valid pricing for each model', () => {
      Object.values(MODELS).forEach((modelList) => {
        modelList.forEach((model) => {
          expect(model.inputCostPerMillion).toBeDefined();
          expect(typeof model.inputCostPerMillion).toBe('number');
          expect(model.inputCostPerMillion).toBeGreaterThanOrEqual(0);

          expect(model.outputCostPerMillion).toBeDefined();
          expect(typeof model.outputCostPerMillion).toBe('number');
          expect(model.outputCostPerMillion).toBeGreaterThanOrEqual(0);
        });
      });
    });

    it('should have maxTokens for each model', () => {
      Object.values(MODELS).forEach((modelList) => {
        modelList.forEach((model) => {
          expect(model.maxTokens).toBeDefined();
          expect(typeof model.maxTokens).toBe('number');
          expect(model.maxTokens).toBeGreaterThan(0);
        });
      });
    });

    it('should have id and name for each model', () => {
      Object.values(MODELS).forEach((modelList) => {
        modelList.forEach((model) => {
          expect(model.id).toBeDefined();
          expect(typeof model.id).toBe('string');
          expect(model.name).toBeDefined();
          expect(typeof model.name).toBe('string');
        });
      });
    });

    it('should have provider for each model', () => {
      Object.values(MODELS).forEach((modelList) => {
        modelList.forEach((model) => {
          expect(model.provider).toBeDefined();
          expect(typeof model.provider).toBe('string');
        });
      });
    });

    it('should have FAST strategy with free models', () => {
      const fastModels = MODELS.FAST;
      fastModels.forEach((model) => {
        expect(model.inputCostPerMillion).toBe(0);
        expect(model.outputCostPerMillion).toBe(0);
      });
    });
  });

  // =========================================================================
  // DEFAULT_STATS Tests
  // =========================================================================

  describe('DEFAULT_STATS', () => {
    it('should have all required properties', () => {
      expect(DEFAULT_STATS.ragVariants).toBeDefined();
      expect(DEFAULT_STATS.avgTokensPerQuery).toBeDefined();
      expect(DEFAULT_STATS.tokenReduction).toBeDefined();
      expect(DEFAULT_STATS.targetAccuracy).toBeDefined();
      expect(DEFAULT_STATS.monthlyCost).toBeDefined();
      expect(DEFAULT_STATS.cacheHitRate).toBeDefined();
    });

    it('should have reasonable default values', () => {
      expect(DEFAULT_STATS.ragVariants).toBe(31); // Updated: removed 5 Anonymous variants
      expect(DEFAULT_STATS.avgTokensPerQuery).toBe(1310);
      expect(DEFAULT_STATS.tokenReduction).toBe(-35);
      expect(DEFAULT_STATS.targetAccuracy).toBe(95);
      expect(DEFAULT_STATS.monthlyCost).toBe(2053); // Updated with 2026 pricing
      expect(DEFAULT_STATS.cacheHitRate).toBe(80);
    });
  });

  // =========================================================================
  // Type Validation Tests
  // =========================================================================

  describe('Type Exports', () => {
    it('should export RagStrategy type with valid values', () => {
      const validStrategies: RagStrategy[] = [
        'FAST',
        'BALANCED',
        'PRECISE',
        'EXPERT',
        'CONSENSUS',
        'CUSTOM',
      ];

      validStrategies.forEach((strategy) => {
        expect(typeof strategy).toBe('string');
      });
    });

    it('should export ViewMode type with valid values', () => {
      const validModes: ViewMode[] = ['technical', 'business'];

      validModes.forEach((mode) => {
        expect(typeof mode).toBe('string');
      });
    });

    it('should export UserTier type with valid values', () => {
      const validTiers: UserTier[] = [
        'Anonymous',
        'User',
        'Editor',
        'Admin',
        'Premium',
      ];

      validTiers.forEach((tier) => {
        expect(typeof tier).toBe('string');
      });
    });

    it('should validate DashboardStats structure', () => {
      const mockStats: DashboardStats = {
        ragVariants: 6,
        avgTokensPerQuery: 3500,
        tokenReduction: 60,
        targetAccuracy: 95,
        monthlyCost: 150,
        cacheHitRate: 80,
      };

      expect(mockStats.ragVariants).toBe(6);
      expect(mockStats.avgTokensPerQuery).toBe(3500);
      expect(mockStats.tokenReduction).toBe(60);
      expect(mockStats.targetAccuracy).toBe(95);
      expect(mockStats.monthlyCost).toBe(150);
      expect(mockStats.cacheHitRate).toBe(80);
    });

    it('should validate RagLayer structure', () => {
      const mockLayer: RagLayer = {
        id: 'test-layer',
        name: 'Test Layer',
        shortName: 'T1',
        description: 'A test layer description',
        icon: '🧪',
        tokenRange: { min: 100, max: 500 },
        color: 'hsl(200 80% 50%)',
      };

      expect(mockLayer.id).toBe('test-layer');
      expect(mockLayer.name).toBe('Test Layer');
      expect(mockLayer.shortName).toBe('T1');
      expect(mockLayer.description).toBe('A test layer description');
      expect(mockLayer.icon).toBe('🧪');
      expect(mockLayer.tokenRange.min).toBe(100);
      expect(mockLayer.tokenRange.max).toBe(500);
      expect(mockLayer.color).toBe('hsl(200 80% 50%)');
    });

    it('should validate StrategyConfig structure', () => {
      const mockConfig: StrategyConfig = {
        name: 'FAST',
        displayName: 'FAST',
        description: 'Fast strategy',
        requiredPhases: ['synthesis'],
        estimatedTokens: 2000,
        estimatedCost: 0.01,
        latencyMs: '<200ms',
        accuracyRange: '80-90%',
        usagePercent: '50%',
        useCase: 'Simple queries',
      };

      expect(mockConfig.name).toBe('FAST');
      expect(mockConfig.requiredPhases).toContain('synthesis');
      expect(mockConfig.estimatedTokens).toBe(2000);
    });

    it('should validate ModelConfig structure', () => {
      const mockModel: ModelConfig = {
        id: 'test-model',
        name: 'Test Model',
        provider: 'TestProvider',
        inputCostPerMillion: 1.0,
        outputCostPerMillion: 2.0,
        maxTokens: 4096,
      };

      expect(mockModel.id).toBe('test-model');
      expect(mockModel.name).toBe('Test Model');
      expect(mockModel.provider).toBe('TestProvider');
      expect(mockModel.inputCostPerMillion).toBe(1.0);
      expect(mockModel.outputCostPerMillion).toBe(2.0);
      expect(mockModel.maxTokens).toBe(4096);
    });
  });

  // =========================================================================
  // hasRagAccess Function Tests
  // =========================================================================

  describe('hasRagAccess', () => {
    it('should return false for Anonymous tier', () => {
      expect(hasRagAccess('Anonymous')).toBe(false);
    });

    it('should return true for User tier', () => {
      expect(hasRagAccess('User')).toBe(true);
    });

    it('should return true for Editor tier', () => {
      expect(hasRagAccess('Editor')).toBe(true);
    });

    it('should return true for Admin tier', () => {
      expect(hasRagAccess('Admin')).toBe(true);
    });

    it('should return true for Premium tier', () => {
      expect(hasRagAccess('Premium')).toBe(true);
    });

    it('should return true for all authenticated tiers', () => {
      const authenticatedTiers: UserTier[] = ['User', 'Editor', 'Admin', 'Premium'];

      authenticatedTiers.forEach(tier => {
        expect(hasRagAccess(tier)).toBe(true);
      });
    });
  });
});
