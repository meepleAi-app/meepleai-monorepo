/**
 * ActiveSessionsPanel Component Tests (Issue #2858)
 *
 * Test Coverage:
 * - Loading state (skeleton cards)
 * - Error state (alert)
 * - Empty state ('Nessuna sessione attiva')
 * - Success state with sessions
 * - Game cover display
 * - Session card content (title, time)
 * - Continue and Details buttons
 * - View All link when more sessions exist
 * - Accessibility
 *
 * Target: >=85% coverage
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ActiveSessionsPanel } from '../ActiveSessionsPanel';
import * as useActiveSessionsModule from '@/hooks/queries/useActiveSessions';
import * as useGamesModule from '@/hooks/queries/useGames';
import type { PaginatedSessionsResponse, PaginatedGamesResponse, GameSessionDto, Game } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/useActiveSessions', async importOriginal => {
  const actual = await importOriginal<typeof useActiveSessionsModule>();
  return {
    ...actual,
    useActiveSessions: vi.fn(),
  };
});

vi.mock('@/hooks/queries/useGames', async importOriginal => {
  const actual = await importOriginal<typeof useGamesModule>();
  return {
    ...actual,
    useGames: vi.fn(),
  };
});

vi.mock('next/link', () => ({
  default: ({ children, href, className, 'data-testid': testId }: { children: React.ReactNode; href: string; className?: string; 'data-testid'?: string }) => (
    <a href={href} className={className} data-testid={testId}>{children}</a>
  ),
}));

vi.mock('next/image', () => ({
  default: ({ src, alt, className, fill, sizes }: { src: string; alt: string; className?: string; fill?: boolean; sizes?: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} className={className} data-fill={fill} data-sizes={sizes} />
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGame1: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: '2024-01-01T00:00:00Z',
  imageUrl: 'https://example.com/catan.jpg',
  iconUrl: null,
  description: 'A classic settlement game',
  faqCount: 5,
  averageRating: 4.5,
  sharedGameId: null,
};

const mockGame2: Game = {
  id: 'game-2',
  title: 'Ticket to Ride',
  publisher: 'Days of Wonder',
  yearPublished: 2004,
  minPlayers: 2,
  maxPlayers: 5,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  bggId: 9209,
  createdAt: '2024-01-02T00:00:00Z',
  imageUrl: null,
  iconUrl: 'https://example.com/ttr-icon.png',
  description: 'Train adventure game',
  faqCount: 3,
  averageRating: 4.2,
  sharedGameId: null,
};

const mockGameNoImage: Game = {
  id: 'game-3',
  title: 'Mysterium',
  publisher: 'Libellud',
  yearPublished: 2015,
  minPlayers: 2,
  maxPlayers: 7,
  minPlayTimeMinutes: 42,
  maxPlayTimeMinutes: 42,
  bggId: 181304,
  createdAt: '2024-01-03T00:00:00Z',
  imageUrl: null,
  iconUrl: null,
  description: null,
  faqCount: null,
  averageRating: null,
  sharedGameId: null,
};

const mockSession1: GameSessionDto = {
  id: 'session-1',
  gameId: 'game-1',
  status: 'InProgress',
  startedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(), // 30 min ago
  completedAt: null,
  playerCount: 4,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: 'red' },
    { playerName: 'Bob', playerOrder: 2, color: 'blue' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 30,
};

const mockSession2: GameSessionDto = {
  id: 'session-2',
  gameId: 'game-2',
  status: 'Paused',
  startedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
  completedAt: null,
  playerCount: 3,
  players: [
    { playerName: 'Charlie', playerOrder: 1, color: null },
  ],
  winnerName: null,
  notes: 'Taking a break',
  durationMinutes: 90,
};

const mockSession3: GameSessionDto = {
  id: 'session-3',
  gameId: 'game-3',
  status: 'Setup',
  startedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 min ago
  completedAt: null,
  playerCount: 5,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 5,
};

const mockSessionsData: PaginatedSessionsResponse = {
  sessions: [mockSession1, mockSession2, mockSession3],
  total: 3,
  page: 1,
  pageSize: 3,
};

const mockSessionsDataWithMore: PaginatedSessionsResponse = {
  sessions: [mockSession1, mockSession2, mockSession3],
  total: 7,
  page: 1,
  pageSize: 3,
};

const mockGamesData: PaginatedGamesResponse = {
  games: [mockGame1, mockGame2, mockGameNoImage],
  total: 3,
  page: 1,
  pageSize: 100,
  totalPages: 1,
};

const mockEmptySessions: PaginatedSessionsResponse = {
  sessions: [],
  total: 0,
  page: 1,
  pageSize: 3,
};

// ============================================================================
// Test Suite
// ============================================================================

describe('ActiveSessionsPanel', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = (props: { limit?: number; className?: string } = {}) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <ActiveSessionsPanel {...props} />
      </QueryClientProvider>
    );
  };

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders loading skeleton while sessions are loading', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel')).toBeInTheDocument();
      expect(screen.getByTestId('active-sessions-panel-title')).toBeInTheDocument();
      expect(screen.getByTestId('active-sessions-panel-loading')).toBeInTheDocument();
    });

    it('renders loading skeleton while games are loading', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-loading')).toBeInTheDocument();
    });

    it('renders correct number of skeleton cards based on limit', () => {
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

      renderComponent({ limit: 5 });

      for (let i = 0; i < 5; i++) {
        expect(screen.getByTestId(`active-sessions-panel-skeleton-${i}`)).toBeInTheDocument();
      }
    });

    it('renders PlayCircle icon in loading state header', () => {
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

      const icon = container.querySelector('svg.lucide-circle-play');
      expect(icon).toBeInTheDocument();
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
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel')).toBeInTheDocument();
      expect(screen.getByTestId('active-sessions-panel-title')).toBeInTheDocument();
      expect(screen.getByTestId('active-sessions-panel-error')).toBeInTheDocument();
    });

    it('renders error message text', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-error-message')).toBeInTheDocument();
    });

    it('renders AlertCircle icon in error state', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-circle-alert');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no sessions exist', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-empty')).toBeInTheDocument();
    });

    it('renders empty state text "Nessuna sessione attiva"', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-empty-text')).toBeInTheDocument();
    });

    it('renders "Inizia una Partita" CTA button in empty state', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-start-cta')).toBeInTheDocument();
    });

    it('"Inizia una Partita" links to /games', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-start-cta')).toHaveAttribute('href', '/games');
    });

    it('renders Gamepad2 icon in empty state', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-gamepad-2');
      expect(icon).toBeInTheDocument();
    });

    it('renders empty state when sessions is undefined', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: { sessions: undefined, total: 0, page: 1, pageSize: 3 } as unknown as PaginatedSessionsResponse,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-empty')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Success State - Session Cards Tests
  // ============================================================================

  describe('Session Cards', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('renders sessions list', () => {
      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-list')).toBeInTheDocument();
    });

    it('renders correct number of session cards', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-session-2')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-session-3')).toBeInTheDocument();
    });

    it('renders game title for each session', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-title-session-1')).toHaveTextContent('Catan');
      expect(screen.getByTestId('session-card-title-session-2')).toHaveTextContent('Ticket to Ride');
    });

    it('renders started time for each session', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-time-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-time-session-2')).toBeInTheDocument();
    });

    it('renders game cover image when imageUrl exists', () => {
      renderComponent();

      const coverContainer = screen.getByTestId('session-card-cover-session-1');
      const img = coverContainer.querySelector('img');
      expect(img).toBeInTheDocument();
      expect(img).toHaveAttribute('src', 'https://example.com/catan.jpg');
    });

    it('renders game cover image from iconUrl when imageUrl is null', () => {
      renderComponent();

      const coverContainer = screen.getByTestId('session-card-cover-session-2');
      const img = coverContainer.querySelector('img');
      expect(img).toBeInTheDocument();
      expect(img).toHaveAttribute('src', 'https://example.com/ttr-icon.png');
    });

    it('renders fallback icon when game has no image', () => {
      renderComponent();

      const coverContainer = screen.getByTestId('session-card-cover-session-3');
      const img = coverContainer.querySelector('img');
      expect(img).not.toBeInTheDocument();
      const icon = coverContainer.querySelector('svg.lucide-gamepad-2');
      expect(icon).toBeInTheDocument();
    });

    it('renders "Gioco sconosciuto" for unknown game', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: {
          sessions: [{
            ...mockSession1,
            id: 'session-unknown',
            gameId: 'unknown-game',
          }],
          total: 1,
          page: 1,
          pageSize: 3,
        },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      renderComponent();

      expect(screen.getByTestId('session-card-title-session-unknown')).toHaveTextContent('Gioco sconosciuto');
    });
  });

  // ============================================================================
  // Session Card Buttons Tests
  // ============================================================================

  describe('Session Card Buttons', () => {
    beforeEach(() => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);
    });

    it('renders Continue button for each session', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-continue-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-continue-session-2')).toBeInTheDocument();
    });

    it('Continue button links to session page', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-continue-session-1')).toHaveAttribute('href', '/sessions/session-1');
      expect(screen.getByTestId('session-card-continue-session-2')).toHaveAttribute('href', '/sessions/session-2');
    });

    it('renders Details button for each session', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-details-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-details-session-2')).toBeInTheDocument();
    });

    it('Details button links to game page', () => {
      renderComponent();

      expect(screen.getByTestId('session-card-details-session-1')).toHaveAttribute('href', '/games/game-1');
      expect(screen.getByTestId('session-card-details-session-2')).toHaveAttribute('href', '/games/game-2');
    });

    it('renders PlayCircle icon in Continue button', () => {
      const { container } = renderComponent();

      const continueButton = screen.getByTestId('session-card-continue-session-1');
      const icon = continueButton.querySelector('svg.lucide-circle-play');
      expect(icon).toBeInTheDocument();
    });

    it('renders ExternalLink icon in Details button', () => {
      renderComponent();

      const detailsButton = screen.getByTestId('session-card-details-session-1');
      const icon = detailsButton.querySelector('svg.lucide-external-link');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // View All Link Tests
  // ============================================================================

  describe('View All Link', () => {
    it('shows View All when total > limit', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsDataWithMore,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 3 });

      expect(screen.getByTestId('active-sessions-panel-view-all')).toBeInTheDocument();
    });

    it('View All displays total count', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsDataWithMore,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 3 });

      expect(screen.getByTestId('active-sessions-panel-view-all')).toHaveTextContent('7');
    });

    it('View All links to /sessions', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsDataWithMore,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 3 });

      expect(screen.getByTestId('active-sessions-panel-view-all')).toHaveAttribute('href', '/sessions');
    });

    it('hides View All when total <= limit', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 3 });

      expect(screen.queryByTestId('active-sessions-panel-view-all')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Custom className Tests
  // ============================================================================

  describe('Custom className', () => {
    it('applies custom className to Card', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ className: 'custom-class' });

      const widget = screen.getByTestId('active-sessions-panel');
      expect(widget).toHaveClass('custom-class');
    });

    it('applies custom className in loading state', () => {
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

      renderComponent({ className: 'loading-class' });

      const widget = screen.getByTestId('active-sessions-panel');
      expect(widget).toHaveClass('loading-class');
    });

    it('applies custom className in error state', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ className: 'error-class' });

      const widget = screen.getByTestId('active-sessions-panel');
      expect(widget).toHaveClass('error-class');
    });

    it('applies custom className in empty state', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockEmptySessions,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ className: 'empty-class' });

      const widget = screen.getByTestId('active-sessions-panel');
      expect(widget).toHaveClass('empty-class');
    });
  });

  // ============================================================================
  // Limit Prop Tests
  // ============================================================================

  describe('Limit Prop', () => {
    it('uses default limit of 3', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(useActiveSessionsModule.useActiveSessions).toHaveBeenCalledWith(3);
    });

    it('uses custom limit when provided', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 5 });

      expect(useActiveSessionsModule.useActiveSessions).toHaveBeenCalledWith(5);
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has accessible title', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(screen.getByTestId('active-sessions-panel-title')).toBeInTheDocument();
    });

    it('CTA buttons are accessible links', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      const continueLink = screen.getByTestId('session-card-continue-session-1');
      expect(continueLink.tagName.toLowerCase()).toBe('a');
      expect(continueLink).toHaveAttribute('href');

      const detailsLink = screen.getByTestId('session-card-details-session-1');
      expect(detailsLink.tagName.toLowerCase()).toBe('a');
      expect(detailsLink).toHaveAttribute('href');
    });

    it('game cover image has alt text', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      const coverContainer = screen.getByTestId('session-card-cover-session-1');
      const img = coverContainer.querySelector('img');
      expect(img).toHaveAttribute('alt', 'Cover di Catan');
    });

    it('icons have aria-hidden attribute', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      const { container } = renderComponent();

      const clockIcons = container.querySelectorAll('svg.lucide-clock');
      clockIcons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('calls useActiveSessions hook with limit', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent({ limit: 4 });

      expect(useActiveSessionsModule.useActiveSessions).toHaveBeenCalledWith(4);
    });

    it('calls useGames hook to fetch game data', () => {
      vi.mocked(useActiveSessionsModule.useActiveSessions).mockReturnValue({
        data: mockSessionsData,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useActiveSessionsModule.useActiveSessions>);

      vi.mocked(useGamesModule.useGames).mockReturnValue({
        data: mockGamesData,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useGamesModule.useGames>);

      renderComponent();

      expect(useGamesModule.useGames).toHaveBeenCalled();
    });
  });
});
