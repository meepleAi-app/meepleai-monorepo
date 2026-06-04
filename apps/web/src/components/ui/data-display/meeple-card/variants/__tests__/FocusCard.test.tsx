import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { FocusCard } from '../FocusCard';

describe('FocusCard connections path', () => {
  it('S2: connections=[] renders no chip strip, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<FocusCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <FocusCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });
});

describe('FocusCard headingLevel prop (#1842)', () => {
  it('renders <h2> by default (preserves existing behavior — FocusCard is page-hero-level)', () => {
    render(<FocusCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H2');
  });

  it('renders <h3> when headingLevel={3}', () => {
    render(<FocusCard entity="game" title="H3" headingLevel={3} />);
    expect(screen.getByText('H3').tagName).toBe('H3');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<FocusCard entity="game" title="X" />);
    const h2Class = screen.getByText('X').className;
    rerender(<FocusCard entity="game" title="X" headingLevel={3} />);
    expect(screen.getByText('X').className).toBe(h2Class);
  });
});
