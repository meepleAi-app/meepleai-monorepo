import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { FeaturedCard } from '../FeaturedCard';

describe('FeaturedCard connections path', () => {
  it('S2: connections=[] renders no chip strip, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<FeaturedCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <FeaturedCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });
});

describe('FeaturedCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<FeaturedCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<FeaturedCard entity="game" title="H2" headingLevel={2} />);
    expect(screen.getByText('H2').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<FeaturedCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<FeaturedCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
