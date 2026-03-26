/**
 * PlayerStatsCard Tests
 *
 * AgentMemory — Task 25
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PlayerStatsCard } from '../PlayerStatsCard';

// ─── Mock API Client ─────────────────────────────────────────────────────────

const mockGetMyStats = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    agentMemory: {
      getMyStats: mockGetMyStats,
    },
  }),
}));

// ─── Test Data ────────────────────────────────────────────────────────────────

const mockPlayerStats = [
  {
    id: 'pm-1',
    groupId: 'group-1',
    gameStats: [
      { gameId: 'game-1', wins: 5, losses: 3, totalPlayed: 8, bestScore: 120 },
      { gameId: 'game-2', wins: 2, losses: 4, totalPlayed: 6, bestScore: null },
    ],
    claimedAt: null,
  },
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PlayerStatsCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    mockGetMyStats.mockReturnValue(new Promise(() => {}));
    render(<PlayerStatsCard userId="user-1" />);
    expect(screen.getByText('Loading stats...')).toBeInTheDocument();
  });

  it('renders overall stats header when no gameId', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" />);

    await waitFor(() => {
      expect(screen.getByText('Overall Stats')).toBeInTheDocument();
    });
  });

  it('renders game-specific header when gameId provided', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" gameId="game-1" />);

    await waitFor(() => {
      expect(screen.getByText('Game Stats')).toBeInTheDocument();
    });
  });

  it('shows aggregated win/loss for all games', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" />);

    await waitFor(() => {
      // 5 + 2 = 7 wins, 3 + 4 = 7 losses — both are 7
      const sevens = screen.getAllByText('7');
      expect(sevens.length).toBe(2);
      expect(screen.getByText('wins')).toBeInTheDocument();
      expect(screen.getByText('losses')).toBeInTheDocument();
    });
  });

  it('shows filtered stats for specific game', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" gameId="game-1" />);

    await waitFor(() => {
      expect(screen.getByText('5')).toBeInTheDocument(); // wins for game-1
      expect(screen.getByText('3')).toBeInTheDocument(); // losses for game-1
    });
  });

  it('shows total games played', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" />);

    await waitFor(() => {
      expect(screen.getByText('Total games')).toBeInTheDocument();
      expect(screen.getByText('14')).toBeInTheDocument(); // 8 + 6
    });
  });

  it('shows best score when available', async () => {
    mockGetMyStats.mockResolvedValue(mockPlayerStats);
    render(<PlayerStatsCard userId="user-1" gameId="game-1" />);

    await waitFor(() => {
      expect(screen.getByText('Best score')).toBeInTheDocument();
      expect(screen.getByText('120')).toBeInTheDocument();
    });
  });

  it('shows empty state when no games played', async () => {
    mockGetMyStats.mockResolvedValue([]);
    render(<PlayerStatsCard userId="user-1" />);

    await waitFor(() => {
      expect(screen.getByText('No games played yet')).toBeInTheDocument();
    });
  });

  it('shows error state on fetch failure', async () => {
    mockGetMyStats.mockRejectedValue(new Error('Network error'));
    render(<PlayerStatsCard userId="user-1" />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load player stats')).toBeInTheDocument();
    });
  });
});
