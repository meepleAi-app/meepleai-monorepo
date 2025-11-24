import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import { GameCard } from '@/components/games/GameCard';
import { Game } from '@/lib/api';

describe('GameCard', () => {
  const mockGame: Game = {
    id: '1',
    title: 'Catan',
    publisher: 'Catan Studio',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: '2024-01-01T00:00:00Z',
  };

  it('renders game information correctly', () => {
    render(<GameCard game={mockGame} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Catan Studio')).toBeInTheDocument();
    expect(screen.getByText('3-4p')).toBeInTheDocument();
    expect(screen.getByText('60-120min')).toBeInTheDocument();
    expect(screen.getByText('1995')).toBeInTheDocument();
  });

  it('displays BGG badge when bggId exists', () => {
    render(<GameCard game={mockGame} />);
    expect(screen.getByText('BGG')).toBeInTheDocument();
  });

  it('does not display BGG badge when bggId is null', () => {
    const gameWithoutBgg = { ...mockGame, bggId: null };
    render(<GameCard game={gameWithoutBgg} />);
    expect(screen.queryByText('BGG')).not.toBeInTheDocument();
  });

  it('handles same min/max players correctly', () => {
    const game = { ...mockGame, minPlayers: 4, maxPlayers: 4 };
    render(<GameCard game={game} />);
    expect(screen.getByText('4p')).toBeInTheDocument();
  });

  it('handles same min/max playtime correctly', () => {
    const game = { ...mockGame, minPlayTimeMinutes: 90, maxPlayTimeMinutes: 90 };
    render(<GameCard game={game} />);
    expect(screen.getByText('90min')).toBeInTheDocument();
  });

  it('handles null player counts', () => {
    const game = { ...mockGame, minPlayers: null, maxPlayers: 5 };
    render(<GameCard game={game} />);
    expect(screen.getByText('?-5p')).toBeInTheDocument();
  });

  it('handles null playtimes', () => {
    const game = { ...mockGame, minPlayTimeMinutes: 30, maxPlayTimeMinutes: null };
    render(<GameCard game={game} />);
    expect(screen.getByText('30-?min')).toBeInTheDocument();
  });

  it('handles missing publisher', () => {
    const game = { ...mockGame, publisher: null };
    render(<GameCard game={game} />);
    expect(screen.queryByText('Catan Studio')).not.toBeInTheDocument();
  });

  it('calls onClick handler when clicked', () => {
    const handleClick = vi.fn();
    render(<GameCard game={mockGame} onClick={handleClick} />);

    const card = screen.getByRole('button');
    fireEvent.click(card);

    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('supports keyboard interaction when onClick is provided', () => {
    const handleClick = vi.fn();
    render(<GameCard game={mockGame} onClick={handleClick} />);

    const card = screen.getByRole('button');

    // Test Enter key
    fireEvent.keyDown(card, { key: 'Enter', code: 'Enter' });
    expect(handleClick).toHaveBeenCalledTimes(1);

    // Test Space key
    fireEvent.keyDown(card, { key: ' ', code: 'Space' });
    expect(handleClick).toHaveBeenCalledTimes(2);
  });

  it('does not have button role when onClick is not provided', () => {
    render(<GameCard game={mockGame} />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('has proper accessibility attributes', () => {
    render(<GameCard game={mockGame} onClick={vi.fn()} />);
    const card = screen.getByRole('button');
    expect(card).toHaveAttribute('aria-label', 'Game: Catan');
    expect(card).toHaveAttribute('tabIndex', '0');
  });
});
