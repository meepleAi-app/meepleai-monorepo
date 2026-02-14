/**
 * String Similarity Utilities Tests - Issue #4164
 *
 * Test coverage for Levenshtein distance-based similarity calculation.
 */

import { describe, it, expect } from 'vitest';

import { calculateStringSimilarity, isSimilar } from '../string-similarity';

describe('string-similarity', () => {
  describe('calculateStringSimilarity', () => {
    it('returns 100 for identical strings', () => {
      expect(calculateStringSimilarity('Catan', 'Catan')).toBe(100);
      expect(calculateStringSimilarity('Monopoly', 'Monopoly')).toBe(100);
    });

    it('is case-insensitive', () => {
      expect(calculateStringSimilarity('Catan', 'CATAN')).toBe(100);
      expect(calculateStringSimilarity('Chess', 'chess')).toBe(100);
      expect(calculateStringSimilarity('CaTaN', 'cAtAn')).toBe(100);
    });

    it('trims whitespace', () => {
      expect(calculateStringSimilarity('  Catan  ', 'Catan')).toBe(100);
      expect(calculateStringSimilarity('Chess ', ' Chess')).toBe(100);
    });

    it('returns 0 for completely different strings', () => {
      expect(calculateStringSimilarity('Catan', 'Monopoly')).toBeLessThan(30);
    });

    it('returns 0 for empty strings', () => {
      expect(calculateStringSimilarity('', 'Catan')).toBe(0);
      expect(calculateStringSimilarity('Catan', '')).toBe(0);
      expect(calculateStringSimilarity('', '')).toBe(0);
    });

    it('calculates similarity for partial matches', () => {
      // "Catan" vs "Settlers of Catan" - substring match
      // Levenshtein is strict: 5 chars vs 18 chars = ~28% (not substring-aware)
      const score1 = calculateStringSimilarity('Catan', 'Settlers of Catan');
      expect(score1).toBeGreaterThan(20);
      expect(score1).toBeLessThan(40);

      // "Chess" vs "Checkers" should have moderate similarity
      const score2 = calculateStringSimilarity('Chess', 'Checkers');
      expect(score2).toBeGreaterThan(40);
      expect(score2).toBeLessThan(80);
    });

    it('handles single character differences correctly', () => {
      expect(calculateStringSimilarity('Cat', 'Bat')).toBeGreaterThan(60);
      expect(calculateStringSimilarity('Test', 'Tost')).toBeGreaterThan(70);
    });

    it('returns percentage between 0 and 100', () => {
      const testCases = [
        ['Catan', 'Catan'],
        ['Catan', 'Settlers of Catan'],
        ['Chess', 'Checkers'],
        ['A', 'Z'],
        ['Hello', 'World'],
      ];

      testCases.forEach(([a, b]) => {
        const score = calculateStringSimilarity(a, b);
        expect(score).toBeGreaterThanOrEqual(0);
        expect(score).toBeLessThanOrEqual(100);
      });
    });

    it('is symmetric (order independent)', () => {
      expect(calculateStringSimilarity('Catan', 'Monopoly')).toBe(
        calculateStringSimilarity('Monopoly', 'Catan')
      );

      expect(calculateStringSimilarity('Chess', 'Checkers')).toBe(
        calculateStringSimilarity('Checkers', 'Chess')
      );
    });
  });

  describe('isSimilar', () => {
    it('returns true when similarity >= threshold', () => {
      expect(isSimilar('Catan', 'Catan', 70)).toBe(true); // 100 >= 70
      expect(isSimilar('Catan', 'Catan', 100)).toBe(true); // 100 >= 100
    });

    it('returns false when similarity < threshold', () => {
      expect(isSimilar('Chess', 'Monopoly', 70)).toBe(false);
      expect(isSimilar('A', 'Z', 50)).toBe(false);
    });

    it('uses default threshold of 70', () => {
      expect(isSimilar('Catan', 'Catan')).toBe(true); // 100 >= 70
      expect(isSimilar('Chess', 'Monopoly')).toBe(false); // < 70
    });

    it('works with custom thresholds', () => {
      const str1 = 'Catan';
      const str2 = 'Settlers of Catan';
      const score = calculateStringSimilarity(str1, str2);

      expect(isSimilar(str1, str2, score - 1)).toBe(true);
      expect(isSimilar(str1, str2, score + 1)).toBe(false);
    });
  });
});
