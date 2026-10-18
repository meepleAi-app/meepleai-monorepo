import { describe, expect, it } from 'vitest';

import { CitationSchema } from '@/lib/api/schemas/streaming.schemas';

import { toRuleSourceCitations } from '../assistant-citations';

const wire = (over: Record<string, unknown>) =>
  CitationSchema.parse({
    documentId: 'pdf-1',
    pageNumber: 3,
    relevanceScore: 0.9,
    copyrightTier: 'full',
    ...over,
  });

describe('toRuleSourceCitations (#4411)', () => {
  it('shows the verbatim text of a Full citation', () => {
    const [citation] = toRuleSourceCitations([wire({ snippetPreview: 'Si pescano 4 tessere.' })]);
    expect(citation).toMatchObject({
      documentId: 'pdf-1',
      pageNumber: 3,
      snippet: 'Si pescano 4 tessere.',
      copyrightTier: 'full',
    });
  });

  it('never shows verbatim text for a Protected citation, only its paraphrase', () => {
    const [citation] = toRuleSourceCitations([
      wire({
        copyrightTier: 'protected',
        snippetPreview: 'testo che non deve arrivare',
        paraphrasedSnippet: 'Quattro tessere.',
      }),
    ]);
    expect(citation?.snippet).toBe('');
    expect(citation?.paraphrasedSnippet).toBe('Quattro tessere.');
  });

  it('drops a citation without a page', () => {
    expect(toRuleSourceCitations([wire({ pageNumber: null })])).toEqual([]);
  });

  it('treats no citations as none', () => {
    expect(toRuleSourceCitations(null)).toEqual([]);
    expect(toRuleSourceCitations(undefined)).toEqual([]);
  });
});
