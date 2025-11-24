import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import { GameList } from '@/components/games/GameList';
import { Game } from '@/lib/api';

const mockGames: Game[] = [
  {
    id: '1',
    title: 'Catan',
    publisher: 'Catan Studio',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: '2024-01-01T00:00:00Z',
  },
  {
    id: '2',
    title: 'Pandemic',
    publisher: 'Z-Man Games',
    yearPublished: 2008,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 45,
    maxPlayTimeMinutes: 45,
    bggId: 30549,
    createdAt: '2024-01-02T00:00:00Z',
  },
];

describe('GameList', () => {
  const defaultProps = {
    games: mockGames,
    loading: false,
    viewMode: 'grid' as const,
    onViewModeChange: vi.fn(),
    onGameClick: vi.fn(),
    currentPage: 1,
    totalPages: 3,
    totalGames: 50,
    onPageChange: vi.fn(),
  };

  it('renders loading skeleton when loading', () => {
    render(<GameList {...defaultProps} loading={true} />);
    // Check for loading state by verifying header skeleton and grid
    const skeletons = document.querySelectorAll('.h-6, .h-9, .h-48');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders empty state when no games', () => {
    render(<GameList {...defaultProps} games={[]} />);
    expect(screen.getByText('No games found')).toBeInTheDocument();
    expect(screen.getByText(/Try adjusting your filters/)).toBeInTheDocument();
  });

  it('renders games in grid view', () => {
    render(<GameList {...defaultProps} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('renders games in list view', () => {
    render(<GameList {...defaultProps} viewMode="list" />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('displays correct game count', () => {
    render(<GameList {...defaultProps} />);
    expect(screen.getByText('Showing 1-2 of 50 games')).toBeInTheDocument();
  });

  it('calls onViewModeChange when grid button clicked', () => {
    const handleViewModeChange = vi.fn();
    render(<GameList {...defaultProps} viewMode="list" onViewModeChange={handleViewModeChange} />);

    const gridButton = screen.getByLabelText('Grid view');
    fireEvent.click(gridButton);

    expect(handleViewModeChange).toHaveBeenCalledWith('grid');
  });

  it('calls onViewModeChange when list button clicked', () => {
    const handleViewModeChange = vi.fn();
    render(<GameList {...defaultProps} onViewModeChange={handleViewModeChange} />);

    const listButton = screen.getByLabelText('List view');
    fireEvent.click(listButton);

    expect(handleViewModeChange).toHaveBeenCalledWith('list');
  });

  it('highlights active view mode', () => {
    render(<GameList {...defaultProps} viewMode="grid" />);
    const gridButton = screen.getByLabelText('Grid view');
    expect(gridButton).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows pagination controls when totalPages > 1', () => {
    render(<GameList {...defaultProps} totalPages={3} />);
    expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
    expect(screen.getByLabelText('Next page')).toBeInTheDocument();
  });

  it('hides pagination when totalPages = 1', () => {
    render(<GameList {...defaultProps} totalPages={1} />);
    expect(screen.queryByLabelText('Previous page')).not.toBeInTheDocument();
  });

  it('disables previous button on first page', () => {
    render(<GameList {...defaultProps} currentPage={1} />);
    expect(screen.getByLabelText('Previous page')).toBeDisabled();
  });

  it('disables next button on last page', () => {
    render(<GameList {...defaultProps} currentPage={3} totalPages={3} />);
    expect(screen.getByLabelText('Next page')).toBeDisabled();
  });

  it('calls onPageChange when page button clicked', () => {
    const handlePageChange = vi.fn();
    render(<GameList {...defaultProps} onPageChange={handlePageChange} />);

    const page2Button = screen.getByLabelText('Page 2');
    fireEvent.click(page2Button);

    expect(handlePageChange).toHaveBeenCalledWith(2);
  });

  it('calls onPageChange when next button clicked', () => {
    const handlePageChange = vi.fn();
    render(<GameList {...defaultProps} currentPage={1} onPageChange={handlePageChange} />);

    const nextButton = screen.getByLabelText('Next page');
    fireEvent.click(nextButton);

    expect(handlePageChange).toHaveBeenCalledWith(2);
  });

  it('calls onPageChange when previous button clicked', () => {
    const handlePageChange = vi.fn();
    render(<GameList {...defaultProps} currentPage={2} onPageChange={handlePageChange} />);

    const prevButton = screen.getByLabelText('Previous page');
    fireEvent.click(prevButton);

    expect(handlePageChange).toHaveBeenCalledWith(1);
  });

  it('calls onGameClick when game card is clicked', () => {
    const handleGameClick = vi.fn();
    render(<GameList {...defaultProps} onGameClick={handleGameClick} />);

    const catanCard = screen.getByText('Catan').closest('[role="button"]');
    if (catanCard) {
      fireEvent.click(catanCard);
      expect(handleGameClick).toHaveBeenCalledWith(mockGames[0]);
    }
  });

  it('highlights current page', () => {
    render(<GameList {...defaultProps} currentPage={2} />);
    const page2Button = screen.getByLabelText('Page 2');
    expect(page2Button).toHaveAttribute('aria-current', 'page');
  });
});
