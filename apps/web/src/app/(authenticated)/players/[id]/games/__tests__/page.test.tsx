/**
 * Player Games Page Tests (Issue #4890)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import PlayerGamesPage from '../page';

const mockUsePlayerStatistics = vi.hoisted(() => vi.fn());

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'alice' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/hooks/queries/usePlayersFromRecords', () => ({
  usePlayerStatistics: mockUsePlayerStatistics,
}));

const mockStats = {
  totalSessions: 10,
  totalWins: 4,
  gamePlayCounts: {
    'Catan': 5,
    'Ticket to Ride': 3,
    'Pandemic': 2,
  },
  averageScoresByGame: {
    'Catan': 8.5,
    'Ticket to Ride': 120.3,
  },
};

describe('PlayerGamesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders game list with sorted play counts', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerGamesPage />);

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows game count in header', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerGamesPage />);

    await waitFor(() => {
      expect(screen.getByText('3 Games')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockUsePlayerStatistics.mockReturnValue({ data: null, isLoading: true, error: null });

    renderWithQuery(<PlayerGamesPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockUsePlayerStatistics.mockReturnValue({
      data: null,
      isLoading: false,
      error: new Error('Load failed'),
    });

    renderWithQuery(<PlayerGamesPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load game statistics/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no games', async () => {
    mockUsePlayerStatistics.mockReturnValue({
      data: { ...mockStats, gamePlayCounts: {}, averageScoresByGame: {} },
      isLoading: false,
      error: null,
    });

    renderWithQuery(<PlayerGamesPage />);

    await waitFor(() => {
      expect(screen.getByText(/No games recorded/i)).toBeInTheDocument();
    });
  });

  it('shows heading and player name', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerGamesPage />);

    await waitFor(() => {
      expect(screen.getByText('Games Played')).toBeInTheDocument();
    });
  });
});
