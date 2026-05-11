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
  title: 'Le tue partite',
  subtitle: 'Riepilogo dei giochi che hai giocato.',
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
    expect(screen.getByText('Le tue partite')).toBeTruthy();
    expect(screen.getByText('Riepilogo dei giochi che hai giocato.')).toBeTruthy();
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
