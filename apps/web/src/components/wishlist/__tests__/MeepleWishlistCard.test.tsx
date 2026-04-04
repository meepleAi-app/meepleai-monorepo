/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeepleWishlistCard } from '../MeepleWishlistCard';

describe('MeepleWishlistCard', () => {
  const mockItem = {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    gameId: '00000000-0000-0000-0000-000000000003',
    gameName: 'Catan',
    priority: 'high',
    targetPrice: null,
    notes: null,
    addedAt: '2026-01-01',
    updatedAt: null,
    visibility: 'private',
  };

  it('renders with correct entity type', () => {
    render(<MeepleWishlistCard item={mockItem} />);
    const card = screen.getByTestId('wishlist-card');
    expect(card).toHaveAttribute('data-entity', 'game');
  });

  it('displays game name as title', () => {
    render(<MeepleWishlistCard item={mockItem} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('falls back to truncated ID when no game name', () => {
    const itemNoName = { ...mockItem, gameName: undefined };
    render(<MeepleWishlistCard item={itemNoName} />);
    expect(screen.getByText('Game 00000000...')).toBeInTheDocument();
  });
});
