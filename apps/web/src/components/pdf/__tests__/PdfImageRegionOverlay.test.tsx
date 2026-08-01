import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PdfImageRegionOverlay } from '../PdfImageRegionOverlay';
import type { ImageRegion } from '@/lib/api/schemas';

const rect = (o: Partial<ImageRegion>): ImageRegion => ({
  page: 4,
  x: 0,
  y: 0,
  width: 0.1,
  height: 0.1,
  elementType: 'Image',
  ...o,
});

describe('PdfImageRegionOverlay', () => {
  it('renders one %-positioned rect per region', () => {
    render(<PdfImageRegionOverlay rects={[rect({ x: 0.1, y: 0.2, width: 0.3, height: 0.05 })]} />);
    const el = screen.getByTestId('pdf-image-region-rect');
    expect(el.style.left).toBe('10%');
    expect(el.style.top).toBe('20%');
    expect(el.style.width).toBe('30%');
    expect(el.style.height).toBe('5%');
  });

  it('renders one rect per region', () => {
    render(
      <PdfImageRegionOverlay
        rects={[rect({ page: 4 }), rect({ page: 4, x: 0.5 }), rect({ page: 4, y: 0.5 })]}
      />
    );
    expect(screen.getAllByTestId('pdf-image-region-rect')).toHaveLength(3);
  });

  it('renders nothing for empty rects', () => {
    const { container } = render(<PdfImageRegionOverlay rects={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
