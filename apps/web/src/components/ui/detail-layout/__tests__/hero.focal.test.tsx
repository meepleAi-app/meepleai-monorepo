/**
 * @vitest-environment jsdom
 */
import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { Hero, type HeroLabels } from '../hero';

const labels: HeroLabels = {
  entityLabel: 'Game',
  ratingAriaLabel: 'Rating',
  ratingOf: 'of',
  toolkitsLabel: 'Toolkits',
  agentsLabel: 'Agents',
  kbsLabel: 'KBs',
  metaPlayers: 'players',
  metaMinutes: 'min',
  metaComplexity: 'Complexity',
  metaAuthor: 'by',
};

describe('Hero — punto focale (#3611)', () => {
  it('inquadra la parte alta quando il focal lo richiede', () => {
    // L'<img> ha alt="" → ruolo accessibile "presentation" (dom-accessibility-api),
    // quindi getByRole('img') non lo trova mai. Segue il pattern già usato in
    // hero.test.tsx (container.querySelector('img')) invece di getByRole.
    const { container } = render(
      <Hero
        title="Catan"
        coverUrl="https://r2.example/c.webp"
        coverFocal={{ x: 0.5, y: 0.2 }}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(container.querySelector('img')).toHaveStyle({ objectPosition: '50% 20%' });
  });

  it('non emette stile inline senza focal', () => {
    const { container } = render(
      <Hero
        title="Catan"
        coverUrl="https://r2.example/c.webp"
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(container.querySelector('img')).not.toHaveAttribute('style');
  });
});
