/**
 * Tests for RAG Block Metadata
 *
 * Validates metadata, statistics, and helper functions.
 */

import { describe, it, expect } from 'vitest';
import {
  CATEGORY_META,
  TIER_HIERARCHY,
  BLOCKS_BY_TIER,
  PIPELINE_CONSTRAINTS,
  RECOMMENDED_COMBINATIONS,
  BLOCK_SUMMARY,
  getOrderedCategories,
  hasTierAccess,
  getBlocksForTier,
  getPerformanceTier,
  calculateBlockStatistics,
  generatePaletteGroups,
  filterPaletteGroups,
  getCombinationsForTier,
  validatePipelineConstraints,
} from '../block-metadata';
import type { UserTier, BlockCategory } from '../types';

describe('Block Metadata', () => {
  describe('CATEGORY_META', () => {
    it('should have all 6 categories', () => {
      const categories: BlockCategory[] = [
        'retrieval',
        'optimization',
        'ranking',
        'validation',
        'agents',
        'control',
      ];

      categories.forEach((cat) => {
        expect(CATEGORY_META[cat]).toBeDefined();
        expect(CATEGORY_META[cat].label).toBeDefined();
        expect(CATEGORY_META[cat].icon).toBeDefined();
        expect(CATEGORY_META[cat].order).toBeGreaterThan(0);
      });
    });
  });

  describe('getOrderedCategories', () => {
    it('should return categories in order', () => {
      const ordered = getOrderedCategories();
      expect(ordered[0].id).toBe('retrieval');
      expect(ordered[1].id).toBe('optimization');
      expect(ordered[2].id).toBe('ranking');
      expect(ordered[3].id).toBe('validation');
      expect(ordered[4].id).toBe('agents');
      expect(ordered[5].id).toBe('control');
    });
  });

  describe('TIER_HIERARCHY', () => {
    it('should have correct hierarchy', () => {
      expect(TIER_HIERARCHY.User).toBeLessThan(TIER_HIERARCHY.Editor);
      expect(TIER_HIERARCHY.Editor).toBeLessThan(TIER_HIERARCHY.Admin);
    });
  });

  describe('hasTierAccess', () => {
    it('should grant access for same tier', () => {
      expect(hasTierAccess('User', 'User')).toBe(true);
      expect(hasTierAccess('Editor', 'Editor')).toBe(true);
      expect(hasTierAccess('Admin', 'Admin')).toBe(true);
    });

    it('should grant access for higher tier', () => {
      expect(hasTierAccess('Admin', 'User')).toBe(true);
      expect(hasTierAccess('Admin', 'Editor')).toBe(true);
      expect(hasTierAccess('Editor', 'User')).toBe(true);
    });

    it('should deny access for lower tier', () => {
      expect(hasTierAccess('User', 'Editor')).toBe(false);
      expect(hasTierAccess('User', 'Admin')).toBe(false);
      expect(hasTierAccess('Editor', 'Admin')).toBe(false);
    });
  });

  describe('getBlocksForTier', () => {
    it('should return all blocks for Admin', () => {
      const blocks = getBlocksForTier('Admin');
      expect(blocks.length).toBe(23);
    });

    it('should return fewer blocks for User', () => {
      const userBlocks = getBlocksForTier('User');
      const adminBlocks = getBlocksForTier('Admin');
      expect(userBlocks.length).toBeLessThanOrEqual(adminBlocks.length);
    });

    it('should include User blocks in Editor tier', () => {
      const userBlocks = getBlocksForTier('User');
      const editorBlocks = getBlocksForTier('Editor');
      userBlocks.forEach((block) => {
        expect(editorBlocks).toContain(block);
      });
    });
  });

  describe('getPerformanceTier', () => {
    it('should classify fast blocks correctly', () => {
      // Metadata filtering is very fast (10ms, 0 tokens, 0 cost)
      expect(getPerformanceTier('metadata-filtering')).toBe('fast');
    });

    it('should classify expensive blocks correctly', () => {
      // Parallel agent is expensive (12K tokens, 3000ms, $0.08)
      expect(getPerformanceTier('parallel-agent')).toBe('expensive');
    });
  });

  describe('calculateBlockStatistics', () => {
    it('should return correct statistics', () => {
      const stats = calculateBlockStatistics();

      expect(stats.totalBlocks).toBe(23);
      expect(stats.blocksByCategory.retrieval).toBe(7);
      expect(stats.avgTokens).toBeGreaterThan(0);
      expect(stats.avgLatency).toBeGreaterThan(0);
    });
  });

  describe('generatePaletteGroups', () => {
    it('should generate groups for User tier', () => {
      const groups = generatePaletteGroups('User');
      expect(groups.length).toBeGreaterThan(0);

      // Check that some blocks are disabled for User
      const allItems = groups.flatMap((g) => g.items);
      const disabledItems = allItems.filter((i) => i.disabled);
      expect(disabledItems.length).toBeGreaterThan(0);
    });

    it('should have no disabled blocks for Admin', () => {
      const groups = generatePaletteGroups('Admin');
      const allItems = groups.flatMap((g) => g.items);
      const disabledItems = allItems.filter((i) => i.disabled);
      expect(disabledItems.length).toBe(0);
    });
  });

  describe('filterPaletteGroups', () => {
    it('should filter by search query', () => {
      const groups = generatePaletteGroups('Admin');
      const filtered = filterPaletteGroups(groups, 'vector');

      expect(filtered.length).toBeGreaterThan(0);
      const allNames = filtered.flatMap((g) => g.items.map((i) => i.block.name.toLowerCase()));
      expect(allNames.some((name) => name.includes('vector'))).toBe(true);
    });

    it('should return all groups for empty query', () => {
      const groups = generatePaletteGroups('Admin');
      const filtered = filterPaletteGroups(groups, '');
      expect(filtered.length).toBe(groups.length);
    });

    it('should return empty for non-matching query', () => {
      const groups = generatePaletteGroups('Admin');
      const filtered = filterPaletteGroups(groups, 'zzzzzznonexistent');
      expect(filtered.length).toBe(0);
    });
  });

  describe('RECOMMENDED_COMBINATIONS', () => {
    it('should have valid combinations', () => {
      expect(RECOMMENDED_COMBINATIONS.length).toBeGreaterThan(0);

      RECOMMENDED_COMBINATIONS.forEach((combo) => {
        expect(combo.id).toBeDefined();
        expect(combo.name).toBeDefined();
        expect(combo.blocks.length).toBeGreaterThan(0);
        expect(combo.estimatedTokens).toBeGreaterThan(0);
        expect(combo.requiredTier).toBeDefined();
      });
    });
  });

  describe('getCombinationsForTier', () => {
    it('should return combinations for Admin', () => {
      const combos = getCombinationsForTier('Admin');
      expect(combos.length).toBe(RECOMMENDED_COMBINATIONS.length);
    });

    it('should return fewer combinations for User', () => {
      const userCombos = getCombinationsForTier('User');
      const adminCombos = getCombinationsForTier('Admin');
      expect(userCombos.length).toBeLessThanOrEqual(adminCombos.length);
    });
  });

  describe('validatePipelineConstraints', () => {
    it('should validate empty pipeline', () => {
      const result = validatePipelineConstraints([], 0);
      expect(result.isValid).toBe(true);
      expect(result.details.nodeCount).toBe(0);
      expect(result.details.edgeCount).toBe(0);
    });

    it('should validate small pipeline', () => {
      const result = validatePipelineConstraints(
        ['vector-search', 'cross-encoder-reranking'],
        1
      );
      expect(result.isValid).toBe(true);
      expect(result.tokensValid).toBe(true);
      expect(result.costValid).toBe(true);
    });

    it('should detect constraint violations', () => {
      // Create a pipeline that exceeds node count
      const manyBlocks = Array(25).fill('vector-search') as import('../types').RagBlockType[];
      const result = validatePipelineConstraints(manyBlocks, 30);
      expect(result.nodeCountValid).toBe(false);
    });
  });

  describe('PIPELINE_CONSTRAINTS', () => {
    it('should have reasonable constraints', () => {
      expect(PIPELINE_CONSTRAINTS.maxTokens).toBe(30000);
      expect(PIPELINE_CONSTRAINTS.maxLatencyMs).toBe(30000);
      expect(PIPELINE_CONSTRAINTS.maxCostUsd).toBe(0.50);
      expect(PIPELINE_CONSTRAINTS.maxNodes).toBe(20);
      expect(PIPELINE_CONSTRAINTS.maxEdges).toBe(50);
    });
  });

  describe('BLOCK_SUMMARY', () => {
    it('should have correct totals', () => {
      expect(BLOCK_SUMMARY.total).toBe(23);
      expect(BLOCK_SUMMARY.categories).toBe(6);
      expect(BLOCK_SUMMARY.retrieval).toBe(7);
      expect(BLOCK_SUMMARY.optimization).toBe(4);
      expect(BLOCK_SUMMARY.ranking).toBe(4);
      expect(BLOCK_SUMMARY.validation).toBe(5);
      expect(BLOCK_SUMMARY.agents).toBe(3);
    });

    it('should have valid tier counts', () => {
      const tierSum =
        BLOCK_SUMMARY.userTierBlocks +
        BLOCK_SUMMARY.editorTierBlocks +
        BLOCK_SUMMARY.adminTierBlocks;
      expect(tierSum).toBe(23);
    });
  });
});
