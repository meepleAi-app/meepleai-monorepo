/**
 * ActiveSessionsPanel Storybook Stories (Issue #2858)
 *
 * Comprehensive stories for ActiveSessionsPanel component
 * Covers: loading, error, empty state, sessions with/without covers, View All link
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { PaginatedSessionsResponse, PaginatedGamesResponse, GameSessionDto, Game } from '@/lib/api';

import { ActiveSessionsPanel } from './ActiveSessionsPanel';

import type { Meta, StoryObj } from '@storybook/react';

// ============================================================================
// Mock Data
// ============================================================================

const createMockGame = (override: Partial<Game>): Game => ({
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
  imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/xV7oisd3RQ8R-k18cdWAYthHXsA=/0x0/filters:format(jpeg)/pic2419375.jpg',
  iconUrl: null,
  description: 'A classic settlement game',
  faqCount: 5,
  averageRating: 4.5,
  sharedGameId: null,
  ...override,
});

const createMockSession = (override: Partial<GameSessionDto>): GameSessionDto => ({
  id: 'session-1',
  gameId: 'game-1',
  status: 'InProgress',
  startedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
  completedAt: null,
  playerCount: 4,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: 'red' },
    { playerName: 'Bob', playerOrder: 2, color: 'blue' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 30,
  ...override,
});

const mockGames: Game[] = [
  createMockGame({
    id: 'game-1',
    title: 'Catan',
    imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/xV7oisd3RQ8R-k18cdWAYthHXsA=/0x0/filters:format(jpeg)/pic2419375.jpg',
  }),
  createMockGame({
    id: 'game-2',
    title: 'Ticket to Ride',
    imageUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__original/img/HrcYAkcUYMBA9VBJdlqowkdRwlQ=/0x0/filters:format(jpeg)/pic38668.jpg',
  }),
  createMockGame({
    id: 'game-3',
    title: 'Pandemic',
    imageUrl: 'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__original/img/o1qCeEpFQ5LO7MtkCjGMVHlSjTA=/0x0/filters:format(jpeg)/pic1534148.jpg',
  }),
  createMockGame({
    id: 'game-4',
    title: 'Wingspan',
    imageUrl: null,
    iconUrl: null,
  }),
];

const mockSessions: GameSessionDto[] = [
  createMockSession({
    id: 'session-1',
    gameId: 'game-1',
    status: 'InProgress',
    startedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
  }),
  createMockSession({
    id: 'session-2',
    gameId: 'game-2',
    status: 'Paused',
    startedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  }),
  createMockSession({
    id: 'session-3',
    gameId: 'game-3',
    status: 'Setup',
    startedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
  }),
];

// ============================================================================
// Wrapper Component for Stories
// ============================================================================

const createWrapper = (
  sessionsData: PaginatedSessionsResponse | null,
  gamesData: PaginatedGamesResponse | null,
) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        refetchOnWindowFocus: false,
      },
    },
  });

  // Pre-populate the cache with mock data
  if (sessionsData) {
    queryClient.setQueryData(['sessions', 'active', { limit: 3 }], sessionsData);
    queryClient.setQueryData(['sessions', 'active', { limit: 5 }], sessionsData);
  }
  if (gamesData) {
    queryClient.setQueryData(['games', { searchTerm: undefined, genre: undefined, page: 1, pageSize: 100 }], gamesData);
  }

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
};

// ============================================================================
// Meta
// ============================================================================

const meta = {
  title: 'Components/Dashboard/ActiveSessionsPanel',
  component: ActiveSessionsPanel,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
Dashboard widget showing active game sessions with game covers.

**Features:**
- Show game cover, title, started time
- Continue and Details buttons
- Empty state: 'Nessuna sessione attiva'
- Link to session detail page
- View All when more sessions available
        `,
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ActiveSessionsPanel>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Success State Stories
// ============================================================================

export const WithSessions: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: mockSessions,
          total: 3,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel with three active sessions showing game covers, titles, and action buttons',
      },
    },
  },
};

export const SingleSession: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [mockSessions[0]],
          total: 1,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel with a single active session',
      },
    },
  },
};

export const WithMoreSessions: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: mockSessions,
          total: 7, // More than limit
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel showing "Vedi Tutte" link when more sessions exist than displayed',
      },
    },
  },
};

export const SessionWithNoImage: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [
            createMockSession({
              id: 'session-no-image',
              gameId: 'game-4',
            }),
          ],
          total: 1,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Session card showing fallback icon when game has no cover image',
      },
    },
  },
};

export const SessionWithUnknownGame: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [
            createMockSession({
              id: 'session-unknown',
              gameId: 'unknown-game-id',
            }),
          ],
          total: 1,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Session card showing "Gioco sconosciuto" when game is not found in games list',
      },
    },
  },
};

export const RecentlyStarted: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [
            createMockSession({
              id: 'session-recent',
              gameId: 'game-1',
              startedAt: new Date(Date.now() - 2 * 60 * 1000).toISOString(), // 2 minutes ago
            }),
          ],
          total: 1,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Session started 2 minutes ago showing relative time in Italian',
      },
    },
  },
};

export const LongRunningSessions: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [
            createMockSession({
              id: 'session-long-1',
              gameId: 'game-1',
              startedAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(), // 5 hours ago
            }),
            createMockSession({
              id: 'session-long-2',
              gameId: 'game-2',
              startedAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
            }),
          ],
          total: 2,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Sessions running for extended periods (5 hours, 1 day)',
      },
    },
  },
};

// ============================================================================
// Empty State Stories
// ============================================================================

export const EmptyState: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Empty state with "Nessuna sessione attiva" message and CTA to start a game',
      },
    },
  },
};

// ============================================================================
// Loading State Stories
// ============================================================================

export const Loading: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchOnWindowFocus: false,
          },
        },
      });
      // Don't set any data - hooks will return loading state
      queryClient.setQueryDefaults(['sessions', 'active'], {
        enabled: false,
      });
      queryClient.setQueryDefaults(['games'], {
        enabled: false,
      });
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Loading state with skeleton placeholders for session cards',
      },
    },
  },
};

// ============================================================================
// Layout Stories
// ============================================================================

export const InDashboardGrid: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: mockSessions.slice(0, 2),
          total: 2,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 w-[900px]">
            <Story />
            <div className="rounded-lg border p-4">Library Quota Widget</div>
            <div className="rounded-lg border p-4">Quick Actions Widget</div>
          </div>
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel in a typical dashboard grid layout alongside other widgets',
      },
    },
  },
};

export const WithCustomClassName: Story = {
  args: {
    className: 'border-2 border-primary shadow-lg',
  },
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(
        {
          sessions: mockSessions.slice(0, 2),
          total: 2,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel with custom className for additional styling',
      },
    },
  },
};

export const WithCustomLimit: Story = {
  args: {
    limit: 5,
  },
  decorators: [
    (Story) => {
      const fiveSessions = [
        ...mockSessions,
        createMockSession({
          id: 'session-4',
          gameId: 'game-1',
          startedAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
        }),
        createMockSession({
          id: 'session-5',
          gameId: 'game-2',
          startedAt: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(),
        }),
      ];
      const Wrapper = createWrapper(
        {
          sessions: fiveSessions,
          total: 5,
          page: 1,
          pageSize: 5,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Panel showing 5 sessions with custom limit prop',
      },
    },
  },
};

// ============================================================================
// Session Status Variations
// ============================================================================

export const MixedStatuses: Story = {
  decorators: [
    (Story) => {
      const mixedSessions = [
        createMockSession({
          id: 'session-inprogress',
          gameId: 'game-1',
          status: 'InProgress',
          startedAt: new Date(Date.now() - 45 * 60 * 1000).toISOString(),
        }),
        createMockSession({
          id: 'session-paused',
          gameId: 'game-2',
          status: 'Paused',
          startedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        }),
        createMockSession({
          id: 'session-setup',
          gameId: 'game-3',
          status: 'Setup',
          startedAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
        }),
      ];
      const Wrapper = createWrapper(
        {
          sessions: mixedSessions,
          total: 3,
          page: 1,
          pageSize: 3,
        },
        {
          games: mockGames,
          total: 4,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }
      );
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Sessions with different statuses: InProgress, Paused, and Setup',
      },
    },
  },
};
