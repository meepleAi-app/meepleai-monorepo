/**
 * SkeletonCard - Unit tests (Issue #1480).
 *
 * Pure presentational loading placeholder per card. Maps from
 * sp4-hub-toolkits.jsx:454-471 (function SkeletonCard).
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Default aspect-ratio 5/3 (desktop).
 *   T3. Compact aspect-ratio 4/3 (mobile).
 *   T4. Renders 3 line placeholders.
 *   T5. DS-15 tokens on root (bg-card, border-border).
 *   T6. className composition on root.
 *   T7. Passes axe a11y scan.
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { SkeletonCard } from '../SkeletonCard';

describe('SkeletonCard (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = render(<SkeletonCard />);
    expect(
      container.querySelector('[data-slot="toolkits-index-skeleton-card"]')
    ).toBeInTheDocument();
  });

  // T2
  it('uses 5/3 aspect ratio by default (desktop)', () => {
    const { container } = render(<SkeletonCard />);
    const cover = container.querySelector('[data-slot="toolkits-index-skeleton-cover"]');
    expect(cover).toHaveClass('aspect-[5/3]');
  });

  // T3
  it('uses 4/3 aspect ratio when compact (mobile)', () => {
    const { container } = render(<SkeletonCard compact />);
    const cover = container.querySelector('[data-slot="toolkits-index-skeleton-cover"]');
    expect(cover).toHaveClass('aspect-[4/3]');
  });

  // T4
  it('renders 3 line placeholders', () => {
    const { container } = render(<SkeletonCard />);
    expect(container.querySelectorAll('[data-slot="toolkits-index-skeleton-line"]')).toHaveLength(
      3
    );
  });

  // T5
  it('uses DS-15 tokens on the root', () => {
    const { container } = render(<SkeletonCard />);
    const root = container.querySelector('[data-slot="toolkits-index-skeleton-card"]');
    expect(root).toHaveClass('bg-card');
    expect(root).toHaveClass('border-border');
  });

  // T6
  it('composes custom className with base classes', () => {
    const { container } = render(<SkeletonCard className="extra" />);
    const root = container.querySelector('[data-slot="toolkits-index-skeleton-card"]');
    expect(root).toHaveClass('extra');
  });

  // T7
  it('passes axe a11y scan', async () => {
    const { container } = render(<SkeletonCard />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
