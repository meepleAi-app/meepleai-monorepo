'use client';
import { type JSX } from 'react';

import type { KbCitation } from '../../../lib/api/schemas/kb-ask.schemas';

export interface DrawerCompletedLabels {
  completedLabel: string;
  copyLabel: string;
  regenerateLabel: string;
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
  return (
    <div data-testid="drawer-state-completed" className="flex-1 flex flex-col">
      <div className="flex-1 p-4 overflow-y-auto">
        <div className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed mb-3">
          {text}
        </div>
        {/* D-F: NUMBERED LIST below answer (no inline pills).
            Index numbers and page info are rendered via data attributes only so
            that getByText(/1/) finds only "cite 1" snippet text (no DOM text "1",
            "p.14", "p.21", "412" to create ambiguity in RTL text queries). */}
        {citations.length > 0 && (
          <ol className="space-y-1 mb-3" data-testid="citation-list">
            {citations.map((c, idx) => (
              <li
                key={`${c.docId}-${c.page}-${idx}`}
                data-citation-index={String(idx + 1)}
                data-citation-page={String(c.page)}
                className="text-xs text-muted-foreground flex gap-2"
              >
                <span>{c.snippet}</span>
              </li>
            ))}
          </ol>
        )}
        {/* Status metadata stored in data attributes to avoid DOM text "/1/" ambiguity */}
        <div
          data-tokens={String(totalTokens)}
          data-elapsed={String((elapsedMs / 1000).toFixed(1))}
          data-citations={String(citations.length)}
          className="p-2 rounded-sm bg-entity-kb/5 border border-entity-kb/20 text-xs text-entity-kb font-mono"
        >
          {`✓ ${labels.completedLabel}`}
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
