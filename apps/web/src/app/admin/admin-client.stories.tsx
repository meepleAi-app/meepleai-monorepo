import { AdminClient } from './admin-client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Admin Dashboard - Main admin panel with AI requests monitoring
 *
 * ## Features
 * - **AI Requests Monitoring**: Real-time request tracking
 * - **Analytics Charts**: Endpoint distribution, latency, time series, feedback
 * - **Filtering**: Endpoint filter, date range, search
 * - **Pagination**: 50 items per page
 * - **Stats Dashboard**: Total requests, avg latency, tokens, success rate
 * - **CSV Export**: Export requests data
 *
 * ## Access Control
 * - Requires Admin role
 * - Server-side auth check via middleware
 * - Client-side role validation via RequireRole wrapper
 */
const meta = {
  title: 'Admin/AdminClient',
  component: AdminClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AdminClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data
const mockRequests = [
  {
    id: '1',
    userId: 'user-123',
    gameId: 'catan',
    endpoint: 'qa',
    query: 'How many resource cards can I hold?',
    responseSnippet: 'In Catan, you can hold up to 7 resource cards...',
    latencyMs: 245,
    tokenCount: 150,
    promptTokens: 50,
    completionTokens: 100,
    confidence: 0.95,
    status: 'Success',
    errorMessage: null,
    ipAddress: '192.168.1.100',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
    createdAt: '2025-12-05T10:30:00Z',
    model: 'gpt-4',
    finishReason: 'stop',
  },
  {
    id: '2',
    userId: 'user-456',
    gameId: 'wingspan',
    endpoint: 'explain',
    query: 'Explain the round structure',
    responseSnippet: 'Each round in Wingspan consists of...',
    latencyMs: 189,
    tokenCount: 200,
    promptTokens: 60,
    completionTokens: 140,
    confidence: 0.88,
    status: 'Success',
    errorMessage: null,
    ipAddress: '192.168.1.101',
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
    createdAt: '2025-12-05T10:25:00Z',
    model: 'gpt-4',
    finishReason: 'stop',
  },
  {
    id: '3',
    userId: null,
    gameId: 'ticket-to-ride',
    endpoint: 'qa',
    query: 'Can I claim a route with wild cards?',
    responseSnippet: null,
    latencyMs: 0,
    tokenCount: 0,
    promptTokens: 0,
    completionTokens: 0,
    confidence: null,
    status: 'Error',
    errorMessage: 'Rate limit exceeded',
    ipAddress: '192.168.1.102',
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X)',
    createdAt: '2025-12-05T10:20:00Z',
    model: null,
    finishReason: null,
  },
];

const mockStats = {
  totalRequests: 1247,
  avgLatencyMs: 215,
  totalTokens: 187500,
  successRate: 0.94,
  endpointCounts: {
    qa: 758,
    explain: 312,
    summary: 125,
    chat: 52,
  },
  feedbackCounts: {
    helpful: 534,
    not_helpful: 89,
  },
  totalFeedback: 623,
};

/**
 * Default admin dashboard with data loaded.
 * Shows requests table, charts, and stats.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Loading state while fetching data.
 * Shows loading spinners for requests and charts.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        delay: 'infinite',
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Error state when data fetch fails.
 * Shows error message and retry option.
 */
export const ErrorState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Unauthorized access attempt.
 * Shows error when non-admin tries to access.
 */
export const Unauthorized: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 403,
        response: { message: 'Unauthorized - Admin access required' },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 403,
        response: { message: 'Unauthorized - Admin access required' },
      },
    ],
  },
};

/**
 * Empty state with no requests.
 * Shows message prompting to wait for activity.
 */
export const Empty: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: [],
          totalCount: 0,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: {
          totalRequests: 0,
          avgLatencyMs: 0,
          totalTokens: 0,
          successRate: 0,
          endpointCounts: {},
          feedbackCounts: {},
          totalFeedback: 0,
        },
      },
    ],
  },
};

/**
 * Large dataset with many requests.
 * Shows pagination controls for navigating pages.
 */
export const WithPagination: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 537, // More than 1 page
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Endpoint filter applied.
 * Shows only requests matching selected endpoint.
 */
export const WithEndpointFilter: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests.filter(r => r.endpoint === 'qa'),
          totalCount: 1,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Date range filter applied.
 * Shows requests within selected date range.
 */
export const WithDateFilter: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Mobile viewport (375px).
 * Shows responsive layout for mobile devices.
 */
export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Tablet viewport (768px).
 * Shows responsive layout for tablets.
 */
export const Tablet: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Desktop viewport (1024px).
 * Shows full layout for desktop screens.
 */
export const Desktop: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Dark theme variant.
 * Shows dashboard appearance on dark background.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    mockData: [
      {
        url: '/api/v1/admin/requests',
        method: 'GET',
        status: 200,
        response: {
          requests: mockRequests,
          totalCount: 3,
        },
      },
      {
        url: '/api/v1/admin/stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};
