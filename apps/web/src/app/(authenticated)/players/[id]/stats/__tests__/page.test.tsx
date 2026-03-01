/**
 * Player Stats Page Tests (Issue #4890)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import PlayerStatsPage from '../page';

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
  totalSessions: 15,
  totalWins: 6,
  gamePlayCounts: { Catan: 8, Pandemic: 7 },
  averageScoresByGame: { Catan: 9.2, Pandemic: 7.1 },
};

describe('PlayerStatsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stat summary cards', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerStatsPage />);

    await waitFor(() => {
      expect(screen.getByText('15')).toBeInTheDocument(); // total sessions
    });

    expect(screen.getByText('6')).toBeInTheDocument(); // total wins
    expect(screen.getByText('40%')).toBeInTheDocument(); // win rate
    expect(screen.getByText('2')).toBeInTheDocument(); // unique games
  });

  it('renders per-game breakdown', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerStatsPage />);

    await waitFor(() => {
      // Catan appears in both Sessions and Scores sections
      expect(screen.getAllByText('Catan').length).toBeGreaterThan(0);
    });

    expect(screen.getAllByText('Pandemic').length).toBeGreaterThan(0);
  });

  it('shows loading skeletons', () => {
    mockUsePlayerStatistics.mockReturnValue({ data: null, isLoading: true, error: null });

    renderWithQuery(<PlayerStatsPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockUsePlayerStatistics.mockReturnValue({
      data: null,
      isLoading: false,
      error: new Error('Stats error'),
    });

    renderWithQuery(<PlayerStatsPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load statistics/i)).toBeInTheDocument();
    });
  });

  it('shows page heading', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: mockStats, isLoading: false, error: null });

    renderWithQuery(<PlayerStatsPage />);

    await waitFor(() => {
      expect(screen.getByText('Statistics')).toBeInTheDocument();
    });
  });

  it('shows no data state when stats is null', async () => {
    mockUsePlayerStatistics.mockReturnValue({ data: null, isLoading: false, error: null });

    renderWithQuery(<PlayerStatsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No statistics available/i)).toBeInTheDocument();
    });
  });
});
