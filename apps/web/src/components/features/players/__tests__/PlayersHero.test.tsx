/**
 * PlayersHero unit tests — Wave 4 D1 (Issue #682).
 *
 * TDD red phase: written before the component implementation.
 * Mirror pattern from AgentsHero.test.tsx (Wave B.2 reference).
 *
 * 4 tests:
 * 1. Renders data-slot="players-hero"
 * 2. Renders title and subtitle from labels
 * 3. Renders totalSessions KPI tile
 * 4. Renders distinctGames KPI tile
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PlayersHero } from '../PlayersHero';
import type { PlayersHeroProps } from '../PlayersHero';

const LABELS: PlayersHeroProps['labels'] = {
  // F28 #1974: copy disambiguates /players (gaming partners) from /play-records.
  title: 'I tuoi compagni di gioco',
  subtitle: 'Vedi con chi hai giocato di più.',
  totalPlays: 'Sessioni totali',
  distinctGames: 'Giochi distinti',
};

const DEFAULT_PROPS: PlayersHeroProps = {
  totalSessions: 30,
  distinctGames: 5,
  labels: LABELS,
};

describe('PlayersHero', () => {
  it('renders data-slot="players-hero"', () => {
    render(<PlayersHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="players-hero"]')).not.toBeNull();
  });

  it('renders title and subtitle from labels', () => {
    render(<PlayersHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('I tuoi compagni di gioco')).toBeTruthy();
    expect(screen.getByText('Vedi con chi hai giocato di più.')).toBeTruthy();
  });

  it('renders totalSessions KPI tile with count', () => {
    render(<PlayersHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('Sessioni totali')).toBeTruthy();
    expect(screen.getByText('30')).toBeTruthy();
  });

  it('renders distinctGames KPI tile with count', () => {
    render(<PlayersHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('Giochi distinti')).toBeTruthy();
    expect(screen.getByText('5')).toBeTruthy();
  });
});
