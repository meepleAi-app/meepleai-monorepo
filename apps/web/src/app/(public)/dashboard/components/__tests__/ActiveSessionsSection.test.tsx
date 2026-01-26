/**
 * ActiveSessionsSection Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - Loading state (skeleton grid)
 * - Error state (alert with message)
 * - Empty state (hidden/null)
 * - Data rendering (session cards)
 * - Session status badges (InProgress, Paused, Setup)
 * - Pause/Resume button states
 * - Navigation to session detail
 * - Accessibility
 *
 * Target: >=85% coverage
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ActiveSessionsSection } from '../ActiveSessionsSection';
import * as useActiveSessionsModule from '@/hooks/queries/useActiveSessions';
import * as useGamesModule from '@/hooks/queries/useGames';
import type { GameSessionDto, PaginatedSessionsResponse, Game, PaginatedGamesResponse } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/hooks/queries/useActiveSessions', async importOriginal => {
  const actual = await importOriginal<typeof useActiveSessionsModule>();
  return {
    ...actual,
    useActiveSessions: vi.fn(),
    usePauseSession: vi.fn(),
    useResumeSession: vi.fn(),
  };
});

vi.mock('@/hooks/queries/useGames', async importOriginal => {
  const actual = await importOriginal<typeof useGamesModule>();
  return {
    ...actual,
    useGames: vi.fn(),
  };
});

// ============================================================================
// Test Data
// ============================================================================

const mockSessionInProgress: GameSessionDto = {
  id: 'session-1',
  gameId: 'game-1',
  status: 'InProgress',
  startedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(), // 30 min ago
  completedAt: null,
  playerCount: 4,
  players: [
    { id: 'p1', name: 'Mario', color: '#ff0000' },
    { id: 'p2', name: 'Luigi', color: '#00ff00' },
    { id: 'p3', name: 'Peach', color: '#ff69b4' },
    { id: 'p4', name: 'Toad', color: '#0000ff' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 30,
};

const mockSessionPaused: GameSessionDto = {
  id: 'session-2',
  gameId: 'game-2',
  status: 'Paused',
  startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(), // 1 hour ago
  completedAt: null,
  playerCount: 2,
  players: [
    { id: 'p5', name: 'Wario', color: '#ffff00' },
    { id: 'p6', name: 'Waluigi', color: '#800080' },
  ],
  winnerName: null,
  notes: 'Paused for dinner',
  durationMinutes: 45,
};

const mockSessionSetup: GameSessionDto = {
  id: 'session-3',
  gameId: 'game-3',
  status: 'Setup',
  startedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 min ago
  completedAt: null,
  playerCount: 3,
  players: [
    { id: 'p7', name: 'Yoshi', color: '#00ff00' },
    { id: 'p8', name: 'Birdo', color: '#ff69b4' },
    { id: 'p9', name: 'Donkey Kong', color: '#8b4513' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 0,
};

const mockSessionsResponse: PaginatedSessionsResponse = {
  sessions: [mockSessionInProgress, mockSessionPaused, mockSessionSetup],
  total: 3,
  page: 1,
  pageSize: 3,
};

const emptySessionsResponse: PaginatedSessionsResponse = {
  sessions: [],
  total: 0,
  page: 1,
  pageSize: 3,
};

const mockGames: Game[] = [
  { id: 'game-1', title: 'Catan', description: '', imageUrl: null, minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, complexity: 2.5, yearPublished: 1995, publisher: 'Kosmos', designer: 'Klaus Teuber', categories: [], mechanics: [], source: 'manual', sourceId: null, createdAt: '', updatedAt: null },
  { id: 'game-2', title: 'Azul', description: '', imageUrl: null, minPlayers: 2, maxPlayers: 4, playingTimeMinutes: 45, complexity: 1.8, yearPublished: 2017, publisher: 'Plan B', designer: 'Michael Kiesling', categories: [], mechanics: [], source: 'manual', sourceId: null, createdAt: '', updatedAt: null },
  { id: 'game-3', title: 'Wingspan', description: '', imageUrl: null, minPlayers: 1, maxPlayers: 5, playingTimeMinutes: 60, complexity: 2.4, yearPublished: 2019, publisher: 'Stonemaier', designer: 'Elizabeth Hargrave', categories: [], mechanics: [], source: 'manual', sourceId: null, createdAt: '', updatedAt: null },
];

const mockGamesResponse: PaginatedGamesResponse = {
  games: mockGames,
  totalCount: 3,
  page: 1,
  pageSize: 100,
  hasNextPage: false,
  hasPreviousPage: false,
};

// ============================================================================
// Test Suite
// ============================================================================

describe('ActiveSessionsSection', () => {
  let queryClient: QueryClient;

  const mockPauseMutation = {
    mutateAsync: vi.fn(),
    isPending: false,
    variables: undefined,
  };

  const mockResumeMutation = {
    mutateAsync: vi.fn(),
    isPending: false,
    variables: undefined,
  };

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();

    vi.mocked(useActiveSessionsModule.usePauseSession).mockReturnValue(mockPauseMutation as unknown as ReturnType<typeof useActiveSessionsModule.usePauseSession>);
    vi.mocked(useActiveSessionsModule.useResumeSession).mockReturnValue(mockResumeMutation as unknown as ReturnType<typeof useActiveSessionsModule.useResumeSession>);
  });

  const renderComponent = (limit?: number) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <ActiveSessionsSection limit={limit} />
      </QueryClientProvider>
    );
  };

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton grid while loading', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      expect(screen.getByText('Partite in Corso')).toBeInTheDocument();
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBe(3); // default limit
    });

    it('renders correct number of skeletons based on limit', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent(5);

      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBe(5);
    });
  });

  // ============================================================================
  // Error State Tests
  // ============================================================================

  describe('Error State', () => {
    it('renders error alert when sessions query fails', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByText('Errore di Caricamento')).toBeInTheDocument();
      expect(screen.getByText(/Impossibile caricare le sessioni attive/)).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    it('handles non-Error error objects', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: 'String error message',
      } as unknown as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByText('String error message')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('returns null when no active sessions', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: emptySessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      expect(container.querySelector('section')).not.toBeInTheDocument();
    });

    it('returns null when sessions data is null', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      expect(container.querySelector('section')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Data Rendering Tests
  // ============================================================================

  describe('Data Rendering', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('renders session cards with game titles', () => {
      renderComponent();

      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Azul')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('renders player count for each session', () => {
      renderComponent();

      expect(screen.getByText('4 giocatori')).toBeInTheDocument();
      expect(screen.getByText('2 giocatori')).toBeInTheDocument();
      expect(screen.getByText('3 giocatori')).toBeInTheDocument();
    });

    it('renders singular "giocatore" for 1 player', () => {
      const singlePlayerSession: PaginatedSessionsResponse = {
        sessions: [{
          ...mockSessionInProgress,
          playerCount: 1,
          players: [{ id: 'p1', name: 'Solo', color: '#ff0000' }],
        }],
        total: 1,
        page: 1,
        pageSize: 3,
      };

      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: singlePlayerSession,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      renderComponent();

      expect(screen.getByText('1 giocatore')).toBeInTheDocument();
    });

    it('renders total sessions badge', () => {
      renderComponent();

      // Badge shows total count
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    it('renders "Vedi Tutte" link', () => {
      renderComponent();

      const link = screen.getByRole('link', { name: /Vedi Tutte/i });
      expect(link).toHaveAttribute('href', '/sessions');
    });

    it('shows "Gioco sconosciuto" for unknown game', () => {
      const unknownGameSession: PaginatedSessionsResponse = {
        sessions: [{
          ...mockSessionInProgress,
          gameId: 'unknown-game-id',
        }],
        total: 1,
        page: 1,
        pageSize: 3,
      };

      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: unknownGameSession,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      renderComponent();

      expect(screen.getByText('Gioco sconosciuto')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Status Badge Tests
  // ============================================================================

  describe('Status Badges', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('renders "In Corso" badge for InProgress status', () => {
      renderComponent();

      expect(screen.getByText('In Corso')).toBeInTheDocument();
    });

    it('renders "In Pausa" badge for Paused status', () => {
      renderComponent();

      expect(screen.getByText('In Pausa')).toBeInTheDocument();
    });

    it('renders "Preparazione" badge for Setup status', () => {
      renderComponent();

      expect(screen.getByText('Preparazione')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Action Button Tests
  // ============================================================================

  describe('Action Buttons', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('renders Pause button for InProgress session', () => {
      renderComponent();

      expect(screen.getByRole('button', { name: /Metti in pausa sessione di Catan/i })).toBeInTheDocument();
    });

    it('renders Resume button for Paused session', () => {
      renderComponent();

      expect(screen.getByRole('button', { name: /Riprendi sessione di Azul/i })).toBeInTheDocument();
    });

    it('shows "Pausando..." when pause is pending', () => {
      vi.mocked(useActiveSessionsModule.usePauseSession).mockReturnValue({
        mutateAsync: vi.fn(),
        isPending: true,
        variables: 'session-1',
      } as unknown as ReturnType<typeof useActiveSessionsModule.usePauseSession>);

      renderComponent();

      expect(screen.getByText('Pausando...')).toBeInTheDocument();
    });

    it('shows "Riprendendo..." when resume is pending', () => {
      vi.mocked(useActiveSessionsModule.useResumeSession).mockReturnValue({
        mutateAsync: vi.fn(),
        isPending: true,
        variables: 'session-2',
      } as unknown as ReturnType<typeof useActiveSessionsModule.useResumeSession>);

      renderComponent();

      expect(screen.getByText('Riprendendo...')).toBeInTheDocument();
    });

    it('calls pause mutation when Pause button is clicked', async () => {
      const user = userEvent.setup();
      const mockMutateAsync = vi.fn().mockResolvedValue({});

      vi.mocked(useActiveSessionsModule.usePauseSession).mockReturnValue({
        mutateAsync: mockMutateAsync,
        isPending: false,
        variables: undefined,
      } as unknown as ReturnType<typeof useActiveSessionsModule.usePauseSession>);

      renderComponent();

      const pauseButton = screen.getByRole('button', { name: /Metti in pausa sessione di Catan/i });
      await user.click(pauseButton);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith('session-1');
      });
    });

    it('calls resume mutation when Resume button is clicked', async () => {
      const user = userEvent.setup();
      const mockMutateAsync = vi.fn().mockResolvedValue({});

      vi.mocked(useActiveSessionsModule.useResumeSession).mockReturnValue({
        mutateAsync: mockMutateAsync,
        isPending: false,
        variables: undefined,
      } as unknown as ReturnType<typeof useActiveSessionsModule.useResumeSession>);

      renderComponent();

      const resumeButton = screen.getByRole('button', { name: /Riprendi sessione di Azul/i });
      await user.click(resumeButton);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith('session-2');
      });
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('navigates to session detail when card is clicked', async () => {
      const user = userEvent.setup();
      renderComponent();

      const catanCard = screen.getByRole('button', { name: /Vai alla sessione di Catan/i });
      await user.click(catanCard);

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });

    it('navigates on Enter key press', async () => {
      const user = userEvent.setup();
      renderComponent();

      const catanCard = screen.getByRole('button', { name: /Vai alla sessione di Catan/i });
      catanCard.focus();
      await user.keyboard('{Enter}');

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });

    it('navigates on Space key press', async () => {
      const user = userEvent.setup();
      renderComponent();

      const catanCard = screen.getByRole('button', { name: /Vai alla sessione di Catan/i });
      catanCard.focus();
      await user.keyboard(' ');

      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('has correct aria-label on section', () => {
      renderComponent();

      expect(screen.getByLabelText('Sessioni attive')).toBeInTheDocument();
    });

    it('has correct heading structure', () => {
      renderComponent();

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Partite in Corso');
    });

    it('session cards have button role and aria-label', () => {
      renderComponent();

      const sessionButtons = screen.getAllByRole('button', { name: /Vai alla sessione di/i });
      expect(sessionButtons.length).toBe(3);
    });

    it('session cards are keyboard focusable', () => {
      renderComponent();

      const sessionCards = screen.getAllByRole('button', { name: /Vai alla sessione di/i });
      sessionCards.forEach(card => {
        expect(card).toHaveAttribute('tabIndex', '0');
      });
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('passes correct limit to useActiveSessions', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent(5);

      expect(useActiveSessionsModule.useActiveSessions).toHaveBeenCalledWith(5);
    });

    it('uses default limit of 3 when not specified', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesResponse,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(useActiveSessionsModule.useActiveSessions).toHaveBeenCalledWith(3);
    });
  });
});
