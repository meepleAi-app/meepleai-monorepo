/**
 * BggPreviewCard Tests - Issue #4141
 * Issue #4859: Updated for MeepleCard migration
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { BggGameDetailsDto } from '@/types/bgg';

import { BggPreviewCard } from '../BggPreviewCard';

const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return <div data-testid={props['data-testid'] as string}>MeepleCard</div>;
  },
}));

const mockGame: BggGameDetailsDto = {
  id: 13,
  name: 'Catan',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 90,
  minAge: 10,
  rating: 7.2,
  thumbnail: 'https://example.com/catan.jpg',
  description: 'Trade, build, and settle the island of Catan',
  categories: ['Economic', 'Negotiation'],
  mechanics: ['Trading', 'Dice Rolling'],
};

const mockGameMinimal: BggGameDetailsDto = {
  id: 822,
  name: 'Carcassonne',
  yearPublished: 2000,
  minPlayers: 2,
  maxPlayers: 5,
  playingTime: 45,
  minAge: 7,
  rating: 7.4,
  thumbnail: null,
};

describe('BggPreviewCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  it('renders with entity="game" and variant="featured"', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'game',
        variant: 'featured',
      })
    );
  });

  it('passes game name as title', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Catan' })
    );
  });

  it('passes year and minAge as subtitle', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ subtitle: '1995 \u00b7 Age 10+' })
    );
  });

  it('passes thumbnail as imageUrl', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ imageUrl: 'https://example.com/catan.jpg' })
    );
  });

  it('passes undefined imageUrl when thumbnail is null', () => {
    render(<BggPreviewCard game={mockGameMinimal} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ imageUrl: undefined })
    );
  });

  it('passes rating and ratingMax', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ rating: 7.2, ratingMax: 10 })
    );
  });

  it('passes BGG ID as badge', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'BGG #13' })
    );
  });

  it('passes metadata with player count, playing time, and rating', () => {
    render(<BggPreviewCard game={mockGame} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata).toHaveLength(3);
    expect(metadata[0].label).toBe('3-4 players');
    expect(metadata[1].label).toBe('90 min');
    expect(metadata[2].label).toBe('7.2 / 10');
  });

  it('displays single player count when min equals max', () => {
    const samePlayersGame: BggGameDetailsDto = {
      ...mockGame,
      minPlayers: 4,
      maxPlayers: 4,
    };

    render(<BggPreviewCard game={samePlayersGame} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata[0].label).toBe('4 players');
  });

  it('formats rating with one decimal for whole numbers', () => {
    const wholeRatingGame: BggGameDetailsDto = {
      ...mockGame,
      rating: 8.0,
    };

    render(<BggPreviewCard game={wholeRatingGame} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata[2].label).toBe('8.0 / 10');
  });

  it('enables preview when description exists', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        showPreview: true,
        previewData: {
          description: 'Trade, build, and settle the island of Catan',
          categories: ['Economic', 'Negotiation'],
          mechanics: ['Trading', 'Dice Rolling'],
        },
      })
    );
  });

  it('disables preview when description is missing', () => {
    render(<BggPreviewCard game={mockGameMinimal} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ showPreview: false })
    );
  });

  it('sets correct data-testid', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByTestId('bgg-preview-card-13')).toBeInTheDocument();
  });

  it('handles missing optional fields gracefully', () => {
    const gameWithoutOptionals: BggGameDetailsDto = {
      ...mockGameMinimal,
      description: undefined,
      categories: undefined,
      mechanics: undefined,
    };

    render(<BggPreviewCard game={gameWithoutOptionals} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        showPreview: false,
        previewData: {
          description: undefined,
          categories: undefined,
          mechanics: undefined,
        },
      })
    );
  });
});
