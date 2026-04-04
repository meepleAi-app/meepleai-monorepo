/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameTableSkeleton } from '../GameTableSkeleton';

describe('GameTableSkeleton', () => {
  it('renders desktop skeleton zones', () => {
    render(<GameTableSkeleton />);

    expect(screen.getByTestId('skeleton-tools')).toBeInTheDocument();
    expect(screen.getByTestId('skeleton-card')).toBeInTheDocument();
    expect(screen.getByTestId('skeleton-knowledge')).toBeInTheDocument();
    expect(screen.getByTestId('skeleton-sessions')).toBeInTheDocument();
  });

  it('renders mobile skeleton', () => {
    render(<GameTableSkeleton />);

    expect(screen.getByTestId('skeleton-mobile')).toBeInTheDocument();
  });

  it('has dark background matching Game Table theme', () => {
    const { container } = render(<GameTableSkeleton />);

    const root = container.firstElementChild as HTMLElement;
    expect(root.className).toContain('bg-[#0d1117]');
    expect(root.className).toContain('min-h-screen');
  });
});
