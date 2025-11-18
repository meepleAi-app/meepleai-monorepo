/**
 * Active Sessions Page Tests (Issue #864)
 *
 * Tests for Active Session Management UI component.
 * Covers: Session listing, filtering, pagination, lifecycle actions (pause, resume, end)
 */

import React from 'react';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/navigation';
import ActiveSessionsPage from '../page';
import { api } from '@/lib/api';
import type { PaginatedSessionsResponse, GameSessionDto, Game } from '@/lib/api';

// Mock dependencies
jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
}));

jest.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getActive: jest.fn(),
      pause: jest.fn(),
      resume: jest.fn(),
      end: jest.fn(),
    },
    games: {
      getAll: jest.fn(),
    },
  },
}));

describe('ActiveSessionsPage', () => {
  const mockPush = jest.fn();
  const mockRouter = { push: mockPush };

  const mockGames: Game[] = [
    {
      id: 'game-1',
      title: 'Catan',
      publisher: 'Kosmos',
      yearPublished: 1995,
      minPlayers: 3,
      maxPlayers: 4,
      minPlayTimeMinutes: 60,
      maxPlayTimeMinutes: 120,
      bggId: null,
      createdAt: '2025-11-17T10:00:00Z',
    },
    {
      id: 'game-2',
      title: 'Ticket to Ride',
      publisher: 'Days of Wonder',
      yearPublished: 2004,
      minPlayers: 2,
      maxPlayers: 5,
      minPlayTimeMinutes: 30,
      maxPlayTimeMinutes: 60,
      bggId: null,
      createdAt: '2025-11-17T10:00:00Z',
    },
  ];

  const mockSessions: GameSessionDto[] = [
    {
      id: 'session-1',
      gameId: 'game-1',
      status: 'InProgress',
      startedAt: '2025-11-17T10:00:00Z',
      completedAt: null,
      playerCount: 4,
      players: [
        { playerName: 'Alice', playerOrder: 1, color: 'red' },
        { playerName: 'Bob', playerOrder: 2, color: 'blue' },
      ],
      winnerName: null,
      notes: null,
      durationMinutes: 45,
    },
    {
      id: 'session-2',
      gameId: 'game-2',
      status: 'Paused',
      startedAt: '2025-11-17T09:00:00Z',
      completedAt: null,
      playerCount: 3,
      players: [
        { playerName: 'Charlie', playerOrder: 1, color: 'green' },
      ],
      winnerName: null,
      notes: null,
      durationMinutes: 30,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);

    (api.sessions.getActive as jest.Mock).mockResolvedValue({
      sessions: mockSessions,
      total: 2,
      page: 1,
      pageSize: 20,
    } as PaginatedSessionsResponse);

    (api.games.getAll as jest.Mock).mockResolvedValue({
      games: mockGames,
      total: 2,
      page: 1,
      pageSize: 100,
      totalPages: 1,
    });
  });

  describe('Initial Rendering', () => {
    it('should render the page title and description', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Active Game Sessions')).toBeInTheDocument();
        expect(screen.getByText('Manage your currently active and paused game sessions')).toBeInTheDocument();
      });
    });

    it('should load and display active sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      expect(api.sessions.getActive).toHaveBeenCalledWith(20, 0);
    });

    it('should load games for filter dropdown', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(api.games.getAll).toHaveBeenCalled();
      });
    });
  });

  describe('Loading State', () => {
    it('should show loading skeletons while fetching data', () => {
      (api.sessions.getActive as jest.Mock).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<ActiveSessionsPage />);

      expect(screen.getByRole('status', { name: /loading sessions/i })).toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('should show empty state when no sessions exist', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('No active sessions found')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /start a new session/i })).toBeInTheDocument();
      });
    });

    it('should navigate to games page when clicking start button', async () => {
      const user = userEvent.setup();
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('No active sessions found')).toBeInTheDocument();
      });

      const startButton = screen.getByRole('button', { name: /start a new session/i });
      await user.click(startButton);

      expect(mockPush).toHaveBeenCalledWith('/games');
    });
  });

  describe('Session Display', () => {
    it('should display session status badges', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('InProgress')).toBeInTheDocument();
        expect(screen.getByText('Paused')).toBeInTheDocument();
      });
    });

    it('should display player count', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('4 players')).toBeInTheDocument();
        expect(screen.getByText('3 players')).toBeInTheDocument();
      });
    });

    it('should display duration', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('45m')).toBeInTheDocument();
        expect(screen.getByText('30m')).toBeInTheDocument();
      });
    });

    it('should format duration with hours when over 60 minutes', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: [{
          ...mockSessions[0],
          durationMinutes: 125,
        }],
        total: 1,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('2h 5m')).toBeInTheDocument();
      });
    });
  });

  describe('Session Actions', () => {
    it('should show pause button for InProgress sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const rows = screen.getAllByRole('row');
        const session1Row = rows.find(row => row.textContent?.includes('Catan'));
        expect(session1Row).toBeDefined();

        if (session1Row) {
          expect(within(session1Row).getByRole('button', { name: /pause/i })).toBeInTheDocument();
        }
      });
    });

    it('should show resume button for Paused sessions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const rows = screen.getAllByRole('row');
        const session2Row = rows.find(row => row.textContent?.includes('Ticket to Ride'));
        expect(session2Row).toBeDefined();

        if (session2Row) {
          expect(within(session2Row).getByRole('button', { name: /resume/i })).toBeInTheDocument();
        }
      });
    });

    it('should pause a session when pause button is clicked', async () => {
      const user = userEvent.setup();
      (api.sessions.pause as jest.Mock).mockResolvedValue(mockSessions[0]);

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));
      expect(session1Row).toBeDefined();

      if (session1Row) {
        const pauseButton = within(session1Row).getByRole('button', { name: /pause/i });
        await user.click(pauseButton);

        await waitFor(() => {
          expect(api.sessions.pause).toHaveBeenCalledWith('session-1');
        });
      }
    });

    it('should resume a session when resume button is clicked', async () => {
      const user = userEvent.setup();
      (api.sessions.resume as jest.Mock).mockResolvedValue(mockSessions[1]);

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session2Row = rows.find(row => row.textContent?.includes('Ticket to Ride'));
      expect(session2Row).toBeDefined();

      if (session2Row) {
        const resumeButton = within(session2Row).getByRole('button', { name: /resume/i });
        await user.click(resumeButton);

        await waitFor(() => {
          expect(api.sessions.resume).toHaveBeenCalledWith('session-2');
        });
      }
    });

    it('should show confirmation dialog when ending a session', async () => {
      const user = userEvent.setup();
      const mockConfirm = jest.spyOn(window, 'confirm').mockReturnValue(true);
      (api.sessions.end as jest.Mock).mockResolvedValue(mockSessions[0]);

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));
      expect(session1Row).toBeDefined();

      if (session1Row) {
        const endButton = within(session1Row).getByRole('button', { name: /end session/i });
        await user.click(endButton);

        expect(mockConfirm).toHaveBeenCalled();
        await waitFor(() => {
          expect(api.sessions.end).toHaveBeenCalledWith('session-1');
        });
      }

      mockConfirm.mockRestore();
    });

    it('should not end session if user cancels confirmation', async () => {
      const user = userEvent.setup();
      const mockConfirm = jest.spyOn(window, 'confirm').mockReturnValue(false);

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));
      expect(session1Row).toBeDefined();

      if (session1Row) {
        const endButton = within(session1Row).getByRole('button', { name: /end session/i });
        await user.click(endButton);

        expect(mockConfirm).toHaveBeenCalled();
        expect(api.sessions.end).not.toHaveBeenCalled();
      }

      mockConfirm.mockRestore();
    });
  });

  describe('Filtering', () => {
    it('should filter sessions by game', async () => {
      const user = userEvent.setup();
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });

      const filterSelect = screen.getByLabelText(/filter sessions by game/i);
      await user.selectOptions(filterSelect, 'game-1');

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
      });
    });

    it('should show all sessions when "All Games" is selected', async () => {
      const user = userEvent.setup();
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const filterSelect = screen.getByLabelText(/filter sessions by game/i);

      // Filter to one game
      await user.selectOptions(filterSelect, 'game-1');
      await waitFor(() => {
        expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
      });

      // Filter back to all
      await user.selectOptions(filterSelect, 'all');
      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      });
    });
  });

  describe('Pagination', () => {
    it('should not show pagination when there is only one page', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      expect(screen.queryByRole('navigation', { name: /pagination/i })).not.toBeInTheDocument();
    });

    it('should show pagination when there are multiple pages', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 50,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /pagination/i })).toBeInTheDocument();
        expect(screen.getByText(/page 1 of 3/i)).toBeInTheDocument();
      });
    });

    it('should navigate to next page when next button is clicked', async () => {
      const user = userEvent.setup();
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 50,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /pagination/i })).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: /next page/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(api.sessions.getActive).toHaveBeenCalledWith(20, 20);
      });
    });

    it('should disable previous button on first page', async () => {
      (api.sessions.getActive as jest.Mock).mockResolvedValue({
        sessions: mockSessions,
        total: 50,
        page: 1,
        pageSize: 20,
      });

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const prevButton = screen.getByRole('button', { name: /previous page/i });
        expect(prevButton).toBeDisabled();
      });
    });
  });

  describe('Navigation', () => {
    it('should navigate to session details when row is clicked', async () => {
      const user = userEvent.setup();
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));
      expect(session1Row).toBeDefined();

      if (session1Row) {
        await user.click(session1Row);
        expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
      }
    });

    it('should navigate to session details when Enter key is pressed', async () => {
      const user = userEvent.setup();
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));
      expect(session1Row).toBeDefined();

      if (session1Row) {
        session1Row.focus();
        await user.keyboard('{Enter}');
        expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
      }
    });
  });

  describe('Error Handling', () => {
    it('should display error message when session fetch fails', async () => {
      const errorMessage = 'Failed to load sessions';
      (api.sessions.getActive as jest.Mock).mockRejectedValue(new Error(errorMessage));

      render(<ActiveSessionsPage />);

      // categorizeError() transforms unknown errors into generic message
      await waitFor(() => {
        expect(screen.getByText('An unexpected error occurred. Please try again.')).toBeInTheDocument();
      });
    });

    it('should display error when pause action fails', async () => {
      const user = userEvent.setup();
      (api.sessions.pause as jest.Mock).mockRejectedValue(new Error('Failed to pause session'));

      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rows = screen.getAllByRole('row');
      const session1Row = rows.find(row => row.textContent?.includes('Catan'));

      if (session1Row) {
        const pauseButton = within(session1Row).getByRole('button', { name: /pause/i });
        await user.click(pauseButton);

        // categorizeError() transforms unknown errors into generic message
        await waitFor(() => {
          expect(screen.getByText('An unexpected error occurred. Please try again.')).toBeInTheDocument();
        });
      }
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels for actions', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/filter sessions by game/i)).toBeInTheDocument();
      });
    });

    it('should have proper roles for interactive elements', async () => {
      render(<ActiveSessionsPage />);

      await waitFor(() => {
        const rows = screen.getAllByRole('button');
        expect(rows.length).toBeGreaterThan(0);
      });
    });
  });
});
