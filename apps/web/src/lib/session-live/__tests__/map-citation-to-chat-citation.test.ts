import { describe, it, expect } from 'vitest';
import { mapCitationToChatCitation } from '../map-citation-to-chat-citation';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

// ─── Real wire CitationDto shape (BE Contracts.cs:137-144) ──────────────────
// C2 (#2500): { documentId, pageNumber, relevanceScore, snippetPreview,
//               copyrightTier, paraphrasedSnippet, isPublic }
// No `source`, no `snippet`, no `text`.

const wireBase: Partial<Citation> = {
  documentId: 'doc-uuid-wire-001',
  pageNumber: 7,
  relevanceScore: 0.92,
  copyrightTier: 'full',
  isPublic: true,
};

// ─── Legacy shape base (kept for backward-compat tests) ──────────────────────
// Consumers like kb-ask or stored citationsJson from before C2 may have `source`/`snippet`.

const legacyBase: Partial<Citation> = {
  source: 'Regolamento Azul',
  documentId: 'doc-1',
  pageNumber: 7,
  copyrightTier: 'full',
};

describe('mapCitationToChatCitation', () => {
  // ─── Real wire format (C2 #2500) ───────────────────────────────────────────

  describe('real wire format (C2 #2500)', () => {
    it('full tier → excerpt from snippetPreview (real wire field)', () => {
      const citation = {
        ...wireBase,
        snippetPreview: 'Posiziona la plancia al centro.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toEqual({
        documentName: 'doc-uuid-wire-001', // no `source` → falls back to documentId
        pages: [7],
        excerpt: 'Posiziona la plancia al centro.',
      });
    });

    it('real wire: documentName is documentId when source absent', () => {
      const citation = {
        ...wireBase,
        snippetPreview: 'Testo regola.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.documentName).toBe('doc-uuid-wire-001');
    });

    it('real wire: protected tier → paraphrasedSnippet (never verbatim snippetPreview)', () => {
      const citation = {
        ...wireBase,
        copyrightTier: 'protected',
        snippetPreview: 'Verbatim protetto — NON mostrare.',
        paraphrasedSnippet: 'Riassunto consentito della regola.',
        isPublic: false,
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.excerpt).toBe('Riassunto consentito della regola.');
      expect(result?.excerpt).not.toContain('Verbatim');
    });

    it('real wire: protected tier, no paraphrasedSnippet → returns null', () => {
      const citation = {
        ...wireBase,
        copyrightTier: 'protected',
        snippetPreview: 'Verbatim non divulgabile.',
        paraphrasedSnippet: null,
        isPublic: false,
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toBeNull();
    });

    it('real wire: no snippetPreview and no text → returns null (full tier)', () => {
      const citation = {
        ...wireBase,
        snippetPreview: null,
        // no snippet, no text
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toBeNull();
    });

    it('real wire: pageNumber used correctly', () => {
      const citation = {
        ...wireBase,
        pageNumber: 42,
        snippetPreview: 'contenuto',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.pages).toEqual([42]);
    });

    it('real wire: no documentId and no source → documentName is empty string', () => {
      const citation = {
        pageNumber: 1,
        relevanceScore: 0.5,
        copyrightTier: 'full',
        isPublic: true,
        snippetPreview: 'contenuto',
      } as unknown as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.documentName).toBe('');
    });
  });

  // ─── Legacy / backward-compat format ───────────────────────────────────────
  // Ensures that kb-ask consumers and old citationsJson (with `source`/`snippet`) still work.

  describe('legacy format (backward-compat)', () => {
    it('full tier → excerpt from snippet (legacy field)', () => {
      const citation = {
        ...legacyBase,
        snippet: 'Posiziona la plancia.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toEqual({
        documentName: 'Regolamento Azul',
        pages: [7],
        excerpt: 'Posiziona la plancia.',
      });
    });

    it('full tier → excerpt from text when snippet is absent', () => {
      const citation = {
        ...legacyBase,
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

    it('full tier → prefers snippetPreview over snippet (priority order)', () => {
      const citation = {
        ...legacyBase,
        snippetPreview: 'snippetPreview wins.',
        snippet: 'snippet second.',
        text: 'text last.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.excerpt).toBe('snippetPreview wins.');
    });

    it('full tier → prefers snippet over text when snippetPreview absent', () => {
      const citation = {
        ...legacyBase,
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

    it('protected tier → excerpt from paraphrasedSnippet only, never verbatim', () => {
      const citation = {
        ...legacyBase,
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
        ...legacyBase,
        copyrightTier: 'protected',
        snippet: null,
        paraphrasedSnippet: null,
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toBeNull();
    });

    it('protected tier with no paraphrase and no text -> returns null', () => {
      const citation = {
        ...legacyBase,
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
        ...legacyBase,
        snippet: '  Spaced content  ',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.excerpt).toBe('Spaced content');
    });

    it('returns null when excerpt is empty after trimming', () => {
      const citation = {
        ...legacyBase,
        snippet: '   ',
        text: null,
        paraphrasedSnippet: null,
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result).toBeNull();
    });

    it('full tier with empty string snippet falls back to text', () => {
      const citation = {
        ...legacyBase,
        snippet: '',
        text: 'Fallback text.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.excerpt).toBe('Fallback text.');
    });

    it('protected tier prefers paraphrasedSnippet even if snippet exists', () => {
      const citation = {
        ...legacyBase,
        copyrightTier: 'protected',
        snippet: 'Verbatim should be ignored.',
        paraphrasedSnippet: 'Paraphrased version.',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.excerpt).toBe('Paraphrased version.');
    });

    it('source is preferred over documentId for documentName when both present', () => {
      const citation = {
        source: 'Regolamento Catan',
        documentId: 'doc-guid-002',
        pageNumber: 10,
        copyrightTier: 'full',
        snippet: 'contenuto',
      } as Citation;

      const result = mapCitationToChatCitation(citation);

      expect(result?.documentName).toBe('Regolamento Catan');
    });
  });
});
