/**
 * PdfBBoxOverlay — presentational %-based region overlay (SP-D #3408).
 *
 * Pure component: given normalized [0,1] top-left CitationRegion rects (already
 * filtered to the visible page by the caller), renders one absolutely-positioned
 * rectangle per rect as a child of the react-pdf <Page> wrapper.
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { CitationRegion } from '@/types';

import { PdfBBoxOverlay } from '../PdfBBoxOverlay';

function rect(overrides: Partial<CitationRegion> = {}): CitationRegion {
  return { page: 5, x: 0.1, y: 0.2, width: 0.3, height: 0.05, ...overrides };
}

describe('PdfBBoxOverlay', () => {
  it('renders one pdf-bbox-rect per region', () => {
    render(<PdfBBoxOverlay rects={[rect(), rect({ y: 0.5 }), rect({ y: 0.8 })]} />);
    expect(screen.getAllByTestId('pdf-bbox-rect')).toHaveLength(3);
  });

  it('maps normalized [0,1] coords to left/top/width/height percentages', () => {
    render(<PdfBBoxOverlay rects={[rect({ x: 0.1, y: 0.2, width: 0.3, height: 0.05 })]} />);
    const el = screen.getByTestId('pdf-bbox-rect');
    expect(el.style.left).toBe('10%');
    expect(el.style.top).toBe('20%');
    expect(el.style.width).toBe('30%');
    expect(el.style.height).toBe('5%');
  });

  it('uses top-left y-down convention: a rect near the top has a SMALL top% (no mirroring)', () => {
    render(<PdfBBoxOverlay rects={[rect({ y: 0.02, height: 0.04 })]} />);
    const el = screen.getByTestId('pdf-bbox-rect');
    // top-left convention → top ≈ 2% (NOT 1 - 0.02 = 98%)
    expect(el.style.top).toBe('2%');
  });

  it('renders nothing when rects is empty', () => {
    const { container } = render(<PdfBBoxOverlay rects={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('is decorative (aria-hidden) and does not capture pointer events', () => {
    render(<PdfBBoxOverlay rects={[rect()]} />);
    const overlay = screen.getByTestId('pdf-bbox-overlay');
    expect(overlay).toHaveAttribute('aria-hidden', 'true');
    expect(overlay.className).toContain('pointer-events-none');
  });
});
