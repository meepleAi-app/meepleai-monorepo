/**
 * SPRINT-4: Active Sessions Dashboard Tests (Issue #1134)
 *
 * Comprehensive tests for /sessions/index.tsx
 * Coverage targets: 90%+
 */

import React from 'react';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import { useRouter } from 'next/router';
import ActiveSessionsPage from '@/pages/sessions/index';
import { api } from '@/lib/api';
import userEvent from '@testing-library/user-event';

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

// Mock API
jest.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getActive: jest.fn(),
      pause: jest.fn(),
      resume: jest.fn(),
      end: jest.fn()
    },
    games: {
      getAll: jest.fn()
    }
  }
}));

describe('ActiveSessionsPage', () => {
  const mockPush = jest.fn();
  const mockRouter = { push: mockPush };

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe('Loading State', () => {
    it('should display loading skeletons while fetching data', async () => {
      (api.sessions.getActive as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 1000))
      );
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      expect(screen.getByRole('status', { name: /loading sessions/i })).toBeInTheDocument();
    });

    it('should have accessible loading state with aria-live', () => {
      (api.sessions.getActive as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 1000))
      );
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      const loadingElement = screen.getByRole('status', { name: /loading sessions/i });
      expect(loadingElement).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Empty State', () => {
    it('should display empty state when no sessions exist', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText(/no active sessions found/i)).toBeInTheDocument();
      });
    });

    it('should provide link to start new session from empty state', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ sessions: [], total: 0, page: 1, pageSize: 20 })
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Start a New Session' })).toBeInTheDocument();
      });
    });

    it('should navigate to games library on start button click', async () => {
      const user = userEvent.setup();
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ sessions: [], total: 0, page: 1, pageSize: 20 })
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Start a New Session' })).toBeInTheDocument();
      });

      const startButton = screen.getByRole('button', { name: 'Start a New Session' });
      await user.click(startButton);

      expect(mockPush).toHaveBeenCalledWith('/games');
    });
  });

  describe('Sessions Display', () => {
    const mockSessions = [
      {
        id: 'session-1',
        gameId: 'game-1',
        status: 'InProgress',
        startedAt: '2024-01-15T10:00:00Z',
        completedAt: null,
        playerCount: 4,
        players: [
          { playerName: 'Alice', playerOrder: 1, color: '#FF0000' },
          { playerName: 'Bob', playerOrder: 2, color: '#00FF00' }
        ],
        winnerName: null,
        notes: null,
        durationMinutes: 45
      },
      {
        id: 'session-2',
        gameId: 'game-2',
        status: 'Paused',
        startedAt: '2024-01-15T09:00:00Z',
        completedAt: null,
        playerCount: 2,
        players: [
          { playerName: 'Charlie', playerOrder: 1, color: '#0000FF' }
        ],
        winnerName: null,
        notes: null,
        durationMinutes: 30
      }
    ];

    const mockGames = [
      { id: 'game-1', title: 'Catan', publisher: 'KOSMOS', yearPublished: 1995 },
      { id: 'game-2', title: 'Ticket to Ride', publisher: 'Days of Wonder', yearPublished: 2004 }
    ];

    beforeEach(() => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should display all active sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });
    });

    it('should display session status badges', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/session status: inprogress/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/session status: paused/i)).toBeInTheDocument();
      });
    });

    it('should display player count for each session', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('4 players')).toBeInTheDocument();
        expect(screen.getByText('2 players')).toBeInTheDocument();
      });
    });

    it('should format duration correctly (minutes)', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('45m')).toBeInTheDocument();
        expect(screen.getByText('30m')).toBeInTheDocument();
      });
    });

    it('should format duration correctly (hours and minutes)', async () => {
      const longSession = {
        ...mockSessions[0],
        id: 'session-3',
        durationMinutes: 125
      };

      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [longSession],
        total: 1,
        page: 1,
        pageSize: 20
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('2h 5m')).toBeInTheDocument();
      });
    });
  });

  describe('Session Actions', () => {
    const mockSession = {
      id: 'session-1',
      gameId: 'game-1',
      status: 'InProgress',
      startedAt: '2024-01-15T10:00:00Z',
      completedAt: null,
      playerCount: 4,
      players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
      winnerName: null,
      notes: null,
      durationMinutes: 45
    };

    beforeEach(() => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [mockSession],
        total: 1,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({
        games: [{ id: 'game-1', title: 'Catan' }],
        total: 1
      });
    });

    it('should show pause button for InProgress sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /pause session/i })).toBeInTheDocument();
      });
    });

    it('should show resume button for Paused sessions', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{ ...mockSession, status: 'Paused' }],
        total: 1,
        page: 1,
        pageSize: 20
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resume session/i })).toBeInTheDocument();
      });
    });

    it('should show end button for active sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /end session/i })).toBeInTheDocument();
      });
    });

    it('should call pause API on pause button click', async () => {
      (api.sessions.pause as jest.Mock).mockResolvedValue({ ...mockSession, status: 'Paused' });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const pauseButton = screen.getByRole('button', { name: /pause session/i });
        fireEvent.click(pauseButton);
      });

      await waitFor(() => {
        expect(api.sessions.pause).toHaveBeenCalledWith('session-1');
      });
    });

    it('should call resume API on resume button click', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{ ...mockSession, status: 'Paused' }],
        total: 1,
        page: 1,
        pageSize: 20
      });
      (api.sessions.resume as jest.Mock).mockResolvedValue({ ...mockSession, status: 'InProgress' });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const resumeButton = screen.getByRole('button', { name: /resume session/i });
        fireEvent.click(resumeButton);
      });

      await waitFor(() => {
        expect(api.sessions.resume).toHaveBeenCalledWith('session-1');
      });
    });

    it('should show confirmation dialog before ending session', async () => {
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const endButton = screen.getByRole('button', { name: /end session/i });
        fireEvent.click(endButton);
      });

      expect(confirmSpy).toHaveBeenCalled();
      expect(api.sessions.end).not.toHaveBeenCalled();

      confirmSpy.mockRestore();
    });

    it('should call end API when confirmed', async () => {
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      (api.sessions.end as jest.Mock).mockResolvedValue({ ...mockSession, status: 'Completed' });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const endButton = screen.getByRole('button', { name: /end session/i });
        fireEvent.click(endButton);
      });

      await waitFor(() => {
        expect(api.sessions.end).toHaveBeenCalledWith('session-1');
      });

      confirmSpy.mockRestore();
    });
  });

  describe('Filtering', () => {
    const mockSessions = [
      {
        id: 'session-1',
        gameId: 'game-1',
        status: 'InProgress',
        startedAt: '2024-01-15T10:00:00Z',
        completedAt: null,
        playerCount: 4,
        players: [],
        winnerName: null,
        notes: null,
        durationMinutes: 45
      },
      {
        id: 'session-2',
        gameId: 'game-2',
        status: 'Paused',
        startedAt: '2024-01-15T09:00:00Z',
        completedAt: null,
        playerCount: 2,
        players: [],
        winnerName: null,
        notes: null,
        durationMinutes: 30
      }
    ];

    const mockGames = [
      { id: 'game-1', title: 'Catan' },
      { id: 'game-2', title: 'Ticket to Ride' }
    ];

    beforeEach(() => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 2,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: mockGames, total: 2 });
    });

    it('should display game filter dropdown', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/filter by game/i)).toBeInTheDocument();
      });
    });

    it('should filter sessions by game', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const filter = screen.getByLabelText(/filter by game/i);
        fireEvent.change(filter, { target: { value: 'game-1' } });
      });

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
      });
    });

    it('should show all sessions when filter is "all"', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const filter = screen.getByLabelText(/filter by game/i);
        fireEvent.change(filter, { target: { value: 'all' } });
      });

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });
    });
  });

  describe('Pagination', () => {
    it('should display pagination controls when multiple pages exist', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: Array(20).fill({
          id: 'session-1',
          gameId: 'game-1',
          status: 'InProgress',
          startedAt: '2024-01-15T10:00:00Z',
          completedAt: null,
          playerCount: 4,
          players: [],
          winnerName: null,
          notes: null,
          durationMinutes: 45
        }),
        total: 50,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /pagination/i })).toBeInTheDocument();
      });
    });

    it('should navigate to next page on next button click', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 50,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const nextButton = screen.getByRole('button', { name: /next page/i });
        fireEvent.click(nextButton);
      });

      await waitFor(() => {
        expect(api.sessions.getActive).toHaveBeenCalledWith(20, 20); // offset = 20
      });
    });

    it('should disable previous button on first page', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 50,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const prevButton = screen.getByRole('button', { name: /previous page/i });
        expect(prevButton).toBeDisabled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error message on API failure', async () => {
      (api.sessions.getActive as jest.Mock).mockRejectedValue(new Error('API Error'));
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/API Error/i)).toBeInTheDocument();
      });
    });

    it('should display error when pause fails', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{
          id: 'session-1',
          gameId: 'game-1',
          status: 'InProgress',
          startedAt: '2024-01-15T10:00:00Z',
          completedAt: null,
          playerCount: 4,
          players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
          winnerName: null,
          notes: null,
          durationMinutes: 45
        }],
        total: 1,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [{ id: 'game-1', title: 'Catan' }], total: 1 });
      (api.sessions.pause as jest.Mock).mockRejectedValue(new Error('Pause failed'));

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const pauseButton = screen.getByRole('button', { name: /pause session/i });
        fireEvent.click(pauseButton);
      });

      await waitFor(() => {
        expect(screen.getByText(/pause failed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have proper heading structure', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({ games: [], total: 0 });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Active Game Sessions')).toBeInTheDocument();
      });
    });

    it('should have keyboard navigable session rows', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{
          id: 'session-1',
          gameId: 'game-1',
          status: 'InProgress',
          startedAt: '2024-01-15T10:00:00Z',
          completedAt: null,
          playerCount: 4,
          players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
          winnerName: null,
          notes: null,
          durationMinutes: 45
        }],
        total: 1,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({
        games: [{ id: 'game-1', title: 'Catan' }],
        total: 1
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const row = screen.getByRole('button', { name: /view session details/i });
        expect(row).toHaveAttribute('tabIndex', '0');
      });
    });

    it('should navigate on Enter key press', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{
          id: 'session-1',
          gameId: 'game-1',
          status: 'InProgress',
          startedAt: '2024-01-15T10:00:00Z',
          completedAt: null,
          playerCount: 4,
          players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
          winnerName: null,
          notes: null,
          durationMinutes: 45
        }],
        total: 1,
        page: 1,
        pageSize: 20
      });
      (api.games.getAll as jest.Mock).mockResolvedValue({
        games: [{ id: 'game-1', title: 'Catan' }],
        total: 1
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const row = screen.getByRole('button', { name: /view session details/i });
        fireEvent.keyDown(row, { key: 'Enter' });
      });

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });
  });
});