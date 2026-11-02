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

  // #4819: the server leaves the page out for a document whose pages it cannot tell apart. The source
  // is still worth showing: it loses its page, not its place.
  it.each([
    ['null', { pageNumber: null }],
    ['absent', { pageNumber: undefined }],
    ['zero (a server that still writes 0 for it)', { pageNumber: 0 }],
  ])('keeps a citation whose page is %s, without a page', (_label, over) => {
    const [citation] = toRuleSourceCitations([wire({ ...over, snippetPreview: 'Si pescano 4.' })]);
    expect(citation).toMatchObject({
      documentId: 'pdf-1',
      pageNumber: null,
      snippet: 'Si pescano 4.',
    });
  });

  it('falls back to the legacy `page` field when `pageNumber` is absent', () => {
    const [citation] = toRuleSourceCitations([wire({ pageNumber: undefined, page: 7 })]);
    expect(citation?.pageNumber).toBe(7);
  });

  it('treats no citations as none', () => {
    expect(toRuleSourceCitations(null)).toEqual([]);
    expect(toRuleSourceCitations(undefined)).toEqual([]);
  });
});
