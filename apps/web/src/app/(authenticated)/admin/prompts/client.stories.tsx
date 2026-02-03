import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { AdminPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Prompt Templates Management Page
 *
 * ## Features
 * - **Template CRUD**: Create, read, update, delete prompt templates
 * - **Category Organization**: qa-system-prompt, chess-system-prompt, etc.
 * - **Version Tracking**: Active version indicator per template
 * - **Search & Filter**: Filter by name and category
 * - **Sorting**: Sort by name, category, or creation date
 * - **Pagination**: Client-side pagination (20 per page)
 *
 * ## Access Control
 * - Requires Admin role (wrapped with AdminAuthGuard)
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Mobile (375px)
 * - Tablet (768px)
 * - Desktop (1920px)
 */
const meta = {
  title: 'Pages/Admin/Prompts',
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
            <div className="min-h-screen">
              <Story />
            </div>
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof AdminPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view with multiple prompt templates
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                name: 'Game QA System Prompt',
                description: 'System prompt for board game question answering',
                category: 'qa-system-prompt',
                createdAt: '2025-01-15T00:00:00Z',
                updatedAt: '2025-12-10T10:30:00Z',
                activeVersionId: 'v3-active',
              },
              {
                id: '22222222-2222-2222-2222-222222222222',
                name: 'Chess Rules Expert',
                description: 'Chess rules and strategy expert system prompt',
                category: 'chess-system-prompt',
                createdAt: '2025-02-20T00:00:00Z',
                updatedAt: '2025-11-25T14:15:00Z',
                activeVersionId: 'v2-active',
              },
              {
                id: '33333333-3333-3333-3333-333333333333',
                name: 'Setup Guide Assistant',
                description: 'Interactive game setup instructions',
                category: 'setup-guide-system-prompt',
                createdAt: '2025-03-10T00:00:00Z',
                updatedAt: '2025-12-05T09:00:00Z',
                activeVersionId: 'v1-active',
              },
              {
                id: '44444444-4444-4444-4444-444444444444',
                name: 'Streaming QA Handler',
                description: 'Real-time streaming question answering',
                category: 'streaming-qa-prompt',
                createdAt: '2025-04-05T00:00:00Z',
                updatedAt: '2025-12-01T11:45:00Z',
                activeVersionId: null,
              },
            ],
            total: 4,
            page: 1,
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
          url: '/api/v1/admin/prompts',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { items: [], total: 0, page: 1, pageSize: 20 },
        },
      ],
    },
  },
};

/**
 * Empty state - no templates
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: [],
            total: 0,
            page: 1,
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
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 500,
          response: { error: 'Internal Server Error' },
        },
      ],
    },
  },
};

/**
 * Filtered by category (qa-system-prompt)
 */
export const FilteredByCategory: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                name: 'Game QA System Prompt',
                description: 'System prompt for board game question answering',
                category: 'qa-system-prompt',
                createdAt: '2025-01-15T00:00:00Z',
                updatedAt: '2025-12-10T10:30:00Z',
                activeVersionId: 'v3-active',
              },
              {
                id: '55555555-5555-5555-5555-555555555555',
                name: 'Advanced QA Prompt',
                description: 'Enhanced QA with context awareness',
                category: 'qa-system-prompt',
                createdAt: '2025-05-20T00:00:00Z',
                updatedAt: '2025-12-08T16:20:00Z',
                activeVersionId: 'v1-active',
              },
            ],
            total: 2,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Templates without active versions
 */
export const NoActiveVersions: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '44444444-4444-4444-4444-444444444444',
                name: 'Streaming QA Handler',
                description: 'Real-time streaming question answering',
                category: 'streaming-qa-prompt',
                createdAt: '2025-04-05T00:00:00Z',
                updatedAt: '2025-12-01T11:45:00Z',
                activeVersionId: null,
              },
              {
                id: '66666666-6666-6666-6666-666666666666',
                name: 'Experimental Prompt',
                description: 'Testing new prompt format',
                category: 'qa-system-prompt',
                createdAt: '2025-12-10T00:00:00Z',
                updatedAt: '2025-12-10T00:00:00Z',
                activeVersionId: null,
              },
            ],
            total: 2,
            page: 1,
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
 * Large dataset with pagination
 */
export const LargeDataset: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: Array.from({ length: 20 }, (_, i) => ({
              id: `${i + 1}0000000-0000-0000-0000-000000000000`,
              name: `Prompt Template ${i + 1}`,
              description: `Description for template ${i + 1}`,
              category: ['qa-system-prompt', 'chess-system-prompt', 'setup-guide-system-prompt'][
                i % 3
              ],
              createdAt: new Date(2025, i % 12, i + 1).toISOString(),
              updatedAt: new Date(2025, 11, 15 - (i % 15)).toISOString(),
              activeVersionId: i % 3 === 0 ? null : `v${i + 1}-active`,
            })),
            total: 120,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};

/**
 * Search results
 */
export const SearchResults: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/prompts',
          method: 'GET',
          status: 200,
          response: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                name: 'Game QA System Prompt',
                description: 'System prompt for board game question answering',
                category: 'qa-system-prompt',
                createdAt: '2025-01-15T00:00:00Z',
                updatedAt: '2025-12-10T10:30:00Z',
                activeVersionId: 'v3-active',
              },
            ],
            total: 1,
            page: 1,
            pageSize: 20,
          },
        },
      ],
    },
  },
};
