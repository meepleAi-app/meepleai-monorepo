'use client';

/**
 * PdfBBoxOverlay — %-based region overlay for the RAG citation grounding (SP-D #3408).
 *
 * Draws one rectangle per {@link CitationRegion} as an absolutely-positioned child of the
 * react-pdf `<Page>` wrapper (which is `position: relative`, react-pdf@10 Page.js:247). Because
 * the rects are normalized [0,1] top-left and expressed in percentages, the overlay is
 * scale/DPR-independent — no pixel math, no coupling to the current zoom.
 *
 * The caller is responsible for filtering `rects` to the visible page (regions carry a `page`,
 * but this component is intentionally page-agnostic and purely presentational). Decorative only
 * (`aria-hidden`) — the citation text is already conveyed by the snippet / page header.
 *
 * Spec: docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md §6.5 (DA-5)
 */

import type { ReactElement } from 'react';

import type { CitationRegion } from '@/types';

export interface PdfBBoxOverlayProps {
  readonly rects: readonly CitationRegion[];
}

export function PdfBBoxOverlay({ rects }: PdfBBoxOverlayProps): ReactElement | null {
  if (rects.length === 0) return null;

  return (
    <div
      aria-hidden="true"
      data-slot="pdf-bbox-overlay"
      data-testid="pdf-bbox-overlay"
      className="pointer-events-none absolute inset-0"
    >
      {rects.map((r, i) => (
        <div
          key={`${r.page}:${r.x},${r.y},${r.width},${r.height}:${i}`}
          data-testid="pdf-bbox-rect"
          className="pdf-bbox-highlight absolute"
          style={{
            left: `${r.x * 100}%`,
            top: `${r.y * 100}%`,
            width: `${r.width * 100}%`,
            height: `${r.height * 100}%`,
          }}
        />
      ))}
    </div>
  );
}
