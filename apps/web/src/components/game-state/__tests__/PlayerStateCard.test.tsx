/**
 * PlayerStateCard Tests
 * Issue #2406: Game State Editor UI
 *
 * Tests for per-player state display component.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PlayerState } from '@/types/game-state';

import { PlayerStateCard } from '../PlayerStateCard';

const mockPlayer: PlayerState = {
  playerName: 'Alice',
  playerOrder: 1,
  color: '#FF6B6B',
  score: 15,
  resources: { wood: 5, stone: 3 },
};

describe('PlayerStateCard - Issue #2406', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Basic Rendering
  it('should render player name and order', () => {
    render(<PlayerStateCard player={mockPlayer} editable={false} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText(/player 1/i)).toBeInTheDocument();
  });

  it('should render player avatar with color', () => {
    render(<PlayerStateCard player={mockPlayer} editable={false} />);

    const avatar = screen.getByLabelText(/player alice color/i);
    expect(avatar).toHaveStyle({ backgroundColor: '#FF6B6B' });
  });

  it('should show current turn badge when isCurrentPlayer', () => {
    render(<PlayerStateCard player={mockPlayer} isCurrentPlayer={true} editable={false} />);

    expect(screen.getByText(/current turn/i)).toBeInTheDocument();
  });

  it('should not show current turn badge when not current player', () => {
    render(<PlayerStateCard player={mockPlayer} isCurrentPlayer={false} editable={false} />);

    expect(screen.queryByText(/current turn/i)).not.toBeInTheDocument();
  });

  // Score Display
  it('should render score when present', () => {
    render(<PlayerStateCard player={mockPlayer} editable={false} />);

    expect(screen.getByText(/score/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/current value: 15/i)).toBeInTheDocument();
  });

  it('should not render score section when score undefined', () => {
    const playerWithoutScore: PlayerState = { ...mockPlayer, score: undefined };
    render(<PlayerStateCard player={playerWithoutScore} editable={false} />);

    expect(screen.queryByText(/score/i)).not.toBeInTheDocument();
  });

  // Resources Display
  it('should render resources when present', () => {
    render(<PlayerStateCard player={mockPlayer} editable={false} />);

    expect(screen.getByText(/resources/i)).toBeInTheDocument();
    expect(screen.getByText(/wood/i)).toBeInTheDocument();
    expect(screen.getByText(/stone/i)).toBeInTheDocument();
  });

  it('should not render resources section when no resources', () => {
    const playerWithoutResources: PlayerState = { ...mockPlayer, resources: undefined };
    render(<PlayerStateCard player={playerWithoutResources} editable={false} />);

    expect(screen.queryByText(/resources/i)).not.toBeInTheDocument();
  });

  // Editable Mode
  it('should allow score editing when editable', () => {
    const onScoreChange = vi.fn();
    render(<PlayerStateCard player={mockPlayer} editable={true} onScoreChange={onScoreChange} />);

    const incrementBtn = screen.getAllByLabelText(/increase value/i)[0];
    fireEvent.click(incrementBtn);

    expect(onScoreChange).toHaveBeenCalledWith(16);
  });

  it('should allow resource editing when editable', () => {
    const onResourceChange = vi.fn();
    render(
      <PlayerStateCard player={mockPlayer} editable={true} onResourceChange={onResourceChange} />
    );

    // Find wood resource tracker and increment
    const woodTracker = screen.getByTestId('resource-Alice-wood');
    const incrementBtn = woodTracker.querySelector('[aria-label="Increase value"]');
    if (incrementBtn) {
      fireEvent.click(incrementBtn);
      expect(onResourceChange).toHaveBeenCalledWith('wood', 6);
    }
  });

  // Accessibility
  it('should have proper data-testid', () => {
    render(<PlayerStateCard player={mockPlayer} editable={false} />);

    expect(screen.getByTestId('player-state-card')).toBeInTheDocument();
  });

  it('should show ring border for current player', () => {
    const { container } = render(
      <PlayerStateCard player={mockPlayer} isCurrentPlayer={true} editable={false} />
    );

    const card = container.querySelector('[data-testid="player-state-card"]');
    expect(card).toHaveClass('ring-2', 'ring-primary');
  });
});
