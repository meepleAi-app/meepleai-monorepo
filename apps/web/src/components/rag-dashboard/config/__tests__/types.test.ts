/**
 * @fileoverview Tests for RAG Configuration types and constants
 * @description Validates type definitions, default values, and strategy presets
 */

import { describe, it, expect } from 'vitest';

import {
  DEFAULT_RAG_CONFIG,
  STRATEGY_PRESETS,
  LLM_MODELS,
  RERANKER_MODELS,
} from '../types';
import type {
  RagConfig,
  LlmModelId,
  RerankerModelId,
  RetrievalStrategyType,
} from '../types';

describe('types', () => {
  describe('DEFAULT_RAG_CONFIG', () => {
    it('should have valid generation parameters', () => {
      const { generation } = DEFAULT_RAG_CONFIG;

      expect(generation.temperature).toBeGreaterThanOrEqual(0);
      expect(generation.temperature).toBeLessThanOrEqual(2);
      expect(generation.topK).toBeGreaterThanOrEqual(1);
      expect(generation.topK).toBeLessThanOrEqual(100);
      expect(generation.topP).toBeGreaterThanOrEqual(0);
      expect(generation.topP).toBeLessThanOrEqual(1);
      expect(generation.maxTokens).toBeGreaterThanOrEqual(100);
      expect(generation.maxTokens).toBeLessThanOrEqual(4000);
    });

    it('should have valid retrieval parameters', () => {
      const { retrieval } = DEFAULT_RAG_CONFIG;

      expect(retrieval.chunkSize).toBeGreaterThanOrEqual(200);
      expect(retrieval.chunkSize).toBeLessThanOrEqual(2000);
      expect(retrieval.chunkOverlap).toBeGreaterThanOrEqual(0);
      expect(retrieval.chunkOverlap).toBeLessThanOrEqual(50);
      expect(retrieval.topResults).toBeGreaterThanOrEqual(1);
      expect(retrieval.topResults).toBeLessThanOrEqual(20);
      expect(retrieval.similarityThreshold).toBeGreaterThanOrEqual(0);
      expect(retrieval.similarityThreshold).toBeLessThanOrEqual(1);
    });

    it('should have valid reranker settings', () => {
      const { reranker } = DEFAULT_RAG_CONFIG;

      expect(typeof reranker.enabled).toBe('boolean');
      expect(RERANKER_MODELS.some((m) => m.id === reranker.model)).toBe(true);
      expect(reranker.topN).toBeGreaterThanOrEqual(3);
      expect(reranker.topN).toBeLessThanOrEqual(20);
    });

    it('should have valid model selection', () => {
      const { models } = DEFAULT_RAG_CONFIG;

      expect(LLM_MODELS.some((m) => m.id === models.primaryModel)).toBe(true);
      // fallbackModel can be null
      if (models.fallbackModel) {
        expect(LLM_MODELS.some((m) => m.id === models.fallbackModel)).toBe(true);
      }
      // evaluationModel can be null
      if (models.evaluationModel) {
        expect(LLM_MODELS.some((m) => m.id === models.evaluationModel)).toBe(true);
      }
    });

    it('should have valid strategy-specific settings', () => {
      const { strategySpecific } = DEFAULT_RAG_CONFIG;

      expect(strategySpecific.hybridAlpha).toBeGreaterThanOrEqual(0);
      expect(strategySpecific.hybridAlpha).toBeLessThanOrEqual(1);
      expect(strategySpecific.contextWindow).toBeGreaterThanOrEqual(1);
      expect(strategySpecific.contextWindow).toBeLessThanOrEqual(20);
      expect(strategySpecific.maxHops).toBeGreaterThanOrEqual(1);
      expect(strategySpecific.maxHops).toBeLessThanOrEqual(10);
    });

    it('should have valid active strategy', () => {
      const validStrategies: RetrievalStrategyType[] = [
        'Hybrid',
        'Semantic',
        'Keyword',
        'Contextual',
        'MultiQuery',
        'Agentic',
      ];
      expect(validStrategies).toContain(DEFAULT_RAG_CONFIG.activeStrategy);
    });
  });

  describe('STRATEGY_PRESETS', () => {
    const strategies: RetrievalStrategyType[] = [
      'Hybrid',
      'Semantic',
      'Keyword',
      'Contextual',
      'MultiQuery',
      'Agentic',
    ];

    it.each(strategies)('should have a preset for %s strategy', (strategy) => {
      expect(STRATEGY_PRESETS).toHaveProperty(strategy);
    });

    it('should have Hybrid preset with hybridAlpha setting', () => {
      const preset = STRATEGY_PRESETS.Hybrid;
      expect(preset.strategySpecific?.hybridAlpha).toBeDefined();
      expect(preset.strategySpecific?.hybridAlpha).toBe(0.5);
    });

    it('should have Semantic preset with high similarity threshold', () => {
      const preset = STRATEGY_PRESETS.Semantic;
      expect(preset.retrieval?.similarityThreshold).toBeGreaterThan(0.7);
    });

    it('should have Keyword preset with lower similarity threshold', () => {
      const preset = STRATEGY_PRESETS.Keyword;
      expect(preset.retrieval?.similarityThreshold).toBeLessThan(0.7);
    });

    it('should have Contextual preset with context window', () => {
      const preset = STRATEGY_PRESETS.Contextual;
      expect(preset.strategySpecific?.contextWindow).toBeDefined();
      expect(preset.strategySpecific?.contextWindow).toBeGreaterThan(0);
    });

    it('should have MultiQuery preset with maxHops', () => {
      const preset = STRATEGY_PRESETS.MultiQuery;
      expect(preset.strategySpecific?.maxHops).toBeDefined();
      expect(preset.strategySpecific?.maxHops).toBeGreaterThanOrEqual(2);
    });

    it('should have Agentic preset with maxHops and more results', () => {
      const preset = STRATEGY_PRESETS.Agentic;
      expect(preset.strategySpecific?.maxHops).toBeDefined();
      expect(preset.retrieval?.topResults).toBeGreaterThan(5);
    });
  });

  describe('LLM_MODELS', () => {
    it('should have at least 3 models', () => {
      expect(LLM_MODELS.length).toBeGreaterThanOrEqual(3);
    });

    it('should have models with required fields', () => {
      LLM_MODELS.forEach((model) => {
        expect(model.id).toBeDefined();
        expect(model.name).toBeDefined();
        expect(model.provider).toBeDefined();
      });
    });

    it('should have unique model IDs', () => {
      const ids = LLM_MODELS.map((m) => m.id);
      const uniqueIds = [...new Set(ids)];
      expect(uniqueIds.length).toBe(ids.length);
    });

    it('should include major providers', () => {
      const providers = [...new Set(LLM_MODELS.map((m) => m.provider))];
      expect(providers.length).toBeGreaterThanOrEqual(2);
    });
  });

  describe('RERANKER_MODELS', () => {
    it('should have at least 2 models', () => {
      expect(RERANKER_MODELS.length).toBeGreaterThanOrEqual(2);
    });

    it('should have models with required fields', () => {
      RERANKER_MODELS.forEach((model) => {
        expect(model.id).toBeDefined();
        expect(model.name).toBeDefined();
      });
    });

    it('should have unique model IDs', () => {
      const ids = RERANKER_MODELS.map((m) => m.id);
      const uniqueIds = [...new Set(ids)];
      expect(uniqueIds.length).toBe(ids.length);
    });
  });

  describe('Type compatibility', () => {
    it('should allow creating a valid RagConfig from defaults', () => {
      const config: RagConfig = {
        ...DEFAULT_RAG_CONFIG,
      };
      expect(config).toBeDefined();
    });

    it('should allow overriding specific settings', () => {
      const config: RagConfig = {
        ...DEFAULT_RAG_CONFIG,
        generation: {
          ...DEFAULT_RAG_CONFIG.generation,
          temperature: 0.5,
        },
      };
      expect(config.generation.temperature).toBe(0.5);
    });

    it('should allow merging with strategy preset', () => {
      const preset = STRATEGY_PRESETS.Hybrid;
      const config: RagConfig = {
        ...DEFAULT_RAG_CONFIG,
        ...preset,
        retrieval: {
          ...DEFAULT_RAG_CONFIG.retrieval,
          ...preset.retrieval,
        },
        strategySpecific: {
          ...DEFAULT_RAG_CONFIG.strategySpecific,
          ...preset.strategySpecific,
        },
        activeStrategy: 'Hybrid',
      };
      expect(config.activeStrategy).toBe('Hybrid');
      expect(config.strategySpecific.hybridAlpha).toBe(0.5);
    });
  });
});
