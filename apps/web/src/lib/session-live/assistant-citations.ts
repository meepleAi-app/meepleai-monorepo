import type { Citation as WireCitation } from '@/lib/api/schemas/streaming.schemas';
import { citedPage } from '@/lib/citations/citedPage';
import type { Citation } from '@/types';

/**
 * #4411: the citations of an assistant answer, in the shape `RuleSourceCard` shows. The server already gated
 * them by tier; this keeps the gate on the client too: a Protected citation never shows verbatim text.
 */
export function toRuleSourceCitations(
  citations: readonly WireCitation[] | null | undefined
): Citation[] {
  return (citations ?? []).flatMap(c => {
    const pageNumber = citedPage(c.pageNumber ?? c.page);
    // A source with neither a document nor a page points nowhere.
    if (pageNumber === null && !c.documentId) return [];
    const full = c.copyrightTier === 'full';
    return [
      {
        documentId: c.documentId ?? '',
        // #4819: a citation without a page is still a citation: the page is left out, not the source.
        pageNumber,
        snippet: full ? (c.snippetPreview ?? c.snippet ?? '') : '',
        relevanceScore: c.relevanceScore ?? c.score ?? 0,
        copyrightTier: c.copyrightTier,
        paraphrasedSnippet: c.paraphrasedSnippet ?? undefined,
        isPublic: c.isPublic,
        regions: full ? (c.regions ?? null) : null,
        charStart: full ? (c.charStart ?? null) : null,
        charEnd: full ? (c.charEnd ?? null) : null,
      },
    ];
  });
}
