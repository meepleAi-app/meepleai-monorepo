import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { InlineGamePicker } from '../InlineGamePicker';

const mockGames = [
  { id: 'g1', title: 'Catan', thumbnailUrl: '', minPlayers: 3, maxPlayers: 4 },
  { id: 'g2', title: 'Ticket to Ride', thumbnailUrl: '', minPlayers: 2, maxPlayers: 5 },
  { id: 'g3', title: 'Gloomhaven', thumbnailUrl: '', minPlayers: 1, maxPlayers: 4 },
];

describe('InlineGamePicker', () => {
  it('renders available games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('calls onSelect when a game is clicked', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();
    render(<InlineGamePicker games={mockGames} onSelect={onSelect} />);
    await user.click(screen.getByText('Catan'));
    expect(onSelect).toHaveBeenCalledWith(mockGames[0]);
  });

  it('filters by player count', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={5} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('shows empty state when no games match', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={10} />);
    expect(screen.getByText(/nessun gioco adatto/i)).toBeInTheDocument();
  });

  it('excludes already selected games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} excludeIds={['g1']} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });
});
