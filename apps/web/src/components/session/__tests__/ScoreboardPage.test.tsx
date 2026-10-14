/**
 * ScoreboardPage Component Tests
 * Task 3: Dedicated Scoreboard Page — #4156: legge la LiveGameSession
 *
 * Test Coverage:
 * - Loading skeleton rendered while fetching
 * - Error state when the live session fetch fails
 * - Players ranked by the totals of the live session, spectators left out
 * - Ties share a position
 * - Winner badge only on a completed session with a single leader
 * - Status badge for the live session states
 * - "Registra Punteggio" leads to the Score tab of the live page, and only while playing
 * - Back navigation link present
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

// Mock the api module
vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      getSession: vi.fn(),
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

const mockGetSession = vi.mocked(api.liveSessions.getSession);

type LivePlayer = LiveSessionDto['players'][number];

function player(
  id: string,
  displayName: string,
  totalScore: number,
  overrides: Partial<LivePlayer> = {}
): LivePlayer {
  return {
    id,
    userId: null,
    displayName,
    avatarUrl: null,
    color: 'Red',
    role: 'Player',
    teamId: null,
    totalScore,
    currentRank: 0,
    joinedAt: '2026-03-10T14:00:00.000Z',
    isActive: true,
    ...overrides,
  };
}

function liveSession(overrides: Partial<LiveSessionDto> = {}): LiveSessionDto {
  return {
    id: 'session-1',
    sessionCode: 'ABC123',
    gameId: null,
    gameName: 'Azul',
    gameSlug: 'azul',
    createdByUserId: '00000000-0000-4000-8000-00000000f001',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-03-10T13:55:00.000Z',
    startedAt: '2026-03-10T14:00:00.000Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2026-03-10T14:30:00.000Z',
    lastSavedAt: null,
    currentTurnIndex: 3,
    currentTurnPlayerId: null,
    agentMode: 'None',
    notes: null,
    // In ordine di ingresso: Marco prima di Giulia, ma Giulia ha più punti.
    players: [player('p1', 'Marco', 10, { role: 'Host' }), player('p2', 'Giulia', 30)],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: ['punti'], dimensionUnits: {} },
    ...overrides,
  };
}

async function rows(): Promise<HTMLElement[]> {
  await waitFor(() => expect(screen.getAllByTestId('scoreboard-row').length).toBeGreaterThan(0));
  return screen.getAllByTestId('scoreboard-row');
}

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
      mockGetSession.mockReturnValue(new Promise(() => {}));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      expect(screen.getByTestId('scoreboard-loading')).toBeInTheDocument();
    });
  });

  describe('Error state', () => {
    it('renders error message when fetch fails', async () => {
      mockGetSession.mockRejectedValue(new Error('Session not found'));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('scoreboard-error')).toBeInTheDocument();
      });
    });
  });

  describe('Ranking (#4156)', () => {
    it('reads the live session of the route id', async () => {
      mockGetSession.mockResolvedValue(liveSession());

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await rows();
      expect(mockGetSession).toHaveBeenCalledWith('session-1');
    });

    it('ranks players by their total, highest first', async () => {
      mockGetSession.mockResolvedValue(liveSession());

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      const [first, second] = await rows();
      expect(first).toHaveAttribute('data-rank', '1');
      expect(first).toHaveTextContent('Giulia');
      expect(first).toHaveTextContent('30');
      expect(second).toHaveAttribute('data-rank', '2');
      expect(second).toHaveTextContent('Marco');
      expect(second).toHaveTextContent('10');
    });

    it('gives tied players the same position, in the order they joined', async () => {
      // In ordine di ingresso l'ultimo in classifica entra per primo: così il test vede anche
      // un ordinamento che non guarda il totale.
      mockGetSession.mockResolvedValue(
        liveSession({
          players: [player('p3', 'Luca', 5), player('p1', 'Marco', 20), player('p2', 'Giulia', 20)],
        })
      );

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      const ranked = await rows();
      expect(ranked.map(r => r.getAttribute('data-rank'))).toEqual(['1', '1', '3']);
      expect(ranked.map(r => r.textContent)).toEqual([
        expect.stringContaining('Marco'),
        expect.stringContaining('Giulia'),
        expect.stringContaining('Luca'),
      ]);
    });

    it('leaves spectators out of the ranking and the player count', async () => {
      mockGetSession.mockResolvedValue(
        liveSession({
          players: [
            player('p1', 'Marco', 10, { role: 'Host' }),
            player('p2', 'Giulia', 30),
            player('p3', 'Chi guarda', 0, { role: 'Spectator' }),
          ],
        })
      );

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      expect(await rows()).toHaveLength(2);
      expect(screen.queryByText('Chi guarda')).not.toBeInTheDocument();
      expect(screen.getByTestId('player-count')).toHaveTextContent('2');
    });
  });

  describe('Winner display', () => {
    it('names the winner of a completed session', async () => {
      mockGetSession.mockResolvedValue(
        liveSession({ status: 'Completed', completedAt: '2026-03-10T15:00:00.000Z' })
      );

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('winner-badge')).toHaveTextContent('Vincitore: Giulia');
      });
    });

    it('names no winner while the game is still going', async () => {
      mockGetSession.mockResolvedValue(liveSession({ status: 'InProgress' }));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await rows();
      expect(screen.queryByTestId('winner-badge')).not.toBeInTheDocument();
    });

    it('names no winner when the first place is shared', async () => {
      mockGetSession.mockResolvedValue(
        liveSession({
          status: 'Completed',
          players: [player('p1', 'Marco', 20), player('p2', 'Giulia', 20)],
        })
      );

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await rows();
      expect(screen.queryByTestId('winner-badge')).not.toBeInTheDocument();
    });
  });

  describe('Status badge', () => {
    it.each([
      ['InProgress', 'In corso'],
      ['Paused', 'In pausa'],
      ['Completed', 'Completata'],
      ['Created', 'In preparazione'],
    ] as const)('renders the %s status', async (status, label) => {
      mockGetSession.mockResolvedValue(liveSession({ status }));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('status-badge')).toHaveTextContent(label);
      });
    });
  });

  describe('Actions', () => {
    it('leads "Registra Punteggio" to the Score tab of the live page', async () => {
      mockGetSession.mockResolvedValue(liveSession());

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('record-score-link')).toHaveAttribute(
          'href',
          '/sessions/session-1/live?tab=score'
        );
      });
    });

    it('offers no score entry once the game is over', async () => {
      mockGetSession.mockResolvedValue(liveSession({ status: 'Completed' }));

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await rows();
      expect(screen.queryByTestId('record-score-link')).not.toBeInTheDocument();
    });
  });

  describe('Navigation', () => {
    it('renders back navigation link to session', async () => {
      mockGetSession.mockResolvedValue(liveSession());

      renderWithQuery(<ScoreboardPage sessionId="session-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('back-link')).toHaveAttribute('href', '/sessions/session-1');
      });
    });
  });
});
