import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { HeroCard } from '../HeroCard';

describe('HeroCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<HeroCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<HeroCard entity="game" title="H2" headingLevel={2} />);
    expect(screen.getByText('H2').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<HeroCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<HeroCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
