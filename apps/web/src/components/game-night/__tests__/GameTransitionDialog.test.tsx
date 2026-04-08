import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GameTransitionDialog } from '../GameTransitionDialog';

describe('GameTransitionDialog', () => {
  const defaultProps = {
    open: true,
    completedGameTitle: 'Catan',
    availableGames: [
      { id: 'g2', title: 'Dixit' },
      { id: 'g3', title: 'Uno' },
    ],
    onSelectGame: vi.fn(),
    onFinishNight: vi.fn(),
    onClose: vi.fn(),
  };

  it('shows completed game and available next games', () => {
    render(<GameTransitionDialog {...defaultProps} />);
    expect(screen.getByText(/Catan completato/i)).toBeInTheDocument();
    expect(screen.getByText('Dixit')).toBeInTheDocument();
    expect(screen.getByText('Uno')).toBeInTheDocument();
  });

  it('shows winner name when provided', () => {
    render(<GameTransitionDialog {...defaultProps} winnerName="Marco" />);
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });

  it('calls onSelectGame when a game is picked', () => {
    const onSelect = vi.fn();
    render(<GameTransitionDialog {...defaultProps} onSelectGame={onSelect} />);
    fireEvent.click(screen.getByText('Dixit'));
    expect(onSelect).toHaveBeenCalledWith('g2', 'Dixit');
  });

  it('calls onFinishNight when termina serata is clicked', () => {
    const onFinish = vi.fn();
    render(<GameTransitionDialog {...defaultProps} onFinishNight={onFinish} />);
    fireEvent.click(screen.getByText(/termina serata/i));
    expect(onFinish).toHaveBeenCalled();
  });
});
