import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameDrawerContent } from './GameDrawerContent';

describe('GameDrawerContent', () => {
  const game = {
    id: 'g1',
    title: 'Catan',
    publisher: 'Kosmos',
    description: 'Classico di Klaus Teuber',
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 75,
    sessionCount: 5,
    chatCount: 2,
  };

  it('renders Info tab by default with game description', () => {
    render(<GameDrawerContent game={game} />);
    expect(screen.getByText('Classico di Klaus Teuber')).toBeInTheDocument();
  });

  it('switches to Sessioni tab', () => {
    render(<GameDrawerContent game={game} />);
    fireEvent.click(screen.getByRole('tab', { name: /sessioni/i }));
    expect(screen.getByText(/5 sessioni/i)).toBeInTheDocument();
  });

  it('uses entity game color on header', () => {
    const { container } = render(<GameDrawerContent game={game} />);
    expect(container.querySelector('.bg-entity-game\\/10')).toBeInTheDocument();
  });
});
