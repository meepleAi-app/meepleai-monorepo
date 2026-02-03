import { AdminPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Admin Analytics Dashboard
 *
 * ## Features
 * - **Metrics Overview**: Total users, sessions, API requests, PDFs, chat messages
 * - **Time Series Charts**: User, session, API request, PDF upload, chat message trends
 * - **Filters**: Days range (7/30/90), game ID, role filter
 * - **Auto-refresh**: Automatic data updates
 * - **Toast Notifications**: Success/error feedback
 */
const meta = {
  title: 'Admin/Analytics',
  component: AdminPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AdminPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data
const mockStats = {
  metrics: {
    totalUsers: 1247,
    activeSessions: 89,
    apiRequestsToday: 3456,
    totalPdfDocuments: 234,
    totalChatMessages: 8912,
    averageConfidenceScore: 0.87,
    totalRagRequests: 2134,
    totalTokensUsed: 1250000,
  },
  userTrend: [
    { date: '2025-11-05', count: 45 },
    { date: '2025-11-12', count: 67 },
    { date: '2025-11-19', count: 89 },
    { date: '2025-11-26', count: 102 },
    { date: '2025-12-03', count: 125 },
  ],
  sessionTrend: [
    { date: '2025-11-05', count: 23 },
    { date: '2025-11-12', count: 34 },
    { date: '2025-11-19', count: 45 },
    { date: '2025-11-26', count: 56 },
    { date: '2025-12-03', count: 67 },
  ],
  apiRequestTrend: [
    { date: '2025-11-05', count: 456, averageValue: 210 },
    { date: '2025-11-12', count: 678, averageValue: 198 },
    { date: '2025-11-19', count: 890, averageValue: 225 },
    { date: '2025-11-26', count: 1023, averageValue: 203 },
    { date: '2025-12-03', count: 1234, averageValue: 215 },
  ],
  pdfUploadTrend: [
    { date: '2025-11-05', count: 12 },
    { date: '2025-11-12', count: 18 },
    { date: '2025-11-19', count: 24 },
    { date: '2025-11-26', count: 30 },
    { date: '2025-12-03', count: 36 },
  ],
  chatMessageTrend: [
    { date: '2025-11-05', count: 234 },
    { date: '2025-11-12', count: 345 },
    { date: '2025-11-19', count: 456 },
    { date: '2025-11-26', count: 567 },
    { date: '2025-12-03', count: 678 },
  ],
  generatedAt: '2025-12-05T12:00:00Z',
};

/**
 * Default analytics dashboard with data loaded.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Loading state while fetching analytics data.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Error state when data fetch fails.
 */
export const ErrorState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Empty state with no data.
 */
export const Empty: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 200,
        response: {
          metrics: {
            totalUsers: 0,
            activeSessions: 0,
            apiRequestsToday: 0,
            totalPdfDocuments: 0,
            totalChatMessages: 0,
            averageConfidenceScore: 0,
            totalRagRequests: 0,
            totalTokensUsed: 0,
          },
          userTrend: [],
          sessionTrend: [],
          apiRequestTrend: [],
          pdfUploadTrend: [],
          chatMessageTrend: [],
          generatedAt: '2025-12-05T12:00:00Z',
        },
      },
    ],
  },
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
        method: 'GET',
        status: 200,
        response: mockStats,
      },
    ],
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    mockData: [
      {
        url: '/api/v1/admin/dashboard-stats',
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
