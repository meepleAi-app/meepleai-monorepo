import { describe, it, expect } from 'vitest';
import { mapCitationToChatCitation } from '../map-citation-to-chat-citation';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

const base: Partial<Citation> = {
  source: 'Regolamento Azul',
  documentId: 'doc-1',
  pageNumber: 7,
  copyrightTier: 'full',
};

describe('mapCitationToChatCitation', () => {
  it('full tier -> excerpt from snippet', () => {
    const citation = {
      ...base,
      snippet: 'Posiziona la plancia.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toEqual({
      documentName: 'Regolamento Azul',
      pages: [7],
      excerpt: 'Posiziona la plancia.',
    });
  });

  it('full tier -> excerpt from text when snippet is absent', () => {
    const citation = {
      ...base,
      snippet: null,
      text: 'Fallback text content.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toEqual({
      documentName: 'Regolamento Azul',
      pages: [7],
      excerpt: 'Fallback text content.',
    });
  });

  it('full tier -> prefers snippet over text', () => {
    const citation = {
      ...base,
      snippet: 'Preferred snippet.',
      text: 'Fallback text.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toEqual({
      documentName: 'Regolamento Azul',
      pages: [7],
      excerpt: 'Preferred snippet.',
    });
  });

  it('protected tier -> excerpt from paraphrasedSnippet only, never verbatim', () => {
    const citation = {
      ...base,
      copyrightTier: 'protected',
      snippet: 'Verbatim text should be ignored.',
      text: 'More verbatim text.',
      paraphrasedSnippet: 'Sintesi della regola in paraphrase.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.excerpt).toBe('Sintesi della regola in paraphrase.');
    expect(result?.excerpt).not.toContain('Verbatim');
  });

  it('protected tier with no paraphrase and no snippet -> returns null', () => {
    const citation = {
      ...base,
      copyrightTier: 'protected',
      snippet: null,
      paraphrasedSnippet: null,
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toBeNull();
  });

  it('protected tier with no paraphrase and no text -> returns null', () => {
    const citation = {
      ...base,
      copyrightTier: 'protected',
      snippet: null,
      text: null,
      paraphrasedSnippet: null,
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toBeNull();
  });

  it('uses pageNumber when present', () => {
    const citation = {
      source: 'Doc A',
      pageNumber: 42,
      copyrightTier: 'full',
      snippet: 'content',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.pages).toEqual([42]);
  });

  it('uses page field when pageNumber is absent', () => {
    const citation = {
      source: 'Doc B',
      pageNumber: null,
      page: 3,
      copyrightTier: 'full',
      snippet: 'content',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.pages).toEqual([3]);
  });

  it('returns empty pages array when both pageNumber and page are absent', () => {
    const citation = {
      source: 'Doc C',
      pageNumber: null,
      page: null,
      copyrightTier: 'full',
      snippet: 'content',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.pages).toEqual([]);
  });

  it('trims whitespace from excerpt', () => {
    const citation = {
      ...base,
      snippet: '  Spaced content  ',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.excerpt).toBe('Spaced content');
  });

  it('returns null when excerpt is empty after trimming', () => {
    const citation = {
      ...base,
      snippet: '   ',
      text: null,
      paraphrasedSnippet: null,
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result).toBeNull();
  });

  it('full tier with empty string snippet falls back to text', () => {
    const citation = {
      ...base,
      snippet: '',
      text: 'Fallback text.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.excerpt).toBe('Fallback text.');
  });

  it('protected tier prefers paraphrasedSnippet even if snippet exists', () => {
    const citation = {
      ...base,
      copyrightTier: 'protected',
      snippet: 'Verbatim should be ignored.',
      paraphrasedSnippet: 'Paraphrased version.',
    } as Citation;

    const result = mapCitationToChatCitation(citation);

    expect(result?.excerpt).toBe('Paraphrased version.');
  });
});
