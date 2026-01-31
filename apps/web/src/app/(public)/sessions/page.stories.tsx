/**
 * User Active Sessions Page - Visual Tests
 * Issue #1134 - SPRINT-4: Active Sessions Dashboard
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for user sessions management.
 * Covers: session listing, filters, actions, pagination, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import ActiveSessionsPage from './page';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof ActiveSessionsPage> = {
  title: 'Pages/User/Sessions',
  component: ActiveSessionsPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchInterval: false,
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <Story />
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
};

export default meta;
type Story = StoryObj<typeof ActiveSessionsPage>;

// ========== Mock Data ==========

const mockGames = [
  { id: 'game-1', title: 'Catan' },
  { id: 'game-2', title: 'Ticket to Ride' },
  { id: 'game-3', title: 'Gloomhaven' },
];

const mockSessions = [
  {
    id: 'session-1',
    gameId: 'game-1',
    status: 'InProgress',
    playerCount: 4,
    startedAt: '2025-01-31T10:00:00Z',
    durationMinutes: 45,
    players: [{ playerName: 'Alice' }],
  },
  {
    id: 'session-2',
    gameId: 'game-2',
    status: 'Paused',
    playerCount: 3,
    startedAt: '2025-01-31T09:30:00Z',
    durationMinutes: 30,
    players: [{ playerName: 'Bob' }],
  },
  {
    id: 'session-3',
    gameId: 'game-3',
    status: 'InProgress',
    playerCount: 2,
    startedAt: '2025-01-31T11:15:00Z',
    durationMinutes: 120,
    players: [{ playerName: 'Charlie' }],
  },
  {
    id: 'session-4',
    gameId: 'game-1',
    status: 'InProgress',
    playerCount: 5,
    startedAt: '2025-01-31T08:00:00Z',
    durationMinutes: 90,
    players: [{ playerName: 'Diana' }],
  },
];

// ========== Stories ==========

/**
 * Default view with active sessions
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: mockSessions,
            total: 4,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { sessions: [], total: 0 },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { games: [], total: 0 },
        },
      ],
    },
  },
};

/**
 * Empty state - no active sessions
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: [],
            total: 0,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Error state
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 500,
          response: { error: 'Failed to load active sessions' },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * All paused sessions
 */
export const AllPaused: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: mockSessions.map(s => ({ ...s, status: 'Paused' })),
            total: 4,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Large dataset with pagination
 */
export const WithPagination: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: Array.from({ length: 20 }, (_, i) => ({
              id: `session-${i + 1}`,
              gameId: mockGames[i % 3].id,
              status: i % 3 === 0 ? 'Paused' : 'InProgress',
              playerCount: 2 + (i % 4),
              startedAt: new Date(2025, 0, 31, 10 - (i % 12), 0).toISOString(),
              durationMinutes: 30 + (i * 10),
              players: [{ playerName: `Player ${i + 1}` }],
            })),
            total: 50,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Single session
 */
export const SingleSession: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: [mockSessions[0]],
            total: 1,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Mobile view
 */
export const MobileView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet view
 */
export const TabletView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Long session durations
 */
export const LongDurations: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: [
              { ...mockSessions[0], durationMinutes: 305 }, // 5h 5m
              { ...mockSessions[1], durationMinutes: 720 }, // 12h
              { ...mockSessions[2], durationMinutes: 1440 }, // 24h
            ],
            total: 3,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Many players per session
 */
export const ManyPlayers: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/sessions/active',
          method: 'GET',
          status: 200,
          response: {
            sessions: mockSessions.map(s => ({ ...s, playerCount: 8 })),
            total: 4,
          },
        },
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 3,
          },
        },
      ],
    },
  },
};
