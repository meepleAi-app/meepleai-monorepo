/**
 * Citation Type Tests
 * Validates the Citation interface supports both 'full' and 'protected' copyright tiers.
 *
 * Tests:
 * 1. Full tier citation without paraphrase
 * 2. Protected tier citation with paraphrase and isPublic
 * 3. Backward compat: copyrightTier is required but defaults handled at schema level
 */

import { describe, it, expect } from 'vitest';
import type { Citation } from '@/types/domain';

describe('Citation type', () => {
  it('accepts full tier citation', () => {
    const citation: Citation = {
      documentId: 'doc-1',
      pageNumber: 14,
      snippet: 'Original text',
      relevanceScore: 0.92,
      copyrightTier: 'full',
    };
    expect(citation.copyrightTier).toBe('full');
    expect(citation.paraphrasedSnippet).toBeUndefined();
  });

  it('accepts protected tier citation with paraphrase', () => {
    const citation: Citation = {
      documentId: 'doc-1',
      pageNumber: 14,
      snippet: 'Original text',
      relevanceScore: 0.92,
      copyrightTier: 'protected',
      paraphrasedSnippet: 'Reworded text',
      isPublic: false,
    };
    expect(citation.copyrightTier).toBe('protected');
    expect(citation.paraphrasedSnippet).toBe('Reworded text');
  });

  it('accepts isPublic as true for public games', () => {
    const citation: Citation = {
      documentId: 'doc-2',
      pageNumber: 3,
      snippet: 'Public game rule',
      relevanceScore: 0.85,
      copyrightTier: 'protected',
      isPublic: true,
    };
    expect(citation.isPublic).toBe(true);
  });

  it('paraphrasedSnippet and isPublic are optional', () => {
    const citation: Citation = {
      documentId: 'doc-3',
      pageNumber: 7,
      snippet: 'Some rule text',
      relevanceScore: 0.77,
      copyrightTier: 'full',
    };
    expect(citation.paraphrasedSnippet).toBeUndefined();
    expect(citation.isPublic).toBeUndefined();
  });
});
