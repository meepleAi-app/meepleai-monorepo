import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';

import AlertRulesPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Alert Rules Management Page - Issue #921
 *
 * ## Features
 * - **Rule Management**: Create, edit, delete, and toggle alert rules
 * - **Stats Dashboard**: Overview cards (total, active, critical, templates)
 * - **Templates Tab**: Pre-configured alert templates for common scenarios
 * - **Real-time Updates**: Auto-refresh every 30 seconds
 * - **Form Validation**: Zod schema validation with inline error messages
 * - **Confirm Dialogs**: Safe delete confirmation with destructive variant
 *
 * ## Tabs
 * 1. **Alert Rules**: Table view of configured rules
 * 2. **Templates**: Gallery of template cards for quick setup
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Tablet (768px)
 * - Desktop (1024px, 1920px)
 */
const meta = {
  title: 'Admin/Pages/AlertRules',
  component: AlertRulesPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [768, 1024, 1920],
      delay: 500,
      diffThreshold: 0.2,
    },
    msw: {
      handlers: [
        http.get('/api/v1/admin/alert-rules', () => {
          return HttpResponse.json([
            {
              id: '1',
              name: 'High Error Rate',
              alertType: 'HighErrorRate',
              severity: 'Critical',
              thresholdValue: 5,
              thresholdUnit: '%',
              durationMinutes: 5,
              isEnabled: true,
              description: 'Alert when error rate exceeds 5%',
              createdAt: '2025-12-12T10:00:00Z',
              updatedAt: '2025-12-12T10:00:00Z',
            },
            {
              id: '2',
              name: 'High Latency',
              alertType: 'HighLatency',
              severity: 'Warning',
              thresholdValue: 1000,
              thresholdUnit: 'ms',
              durationMinutes: 10,
              isEnabled: true,
              description: 'Alert when P95 latency exceeds 1000ms',
              createdAt: '2025-12-12T10:00:00Z',
              updatedAt: '2025-12-12T10:00:00Z',
            },
            {
              id: '3',
              name: 'Service Down',
              alertType: 'ServiceDown',
              severity: 'Critical',
              thresholdValue: 1,
              thresholdUnit: 'count',
              durationMinutes: 1,
              isEnabled: false,
              description: 'Alert when health check fails',
              createdAt: '2025-12-12T10:00:00Z',
              updatedAt: '2025-12-12T10:00:00Z',
            },
            {
              id: '4',
              name: 'High Memory',
              alertType: 'HighMemory',
              severity: 'Warning',
              thresholdValue: 85,
              thresholdUnit: '%',
              durationMinutes: 15,
              isEnabled: true,
              description: 'Alert when memory usage exceeds 85%',
              createdAt: '2025-12-12T10:00:00Z',
              updatedAt: '2025-12-12T10:00:00Z',
            },
            {
              id: '5',
              name: 'Low Disk Space',
              alertType: 'LowDiskSpace',
              severity: 'Error',
              thresholdValue: 20,
              thresholdUnit: '%',
              durationMinutes: 5,
              isEnabled: true,
              description: 'Alert when disk space drops below 20%',
              createdAt: '2025-12-12T10:00:00Z',
              updatedAt: '2025-12-12T10:00:00Z',
            },
          ]);
        }),
        http.get('/api/v1/admin/alert-templates', () => {
          return HttpResponse.json([
            {
              name: 'High Error Rate',
              alertType: 'HighErrorRate',
              severity: 'Critical',
              thresholdValue: 5,
              thresholdUnit: '%',
              durationMinutes: 5,
              description: 'Triggers when error rate exceeds 5% for 5 minutes',
              category: 'Performance',
            },
            {
              name: 'High Latency (P95)',
              alertType: 'HighLatency',
              severity: 'Warning',
              thresholdValue: 1000,
              thresholdUnit: 'ms',
              durationMinutes: 10,
              description: 'Triggers when P95 latency exceeds 1s for 10 minutes',
              category: 'Performance',
            },
            {
              name: 'Service Down',
              alertType: 'ServiceDown',
              severity: 'Critical',
              thresholdValue: 1,
              thresholdUnit: 'count',
              durationMinutes: 1,
              description: 'Triggers immediately when health check fails',
              category: 'System',
            },
            {
              name: 'High CPU Usage',
              alertType: 'HighCPU',
              severity: 'Warning',
              thresholdValue: 80,
              thresholdUnit: '%',
              durationMinutes: 15,
              description: 'Triggers when CPU usage exceeds 80% for 15 minutes',
              category: 'System',
            },
            {
              name: 'High Memory Usage',
              alertType: 'HighMemory',
              severity: 'Warning',
              thresholdValue: 85,
              thresholdUnit: '%',
              durationMinutes: 15,
              description: 'Triggers when memory usage exceeds 85% for 15 minutes',
              category: 'System',
            },
            {
              name: 'Low Disk Space',
              alertType: 'LowDiskSpace',
              severity: 'Error',
              thresholdValue: 20,
              thresholdUnit: '%',
              durationMinutes: 5,
              description: 'Triggers when available disk space drops below 20%',
              category: 'System',
            },
            {
              name: 'Qdrant Down',
              alertType: 'QdrantDown',
              severity: 'Critical',
              thresholdValue: 1,
              thresholdUnit: 'count',
              durationMinutes: 1,
              description: 'Triggers immediately when Qdrant health check fails',
              category: 'Infrastructure',
            },
          ]);
        }),
      ],
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
          <Story />
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof AlertRulesPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default View - Page with multiple alert rules
 */
export const Default: Story = {};

/**
 * Empty State - No rules configured
 */
export const EmptyState: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/admin/alert-rules', () => {
          return HttpResponse.json([]);
        }),
        http.get('/api/v1/admin/alert-templates', () => {
          return HttpResponse.json([
            {
              name: 'High Error Rate',
              alertType: 'HighErrorRate',
              severity: 'Critical',
              thresholdValue: 5,
              thresholdUnit: '%',
              durationMinutes: 5,
              description: 'Triggers when error rate exceeds 5% for 5 minutes',
              category: 'Performance',
            },
          ]);
        }),
      ],
    },
  },
};

/**
 * Loading State - Fetching rules
 */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/admin/alert-rules', async () => {
          await new Promise(resolve => setTimeout(resolve, 5000));
          return HttpResponse.json([]);
        }),
      ],
    },
  },
};

/**
 * Error State - Failed to load rules
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/admin/alert-rules', () => {
          return HttpResponse.json({ error: 'Internal Server Error' }, { status: 500 });
        }),
      ],
    },
  },
};

/**
 * Templates Tab - Gallery of alert templates
 */
export const TemplatesTab: Story = {
  play: async ({ canvasElement }) => {
    // Auto-click Templates tab for Chromatic
    const templatesTab = canvasElement.querySelector('[value="templates"]');
    if (templatesTab instanceof HTMLElement) {
      templatesTab.click();
    }
  },
};
