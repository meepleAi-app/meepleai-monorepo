'use client';

/**
 * PdfImageRegionOverlay — %-based table-image region overlay (#3447 slice).
 *
 * Twin of {@link PdfBBoxOverlay}: draws one rectangle per hi_res Image/FigureCaption region as an
 * absolutely-positioned child of the react-pdf `<Page>` wrapper (position: relative). Regions are
 * normalized [0,1] top-left → percentage rects, scale/DPR-independent. The caller filters `rects`
 * to the visible page. Decorative only (`aria-hidden`); a distinct dashed style marks table graphics
 * vs the citation highlight.
 *
 * Spec: docs/superpowers/specs/2026-08-01-image-table-region-slice-design.md
 */

import type { ReactElement } from 'react';

import type { ImageRegion } from '@/lib/api/schemas';

export interface PdfImageRegionOverlayProps {
  readonly rects: readonly ImageRegion[];
}

export function PdfImageRegionOverlay({ rects }: PdfImageRegionOverlayProps): ReactElement | null {
  if (rects.length === 0) return null;

  return (
    <div
      aria-hidden="true"
      data-slot="pdf-image-region-overlay"
      data-testid="pdf-image-region-overlay"
      className="pointer-events-none absolute inset-0"
    >
      {rects.map((r, i) => (
        <div
          key={`${r.page}:${r.x},${r.y},${r.width},${r.height}:${i}`}
          data-testid="pdf-image-region-rect"
          className="pdf-image-region-highlight absolute"
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
