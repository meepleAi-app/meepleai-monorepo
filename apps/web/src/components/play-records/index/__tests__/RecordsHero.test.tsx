/**
 * RecordsHero Component Tests (Minimal - Task 1)
 *
 * Basic smoke tests for RecordsHero rendering.
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

import { RecordsHero } from '../RecordsHero';

describe('RecordsHero', () => {
  it('renders without crashing with isLoading=false', () => {
    const { container } = render(<RecordsHero isLoading={false} />);
    expect(container).toBeInTheDocument();
  });

  it('renders without crashing with isLoading=true', () => {
    const { container } = render(<RecordsHero isLoading={true} />);

    // Should display 4 skeleton boxes
    const skeletons = container.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThanOrEqual(4);
  });

  it('contains a link to /play-records/new', () => {
    const { container } = render(<RecordsHero isLoading={false} />);

    const link = container.querySelector('a[href="/play-records/new"]');
    expect(link).toBeInTheDocument();
  });

  it('applies semantic styling', () => {
    const { container } = render(<RecordsHero isLoading={false} />);

    const heroDiv = container.firstChild;
    expect(heroDiv).toHaveClass('flex-col', 'relative', 'overflow-hidden');
  });
});
