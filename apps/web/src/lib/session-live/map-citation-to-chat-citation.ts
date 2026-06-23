import type { ChatCitation } from '@/components/chat/panel/ChatCitationCard';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

/**
 * Maps a streaming RAG citation to the ChatCitationCard model.
 *
 * Tier-aware mapping:
 * - 'full' tier: uses snippetPreview (real wire), falling back to snippet/text (legacy)
 * - 'protected' tier: uses the paraphrasedSnippet only (never verbatim)
 *
 * Returns null when there is no displayable excerpt (no empty-excerpt card).
 *
 * C2 (#2500): aligned to real wire format (BE CitationDto in Contracts.cs:137-144):
 * - `snippetPreview` is the real wire field for full-tier verbatim snippet
 * - `documentName` falls back to documentId when `source` is absent (real wire has no `source`)
 *
 * NOTE: The real wire does not expose a human-readable document name (only documentId).
 * `documentName` will be the documentId (a GUID) when `source` is not provided by the
 * caller. ChatCitationCard renders it as-is — a follow-up BE enhancement should add
 * `documentName`/`fileName` to CitationDto. See task-8-report.md § Limitations.
 *
 * @param citation - The RAG citation from streaming response
 * @returns ChatCitation or null if no excerpt can be extracted
 */
export function mapCitationToChatCitation(citation: Citation): ChatCitation | null {
  const page = citation.pageNumber ?? citation.page ?? null;

  let excerpt = '';

  if (citation.copyrightTier === 'protected') {
    excerpt = (citation.paraphrasedSnippet ?? '').trim();
  } else {
    // For 'full' tier: prefer snippetPreview (real wire), then snippet, then text (legacy)
    const candidateSnippet = (citation.snippetPreview ?? citation.snippet ?? '').trim();
    excerpt = candidateSnippet || (citation.text ?? '').trim();
  }

  if (!excerpt) {
    return null;
  }

  // documentName: real wire has no `source` — fall back to documentId.
  // This produces a GUID when coming from the BE wire; cosmetic limitation (see jsdoc above).
  const documentName = citation.source ?? citation.documentId ?? '';

  return {
    documentName,
    pages: page != null ? [page] : [],
    excerpt,
  };
}
