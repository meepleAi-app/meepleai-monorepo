/**
 * Admin Bulk Export Page - Visual Tests
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for bulk export functionality.
 * Covers: game selection, export actions, status messages, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AdminPageClient } from './client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof AdminPageClient> = {
  title: 'Pages/Admin/BulkExport',
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
            <div className="min-h-screen bg-slate-950">
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
  {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Catan',
    description: 'Strategic board game of trade and settlement',
    createdAt: '2024-01-15T10:00:00Z',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Ticket to Ride',
    description: 'Railway-themed board game',
    createdAt: '2024-02-10T08:00:00Z',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    title: 'Gloomhaven',
    description: 'Cooperative dungeon crawler',
    createdAt: '2024-03-20T15:00:00Z',
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    title: 'Wingspan',
    description: 'Bird-collection strategy game',
    createdAt: '2024-04-12T10:00:00Z',
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Azul',
    description: 'Tile-placement puzzle game',
    createdAt: '2024-05-08T14:30:00Z',
  },
];

// ========== Stories ==========

/**
 * Default view with games available for export
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 5,
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
 * Empty state - no games to export
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [],
            total: 0,
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
          url: '/api/v1/games',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * Large dataset (100 games)
 */
export const LargeDataset: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: Array.from({ length: 100 }, (_, i) => ({
              id: `game-${i + 1}`,
              title: `Game ${i + 1}`,
              description: `Description for game ${i + 1}`,
              createdAt: new Date(2024, 0, 1 + (i % 365)).toISOString(),
            })),
            total: 100,
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
 * Access denied - non-admin user
 */
export const AccessDenied: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 403,
          response: { error: 'Access denied. Editor or Admin role required.' },
        },
      ],
    },
  },
};

/**
 * Export in progress
 */
export const ExportingState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: mockGames,
            total: 5,
          },
        },
        {
          url: '/api/v1/chat/bulk-export-rule-specs',
          method: 'POST',
          delay: 3000,
          status: 200,
          response: { success: true },
        },
      ],
    },
  },
};

/**
 * Single game available
 */
export const SingleGame: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [mockGames[0]],
            total: 1,
          },
        },
      ],
    },
  },
};
