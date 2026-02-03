import { ReportsPageClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Admin Reports Management (Issue #920)
 *
 * ## Features
 * - **Generate Reports**: On-demand report generation with download
 * - **Schedule Reports**: Recurring reports with cron expressions
 * - **Email Delivery**: Configure up to 10 email recipients per report
 * - **Execution History**: View past report runs and status
 * - **Multiple Templates**: SystemHealth, UserActivity, AIUsage, ContentMetrics
 * - **Multiple Formats**: CSV, JSON, PDF
 */
const meta = {
  title: 'Admin/Reports',
  component: ReportsPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ReportsPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data
const mockScheduledReports = [
  {
    id: '550e8400-e29b-41d4-a716-446655440001',
    name: 'Weekly System Health',
    description: 'Comprehensive system health metrics every Monday morning',
    template: 'SystemHealth' as const,
    format: 'PDF' as const,
    parameters: {},
    scheduleExpression: '0 9 * * 1',
    isActive: true,
    createdAt: '2025-12-01T10:00:00Z',
    lastExecutedAt: '2025-12-11T09:00:00Z',
    createdBy: 'admin@meepleai.com',
    emailRecipients: ['team@meepleai.com', 'cto@meepleai.com'],
  },
  {
    id: '550e8400-e29b-41d4-a716-446655440002',
    name: 'Daily User Activity',
    description: 'User engagement metrics delivered every morning',
    template: 'UserActivity' as const,
    format: 'CSV' as const,
    parameters: {},
    scheduleExpression: '0 9 * * *',
    isActive: true,
    createdAt: '2025-12-01T10:00:00Z',
    lastExecutedAt: '2025-12-12T09:00:00Z',
    createdBy: 'admin@meepleai.com',
    emailRecipients: ['product@meepleai.com'],
  },
  {
    id: '550e8400-e29b-41d4-a716-446655440003',
    name: 'Monthly AI Usage Report',
    description: 'LLM costs and token usage summary',
    template: 'AIUsage' as const,
    format: 'JSON' as const,
    parameters: {},
    scheduleExpression: '0 9 1 * *',
    isActive: false,
    createdAt: '2025-12-01T10:00:00Z',
    lastExecutedAt: '2025-12-01T09:00:00Z',
    createdBy: 'admin@meepleai.com',
    emailRecipients: ['finance@meepleai.com'],
  },
];

const mockExecutions = [
  {
    id: '650e8400-e29b-41d4-a716-446655440001',
    reportId: '550e8400-e29b-41d4-a716-446655440001',
    reportName: 'Weekly System Health',
    template: 'SystemHealth' as const,
    status: 'Completed' as const,
    startedAt: '2025-12-11T09:00:00Z',
    completedAt: '2025-12-11T09:00:15Z',
    errorMessage: null,
    filePath: '/reports/system-health-20251211.pdf',
    fileSize: 245680,
  },
  {
    id: '650e8400-e29b-41d4-a716-446655440002',
    reportId: '550e8400-e29b-41d4-a716-446655440002',
    reportName: 'Daily User Activity',
    template: 'UserActivity' as const,
    status: 'Completed' as const,
    startedAt: '2025-12-12T09:00:00Z',
    completedAt: '2025-12-12T09:00:08Z',
    errorMessage: null,
    filePath: '/reports/user-activity-20251212.csv',
    fileSize: 52340,
  },
  {
    id: '650e8400-e29b-41d4-a716-446655440003',
    reportId: '550e8400-e29b-41d4-a716-446655440002',
    reportName: 'Daily User Activity',
    template: 'UserActivity' as const,
    status: 'Failed' as const,
    startedAt: '2025-12-10T09:00:00Z',
    completedAt: '2025-12-10T09:00:05Z',
    errorMessage: 'Database connection timeout',
    filePath: null,
    fileSize: null,
  },
  {
    id: '650e8400-e29b-41d4-a716-446655440004',
    reportId: '550e8400-e29b-41d4-a716-446655440001',
    reportName: 'Weekly System Health',
    template: 'SystemHealth' as const,
    status: 'Running' as const,
    startedAt: '2025-12-12T08:30:00Z',
    completedAt: null,
    errorMessage: null,
    filePath: null,
    fileSize: null,
  },
];

/**
 * Default reports page with scheduled reports and history.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
      },
    ],
  },
};

/**
 * Loading state while fetching reports.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        delay: 'infinite',
      },
      {
        url: '/api/v1/admin/reports/executions',
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
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Empty state with no scheduled reports.
 */
export const Empty: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: [],
      },
    ],
  },
};

/**
 * Scheduled reports tab with multiple reports.
 */
export const ScheduledReports: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
      },
    ],
  },
  play: async ({ canvasElement }) => {
    // Simulate switching to scheduled tab
    const scheduledTab = canvasElement.querySelector('[value="scheduled"]') as HTMLElement;
    if (scheduledTab) {
      scheduledTab.click();
    }
  },
};

/**
 * Execution history tab.
 */
export const ExecutionHistory: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
      },
    ],
  },
  play: async ({ canvasElement }) => {
    // Simulate switching to history tab
    const historyTab = canvasElement.querySelector('[value="history"]') as HTMLElement;
    if (historyTab) {
      historyTab.click();
    }
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
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
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
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
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
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
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
        url: '/api/v1/admin/reports/scheduled',
        method: 'GET',
        status: 200,
        response: mockScheduledReports,
      },
      {
        url: '/api/v1/admin/reports/executions',
        method: 'GET',
        status: 200,
        response: mockExecutions,
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
