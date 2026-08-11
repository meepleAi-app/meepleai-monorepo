/** Normalize for tolerant substring matching: lowercase, strip soft hyphens, collapse whitespace. */
export function normalizeQuoteText(s: string): string {
  return s.replace(/­/g, '').toLowerCase().replace(/\s+/g, ' ').trim();
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * Best-effort per-item highlighter for react-pdf `customTextRenderer` (AC-2 Pattern A).
 * A text item is wrapped in <mark> when its normalized string (len>2) is a substring of the
 * normalized quote. Imperfect for very common short items — the caller shows a fallback banner
 * via `matched()`. FU could upgrade to contiguous-run matching or Pattern-B coordinates.
 */
export function makeQuoteTextRenderer(quote: string): {
  render: (item: { str: string }) => string;
  matched: () => boolean;
} {
  const normQuote = normalizeQuoteText(quote);
  let didMatch = false;
  return {
    render: ({ str }) => {
      const norm = normalizeQuoteText(str);
      if (norm.length > 2 && normQuote.includes(norm)) {
        didMatch = true;
        return `<mark class="pdf-quote-highlight">${escapeHtml(str)}</mark>`;
      }
      return escapeHtml(str);
    },
    matched: () => didMatch,
  };
}
