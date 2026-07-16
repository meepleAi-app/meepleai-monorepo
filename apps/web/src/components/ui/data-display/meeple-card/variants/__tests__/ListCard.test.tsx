import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ListCard } from '../ListCard';

describe('ListCard connections path', () => {
  it('S2: connections=[] renders no chip strip, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<ListCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <ListCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });
});

describe('ListCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<ListCard entity="game" title="Default title" />);
    expect(screen.getByText('Default title').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<ListCard entity="game" title="H2 title" headingLevel={2} />);
    expect(screen.getByText('H2 title').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<ListCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<ListCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});

describe('ListCard ariaLabel passthrough (#3010 Task 7)', () => {
  it('exposes ariaLabel as the accessible name when onClick is present', () => {
    render(<ListCard entity="game" title="X" onClick={() => {}} ariaLabel="Open game detail X" />);
    expect(screen.getByRole('button', { name: 'Open game detail X' })).toBeInTheDocument();
  });

  it('does not render aria-label when onClick is absent, even if ariaLabel is passed', () => {
    render(<ListCard entity="game" title="X" ariaLabel="Open game detail X" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByText('X').closest('div[data-entity]')).not.toHaveAttribute('aria-label');
  });
});
