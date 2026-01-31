/**
 * N8N Workflow Templates - Visual Tests
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for workflow templates management.
 * Covers: template listing, categories, template details, import actions, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AdminPageClient } from './client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof AdminPageClient> = {
  title: 'Pages/Admin/N8NTemplates',
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

const mockTemplates = [
  {
    id: 'template-1',
    name: 'User Activity Monitoring',
    version: '1.2.0',
    description: 'Monitor user activity and send alerts for suspicious patterns',
    category: 'monitoring',
    author: 'MeepleAI',
    tags: ['monitoring', 'security', 'alerts'],
    icon: '🔍',
    screenshot: 'https://example.com/screenshot1.png',
    documentation: 'https://docs.meepleai.com/workflows/user-monitoring',
    parameters: [
      {
        name: 'alertEmail',
        type: 'string',
        label: 'Alert Email',
        description: 'Email address for security alerts',
        required: true,
        default: null,
        options: null,
        sensitive: false,
      },
      {
        name: 'threshold',
        type: 'number',
        label: 'Alert Threshold',
        description: 'Number of suspicious activities before alert',
        required: false,
        default: '5',
        options: null,
        sensitive: false,
      },
    ],
  },
  {
    id: 'template-2',
    name: 'Database Backup Automation',
    version: '2.0.1',
    description: 'Automated daily database backups with S3 upload',
    category: 'automation',
    author: 'MeepleAI',
    tags: ['backup', 'database', 'automation'],
    icon: '💾',
    screenshot: 'https://example.com/screenshot2.png',
    documentation: null,
    parameters: [
      {
        name: 's3Bucket',
        type: 'string',
        label: 'S3 Bucket Name',
        description: 'AWS S3 bucket for backup storage',
        required: true,
        default: null,
        options: null,
        sensitive: false,
      },
      {
        name: 's3AccessKey',
        type: 'string',
        label: 'AWS Access Key',
        description: 'AWS access key for S3',
        required: true,
        default: null,
        options: null,
        sensitive: true,
      },
    ],
  },
  {
    id: 'template-3',
    name: 'Slack Integration',
    version: '1.0.0',
    description: 'Send notifications to Slack channels',
    category: 'integration',
    author: 'Community',
    tags: ['slack', 'notifications', 'integration'],
    icon: '💬',
    screenshot: null,
    documentation: 'https://docs.meepleai.com/workflows/slack',
    parameters: [
      {
        name: 'webhookUrl',
        type: 'string',
        label: 'Slack Webhook URL',
        description: 'Incoming webhook URL from Slack',
        required: true,
        default: null,
        options: null,
        sensitive: true,
      },
      {
        name: 'channel',
        type: 'select',
        label: 'Default Channel',
        description: 'Default Slack channel for notifications',
        required: false,
        default: '#general',
        options: ['#general', '#alerts', '#monitoring', '#dev'],
        sensitive: false,
      },
    ],
  },
  {
    id: 'template-4',
    name: 'Data Sync Pipeline',
    version: '1.5.2',
    description: 'Synchronize data between databases and external APIs',
    category: 'data-processing',
    author: 'MeepleAI',
    tags: ['data', 'sync', 'api', 'integration'],
    icon: '🔄',
    screenshot: 'https://example.com/screenshot4.png',
    documentation: 'https://docs.meepleai.com/workflows/data-sync',
    parameters: [
      {
        name: 'apiEndpoint',
        type: 'string',
        label: 'API Endpoint',
        description: 'External API endpoint URL',
        required: true,
        default: null,
        options: null,
        sensitive: false,
      },
      {
        name: 'syncInterval',
        type: 'select',
        label: 'Sync Interval',
        description: 'How often to sync data',
        required: true,
        default: '1h',
        options: ['15m', '30m', '1h', '6h', '24h'],
        sensitive: false,
      },
    ],
  },
];

// ========== Stories ==========

/**
 * Default view with workflow templates
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: mockTemplates,
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
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: [],
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
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: [],
        },
      ],
    },
  },
};

/**
 * Error loading templates
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 500,
          response: { error: 'Failed to load workflow templates' },
        },
      ],
    },
  },
};

/**
 * Monitoring category only
 */
export const MonitoringCategory: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: mockTemplates.filter(t => t.category === 'monitoring'),
        },
      ],
    },
  },
};

/**
 * Integration category only
 */
export const IntegrationCategory: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: mockTemplates.filter(t => t.category === 'integration'),
        },
      ],
    },
  },
};

/**
 * Automation category only
 */
export const AutomationCategory: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: mockTemplates.filter(t => t.category === 'automation'),
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
 * Single template
 */
export const SingleTemplate: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: [mockTemplates[0]],
        },
      ],
    },
  },
};

/**
 * Templates without documentation
 */
export const NoDocumentation: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: mockTemplates.map(t => ({ ...t, documentation: null, screenshot: null })),
        },
      ],
    },
  },
};

/**
 * Templates with many parameters
 */
export const ManyParameters: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/admin/workflow-templates',
          method: 'GET',
          status: 200,
          response: [
            {
              ...mockTemplates[0],
              parameters: Array.from({ length: 10 }, (_, i) => ({
                name: `param${i + 1}`,
                type: i % 3 === 0 ? 'string' : i % 3 === 1 ? 'number' : 'select',
                label: `Parameter ${i + 1}`,
                description: `Description for parameter ${i + 1}`,
                required: i % 2 === 0,
                default: i % 2 === 0 ? null : 'default-value',
                options: i % 3 === 2 ? ['opt1', 'opt2', 'opt3'] : null,
                sensitive: i % 4 === 0,
              })),
            },
          ],
        },
      ],
    },
  },
};
