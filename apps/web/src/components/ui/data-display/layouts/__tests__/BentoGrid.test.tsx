import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { BentoGrid, BentoGridItem } from '../BentoGrid';

describe('BentoGrid', () => {
  it('renders children in a grid', () => {
    render(
      <BentoGrid>
        <BentoGridItem area="feat">
          <div>Featured</div>
        </BentoGridItem>
        <BentoGridItem area="agent">
          <div>Agent</div>
        </BentoGridItem>
      </BentoGrid>
    );
    expect(screen.getByText('Featured')).toBeTruthy();
    expect(screen.getByText('Agent')).toBeTruthy();
  });

  it('applies grid class', () => {
    const { container } = render(
      <BentoGrid>
        <BentoGridItem area="feat">
          <div>F</div>
        </BentoGridItem>
      </BentoGrid>
    );
    const grid = container.firstChild as HTMLElement;
    expect(grid.className).toContain('grid');
  });

  it('BentoGridItem applies grid-area style', () => {
    const { container } = render(
      <BentoGrid>
        <BentoGridItem area="feat">
          <div>F</div>
        </BentoGridItem>
      </BentoGrid>
    );
    const item = container.querySelector('[style*="grid-area"]');
    expect(item).toBeTruthy();
  });
});
