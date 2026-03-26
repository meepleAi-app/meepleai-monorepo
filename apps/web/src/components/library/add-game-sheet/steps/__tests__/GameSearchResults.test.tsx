/**
 * GameSearchResults Component Tests
 * Issue #4819: AddGameSheet Step 1 - Game Source
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameSearchResults, type GameSearchResultItem } from '../GameSearchResults';

const mockResults: GameSearchResultItem[] = [
  {
    id: 'game-1',
    title: 'Catan',
    yearPublished: 1995,
    thumbnailUrl: 'https://example.com/catan.jpg',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    averageRating: 7.2,
    source: 'catalog',
    bggId: 13,
  },
  {
    id: 'bgg-174430',
    title: 'Gloomhaven',
    yearPublished: 2017,
    source: 'bgg',
    bggId: 174430,
  },
];

describe('GameSearchResults', () => {
  it('should render loading skeletons', () => {
    render(<GameSearchResults results={[]} loading={true} onSelect={vi.fn()} />);
    // Skeletons are rendered as div elements
    const container = document.querySelector('.space-y-2');
    expect(container).toBeTruthy();
  });

  it('should render nothing when no results and not loading', () => {
    const { container } = render(
      <GameSearchResults results={[]} loading={false} onSelect={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('should render result items', () => {
    render(<GameSearchResults results={mockResults} loading={false} onSelect={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
  });

  it('should show source badges', () => {
    render(<GameSearchResults results={mockResults} loading={false} onSelect={vi.fn()} />);
    expect(screen.getByText('Catalogo')).toBeInTheDocument();
    expect(screen.getByText('Importato')).toBeInTheDocument();
  });

  it('should show year and player count for catalog results', () => {
    render(<GameSearchResults results={mockResults} loading={false} onSelect={vi.fn()} />);
    expect(screen.getByText('1995')).toBeInTheDocument();
    expect(screen.getByText('3-4 giocatori')).toBeInTheDocument();
  });

  it('should show rating when available', () => {
    render(<GameSearchResults results={mockResults} loading={false} onSelect={vi.fn()} />);
    expect(screen.getByText('★ 7.2')).toBeInTheDocument();
  });

  it('should call onSelect when clicking a result', () => {
    const onSelect = vi.fn();
    render(<GameSearchResults results={mockResults} loading={false} onSelect={onSelect} />);

    fireEvent.click(screen.getByText('Catan'));
    expect(onSelect).toHaveBeenCalledWith(mockResults[0]);
  });

  it('should show equal players text for same min/max', () => {
    const samePlayerGame: GameSearchResultItem[] = [
      {
        id: 'game-2',
        title: 'Solo Game',
        source: 'catalog',
        minPlayers: 1,
        maxPlayers: 1,
      },
    ];
    render(<GameSearchResults results={samePlayerGame} loading={false} onSelect={vi.fn()} />);
    expect(screen.getByText('1 giocatori')).toBeInTheDocument();
  });
});
