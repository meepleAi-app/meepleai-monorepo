import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SharedGamesHero } from './shared-games-hero';

describe('SharedGamesHero (v2)', () => {
  const defaults = {
    badgeLabel: 'Catalogo community',
    headingLead: 'Giochi',
    headingHighlight: 'condivisi',
    headingTail: 'dalla community',
    subtitle: 'Esplora il catalogo costruito dalla community.',
  };

  it('renders badge, heading parts and subtitle', () => {
    render(<SharedGamesHero {...defaults} />);
    expect(screen.getByText('Catalogo community')).toBeInTheDocument();
    expect(screen.getByText('condivisi')).toBeInTheDocument();
    expect(screen.getByText('Esplora il catalogo costruito dalla community.')).toBeInTheDocument();
  });

  it('emits a single h1 for SEO', () => {
    const { container } = render(<SharedGamesHero {...defaults} />);
    expect(container.querySelectorAll('h1')).toHaveLength(1);
  });

  it('applies the brand gradient to the highlighted token', () => {
    render(<SharedGamesHero {...defaults} />);
    expect(screen.getByText('condivisi').className).toContain('bg-gradient-to-r');
  });

  it('uses compact padding when compact=true', () => {
    const { container } = render(<SharedGamesHero {...defaults} compact />);
    const root = container.querySelector('[data-slot="shared-games-hero"]');
    expect(root?.className).toContain('px-4');
    expect(root?.className).not.toContain('px-6');
  });

  it('uses default padding when compact is omitted', () => {
    const { container } = render(<SharedGamesHero {...defaults} />);
    const root = container.querySelector('[data-slot="shared-games-hero"]');
    expect(root?.className).toContain('px-6');
  });

  it('omits trailing tail when not provided', () => {
    const { headingTail: _drop, ...withoutTail } = defaults;
    void _drop;
    render(<SharedGamesHero {...withoutTail} />);
    expect(screen.queryByText('dalla community')).not.toBeInTheDocument();
  });
});
