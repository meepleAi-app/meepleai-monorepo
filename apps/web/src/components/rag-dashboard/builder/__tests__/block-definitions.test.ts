/**
 * Tests for RAG Block Definitions
 *
 * Validates all 23 blocks are properly defined with correct metadata.
 */

import { describe, it, expect } from 'vitest';
import {
  ALL_BLOCKS,
  BLOCKS_BY_TYPE,
  BLOCKS_BY_CATEGORY,
  getBlock,
  getBlocksByCategory,
  isValidConnection,
  VECTOR_SEARCH,
  HYBRID_SEARCH,
  CROSS_ENCODER_RERANKING,
  CRAG_EVALUATOR,
  SEQUENTIAL_AGENT,
} from '../block-definitions';
import type { RagBlockType, BlockCategory } from '../types';

describe('Block Definitions', () => {
  describe('ALL_BLOCKS', () => {
    it('should contain exactly 23 blocks', () => {
      expect(ALL_BLOCKS).toHaveLength(23);
    });

    it('should have unique IDs for all blocks', () => {
      const ids = ALL_BLOCKS.map((b) => b.id);
      const uniqueIds = new Set(ids);
      expect(uniqueIds.size).toBe(23);
    });

    it('should have valid categories for all blocks', () => {
      const validCategories: BlockCategory[] = [
        'retrieval',
        'optimization',
        'ranking',
        'validation',
        'agents',
        'control',
      ];

      ALL_BLOCKS.forEach((block) => {
        expect(validCategories).toContain(block.category);
      });
    });

    it('should have required fields for all blocks', () => {
      ALL_BLOCKS.forEach((block) => {
        expect(block.id).toBeDefined();
        expect(block.type).toBeDefined();
        expect(block.name).toBeDefined();
        expect(block.category).toBeDefined();
        expect(block.icon).toBeDefined();
        expect(block.color).toBeDefined();
        expect(block.parameters).toBeDefined();
        expect(block.inputs).toBeDefined();
        expect(block.outputs).toBeDefined();
        expect(block.estimatedTokens).toBeGreaterThanOrEqual(0);
        expect(block.estimatedLatencyMs).toBeGreaterThanOrEqual(0);
        expect(block.estimatedCost).toBeGreaterThanOrEqual(0);
        expect(block.requiredTier).toBeDefined();
        expect(block.maxInstances).toBeGreaterThan(0);
        expect(block.description).toBeDefined();
        expect(block.useCases).toBeDefined();
      });
    });
  });

  describe('BLOCKS_BY_TYPE', () => {
    it('should map all block types correctly', () => {
      const blockTypes: RagBlockType[] = [
        'vector-search',
        'keyword-search',
        'hybrid-search',
        'multi-hop-retrieval',
        'graph-rag',
        'web-search',
        'sentence-window',
        'cross-encoder-reranking',
        'metadata-filtering',
        'deduplication',
        'document-repacking',
        'crag-evaluator',
        'self-rag',
        'confidence-scoring',
        'citation-verification',
        'hallucination-detection',
        'sequential-agent',
        'parallel-agent',
        'supervisor-worker',
        'query-rewriting',
        'query-decomposition',
        'hyde',
        'rag-fusion',
      ];

      blockTypes.forEach((type) => {
        expect(BLOCKS_BY_TYPE[type]).toBeDefined();
        expect(BLOCKS_BY_TYPE[type].type).toBe(type);
      });
    });
  });

  describe('BLOCKS_BY_CATEGORY', () => {
    it('should have correct counts per category', () => {
      expect(BLOCKS_BY_CATEGORY.retrieval).toHaveLength(7);
      expect(BLOCKS_BY_CATEGORY.ranking).toHaveLength(4);
      expect(BLOCKS_BY_CATEGORY.validation).toHaveLength(5);
      expect(BLOCKS_BY_CATEGORY.agents).toHaveLength(3);
      expect(BLOCKS_BY_CATEGORY.optimization).toHaveLength(4);
      expect(BLOCKS_BY_CATEGORY.control).toHaveLength(0); // Control uses ReactFlow built-ins
    });

    it('should sum to total blocks', () => {
      const total = Object.values(BLOCKS_BY_CATEGORY).reduce(
        (sum, blocks) => sum + blocks.length,
        0
      );
      expect(total).toBe(23);
    });
  });

  describe('getBlock', () => {
    it('should return block for valid type', () => {
      const block = getBlock('vector-search');
      expect(block).toBeDefined();
      expect(block?.name).toBe('Vector Search');
    });

    it('should return undefined for invalid type', () => {
      const block = getBlock('invalid-type' as RagBlockType);
      expect(block).toBeUndefined();
    });
  });

  describe('getBlocksByCategory', () => {
    it('should return blocks for valid category', () => {
      const retrievalBlocks = getBlocksByCategory('retrieval');
      expect(retrievalBlocks).toHaveLength(7);
    });

    it('should return empty array for invalid category', () => {
      const blocks = getBlocksByCategory('invalid' as BlockCategory);
      expect(blocks).toHaveLength(0);
    });
  });

  describe('isValidConnection', () => {
    it('should allow vector-search to connect to cross-encoder-reranking', () => {
      expect(isValidConnection('vector-search', 'cross-encoder-reranking')).toBe(true);
    });

    it('should allow hybrid-search to connect to crag-evaluator', () => {
      expect(isValidConnection('hybrid-search', 'crag-evaluator')).toBe(true);
    });

    it('should allow cross-encoder-reranking to connect to sequential-agent', () => {
      expect(isValidConnection('cross-encoder-reranking', 'sequential-agent')).toBe(true);
    });

    it('should not allow hallucination-detection to connect to anything', () => {
      expect(isValidConnection('hallucination-detection', 'vector-search')).toBe(false);
      expect(isValidConnection('hallucination-detection', 'sequential-agent')).toBe(false);
    });

    it('should not allow invalid connections', () => {
      expect(isValidConnection('sequential-agent', 'vector-search')).toBe(false);
    });
  });

  describe('Individual Block Validation', () => {
    it('VECTOR_SEARCH should have correct configuration', () => {
      expect(VECTOR_SEARCH.id).toBe('vector-search');
      expect(VECTOR_SEARCH.category).toBe('retrieval');
      expect(VECTOR_SEARCH.inputs).toHaveLength(1);
      expect(VECTOR_SEARCH.outputs).toHaveLength(1);
      expect(VECTOR_SEARCH.requiredTier).toBe('User');
    });

    it('HYBRID_SEARCH should have correct configuration', () => {
      expect(HYBRID_SEARCH.id).toBe('hybrid-search');
      expect(HYBRID_SEARCH.category).toBe('retrieval');
      expect(HYBRID_SEARCH.parameters.length).toBeGreaterThan(0);
      expect(HYBRID_SEARCH.accuracyImpact).toBe(0.7);
    });

    it('CROSS_ENCODER_RERANKING should have correct configuration', () => {
      expect(CROSS_ENCODER_RERANKING.id).toBe('cross-encoder-reranking');
      expect(CROSS_ENCODER_RERANKING.category).toBe('ranking');
      expect(CROSS_ENCODER_RERANKING.inputs).toHaveLength(2); // query + docs
      expect(CROSS_ENCODER_RERANKING.outputs).toHaveLength(1);
    });

    it('CRAG_EVALUATOR should have correct configuration', () => {
      expect(CRAG_EVALUATOR.id).toBe('crag-evaluator');
      expect(CRAG_EVALUATOR.category).toBe('validation');
      expect(CRAG_EVALUATOR.outputs).toHaveLength(2); // docs + eval
    });

    it('SEQUENTIAL_AGENT should have correct configuration', () => {
      expect(SEQUENTIAL_AGENT.id).toBe('sequential-agent');
      expect(SEQUENTIAL_AGENT.category).toBe('agents');
      expect(SEQUENTIAL_AGENT.requiredTier).toBe('Editor');
    });
  });
});
