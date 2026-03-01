/**
 * BggGameCard Tests - Issue #4141
 * Issue #4859: Updated for MeepleCard migration
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { BggSearchResult } from '@/types/bgg';

import { BggGameCard } from '../BggGameCard';

const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return (
      <div data-testid={props['data-testid'] as string}>
        <span>{props.title as string}</span>
        <button onClick={props.onClick as () => void}>select</button>
      </div>
    );
  },
}));

const mockGame: BggSearchResult = {
  id: 13,
  name: 'Catan',
  yearPublished: 1995,
  thumbnail: 'https://example.com/catan.jpg',
};

const mockGameNoThumbnail: BggSearchResult = {
  id: 822,
  name: 'Carcassonne',
  yearPublished: 2000,
  thumbnail: null,
};

describe('BggGameCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  it('renders with entity="game" and variant="compact"', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'game',
        variant: 'compact',
      })
    );
  });

  it('passes game name as title', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Catan' })
    );
  });

  it('passes year as subtitle', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ subtitle: '1995' })
    );
  });

  it('passes thumbnail as imageUrl', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ imageUrl: 'https://example.com/catan.jpg' })
    );
  });

  it('passes undefined imageUrl when thumbnail is null', () => {
    render(<BggGameCard game={mockGameNoThumbnail} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ imageUrl: undefined })
    );
  });

  it('enables selectable mode', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ selectable: true })
    );
  });

  it('passes selected state', () => {
    render(<BggGameCard game={mockGame} selected={true} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ selected: true })
    );
  });

  it('defaults selected to false', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ selected: false })
    );
  });

  it('calls onSelect with game ID on click', async () => {
    const mockOnSelect = vi.fn();
    const user = userEvent.setup();

    render(<BggGameCard game={mockGame} onSelect={mockOnSelect} />);
    await user.click(screen.getByText('select'));

    expect(mockOnSelect).toHaveBeenCalledWith(13);
  });

  it('sets correct data-testid', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(screen.getByTestId('bgg-game-card-13')).toBeInTheDocument();
  });

  it('handles long game names', () => {
    const longNameGame: BggSearchResult = {
      id: 999,
      name: 'Very Long Game Name That Should Be Truncated In The UI',
      yearPublished: 2024,
      thumbnail: null,
    };

    render(<BggGameCard game={longNameGame} onSelect={vi.fn()} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Very Long Game Name That Should Be Truncated In The UI',
      })
    );
  });
});
