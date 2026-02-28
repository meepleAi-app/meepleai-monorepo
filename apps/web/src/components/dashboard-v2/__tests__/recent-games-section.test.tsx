/**
 * RecentGamesSection — Unit Tests
 * Issue #5097, Epic #5094
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { RecentGamesSection } from '../recent-games-section';
import type { UserGameDto } from '@/lib/api/dashboard-client';

const makeGame = (overrides: Partial<UserGameDto> = {}): UserGameDto => ({
  id: 'game-1',
  title: 'I Coloni di Catan',
  isOwned: true,
  inWishlist: false,
  playCount: 12,
  ...overrides,
});

describe('RecentGamesSection', () => {
  it('renders loading skeletons when isLoading', () => {
    const { container } = render(
      <RecentGamesSection games={[]} isLoading={true} />
    );
    // Skeletons present, no game titles
    expect(screen.queryByText('I Coloni di Catan')).not.toBeInTheDocument();
    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0);
  });

  it('renders empty state when no games', () => {
    render(<RecentGamesSection games={[]} isLoading={false} />);

    expect(screen.getByText('Nessun gioco in libreria')).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Aggiungi il tuo primo gioco/ })
    ).toBeInTheDocument();
  });

  it('renders up to 3 games', () => {
    const games = [
      makeGame({ id: 'g1', title: 'Catan' }),
      makeGame({ id: 'g2', title: 'Wingspan' }),
      makeGame({ id: 'g3', title: 'Azul' }),
      makeGame({ id: 'g4', title: 'Pandemic' }), // should be hidden
    ];

    render(<RecentGamesSection games={games} isLoading={false} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.queryByText('Pandemic')).not.toBeInTheDocument();
  });

  it('shows "Libreria" badge for owned games', () => {
    render(
      <RecentGamesSection
        games={[makeGame({ isOwned: true })]}
        isLoading={false}
      />
    );

    expect(screen.getByText('Libreria')).toBeInTheDocument();
  });

  it('links to /library for "Vedi tutti"', () => {
    render(<RecentGamesSection games={[makeGame()]} isLoading={false} />);

    expect(screen.getByRole('link', { name: /Vedi tutti/ })).toHaveAttribute(
      'href',
      '/library'
    );
  });

  it('links each card to /library/{id}', () => {
    render(
      <RecentGamesSection
        games={[makeGame({ id: 'game-42' })]}
        isLoading={false}
      />
    );

    expect(screen.getByRole('link', { name: /I Coloni di Catan/ })).toHaveAttribute(
      'href',
      '/library/game-42'
    );
  });
});
