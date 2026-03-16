/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { CardsZone } from '../CardsZone';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockUseRecentlyAddedGames = vi.fn();
const mockUseActiveSessions = vi.fn();
const mockUseRecentChatSessions = vi.fn();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useRecentlyAddedGames: () => mockUseRecentlyAddedGames(),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => mockUseActiveSessions(),
}));

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: () => mockUseRecentChatSessions(),
}));

// Stub MeepleCard to inspect props
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => (
    <div data-testid={props['data-testid'] as string} data-entity={props.entity as string}>
      {props.title as string}
    </div>
  ),
  MeepleCardSkeleton: ({ variant }: { variant?: string }) => (
    <div data-testid="meeple-card-skeleton" data-variant={variant} />
  ),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------

const MOCK_GAMES = {
  items: [
    {
      id: 'entry-1',
      userId: 'u1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      gamePublisher: 'Kosmos',
      gameImageUrl: '/catan.jpg',
      addedAt: '2026-01-01T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
    },
    {
      id: 'entry-2',
      userId: 'u1',
      gameId: 'game-2',
      gameTitle: 'Ticket to Ride',
      gamePublisher: 'Days of Wonder',
      gameImageUrl: null,
      addedAt: '2026-01-02T00:00:00Z',
      isFavorite: true,
      currentState: 'Owned',
    },
  ],
  page: 1,
  pageSize: 8,
  totalCount: 2,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const MOCK_SESSIONS = {
  sessions: [
    {
      id: 'sess-1',
      gameId: 'game-1',
      status: 'InProgress',
      startedAt: '2026-03-14T10:00:00Z',
      completedAt: null,
      playerCount: 3,
      players: [],
      winnerName: null,
      notes: null,
      durationMinutes: 45,
    },
  ],
  total: 1,
  page: 1,
  pageSize: 8,
};

const MOCK_CHATS = {
  sessions: [
    {
      id: 'chat-1',
      userId: 'u1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      title: 'Regole base',
      messageCount: 12,
      createdAt: '2026-03-14T09:00:00Z',
      lastMessageAt: '2026-03-14T09:30:00Z',
      isArchived: false,
    },
  ],
  totalCount: 1,
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('CardsZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: loaded with data
    mockUseRecentlyAddedGames.mockReturnValue({ data: MOCK_GAMES, isLoading: false });
    mockUseActiveSessions.mockReturnValue({ data: MOCK_SESSIONS, isLoading: false });
    mockUseRecentChatSessions.mockReturnValue({ data: MOCK_CHATS, isLoading: false });
  });

  it('has correct root data-testid', () => {
    renderWithProviders(<CardsZone />);
    expect(screen.getByTestId('cards-zone')).toBeInTheDocument();
  });

  it('renders 3 section headings', () => {
    renderWithProviders(<CardsZone />);

    expect(screen.getByText('Giochi recenti')).toBeInTheDocument();
    expect(screen.getByText('Sessioni recenti')).toBeInTheDocument();
    expect(screen.getByText('Chat attive')).toBeInTheDocument();
  });

  it('renders MeepleCard components with correct entity types', () => {
    renderWithProviders(<CardsZone />);

    // Games: entity="game"
    const gameCard = screen.getByTestId('cards-zone-game-game-1');
    expect(gameCard).toHaveAttribute('data-entity', 'game');
    expect(gameCard).toHaveTextContent('Catan');

    const gameCard2 = screen.getByTestId('cards-zone-game-game-2');
    expect(gameCard2).toHaveAttribute('data-entity', 'game');

    // Sessions: entity="session"
    const sessionCard = screen.getByTestId('cards-zone-session-sess-1');
    expect(sessionCard).toHaveAttribute('data-entity', 'session');

    // Chats: entity="chatSession"
    const chatCard = screen.getByTestId('cards-zone-chat-chat-1');
    expect(chatCard).toHaveAttribute('data-entity', 'chatSession');
    expect(chatCard).toHaveTextContent('Regole base');
  });

  it('shows empty state when no games', () => {
    mockUseRecentlyAddedGames.mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 8,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      },
      isLoading: false,
    });

    renderWithProviders(<CardsZone />);
    expect(screen.getByText('Nessun gioco in libreria')).toBeInTheDocument();
  });

  it('shows empty state when no sessions', () => {
    mockUseActiveSessions.mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 8 },
      isLoading: false,
    });

    renderWithProviders(<CardsZone />);
    expect(screen.getByText('Nessuna sessione recente')).toBeInTheDocument();
  });

  it('shows empty state when no chats', () => {
    mockUseRecentChatSessions.mockReturnValue({
      data: { sessions: [], totalCount: 0 },
      isLoading: false,
    });

    renderWithProviders(<CardsZone />);
    expect(screen.getByText('Nessuna chat recente')).toBeInTheDocument();
  });

  it('shows skeleton cards when loading', () => {
    mockUseRecentlyAddedGames.mockReturnValue({ data: undefined, isLoading: true });
    mockUseActiveSessions.mockReturnValue({ data: undefined, isLoading: true });
    mockUseRecentChatSessions.mockReturnValue({ data: undefined, isLoading: true });

    renderWithProviders(<CardsZone />);

    const skeletons = screen.getAllByTestId('meeple-card-skeleton');
    // 4 game skeletons + 4 session skeletons + 3 chat skeletons = 11
    expect(skeletons.length).toBe(11);
  });

  it('renders chat title fallback for untitled chats', () => {
    mockUseRecentChatSessions.mockReturnValue({
      data: {
        sessions: [
          {
            id: 'chat-2',
            userId: 'u1',
            gameId: 'game-1',
            gameTitle: 'Catan',
            title: null,
            messageCount: 3,
            createdAt: '2026-03-14T09:00:00Z',
            lastMessageAt: null,
            isArchived: false,
          },
        ],
        totalCount: 1,
      },
      isLoading: false,
    });

    renderWithProviders(<CardsZone />);
    expect(screen.getByText('Chat · Catan')).toBeInTheDocument();
  });
});
