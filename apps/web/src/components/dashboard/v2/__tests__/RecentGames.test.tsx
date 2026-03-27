/**
 * RecentGames — Unit Tests
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { RecentGames } from '../RecentGames';
import type { GameItem } from '../RecentGames';

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: any) => (
    <div data-testid={`meeple-card-${props.entity}`}>{props.loading ? 'loading' : props.title}</div>
  ),
}));

const makeGame = (overrides: Partial<GameItem> = {}): GameItem => ({
  id: 'game-1',
  title: 'I Coloni di Catan',
  ...overrides,
});

describe('RecentGames', () => {
  it('renders section title and "Vedi tutti" link', () => {
    render(<RecentGames games={[makeGame()]} />);

    expect(screen.getByText(/Giochi Recenti/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /Vedi tutti/i });
    expect(link).toHaveAttribute('href', '/library');
  });

  it('renders MeepleCard for each game', () => {
    const games = [
      makeGame({ id: 'g1', title: 'Catan' }),
      makeGame({ id: 'g2', title: 'Wingspan' }),
      makeGame({ id: 'g3', title: 'Azul' }),
    ];

    render(<RecentGames games={games} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('renders empty state when games is empty', () => {
    render(<RecentGames games={[]} />);

    expect(screen.getByText('La tua libreria è vuota')).toBeInTheDocument();
    const cta = screen.getByRole('link', { name: /Esplora la libreria/i });
    expect(cta).toHaveAttribute('href', '/library');
  });

  it('renders 3 loading skeletons when loading is true', () => {
    render(<RecentGames games={[]} loading={true} />);

    const cards = screen.getAllByTestId('meeple-card-game');
    expect(cards).toHaveLength(3);
    cards.forEach(card => {
      expect(card).toHaveTextContent('loading');
    });
  });
});
