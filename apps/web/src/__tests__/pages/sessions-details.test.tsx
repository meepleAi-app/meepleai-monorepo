/**
 * SPRINT-4: Session Details Tests (Issue #1134)
 */

import React from 'react';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import { useRouter } from 'next/router';
import SessionDetailsPage from '@/pages/sessions/[id]';
import { api } from '@/lib/api';

jest.mock('next/router', () => ({ useRouter: jest.fn() }));
jest.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getById: jest.fn(),
      pause: jest.fn(),
      resume: jest.fn(),
      end: jest.fn()
    },
    games: {
      getById: jest.fn()
    }
  }
}));

describe('SessionDetailsPage', () => {
  const mockPush = jest.fn();
  const mockSession = {
    id: 'session-1',
    gameId: 'game-1',
    status: 'InProgress',
    startedAt: '2024-01-15T10:00:00Z',
    completedAt: null,
    playerCount: 4,
    players: [
      { playerName: 'Alice', playerOrder: 1, color: '#FF0000' },
      { playerName: 'Bob', playerOrder: 2, color: '#00FF00' },
      { playerName: 'Charlie', playerOrder: 3, color: '#0000FF' },
      { playerName: 'Diana', playerOrder: 4, color: null }
    ],
    winnerName: null,
    notes: 'Great game!',
    durationMinutes: 120
  };

  const mockGame = {
    id: 'game-1',
    title: 'Catan',
    publisher: 'KOSMOS',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: '2024-01-01T00:00:00Z'
  };

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue({
      push: mockPush,
      query: { id: 'session-1' }
    });
  });

  describe('Loading State', () => {
    it('should display loading skeleton', async () => {
      (api.sessions.getById as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 1000))
      );

      render(<SessionDetailsPage />);

      // Check for skeleton class in the DOM
      const skeletons = document.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });
  });

  describe('Session Display', () => {
    beforeEach(() => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    });

    it('should display session details', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Details')).toBeInTheDocument();
        expect(screen.getByLabelText(/session status: inprogress/i)).toBeInTheDocument();
      });
    });

    it('should display game information', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('KOSMOS')).toBeInTheDocument();
        expect(screen.getByText('1995')).toBeInTheDocument();
      });
    });

    it('should display player list with colors', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Alice')).toBeInTheDocument();
        expect(screen.getByText('Bob')).toBeInTheDocument();
        expect(screen.getByText('Charlie')).toBeInTheDocument();
        expect(screen.getByText('Diana')).toBeInTheDocument();
      });
    });

    it('should display player avatars with colors', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        const aliceAvatar = screen.getByLabelText(/player alice color/i);
        expect(aliceAvatar).toBeInTheDocument();
      });
    });

    it('should display session notes', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Great game!')).toBeInTheDocument();
      });
    });

    it('should format duration correctly', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('2 hours 0 minutes')).toBeInTheDocument();
      });
    });

    it('should display winner badge if present', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Completed',
        winnerName: 'Alice'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText(/winner: alice/i)).toBeInTheDocument();
      });
    });
  });

  describe('Timeline Display', () => {
    beforeEach(() => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    });

    it('should display session started event', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Started')).toBeInTheDocument();
      });
    });

    it('should display pause event for paused sessions', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Paused'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Paused')).toBeInTheDocument();
      });
    });

    it('should display completion event for completed sessions', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Completed',
        completedAt: '2024-01-15T12:00:00Z'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Completed')).toBeInTheDocument();
      });
    });

    it('should display abandoned event for abandoned sessions', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Abandoned',
        completedAt: '2024-01-15T11:00:00Z'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText('Session Abandoned')).toBeInTheDocument();
      });
    });
  });

  describe('Session Actions', () => {
    beforeEach(() => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    });

    it('should show pause button for InProgress sessions', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /pause session/i })).toBeInTheDocument();
      });
    });

    it('should show resume button for Paused sessions', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Paused'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /resume session/i })).toBeInTheDocument();
      });
    });

    it('should show end button for active sessions', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /end session/i })).toBeInTheDocument();
      });
    });

    it('should not show action buttons for completed sessions', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Completed',
        completedAt: '2024-01-15T12:00:00Z'
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.queryByRole('button', { name: /pause session/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /end session/i })).not.toBeInTheDocument();
      });
    });

    it('should call pause API on pause button click', async () => {
      (api.sessions.pause as jest.Mock).mockResolvedValue({ ...mockSession, status: 'Paused' });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        const pauseButton = screen.getByRole('button', { name: /pause session/i });
        fireEvent.click(pauseButton);
      });

      await waitFor(() => {
        expect(api.sessions.pause).toHaveBeenCalledWith('session-1');
      });
    });

    it('should call resume API on resume button click', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        status: 'Paused'
      });
      (api.sessions.resume as jest.Mock).mockResolvedValue({ ...mockSession, status: 'InProgress' });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        const resumeButton = screen.getByRole('button', { name: /resume session/i });
        fireEvent.click(resumeButton);
      });

      await waitFor(() => {
        expect(api.sessions.resume).toHaveBeenCalledWith('session-1');
      });
    });

    it('should show confirmation before ending session', async () => {
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);

      render(<SessionDetailsPage />);

      await waitFor(() => {
        const endButton = screen.getByRole('button', { name: /end session/i });
        fireEvent.click(endButton);
      });

      expect(confirmSpy).toHaveBeenCalled();
      expect(api.sessions.end).not.toHaveBeenCalled();

      confirmSpy.mockRestore();
    });

    it('should navigate to sessions page after ending session', async () => {
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      (api.sessions.end as jest.Mock).mockResolvedValue({ ...mockSession, status: 'Completed' });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        const endButton = screen.getByRole('button', { name: /end session/i });
        fireEvent.click(endButton);
      });

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/sessions');
      });

      confirmSpy.mockRestore();
    });
  });

  describe('Navigation', () => {
    beforeEach(() => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    });

    it('should have back link to sessions page', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        const backLink = screen.getByText(/back to sessions/i);
        expect(backLink).toHaveAttribute('href', '/sessions');
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error when session not found', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(null);

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      });
    });

    it('should display error on API failure', async () => {
      (api.sessions.getById as jest.Mock).mockRejectedValue(new Error('API Error'));

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getAllByText(/unexpected error/i)[0]).toBeInTheDocument();
      });
    });

    it('should show back button on error', async () => {
      (api.sessions.getById as jest.Mock).mockRejectedValue(new Error('API Error'));

      render(<SessionDetailsPage />);

      await waitFor(() => {
        // ErrorDisplay shows either "Go Back" or "Cancel" button depending on canRetry
        const buttons = screen.getAllByRole('button');
        const dismissButton = buttons.find(btn => btn.textContent?.match(/(go back|cancel)/i));
        expect(dismissButton).toBeDefined();
        fireEvent.click(dismissButton!);
      });

      expect(mockPush).toHaveBeenCalledWith('/sessions');
    });

    it('should display error when action fails', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
      (api.sessions.pause as jest.Mock).mockRejectedValue(new Error('Pause failed'));

      render(<SessionDetailsPage />);

      await waitFor(() => {
        const pauseButton = screen.getByRole('button', { name: /pause session/i });
        fireEvent.click(pauseButton);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getAllByText(/unexpected error/i)[0]).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    beforeEach(() => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    });

    it('should have proper heading structure', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /session details/i })).toBeInTheDocument();
        expect(screen.getByText('Game Information')).toBeInTheDocument();
      });
    });

    it('should have accessible player avatars', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        const aliceAvatar = screen.getByLabelText(/player alice color/i);
        expect(aliceAvatar).toBeInTheDocument();
      });
    });

    it('should have accessible timeline icons', async () => {
      render(<SessionDetailsPage />);

      await waitFor(() => {
        const startIcon = screen.getByLabelText('Session Started');
        expect(startIcon).toHaveAttribute('role', 'img');
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing query parameter', () => {
      (useRouter as jest.Mock).mockReturnValue({
        push: mockPush,
        query: {}
      });

      render(<SessionDetailsPage />);

      expect(api.sessions.getById).not.toHaveBeenCalled();
    });

    it('should handle session without players', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        players: []
      });
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.getByText(/no players recorded/i)).toBeInTheDocument();
      });
    });

    it('should handle session without notes', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue({
        ...mockSession,
        notes: null
      });
      (api.games.getById as jest.Mock).mockResolvedValue(mockGame);

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.queryByText(/notes/i)).not.toBeInTheDocument();
      });
    });

    it('should handle game without publisher', async () => {
      (api.sessions.getById as jest.Mock).mockResolvedValue(mockSession);
      (api.games.getById as jest.Mock).mockResolvedValue({
        ...mockGame,
        publisher: null
      });

      render(<SessionDetailsPage />);

      await waitFor(() => {
        expect(screen.queryByText(/publisher/i)).not.toBeInTheDocument();
      });
    });
  });
});
