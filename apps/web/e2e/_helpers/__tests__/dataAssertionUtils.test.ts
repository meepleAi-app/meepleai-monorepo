import { describe, it, expect } from 'vitest';
import {
  assertExactStackDepth,
  assertExactUrl,
  assertExactCount,
  assertFunctionalFocus,
} from '../dataAssertionUtils';

describe('dataAssertionUtils (#1929 DEC-C-2)', () => {
  describe('strict assertions', () => {
    it('assertExactStackDepth passes when depth matches expected', () => {
      expect(() => assertExactStackDepth(2, 2)).not.toThrow();
    });

    it('assertExactStackDepth throws when depth mismatches', () => {
      expect(() => assertExactStackDepth(1, 2)).toThrow(/strict.*stack depth.*expected 2.*got 1/i);
    });

    it('assertExactUrl passes for exact string match', () => {
      expect(() =>
        assertExactUrl('https://example.test/dashboard', 'https://example.test/dashboard')
      ).not.toThrow();
    });

    it('assertExactUrl supports regex match', () => {
      expect(() =>
        assertExactUrl('https://example.test/game-nights/abc-123', /\/game-nights\/[a-z0-9-]+$/)
      ).not.toThrow();
    });

    it('assertExactUrl throws when url mismatches', () => {
      expect(() => assertExactUrl('https://example.test/login', '/dashboard')).toThrow(
        /strict.*url.*expected/i
      );
    });

    it('assertExactCount throws on mismatch', () => {
      expect(() => assertExactCount(5, 10, 'cards')).toThrow(
        /strict.*count.*cards.*expected 10.*got 5/i
      );
    });
  });

  describe('functional assertions', () => {
    it('assertFunctionalFocus returns true when selector matches focused element', () => {
      const result = assertFunctionalFocus(
        { tagName: 'BUTTON', dataset: { testid: 'foo' } } as unknown as Element,
        '[data-testid="foo"]'
      );
      expect(result).toBe(true);
    });

    it('assertFunctionalFocus returns false when no element focused', () => {
      const result = assertFunctionalFocus(null, '[data-testid="foo"]');
      expect(result).toBe(false);
    });
  });
});
