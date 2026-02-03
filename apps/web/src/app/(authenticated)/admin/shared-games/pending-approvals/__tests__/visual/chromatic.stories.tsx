/**
 * Shared Games Pending Approvals - Visual Tests
 * Issue #2514 - SharedGameCatalog Approval Workflow
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for the PendingApprovalsClient interface.
 * Covers: approval queue, approve/reject actions, pagination, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PendingApprovalsClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof PendingApprovalsClient> = {
  title: 'Pages/Admin/SharedGames/PendingApprovals',
  component: PendingApprovalsClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 500,
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
type Story = StoryObj<typeof PendingApprovalsClient>;

// ========== Mock Data ==========

const mockPendingGames = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'I Coloni di Catan',
    yearPublished: 1995,
    description: 'Classico gioco di strategia e commercio',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 75,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.1,
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    bggId: 13,
    status: 'PendingApproval',
    hasRules: true,
    categoryCount: 2,
    mechanicCount: 3,
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-06-20T14:30:00Z',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Ticket to Ride',
    yearPublished: 2004,
    description: 'Costruisci linee ferroviarie attraverso Europa e America',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    minAge: 8,
    complexityRating: 1.8,
    averageRating: 7.4,
    imageUrl: 'https://example.com/ticket.jpg',
    thumbnailUrl: 'https://example.com/ticket-thumb.jpg',
    bggId: 9209,
    status: 'PendingApproval',
    hasRules: true,
    categoryCount: 1,
    mechanicCount: 2,
    createdAt: '2024-02-10T08:00:00Z',
    updatedAt: '2024-05-15T11:00:00Z',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    title: 'Gloomhaven',
    yearPublished: 2017,
    description: 'Epico dungeon crawler cooperativo',
    minPlayers: 1,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    minAge: 14,
    complexityRating: 3.8,
    averageRating: 8.8,
    imageUrl: 'https://example.com/gloomhaven.jpg',
    thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
    bggId: 174430,
    status: 'PendingApproval',
    hasRules: false,
    categoryCount: 3,
    mechanicCount: 5,
    createdAt: '2024-03-20T15:00:00Z',
    updatedAt: '2024-04-10T09:00:00Z',
  },
];

// ========== Stories ==========

/**
 * Default View - Games awaiting approval
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 200,
          response: {
            items: mockPendingGames,
            total: 3,
            pageNumber: 1,
            pageSize: 20,
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
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { items: [], total: 0, pageNumber: 1, pageSize: 20 },
        },
      ],
    },
  },
};

/**
 * Empty state - no pending approvals
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 200,
          response: {
            items: [],
            total: 0,
            pageNumber: 1,
            pageSize: 20,
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
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * Large approval queue
 */
export const LargeQueue: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 200,
          response: {
            items: Array.from({ length: 20 }, (_, i) => ({
              ...mockPendingGames[i % 3],
              id: `game-${i + 1}`,
              title: `Game ${i + 1}`,
            })),
            total: 50,
            pageNumber: 1,
            pageSize: 20,
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
 * Games without rules
 */
export const NoRulesGames: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 200,
          response: {
            items: mockPendingGames.map(g => ({ ...g, hasRules: false })),
            total: 3,
            pageNumber: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Mixed complexity ratings
 */
export const MixedComplexity: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/shared-games/pending-approvals',
          method: 'GET',
          status: 200,
          response: {
            items: [
              { ...mockPendingGames[0], complexityRating: 1.2 }, // Easy
              { ...mockPendingGames[1], complexityRating: 2.5 }, // Medium
              { ...mockPendingGames[2], complexityRating: 4.5 }, // Very Complex
            ],
            total: 3,
            pageNumber: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};
