/**
 * parseCitationMarkers — pure utility for inline citation pill rendering.
 *
 * Splits answer text on the strict pattern /\[(\d+(?:,\s*\d+)*)\]/g and
 * substitutes valid 1-based indices with <CitationPill> elements.
 *
 * Invalid indices ([0] or N > citations.length) render as literal text + warn
 * (deduplicated per call). Markdown links [text](url) and copyright markers
 * [ref:docId:page] are excluded by the strict regex (they contain non-digit
 * characters inside the brackets).
 *
 * Issue #1703 D-1703-D (spec-panel 2026-05-30).
 *
 * @see apps/web/src/components/features/kb-globale/CitationPill.tsx
 * @see apps/web/src/lib/api/schemas/kb-ask.schemas.ts (KbCitation)
 */
import { type ReactNode } from 'react';

import { CitationPill } from '@/components/features/kb-globale/CitationPill';
import type { KbCitation } from '@/lib/api/schemas/kb-ask.schemas';

// eslint-disable-next-line security/detect-unsafe-regex -- regex is bounded by bracket delimiters; no catastrophic backtracking risk in practice
const MARKER_REGEX = /\[(\d+(?:,\s*\d+)*)\]/g;

export interface ParseCitationMarkersOptions {
  /** Builds an i18n-aware aria-label for each pill. */
  formatAriaLabel: (citation: KbCitation, n: number) => string;
  /** Optional click handler forwarded to each pill. */
  onCitationClick?: (link: { docId: string; page: number }) => void;
  /**
   * Optional ref-text builder. Default: `p.{page}`.
   * Override for custom display (e.g. localized "Pagina {n}").
   */
  formatRefText?: (citation: KbCitation, n: number) => string;
}

/**
 * Parses inline `[N]` and `[N,M,...]` markers in answer text and returns an
 * array of React nodes: strings for free text and `<CitationPill>` elements
 * for valid indices. Invalid indices remain as literal text and emit a
 * deduplicated `console.warn`.
 */
export function parseCitationMarkers(
  text: string,
  citations: readonly KbCitation[],
  options: ParseCitationMarkersOptions
): ReactNode[] {
  if (text.length === 0) {
    return [''];
  }

  const { formatAriaLabel, onCitationClick, formatRefText } = options;
  const refText = formatRefText ?? ((c: KbCitation) => `p.${c.page}`);

  const nodes: ReactNode[] = [];
  const warnedIndices = new Set<number>();
  let lastIndex = 0;
  let pillKey = 0;

  // Iterate matches of the strict regex.
  for (const match of text.matchAll(MARKER_REGEX)) {
    const matchStart = match.index;
    if (matchStart === undefined) continue;

    // Emit any free text accumulated since the last match.
    if (matchStart > lastIndex) {
      nodes.push(text.slice(lastIndex, matchStart));
    }

    // Parse the indices inside the brackets (e.g. "1" or "1,2,3").
    const inner = match[1] ?? '';
    const indices = inner.split(',').map(s => Number.parseInt(s.trim(), 10));

    const validPillsForThisMatch: ReactNode[] = [];
    for (const n of indices) {
      if (!Number.isInteger(n) || n < 1 || n > citations.length) {
        if (!warnedIndices.has(n)) {
          console.warn(
            `[parseCitationMarkers] Citation marker [${n}] out of bounds (${citations.length} citations)`
          );
          warnedIndices.add(n);
        }
        continue;
      }
      const citation = citations[n - 1];
      if (citation === undefined) continue;
      validPillsForThisMatch.push(
        <CitationPill
          key={`citation-pill-${pillKey++}-${n}`}
          n={n}
          refText={refText(citation, n)}
          docId={citation.docId}
          page={citation.page}
          ariaLabel={formatAriaLabel(citation, n)}
          onClick={onCitationClick}
        />
      );
    }

    if (validPillsForThisMatch.length > 0) {
      // At least one valid pill in this match → emit them (mixed valid/invalid
      // matches still emit the valid pills only; the invalid indices contribute
      // a warn but no literal text — they're swallowed by the match).
      nodes.push(...validPillsForThisMatch);
    } else {
      // All indices invalid → render the original match as literal text.
      nodes.push(match[0]);
    }

    lastIndex = matchStart + match[0].length;
  }

  // Trailing free text after the last match (or full text if no matches).
  if (lastIndex < text.length) {
    nodes.push(text.slice(lastIndex));
  }

  // Empty array would break consumers that map over nodes; guarantee >= 1 node.
  if (nodes.length === 0) {
    nodes.push(text);
  }

  return nodes;
}
