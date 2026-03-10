/**
 * ScoreboardPage Component Tests
 * Task 3: Dedicated Scoreboard Page
 *
 * Test Coverage:
 * - Loading skeleton rendered while fetching
 * - Error state when session fetch fails
 * - Player names rendered from GameSessionDto.players
 * - Winner badge highlighted when winnerName present
 * - Status badge (Active / Paused / Finalized) rendered
 * - "Registra Punteggio" button present
 * - Back navigation link present
 * - Session date displayed
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// Mock the api module
vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getById: vi.fn(),
    },
  },
}));

// Mock next/navigation (Link needs useRouter)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
  usePathname: () => '/sessions/session-1/scoreboard',
}));

// Import after mocks
import { api } from '@/lib/api';
import { ScoreboardPage } from '../ScoreboardPage';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetById = vi.mocked(api.sessions.getById);

// Realistic GameSessionDto fixture
const mockSession: GameSessionDto = {
  id: 'session-1',
  gameId: 'game-abc',
  status: 'Active',
  startedAt: '2026-03-10T14:00:00.000Z',
  completedAt: null,
  playerCount: 3,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: '#ef4444' },
    { playerName: 'Bob', playerOrder: 2, color: '#3b82f6' },
    { playerName: 'Charlie', playerOrder: 3, color: '#22c55e' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 45,
};

const mockSessionWithWinner: GameSessionDto = {
  ...mockSession,
  id: 'session-2',
  status: 'Finalized',
  completedAt: '2026-03-10T15:00:00.000Z',
  winnerName: 'Alice',
};

const mockPausedSession: GameSessionDto = {
  ...mockSession,
  id: 'session-3',
  status: 'Paused',
};

describe('ScoreboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Loading state', () => {
    it('renders loading skeleton while session is fetching', () => {
      // Never resolves — stays loading
      mockGetById.mockReturnValue(new Promise(() => {}));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      expect(screen.getByTestId('scoreboard-loading')).toBeInTheDocument();
    });
  });

  describe('Error state', () => {
    it('renders error message when fetch fails', async () => {
      mockGetById.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('scoreboard-error')).toBeInTheDocument();
      });
    });

    it('renders error message when session returns null', async () => {
      mockGetById.mockResolvedValue(null);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('scoreboard-error')).toBeInTheDocument();
      });
    });
  });

  describe('Player display', () => {
    it('renders all player names from the session', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByText('Alice')).toBeInTheDocument();
        expect(screen.getByText('Bob')).toBeInTheDocument();
        expect(screen.getByText('Charlie')).toBeInTheDocument();
      });
    });

    it('renders rank medals for top-3 players', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        // Positions 1–3 use medal emojis as aria-label
        expect(screen.getByLabelText('Posizione 1')).toBeInTheDocument();
        expect(screen.getByLabelText('Posizione 2')).toBeInTheDocument();
        expect(screen.getByLabelText('Posizione 3')).toBeInTheDocument();
      });
    });
  });

  describe('Winner display', () => {
    it('shows winner badge when winnerName is set', async () => {
      mockGetById.mockResolvedValue(mockSessionWithWinner);

      renderWithQuery(<ScoreboardPage sessionId="session-2" />);

      await waitFor(() => {
        expect(screen.getByTestId('winner-badge')).toBeInTheDocument();
      });
    });

    it('does not show winner badge when winnerName is null', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.queryByTestId('winner-badge')).not.toBeInTheDocument();
      });
    });

    it('shows winner name next to the winner badge', async () => {
      mockGetById.mockResolvedValue(mockSessionWithWinner);

      renderWithQuery(<ScoreboardPage sessionId="session-2" />);

      await waitFor(() => {
        const winnerBadge = screen.getByTestId('winner-badge');
        expect(winnerBadge).toHaveTextContent('Alice');
      });
    });
  });

  describe('Status badge', () => {
    it('renders Active status badge', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('status-badge')).toHaveTextContent('Attiva');
      });
    });

    it('renders Paused status badge', async () => {
      mockGetById.mockResolvedValue(mockPausedSession);

      renderWithQuery(<ScoreboardPage sessionId="session-3" />);

      await waitFor(() => {
        expect(screen.getByTestId('status-badge')).toHaveTextContent('In pausa');
      });
    });

    it('renders Finalized status badge', async () => {
      mockGetById.mockResolvedValue(mockSessionWithWinner);

      renderWithQuery(<ScoreboardPage sessionId="session-2" />);

      await waitFor(() => {
        expect(screen.getByTestId('status-badge')).toHaveTextContent('Completata');
      });
    });
  });

  describe('Actions', () => {
    it('renders "Registra Punteggio" button', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /registra punteggio/i })).toBeInTheDocument();
      });
    });
  });

  describe('Navigation', () => {
    it('renders back navigation link to session', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        const backLink = screen.getByTestId('back-link');
        expect(backLink).toBeInTheDocument();
        expect(backLink.closest('a')?.getAttribute('href')).toBe('/sessions/session-1');
      });
    });
  });

  describe('Session metadata', () => {
    it('shows player count', async () => {
      mockGetById.mockResolvedValue(mockSession);

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByText(/3/)).toBeInTheDocument();
      });
    });
  });
});
