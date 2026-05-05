/**
 * GameSelector — Unit tests
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock api before importing component
const mockGetPrivateGames = vi.fn();
const mockGetLibrary = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getPrivateGames: (...args: unknown[]) => mockGetPrivateGames(...args),
      getLibrary: (...args: unknown[]) => mockGetLibrary(...args),
    },
  },
}));

import { GameSelector } from '@/components/chat/entry/GameSelector';

describe('GameSelector', () => {
  const onSelect = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPrivateGames.mockResolvedValue({
      items: [
        { id: 'g1', title: 'Catan', createdAt: '2026-01-01T00:00:00Z' },
        { id: 'g2', title: 'Wingspan', createdAt: '2026-01-02T00:00:00Z' },
      ],
      totalCount: 2,
    });
    mockGetLibrary.mockResolvedValue({
      items: [
        {
          id: 'lib1',
          userId: 'u1',
          gameId: 's1',
          gameTitle: 'Shared Game',
          addedAt: '2026-01-01T00:00:00Z',
          isFavorite: false,
          currentState: 'Owned',
          hasKb: true,
        },
      ],
      totalCount: 1,
    });
  });

  it('renders loading skeleton initially', () => {
    mockGetPrivateGames.mockReturnValue(new Promise(() => {})); // never resolves
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    // Skeleton divs should be rendered while loading
    expect(screen.getByTestId('game-selection-section')).toBeInTheDocument();
  });

  it('renders private games after loading', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-card-g2')).toBeInTheDocument();
  });

  it('calls onSelect when a game card is clicked', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('game-card-g1'));
    expect(onSelect).toHaveBeenCalledWith('g1');
  });

  it('calls onSelect with empty string for skip-game button', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('skip-game-btn')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('skip-game-btn'));
    expect(onSelect).toHaveBeenCalledWith('');
  });

  it('filters games by search text', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });

    const searchInput = screen.getByTestId('game-search-input');
    fireEvent.change(searchInput, { target: { value: 'wing' } });

    expect(screen.queryByTestId('game-card-g1')).not.toBeInTheDocument();
    expect(screen.getByTestId('game-card-g2')).toBeInTheDocument();
  });

  it('renders tabs and defaults to private', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByTestId('tab-private-games')).toBeInTheDocument();
    });
    expect(screen.getByTestId('tab-private-games')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('tab-shared-games')).toHaveAttribute('aria-selected', 'false');
  });

  it('highlights selected game card', async () => {
    render(<GameSelector onSelect={onSelect} selectedGameId="g1" />);
    await waitFor(() => {
      expect(screen.getByTestId('game-card-g1')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-card-g1')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('game-card-g2')).toHaveAttribute('aria-pressed', 'false');
  });

  it('shows error state on API failure', async () => {
    mockGetPrivateGames.mockRejectedValue(new Error('Network error'));
    render(<GameSelector onSelect={onSelect} selectedGameId={null} />);
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
