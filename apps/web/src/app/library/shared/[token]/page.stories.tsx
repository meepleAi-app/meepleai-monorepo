/**
 * Shared Library Page - Visual Tests
 * Issue #2614 - Public Shared Library Page
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for shared library viewing.
 * Covers: shared library display, game cards, owner stats, token validation, responsive design
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import SharedLibraryPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof SharedLibraryPage> = {
  title: 'Pages/User/Library/Shared',
  component: SharedLibraryPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/library/shared/abc123token',
        query: { token: 'abc123token' },
      },
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
type Story = StoryObj<typeof SharedLibraryPage>;

// ========== Mock Data ==========

const mockSharedLibrary = {
  ownerDisplayName: 'Alice Johnson',
  totalGames: 12,
  favoriteCount: 5,
  shareSettings: {
    includeNotes: true,
    includeFavorites: true,
  },
  games: [
    {
      id: 'game-1',
      title: 'Catan',
      imageUrl: 'https://example.com/catan.jpg',
      isFavorite: true,
      notes: 'Great game for family nights!',
      averageRating: 7.2,
      complexity: 2.3,
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 60,
    },
    {
      id: 'game-2',
      title: 'Ticket to Ride',
      imageUrl: 'https://example.com/ticket.jpg',
      isFavorite: true,
      notes: 'Perfect for beginners',
      averageRating: 7.4,
      complexity: 1.9,
      minPlayers: 2,
      maxPlayers: 5,
      playingTimeMinutes: 45,
    },
    {
      id: 'game-3',
      title: 'Gloomhaven',
      imageUrl: 'https://example.com/gloomhaven.jpg',
      isFavorite: false,
      notes: null,
      averageRating: 8.8,
      complexity: 3.9,
      minPlayers: 1,
      maxPlayers: 4,
      playingTimeMinutes: 120,
    },
    {
      id: 'game-4',
      title: 'Wingspan',
      imageUrl: 'https://example.com/wingspan.jpg',
      isFavorite: true,
      notes: 'Beautiful artwork and smooth gameplay',
      averageRating: 8.0,
      complexity: 2.4,
      minPlayers: 1,
      maxPlayers: 5,
      playingTimeMinutes: 70,
    },
  ],
};

// ========== Stories ==========

/**
 * Default view with shared library
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: mockSharedLibrary,
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
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: mockSharedLibrary,
        },
      ],
    },
  },
};

/**
 * Invalid token - not found
 */
export const InvalidToken: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 404,
          response: { error: 'Share token not found or expired' },
        },
      ],
    },
  },
};

/**
 * Expired/revoked token
 */
export const ExpiredToken: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 410,
          response: { error: 'Share link has been revoked' },
        },
      ],
    },
  },
};

/**
 * Empty library
 */
export const EmptyLibrary: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            games: [],
            totalGames: 0,
            favoriteCount: 0,
          },
        },
      ],
    },
  },
};

/**
 * Notes hidden by owner
 */
export const NotesHidden: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            shareSettings: {
              includeNotes: false,
              includeFavorites: true,
            },
            games: mockSharedLibrary.games.map(g => ({ ...g, notes: null })),
          },
        },
      ],
    },
  },
};

/**
 * Large library
 */
export const LargeLibrary: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            totalGames: 50,
            games: Array.from({ length: 24 }, (_, i) => ({
              id: `game-${i + 1}`,
              title: `Board Game ${i + 1}`,
              imageUrl: `https://example.com/game${i + 1}.jpg`,
              isFavorite: i % 3 === 0,
              notes: i % 2 === 0 ? `Notes for game ${i + 1}` : null,
              averageRating: 6 + (i % 3),
              complexity: 1.5 + (i % 4) * 0.5,
              minPlayers: 2 + (i % 2),
              maxPlayers: 4 + (i % 3),
              playingTimeMinutes: 30 + (i * 5),
            })),
          },
        },
      ],
    },
  },
};

/**
 * Single game in library
 */
export const SingleGame: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            totalGames: 1,
            games: [mockSharedLibrary.games[0]],
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
 * All favorites
 */
export const AllFavorites: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            favoriteCount: mockSharedLibrary.totalGames,
            games: mockSharedLibrary.games.map(g => ({ ...g, isFavorite: true })),
          },
        },
      ],
    },
  },
};

/**
 * No favorites
 */
export const NoFavorites: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/library/shared/:token',
          method: 'GET',
          status: 200,
          response: {
            ...mockSharedLibrary,
            favoriteCount: 0,
            games: mockSharedLibrary.games.map(g => ({ ...g, isFavorite: false })),
          },
        },
      ],
    },
  },
};
