import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Stars } from '../Stars';

describe('Stars', () => {
  it('renders with value 0 (empty stars, 0% fill)', () => {
    render(<Stars value={0} />);
    const root = screen.getByRole('img', { name: /rating: 0\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
    // Foreground layer width should be 0%
    const filled = root.querySelector('span[style*="width: 0%"]');
    expect(filled).not.toBeNull();
  });

  it('renders with value 0.5 (half star, 10% fill)', () => {
    render(<Stars value={0.5} />);
    const root = screen.getByRole('img', { name: /rating: 0\.5 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 10%"]');
    expect(filled).not.toBeNull();
  });

  it('renders with value 1 (one full star, 20% fill)', () => {
    render(<Stars value={1} />);
    const root = screen.getByRole('img', { name: /rating: 1\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 20%"]');
    expect(filled).not.toBeNull();
  });

  it('renders with value 2.5 (50% fill)', () => {
    render(<Stars value={2.5} />);
    const root = screen.getByRole('img', { name: /rating: 2\.5 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 50%"]');
    expect(filled).not.toBeNull();
  });

  it('renders with value 5 (all stars, 100% fill)', () => {
    render(<Stars value={5} />);
    const root = screen.getByRole('img', { name: /rating: 5\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 100%"]');
    expect(filled).not.toBeNull();
  });

  it('clamps values above 5 to 5 (100% fill)', () => {
    render(<Stars value={9.9} />);
    const root = screen.getByRole('img', { name: /rating: 5\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 100%"]');
    expect(filled).not.toBeNull();
  });

  it('clamps negative values to 0 (0% fill)', () => {
    render(<Stars value={-3} />);
    const root = screen.getByRole('img', { name: /rating: 0\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
    const filled = root.querySelector('span[style*="width: 0%"]');
    expect(filled).not.toBeNull();
  });

  it('handles NaN as 0 (0% fill, no crash)', () => {
    render(<Stars value={Number.NaN} />);
    const root = screen.getByRole('img', { name: /rating: 0\.0 \/ 5/i });
    expect(root).toBeInTheDocument();
  });

  it('renders the numeric suffix when `showNumeric` is true', () => {
    render(<Stars value={4.7} showNumeric />);
    expect(screen.getByText('4.7')).toBeInTheDocument();
  });

  it('does not render the numeric suffix by default', () => {
    render(<Stars value={4.7} />);
    expect(screen.queryByText('4.7')).not.toBeInTheDocument();
  });

  it('honors a custom ariaLabel when provided', () => {
    render(<Stars value={3} ariaLabel="Valutazione BGG: 3 stelle su 5" />);
    expect(screen.getByRole('img', { name: 'Valutazione BGG: 3 stelle su 5' })).toBeInTheDocument();
  });

  it('forwards a custom className onto the root span', () => {
    render(<Stars value={3} className="my-custom-class" />);
    const root = screen.getByRole('img');
    expect(root.className).toContain('my-custom-class');
    // Default classes still present
    expect(root.className).toContain('inline-flex');
  });

  it('exposes the standard data-slot for testing/styling hooks', () => {
    const { container } = render(<Stars value={3} />);
    expect(container.querySelector('[data-slot="stars"]')).not.toBeNull();
  });
});
