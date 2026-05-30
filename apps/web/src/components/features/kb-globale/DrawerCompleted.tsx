'use client';
import { type JSX } from 'react';

import { parseCitationMarkers } from '@/lib/citations';

import type { KbCitation } from '../../../lib/api/schemas/kb-ask.schemas';

export interface DrawerCompletedLabels {
  completedLabel: string;
  copyLabel: string;
  regenerateLabel: string;
  /** Optional i18n template for inline pill aria-label. If omitted, defaults to:
   *  "Citation N, document {docId}, page {page}" (English-y, but works in any locale). */
  inlineCitationAriaLabel?: (
    citation: { docId: string; page: number; snippet: string },
    n: number
  ) => string;
}

export function DrawerCompleted({
  text,
  citations,
  totalTokens,
  elapsedMs,
  onCopy,
  onRegenerate,
  labels,
}: {
  text: string;
  citations: readonly KbCitation[];
  totalTokens: number;
  elapsedMs: number;
  onCopy?: () => void;
  onRegenerate?: () => void;
  labels: DrawerCompletedLabels;
}): JSX.Element {
  const defaultAriaLabel = (c: { docId: string; page: number }, n: number): string =>
    `Citation ${n}, document ${c.docId}, page ${c.page}`;
  const ariaLabelBuilder = labels.inlineCitationAriaLabel ?? defaultAriaLabel;

  const parsedNodes = parseCitationMarkers(text, citations, {
    formatAriaLabel: (c, n) => ariaLabelBuilder(c, n),
  });

  // Inline path is active when the parser yielded at least one ReactElement
  // (i.e. at least one CitationPill). Otherwise fall back to the numbered list.
  // UX safety invariant (D-1703-D): NEVER both inline pills AND fallback list.
  const hasInlinePills = parsedNodes.some(node => typeof node !== 'string');

  return (
    <div data-testid="drawer-state-completed" className="flex-1 flex flex-col">
      <div className="flex-1 p-4 overflow-y-auto">
        <div
          className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed mb-3"
          data-testid="drawer-completed-answer"
          data-render-mode={hasInlinePills ? 'inline' : 'fallback'}
        >
          {hasInlinePills ? parsedNodes : text}
        </div>
        {/* D-F fallback: numbered list below answer (rendered only when no inline
            pills were produced — never both — UX safety invariant) */}
        {!hasInlinePills && citations.length > 0 && (
          <ol className="space-y-1 mb-3" data-testid="citation-list">
            {citations.map((c, idx) => (
              <li
                key={`${c.docId}-${c.page}-${idx}`}
                data-citation-index={String(idx + 1)}
                data-citation-page={String(c.page)}
                className="text-xs text-muted-foreground flex gap-2"
              >
                <span className="font-mono font-bold text-entity-kb">{idx + 1}</span>
                <span>
                  p.{c.page} · {c.snippet}
                </span>
              </li>
            ))}
          </ol>
        )}
        <div
          data-tokens={String(totalTokens)}
          data-elapsed={String((elapsedMs / 1000).toFixed(1))}
          data-citations={String(citations.length)}
          className="p-2 rounded-sm bg-entity-kb/5 border border-entity-kb/20 text-xs text-entity-kb font-mono"
        >
          ✓ {labels.completedLabel} · {totalTokens} tokens · {(elapsedMs / 1000).toFixed(1)}s ·{' '}
          {citations.length} citations
        </div>
      </div>
      <div className="p-3 border-t border-border bg-card flex gap-2">
        {onCopy && (
          <button type="button" onClick={onCopy} className="text-xs px-3 py-1 rounded-md bg-muted">
            {labels.copyLabel}
          </button>
        )}
        {onRegenerate && (
          <button
            type="button"
            onClick={onRegenerate}
            className="text-xs px-3 py-1 rounded-md bg-muted"
          >
            {labels.regenerateLabel}
          </button>
        )}
      </div>
    </div>
  );
}
