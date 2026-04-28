import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SkeletonCard } from './skeleton-card';

describe('SkeletonCard (v2)', () => {
  it('renders with data-slot=shared-games-skeleton-card', () => {
    const { container } = render(<SkeletonCard />);
    expect(container.querySelector('[data-slot="shared-games-skeleton-card"]')).not.toBeNull();
  });

  it('hides itself from a11y tree (aria-hidden)', () => {
    const { container } = render(<SkeletonCard />);
    expect(container.querySelector('[data-slot="shared-games-skeleton-card"]')).toHaveAttribute(
      'aria-hidden',
      'true'
    );
  });

  it('uses default cover height (116px) when compact is false', () => {
    const { container } = render(<SkeletonCard />);
    expect(container.querySelector('.h-\\[116px\\]')).not.toBeNull();
  });

  it('uses reduced cover height (96/h-24) when compact is true', () => {
    const { container } = render(<SkeletonCard compact />);
    expect(container.querySelector('.h-24')).not.toBeNull();
    expect(container.querySelector('.h-\\[116px\\]')).toBeNull();
  });

  it('applies mai-shimmer class with motion-reduce guard', () => {
    const { container } = render(<SkeletonCard />);
    const shimmerEls = container.querySelectorAll('.mai-shimmer');
    expect(shimmerEls.length).toBeGreaterThanOrEqual(4);
    shimmerEls.forEach(el => {
      expect(el.className).toContain('motion-reduce:animate-none');
    });
  });

  it('forwards className', () => {
    const { container } = render(<SkeletonCard className="custom-x" />);
    expect(
      container.querySelector('[data-slot="shared-games-skeleton-card"]')?.className
    ).toContain('custom-x');
  });
});
