/**
 * Tests for gamebook zod schemas — issue #1303.
 *
 * Covers AC-4a + AC-4b: `ParagraphSchema` accepts the relaxed `pageNumber`
 * range and the new `paragraphNumber` field with the documented backend
 * invariant (positive nullable).
 */

import { describe, expect, it } from 'vitest';

import { ParagraphSchema } from '../schemas';

describe('ParagraphSchema (#1303)', () => {
  // -------------------------------------------------------------------------
  // AC-4a — pageNumber relaxed from positive() to nonnegative()
  // -------------------------------------------------------------------------

  describe('AC-4a — pageNumber 0 accepted (semantic-fallback case)', () => {
    it('parses successfully when pageNumber is 0', () => {
      const result = ParagraphSchema.safeParse({
        pageNumber: 0,
        text: 'fallback text',
        fallbackUsed: true,
        fallbackMethod: 'semantic',
      });

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.pageNumber).toBe(0);
        expect(result.data.fallbackUsed).toBe(true);
      }
    });

    it('still rejects negative pageNumber values', () => {
      const result = ParagraphSchema.safeParse({
        pageNumber: -1,
        text: 'x',
        fallbackUsed: false,
        fallbackMethod: null,
      });
      expect(result.success).toBe(false);
    });
  });

  // -------------------------------------------------------------------------
  // AC-4b — ByParagraph + semantic-fallback DTO shape round-trips
  // -------------------------------------------------------------------------

  describe('AC-4b — ByParagraph + semantic-fallback shape', () => {
    it('round-trips paragraphNumber: null with fallbackUsed: true', () => {
      const dto = {
        pageNumber: 0,
        text: 'Semantic fallback snippet 1\n\nSnippet 2',
        fallbackUsed: true,
        fallbackMethod: 'semantic',
        paragraphNumber: null,
      };

      const result = ParagraphSchema.safeParse(dto);

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.paragraphNumber).toBeNull();
        expect(result.data.fallbackMethod).toBe('semantic');
      }
    });

    it('round-trips paragraphNumber: 42 (numbered match)', () => {
      const result = ParagraphSchema.safeParse({
        pageNumber: 7,
        text: 'Paragraph 42 content',
        fallbackUsed: false,
        fallbackMethod: null,
        paragraphNumber: 42,
      });

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.paragraphNumber).toBe(42);
        expect(result.data.pageNumber).toBe(7);
      }
    });

    it('defaults paragraphNumber to null when absent (legacy page-lookup response)', () => {
      // Legacy backend (pre PR-B) doesn't emit paragraphNumber at all. The
      // schema default keeps existing callers working.
      const result = ParagraphSchema.safeParse({
        pageNumber: 5,
        text: 'legacy page text',
        fallbackUsed: false,
        fallbackMethod: null,
      });

      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.paragraphNumber).toBeNull();
      }
    });
  });

  // -------------------------------------------------------------------------
  // Backend invariant — paragraphNumber must be ≥ 1 when non-null
  // -------------------------------------------------------------------------

  describe('paragraphNumber positive invariant (PR-B backend contract)', () => {
    it('rejects paragraphNumber: 0', () => {
      // Backend invariant: ByParagraph + numbered match always returns ≥ 1.
      // A 0 here would indicate a backend regression — schema must catch it.
      const result = ParagraphSchema.safeParse({
        pageNumber: 7,
        text: 'x',
        fallbackUsed: false,
        fallbackMethod: null,
        paragraphNumber: 0,
      });

      expect(result.success).toBe(false);
    });

    it('rejects negative paragraphNumber', () => {
      const result = ParagraphSchema.safeParse({
        pageNumber: 7,
        text: 'x',
        fallbackUsed: false,
        fallbackMethod: null,
        paragraphNumber: -5,
      });

      expect(result.success).toBe(false);
    });
  });
});
