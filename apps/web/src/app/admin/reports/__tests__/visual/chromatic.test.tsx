import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from '@storybook/test';
import { ReportsPageClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

/**
 * Chromatic Visual Regression Tests for Reports Page (Issue #920)
 *
 * Tests responsive design (375px, 768px, 1024px) and key interactions:
 * - Generate report dialog
 * - Schedule report dialog
 * - Edit schedule dialog
 * - Scheduled reports list
 * - Execution history
 * - Email recipient management
 */
const meta: Meta<typeof ReportsPageClient> = {
  title: 'Admin/Reports/Visual Tests',
  component: ReportsPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
      viewports: [375, 768, 1024],
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof ReportsPageClient>;

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

const mockDataConfig = {
  '/api/v1/admin/reports/scheduled': mockScheduledReports,
  '/api/v1/admin/reports/executions': mockExecutions,
};

/**
 * Visual Test: Default view (Generate tab)
 */
export const DefaultView: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
};

/**
 * Visual Test: Empty state (no reports)
 */
export const EmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/reports/scheduled': [],
      '/api/v1/admin/reports/executions': [],
    },
  },
};

/**
 * Visual Test: Loading state
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/reports/scheduled': { delay: 'infinite' },
      '/api/v1/admin/reports/executions': { delay: 'infinite' },
    },
  },
};

/**
 * Visual Test: Error state
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/reports/scheduled': { status: 500, error: 'Internal Server Error' },
      '/api/v1/admin/reports/executions': { status: 500, error: 'Internal Server Error' },
    },
  },
};

/**
 * Visual Test: Scheduled reports tab
 */
export const ScheduledReportsTab: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const scheduledTab = await canvas.findByRole('tab', { name: /scheduled/i });
    await userEvent.click(scheduledTab);
  },
};

/**
 * Visual Test: Execution history tab
 */
export const ExecutionHistoryTab: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const historyTab = await canvas.findByRole('tab', { name: /history/i });
    await userEvent.click(historyTab);
  },
};

/**
 * Visual Test: Generate report dialog open
 */
export const GenerateReportDialog: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const generateButton = await canvas.findByRole('button', { name: /generate report/i });
    await userEvent.click(generateButton);
    await expect(canvas.findByText('Generate Report')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Schedule report dialog open
 */
export const ScheduleReportDialog: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const scheduleButton = await canvas.findByRole('button', { name: /schedule report/i });
    await userEvent.click(scheduleButton);
    await expect(canvas.findByText('Schedule Report')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Schedule dialog with form filled
 */
export const ScheduleDialogFilled: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const scheduleButton = await canvas.findByRole('button', { name: /schedule report/i });
    await userEvent.click(scheduleButton);

    // Fill form
    const nameInput = await canvas.findByLabelText(/report name/i);
    await userEvent.type(nameInput, 'Monthly Performance Report');

    const descInput = await canvas.findByLabelText(/description/i);
    await userEvent.type(descInput, 'Comprehensive monthly performance metrics');

    // Add email
    const emailInput = await canvas.findByPlaceholderText(/email@example.com/i);
    await userEvent.type(emailInput, 'analytics@meepleai.com');
    const addButton = await canvas.findByRole('button', { name: /add/i });
    await userEvent.click(addButton);
  },
};

/**
 * Visual Test: Edit schedule dialog
 */
export const EditScheduleDialog: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Go to scheduled tab
    const scheduledTab = await canvas.findByRole('tab', { name: /scheduled/i });
    await userEvent.click(scheduledTab);

    // Click edit button on first report
    const editButtons = await canvas.findAllByRole('button', { name: /edit/i });
    if (editButtons.length > 0) {
      await userEvent.click(editButtons[0]);
      await expect(canvas.findByText('Edit Report Schedule')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Quick generate card interaction
 */
export const QuickGenerateCardClick: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Click on a template card
    const systemHealthCard = await canvas.findByText('System Health');
    await userEvent.click(systemHealthCard);
    await expect(canvas.findByText('Generate Report')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Mobile responsive (375px)
 */
export const MobileView: Story = {
  parameters: {
    mockData: mockDataConfig,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Visual Test: Tablet responsive (768px)
 */
export const TabletView: Story = {
  parameters: {
    mockData: mockDataConfig,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Visual Test: Desktop responsive (1024px)
 */
export const DesktopView: Story = {
  parameters: {
    mockData: mockDataConfig,
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
  },
};

/**
 * Visual Test: Mobile - Schedule dialog
 */
export const MobileScheduleDialog: Story = {
  parameters: {
    mockData: mockDataConfig,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const scheduleButton = await canvas.findByRole('button', { name: /schedule report/i });
    await userEvent.click(scheduleButton);
  },
};

/**
 * Visual Test: Dark theme
 */
export const DarkTheme: Story = {
  parameters: {
    mockData: mockDataConfig,
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Visual Test: Scheduled reports with inactive badge
 */
export const InactiveReport: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const scheduledTab = await canvas.findByRole('tab', { name: /scheduled/i });
    await userEvent.click(scheduledTab);

    // Verify inactive badge is visible
    await expect(canvas.findByText('Inactive')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Execution history with different statuses
 */
export const ExecutionStatuses: Story = {
  parameters: {
    mockData: mockDataConfig,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const historyTab = await canvas.findByRole('tab', { name: /history/i });
    await userEvent.click(historyTab);

    // Verify all status badges
    await expect(canvas.findByText('Completed')).resolves.toBeInTheDocument();
    await expect(canvas.findByText('Failed')).resolves.toBeInTheDocument();
    await expect(canvas.findByText('Running')).resolves.toBeInTheDocument();
  },
};
