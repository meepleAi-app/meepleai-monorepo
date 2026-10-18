import type { Citation as WireCitation } from '@/lib/api/schemas/streaming.schemas';
import type { Citation } from '@/types';

/**
 * #4411: the citations of an assistant answer, in the shape `RuleSourceCard` shows. The server already gated
 * them by tier; this keeps the gate on the client too: a Protected citation never shows verbatim text.
 */
export function toRuleSourceCitations(
  citations: readonly WireCitation[] | null | undefined
): Citation[] {
  return (citations ?? []).flatMap(c => {
    const pageNumber = c.pageNumber ?? c.page;
    if (pageNumber == null) return [];
    const full = c.copyrightTier === 'full';
    return [
      {
        documentId: c.documentId ?? '',
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
