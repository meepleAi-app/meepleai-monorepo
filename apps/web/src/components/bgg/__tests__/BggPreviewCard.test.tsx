/**
 * BggPreviewCard Tests - Issue #4141
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { BggPreviewCard } from '../BggPreviewCard';
import type { BggGameDetailsDto } from '@/types/bgg';

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
  it('should render game name and year', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Published: 1995')).toBeInTheDocument();
  });

  it('should render thumbnail when available', () => {
    render(<BggPreviewCard game={mockGame} />);

    const img = screen.getByAltText('Catan');
    expect(img).toHaveAttribute('src', 'https://example.com/catan.jpg');
  });

  it('should show placeholder when thumbnail is null', () => {
    render(<BggPreviewCard game={mockGameMinimal} />);

    expect(screen.getByText('No image')).toBeInTheDocument();
  });

  it('should display player count range', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('3-4 players')).toBeInTheDocument();
  });

  it('should display single player count when min equals max', () => {
    const gameWithSamePlayers: BggGameDetailsDto = {
      ...mockGame,
      minPlayers: 4,
      maxPlayers: 4,
    };

    render(<BggPreviewCard game={gameWithSamePlayers} />);

    expect(screen.getByText('4 players')).toBeInTheDocument();
  });

  it('should display playing time', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('90 min')).toBeInTheDocument();
  });

  it('should display minimum age', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('Age 10+')).toBeInTheDocument();
  });

  it('should display rating with one decimal', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('7.2 / 10')).toBeInTheDocument();
  });

  it('should display rating with proper formatting for whole numbers', () => {
    const gameWithWholeRating: BggGameDetailsDto = {
      ...mockGame,
      rating: 8.0,
    };

    render(<BggPreviewCard game={gameWithWholeRating} />);

    expect(screen.getByText('8.0 / 10')).toBeInTheDocument();
  });

  it('should display description when provided', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(
      screen.getByText('Trade, build, and settle the island of Catan')
    ).toBeInTheDocument();
  });

  it('should not display description section when not provided', () => {
    render(<BggPreviewCard game={mockGameMinimal} />);

    expect(
      screen.queryByText(/Trade, build/)
    ).not.toBeInTheDocument();
  });

  it('should display categories when provided', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('Categories')).toBeInTheDocument();
    expect(screen.getByText('Economic')).toBeInTheDocument();
    expect(screen.getByText('Negotiation')).toBeInTheDocument();
  });

  it('should display mechanics when provided', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('Mechanics')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
    expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
  });

  it('should display BGG ID', () => {
    render(<BggPreviewCard game={mockGame} />);

    expect(screen.getByText('BoardGameGeek ID: 13')).toBeInTheDocument();
  });

  it('should apply glassmorphism styling', () => {
    const { container } = render(<BggPreviewCard game={mockGame} />);

    const card = container.querySelector('.bg-white\\/70');
    expect(card).toBeInTheDocument();
    expect(card).toHaveClass('backdrop-blur-md');
  });

  it('should apply amber border styling', () => {
    const { container } = render(<BggPreviewCard game={mockGame} />);

    const card = container.querySelector('.border-amber-200');
    expect(card).toBeInTheDocument();
  });

  it('should render all icons', () => {
    const { container } = render(<BggPreviewCard game={mockGame} />);

    // Check for Users, Clock, Star icons (lucide-react)
    const icons = container.querySelectorAll('svg');
    expect(icons.length).toBeGreaterThanOrEqual(3);
  });

  it('should handle missing optional fields gracefully', () => {
    const gameWithoutOptionals: BggGameDetailsDto = {
      ...mockGameMinimal,
      description: undefined,
      categories: undefined,
      mechanics: undefined,
    };

    render(<BggPreviewCard game={gameWithoutOptionals} />);

    expect(screen.queryByText('Categories')).not.toBeInTheDocument();
    expect(screen.queryByText('Mechanics')).not.toBeInTheDocument();
  });
});
