/**
 * SPRINT-4: Session History Tests (Issue #1134)
 */

import React from 'react';
import { render, screen, waitFor, fireEvent, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import { useRouter } from 'next/router';
import SessionHistoryPage from '@/pages/sessions/history';
import { api } from '@/lib/api';

jest.mock('next/router', () => ({ useRouter: jest.fn() }));
jest.mock('@/lib/api', () => ({
  api: {
    sessions: { getHistory: jest.fn() },
    games: { getAll: jest.fn() }
  }
}));

describe('SessionHistoryPage', () => {
  const mockPush = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue({ push: mockPush });
  });

  const mockSessions = [
    {
      id: 'session-1',
      gameId: 'game-1',
      status: 'Completed',
      startedAt: '2024-01-15T10:00:00Z',
      completedAt: '2024-01-15T12:00:00Z',
      playerCount: 4,
      players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
      winnerName: 'Alice',
      notes: null,
      durationMinutes: 120
    },
    {
      id: 'session-2',
      gameId: 'game-2',
      status: 'Abandoned',
      startedAt: '2024-01-14T10:00:00Z',
      completedAt: '2024-01-14T11:00:00Z',
      playerCount: 2,
      players: [],
      winnerName: null,
      notes: null,
      durationMinutes: 60
    }
  ];

  const mockGames = [
    { id: 'game-1', title: 'Catan' },
    { id: 'game-2', title: 'Ticket to Ride' }
  ];

  describe('Statistics Display', () => {
    beforeEach(() => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should display session statistics', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Statistics')).toBeInTheDocument();
        expect(screen.getByText('2')).toBeInTheDocument(); // Total sessions
      });
    });

    it('should show completed and abandoned counts', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        // Find the statistics card specifically
        const statisticsCard = screen.getByText('Session Statistics').closest('.rounded-xl');
        expect(statisticsCard).toBeInTheDocument();

        // Look for the values within the statistics card
        const greenValue = statisticsCard?.querySelector('.text-green-600');
        expect(greenValue).toHaveTextContent('1');

        const redValue = statisticsCard?.querySelector('.text-red-600');
        expect(redValue).toHaveTextContent('1');
      });
    });

    it('should calculate average duration', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByText(/1h 30m/i)).toBeInTheDocument(); // (120+60)/2 = 90 minutes
      });
    });

    it('should display win rates for completed games', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByText(/win rates/i)).toBeInTheDocument();
        // Find Alice specifically in the win rates section (not in the table)
        const winRatesSection = screen.getByText(/win rates/i).parentElement;
        expect(winRatesSection).toHaveTextContent('Alice');
        expect(winRatesSection).toHaveTextContent('100%'); // Alice won 1 out of 1 completed game
      });
    });
  });

  describe('Filtering', () => {
    beforeEach(() => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should filter by game', async () => {
      render(<SessionHistoryPage />);

      // Wait for initial load to complete
      await waitFor(() => {
        expect(screen.getByLabelText(/filter sessions by game/i)).toBeInTheDocument();
      });

      // Wait for initial API calls to complete
      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalled();
      });

      // Clear previous calls to check for new ones
      (api.sessions.getHistory as jest.Mock).mockClear();

      // Change the filter with act wrapper
      const gameFilter = screen.getByLabelText(/filter sessions by game/i) as HTMLSelectElement;

      await act(async () => {
        fireEvent.change(gameFilter, { target: { value: 'game-1' } });
      });

      // Wait for the API to be called with the filter
      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalledWith(
          expect.objectContaining({
            gameId: 'game-1',
            limit: 20,
            offset: 0
          })
        );
      });
    });

    it('should filter by start date', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const startDateInput = screen.getByLabelText(/filter sessions from start date/i);
        fireEvent.change(startDateInput, { target: { value: '2024-01-14' } });
      });

      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalledWith(
          expect.objectContaining({ startDate: '2024-01-14' })
        );
      });
    });

    it('should filter by end date', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const endDateInput = screen.getByLabelText(/filter sessions to end date/i);
        fireEvent.change(endDateInput, { target: { value: '2024-01-15' } });
      });

      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalledWith(
          expect.objectContaining({ endDate: '2024-01-15' })
        );
      });
    });

    it('should reset all filters', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const gameFilter = screen.getByLabelText(/filter sessions by game/i);
        fireEvent.change(gameFilter, { target: { value: 'game-1' } });
      });

      await waitFor(() => {
        const resetButton = screen.getByRole('button', { name: /reset all filters/i });
        fireEvent.click(resetButton);
      });

      await waitFor(() => {
        const gameFilter = screen.getByLabelText(/filter sessions by game/i);
        expect(gameFilter).toHaveValue('all');
      });
    });
  });

  describe('Session Display', () => {
    beforeEach(() => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should display session list with game titles', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        // Look for game titles specifically in the table cells
        const tableRows = screen.getAllByRole('button', { name: /view session details/i });
        expect(tableRows).toHaveLength(2);

        // Check the table contains the game titles
        const table = screen.getByRole('table');
        expect(table).toHaveTextContent('Catan');
        expect(table).toHaveTextContent('Ticket to Ride');
      });
    });

    it('should display winner names for completed sessions', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        // Find the table and look for Alice as a winner in the table specifically
        const table = screen.getByRole('table');
        expect(table).toBeInTheDocument();

        // Look for Alice in the winner column (will be in the table row for session-1)
        const aliceRow = screen.getByRole('button', { name: /view session details for Alice/i });
        expect(aliceRow).toBeInTheDocument();
        expect(aliceRow).toHaveTextContent('Alice');
        expect(aliceRow).toHaveTextContent('Completed');
      });
    });

    it('should display em dash for sessions without winner', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const cells = screen.getAllByText('—');
        expect(cells.length).toBeGreaterThan(0);
      });
    });

    it('should navigate to session details on row click', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const row = screen.getByRole('button', { name: /view session details for Alice/i });
        fireEvent.click(row);
      });

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });
  });

  describe('Empty State', () => {
    it('should display empty state when no history exists', async () => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByText(/no session history found/i)).toBeInTheDocument();
      });
    });

    it('should show clear filters button in empty state with filters', async () => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });

      render(<SessionHistoryPage />);

      await waitFor(() => {
        const gameFilter = screen.getByLabelText(/filter sessions by game/i);
        fireEvent.change(gameFilter, { target: { value: 'game-1' } });
      });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /clear filters/i })).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    beforeEach(() => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should have proper heading structure', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByText('Session History')).toBeInTheDocument();
      });
    });

    it('should have keyboard navigable rows', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const row = screen.getByRole('button', { name: /view session details for Alice/i });
        expect(row).toHaveAttribute('tabIndex', '0');
      });
    });

    it('should navigate on Enter key', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const row = screen.getByRole('button', { name: /view session details for Alice/i });
        fireEvent.keyDown(row, { key: 'Enter' });
      });

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });

    it('should have accessible win rate progress bars', async () => {
      render(<SessionHistoryPage />);

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar', { name: /alice win rate/i });
        expect(progressBar).toHaveAttribute('aria-valuenow');
        expect(progressBar).toHaveAttribute('aria-valuemin', '0');
        expect(progressBar).toHaveAttribute('aria-valuemax', '100');
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error on API failure', async () => {
      (api.sessions.getHistory as jest.Mock).mockRejectedValue(new Error('API Error'));
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<SessionHistoryPage />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/API Error/i)).toBeInTheDocument();
      });
    });
  });
});
