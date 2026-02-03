/**
 * Admin Cache Management Page - Visual Tests
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for cache management interface.
 * Covers: cache stats, game filtering, cache clearing, responsive design
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { AdminPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AdminPageClient> = {
  title: 'Pages/Admin/Cache',
  component: AdminPageClient,
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
            <div className="min-h-screen bg-gray-50">
              <Story />
            </div>
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
};

export default meta;
type Story = StoryObj<typeof AdminPageClient>;

// ========== Mock Data ==========

const mockGames = [
  { id: 'game-1', title: 'Catan' },
  { id: 'game-2', title: 'Ticket to Ride' },
  { id: 'game-3', title: 'Gloomhaven' },
  { id: 'game-4', title: 'Wingspan' },
  { id: 'game-5', title: 'Azul' },
];

const mockCacheStats = {
  totalKeys: 1250,
  totalMemoryMB: 45.8,
  hitRate: 0.87,
  missRate: 0.13,
  evictionCount: 23,
  avgTTLSeconds: 3600,
  keysByType: {
    'game-rules': 350,
    'user-sessions': 240,
    'ai-responses': 420,
    'shared-games': 150,
    'other': 90,
  },
  topKeys: [
    { key: 'game:catan:rules', size: 2.4, hits: 1523, lastAccessed: '2025-01-31T10:30:00Z' },
    { key: 'ai:response:12345', size: 1.8, hits: 892, lastAccessed: '2025-01-31T10:28:00Z' },
    { key: 'user:session:abc123', size: 0.5, hits: 2341, lastAccessed: '2025-01-31T10:29:00Z' },
  ],
};

const mockEmptyCacheStats = {
  totalKeys: 0,
  totalMemoryMB: 0,
  hitRate: 0,
  missRate: 0,
  evictionCount: 0,
  avgTTLSeconds: 0,
  keysByType: {},
  topKeys: [],
};

// ========== Stories ==========

/**
 * Default view with cache statistics
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 200,
          response: mockCacheStats,
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
          url: '/api/v1/games',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { games: [], total: 0 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: mockEmptyCacheStats,
        },
      ],
    },
  },
};

/**
 * Empty cache
 */
export const EmptyCache: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 200,
          response: mockEmptyCacheStats,
        },
      ],
    },
  },
};

/**
 * Error loading stats
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 500,
          response: { error: 'Failed to load cache statistics' },
        },
      ],
    },
  },
};

/**
 * High cache usage
 */
export const HighUsage: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 200,
          response: {
            ...mockCacheStats,
            totalKeys: 15000,
            totalMemoryMB: 256.7,
            evictionCount: 523,
          },
        },
      ],
    },
  },
};

/**
 * Low hit rate (cache inefficient)
 */
export const LowHitRate: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 200,
          response: {
            ...mockCacheStats,
            hitRate: 0.35,
            missRate: 0.65,
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
 * Single game filtered
 */
export const SingleGameFiltered: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 200,
          response: {
            ...mockCacheStats,
            totalKeys: 85,
            totalMemoryMB: 3.2,
            keysByType: {
              'game-rules': 45,
              'ai-responses': 40,
            },
          },
        },
      ],
    },
  },
};

/**
 * Access denied - unauthorized
 */
export const AccessDenied: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: { games: mockGames, total: 5 },
        },
        {
          url: '/api/v1/chat/cache-stats',
          method: 'GET',
          status: 401,
          response: { error: 'Unauthorized - Admin access required' },
        },
      ],
    },
  },
};
