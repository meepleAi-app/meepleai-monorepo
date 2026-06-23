import type { ChatCitation } from '@/components/chat/panel/ChatCitationCard';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

/**
 * Maps a streaming RAG citation to the ChatCitationCard model.
 *
 * Tier-aware mapping:
 * - 'full' tier: uses the verbatim snippet or text
 * - 'protected' tier: uses the paraphrasedSnippet only (never verbatim)
 *
 * Returns null when there is no displayable excerpt (no empty-excerpt card).
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
    // For 'full' tier, prefer snippet, fall back to text, then empty string
    const candidateSnippet = (citation.snippet ?? '').trim();
    excerpt = candidateSnippet || (citation.text ?? '').trim();
  }

  if (!excerpt) {
    return null;
  }

  return {
    documentName: citation.source,
    pages: page != null ? [page] : [],
    excerpt,
  };
}
