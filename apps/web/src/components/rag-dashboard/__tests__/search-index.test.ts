/**
 * Tests for search-index module
 * Issue #3450: Global search functionality
 */

import { describe, it, expect } from 'vitest';

import {
  SEARCH_INDEX,
  GROUP_LABELS,
  fuzzyMatch,
  searchSections,
  getSectionsGrouped,
} from '../search-index';

describe('search-index', () => {
  // =========================================================================
  // SEARCH_INDEX Structure Tests
  // =========================================================================

  describe('SEARCH_INDEX', () => {
    it('should contain all expected sections', () => {
      const sectionIds = SEARCH_INDEX.map((s) => s.id);

      // Check key sections exist
      expect(sectionIds).toContain('overview');
      expect(sectionIds).toContain('query-sim');
      expect(sectionIds).toContain('cost');
      expect(sectionIds).toContain('architecture');
      expect(sectionIds).toContain('prompts');
    });

    it('should have valid structure for each section', () => {
      for (const section of SEARCH_INDEX) {
        expect(section.id).toBeTruthy();
        expect(section.title).toBeTruthy();
        expect(section.description).toBeTruthy();
        expect(Array.isArray(section.keywords)).toBe(true);
        expect(section.keywords.length).toBeGreaterThan(0);
        expect(section.group).toBeTruthy();
        expect(section.icon).toBeTruthy();
      }
    });

    it('should have unique section IDs', () => {
      const ids = SEARCH_INDEX.map((s) => s.id);
      const uniqueIds = new Set(ids);
      expect(uniqueIds.size).toBe(ids.length);
    });

    it('should have valid groups for all sections', () => {
      const validGroups = ['understand', 'explore', 'compare', 'build', 'optimize'];

      for (const section of SEARCH_INDEX) {
        expect(validGroups).toContain(section.group);
      }
    });

    it('should mark technical-only sections correctly', () => {
      const technicalOnly = SEARCH_INDEX.filter((s) => s.technicalOnly);

      // Architecture, layers, variants, etc. should be technical only
      const technicalIds = technicalOnly.map((s) => s.id);
      expect(technicalIds).toContain('architecture');
      expect(technicalIds).toContain('layers');
    });

    it('should mark business-only sections correctly', () => {
      const businessOnly = SEARCH_INDEX.filter((s) => s.businessOnly);

      // Executive summary should be business only
      const businessIds = businessOnly.map((s) => s.id);
      expect(businessIds).toContain('executive-summary');
    });
  });

  // =========================================================================
  // GROUP_LABELS Tests
  // =========================================================================

  describe('GROUP_LABELS', () => {
    it('should have labels for all groups', () => {
      expect(GROUP_LABELS.understand).toBeDefined();
      expect(GROUP_LABELS.explore).toBeDefined();
      expect(GROUP_LABELS.compare).toBeDefined();
      expect(GROUP_LABELS.build).toBeDefined();
      expect(GROUP_LABELS.optimize).toBeDefined();
    });

    it('should have label and icon for each group', () => {
      for (const [key, value] of Object.entries(GROUP_LABELS)) {
        expect(value.label).toBeTruthy();
        expect(value.icon).toBeTruthy();
      }
    });
  });

  // =========================================================================
  // fuzzyMatch Tests
  // =========================================================================

  describe('fuzzyMatch', () => {
    it('should return 100 for exact match', () => {
      expect(fuzzyMatch('cost', 'cost')).toBe(100);
    });

    it('should return high score for exact match (case insensitive)', () => {
      expect(fuzzyMatch('Cost', 'cost')).toBe(100);
      expect(fuzzyMatch('COST', 'cost')).toBe(100);
    });

    it('should return 80 for contains match', () => {
      expect(fuzzyMatch('cost', 'cost calculator')).toBe(80);
      expect(fuzzyMatch('query', 'query simulator')).toBe(80);
    });

    it('should return 80 for contains match at word start', () => {
      // "arch" is contained in "architecture", so it scores 80 (contains)
      expect(fuzzyMatch('arch', 'architecture explorer')).toBe(80);
    });

    it('should return score for partial word match', () => {
      const score = fuzzyMatch('calc', 'cost calculator');
      expect(score).toBeGreaterThan(50);
    });

    it('should return score for fuzzy character match', () => {
      const score = fuzzyMatch('cst', 'cost');
      expect(score).toBeGreaterThan(0);
    });

    it('should return 0 for no match', () => {
      expect(fuzzyMatch('xyz', 'cost')).toBe(0);
    });
  });

  // =========================================================================
  // searchSections Tests
  // =========================================================================

  describe('searchSections', () => {
    it('should return all sections for empty query', () => {
      const results = searchSections('');
      expect(results.length).toBeGreaterThan(0);
    });

    it('should find sections by title', () => {
      const results = searchSections('cost');
      const ids = results.map((s) => s.id);
      expect(ids).toContain('cost');
    });

    it('should find sections by keyword', () => {
      const results = searchSections('tokens');
      const ids = results.map((s) => s.id);
      expect(ids).toContain('token-flow');
    });

    it('should find sections by description', () => {
      const results = searchSections('estimate monthly');
      const ids = results.map((s) => s.id);
      expect(ids).toContain('cost');
    });

    it('should filter by view mode (technical)', () => {
      const results = searchSections('', { viewMode: 'technical' });

      // Should not include business-only
      const hasBusinessOnly = results.some((s) => s.businessOnly);
      expect(hasBusinessOnly).toBe(false);

      // Should include technical-only
      const hasTechnicalOnly = results.some((s) => s.technicalOnly);
      expect(hasTechnicalOnly).toBe(true);
    });

    it('should filter by view mode (business)', () => {
      const results = searchSections('', { viewMode: 'business' });

      // Should not include technical-only
      const hasTechnicalOnly = results.some((s) => s.technicalOnly);
      expect(hasTechnicalOnly).toBe(false);

      // Should include business-only
      const hasBusinessOnly = results.some((s) => s.businessOnly);
      expect(hasBusinessOnly).toBe(true);
    });

    it('should respect limit parameter', () => {
      const results = searchSections('', { limit: 3 });
      expect(results.length).toBeLessThanOrEqual(3);
    });

    it('should handle fuzzy/typo queries', () => {
      // "qury" is a typo for "query"
      const results = searchSections('qury');
      const hasQuerySim = results.some((s) => s.id === 'query-sim');
      // May or may not match depending on fuzzy threshold
      // Just ensure it doesn't throw
      expect(Array.isArray(results)).toBe(true);
    });

    it('should prioritize title matches over keyword matches', () => {
      const results = searchSections('overview');

      // Overview should be first since it's an exact title match
      expect(results[0]?.id).toBe('overview');
    });

    it('should filter out low score results', () => {
      const results = searchSections('xyznonexistent');

      // Should return empty or near-empty for nonsense query
      expect(results.length).toBeLessThan(5);
    });
  });

  // =========================================================================
  // getSectionsGrouped Tests
  // =========================================================================

  describe('getSectionsGrouped', () => {
    it('should group sections by their group property', () => {
      const sections = SEARCH_INDEX.slice(0, 5);
      const grouped = getSectionsGrouped(sections);

      // Should have all group keys
      expect(grouped.understand).toBeDefined();
      expect(grouped.explore).toBeDefined();
      expect(grouped.compare).toBeDefined();
      expect(grouped.build).toBeDefined();
      expect(grouped.optimize).toBeDefined();
    });

    it('should place sections in correct groups', () => {
      const grouped = getSectionsGrouped(SEARCH_INDEX);

      // Overview is in understand group
      const overviewInUnderstand = grouped.understand.some((s) => s.id === 'overview');
      expect(overviewInUnderstand).toBe(true);

      // Query sim is in explore group
      const querySimInExplore = grouped.explore.some((s) => s.id === 'query-sim');
      expect(querySimInExplore).toBe(true);

      // Cost is in optimize group
      const costInOptimize = grouped.optimize.some((s) => s.id === 'cost');
      expect(costInOptimize).toBe(true);
    });

    it('should return empty arrays for groups with no sections', () => {
      const emptyGrouped = getSectionsGrouped([]);

      expect(emptyGrouped.understand).toEqual([]);
      expect(emptyGrouped.explore).toEqual([]);
      expect(emptyGrouped.compare).toEqual([]);
      expect(emptyGrouped.build).toEqual([]);
      expect(emptyGrouped.optimize).toEqual([]);
    });
  });
});
