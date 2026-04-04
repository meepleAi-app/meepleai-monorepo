/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeeplePlayerStateCard } from '../MeeplePlayerStateCard';

describe('MeeplePlayerStateCard', () => {
  const mockPlayer = {
    playerName: 'Alice',
    playerOrder: 1,
    color: '#7c3aed',
    score: 25,
    resources: { wood: 3, stone: 2 },
  };

  it('renders with correct entity type', () => {
    render(<MeeplePlayerStateCard player={mockPlayer} />);
    const card = screen.getByTestId('player-state-card');
    expect(card).toHaveAttribute('data-entity', 'player');
  });

  it('displays player name', () => {
    render(<MeeplePlayerStateCard player={mockPlayer} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('renders score value', () => {
    render(<MeeplePlayerStateCard player={mockPlayer} />);
    expect(screen.getByText('Score')).toBeInTheDocument();
  });

  it('renders resource labels', () => {
    render(<MeeplePlayerStateCard player={mockPlayer} />);
    expect(screen.getByText('wood')).toBeInTheDocument();
    expect(screen.getByText('stone')).toBeInTheDocument();
  });
});
