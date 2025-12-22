import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';

import AlertConfigPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Alert Configuration Page - Issue #915
 *
 * ## Features
 * - **Email Configuration**: SMTP settings with TLS support
 * - **Slack Configuration**: Webhook URL and channel
 * - **PagerDuty Configuration**: Integration key management
 * - **Global Settings**: Enable/disable alerting + throttling
 * - **Test Functionality**: Test alerts per channel
 * - **Real-time Validation**: Inline error messages
 *
 * ## Tabs
 * 1. **Email**: SMTP host, port, credentials, recipients
 * 2. **Slack**: Webhook URL, target channel
 * 3. **PagerDuty**: Integration key
 * 4. **Global**: System-wide settings
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Tablet (768px)
 * - Desktop (1024px, 1920px)
 */
const meta = {
  title: 'Admin/Pages/AlertConfiguration',
  component: AlertConfigPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [768, 1024, 1920],
      delay: 500,
      diffThreshold: 0.2,
    },
    msw: {
      handlers: [
        // GET all configurations
        http.get('/api/v1/admin/alert-configuration', () => {
          return HttpResponse.json([
            {
              id: '1',
              configKey: 'email.smtp',
              configValue: JSON.stringify({
                smtpHost: 'smtp.gmail.com',
                smtpPort: 587,
                from: 'alerts@meepleai.dev',
                to: ['admin@example.com', 'ops@example.com'],
                useTls: true,
                username: 'alerts@meepleai.dev',
              }),
              category: 'Email',
              isEncrypted: false,
              description: 'SMTP email configuration',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            {
              id: '2',
              configKey: 'slack.webhook',
              configValue: JSON.stringify({
                webhookUrl: 'https://hooks.slack.com/services/T00/B00/XXX',
                channel: '#alerts',
              }),
              category: 'Slack',
              isEncrypted: false,
              description: 'Slack webhook configuration',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            {
              id: '3',
              configKey: 'pagerduty.integration',
              configValue: JSON.stringify({
                integrationKey: 'R00000000000000000000000',
              }),
              category: 'PagerDuty',
              isEncrypted: true,
              description: 'PagerDuty integration key',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            {
              id: '4',
              configKey: 'alerting.general',
              configValue: JSON.stringify({
                enabled: true,
                throttleMinutes: 60,
              }),
              category: 'Global',
              isEncrypted: false,
              description: 'General alerting settings',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
          ]);
        }),

        // GET by category
        http.get('/api/v1/admin/alert-configuration/:category', ({ params }) => {
          const { category } = params;

          const configs: Record<string, any> = {
            Email: {
              id: '1',
              configKey: 'email.smtp',
              configValue: JSON.stringify({
                smtpHost: 'smtp.gmail.com',
                smtpPort: 587,
                from: 'alerts@meepleai.dev',
                to: ['admin@example.com'],
                useTls: true,
              }),
              category: 'Email',
              isEncrypted: false,
              description: 'SMTP configuration',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            Slack: {
              id: '2',
              configKey: 'slack.webhook',
              configValue: JSON.stringify({
                webhookUrl: 'https://hooks.slack.com/services/T00/B00/XXX',
                channel: '#alerts',
              }),
              category: 'Slack',
              isEncrypted: false,
              description: 'Slack webhook',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            PagerDuty: {
              id: '3',
              configKey: 'pagerduty.integration',
              configValue: JSON.stringify({
                integrationKey: 'R00000000000000000000000',
              }),
              category: 'PagerDuty',
              isEncrypted: true,
              description: 'PagerDuty key',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
            Global: {
              id: '4',
              configKey: 'alerting.general',
              configValue: JSON.stringify({
                enabled: true,
                throttleMinutes: 60,
              }),
              category: 'Global',
              isEncrypted: false,
              description: 'General settings',
              updatedAt: '2025-12-12T10:00:00Z',
              updatedBy: 'admin',
            },
          };

          return HttpResponse.json(configs[category as string] || null);
        }),

        // PUT update configuration
        http.put('/api/v1/admin/alert-configuration', async ({ request }) => {
          const body = await request.json();
          console.log('Update alert configuration:', body);
          return HttpResponse.json({ message: 'Configuration updated successfully' });
        }),

        // Test alert endpoints
        http.post('/api/v1/admin/alert-test', async ({ request }) => {
          const body = await request.json();
          console.log('Test alert:', body);
          return HttpResponse.json({ success: true });
        }),

        // Mock current user
        http.get('/api/v1/auth/me', () => {
          return HttpResponse.json({
            id: 'user-1',
            email: 'admin@meepleai.dev',
            displayName: 'Admin User',
            role: 'admin',
            has2FAEnabled: false,
          });
        }),
      ],
    },
  },
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: Infinity,
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
} satisfies Meta<typeof AlertConfigPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view - Email tab active
 * Shows SMTP configuration form with all fields
 */
export const Default: Story = {};

/**
 * Email Tab - Complete configuration
 * Shows populated SMTP settings with multiple recipients
 */
export const EmailTab: Story = {
  play: async ({ canvasElement: _canvasElement }) => {
    // Tab is active by default
  },
};

/**
 * Slack Tab - Webhook configuration
 * Shows Slack webhook URL and channel settings
 */
export const SlackTab: Story = {
  play: async ({ canvasElement }) => {
    const canvas = canvasElement;
    const slackTab = canvas.querySelector('button[value="slack"]') as HTMLElement;
    if (slackTab) slackTab.click();
  },
};

/**
 * PagerDuty Tab - Integration key
 * Shows PagerDuty integration key configuration
 */
export const PagerDutyTab: Story = {
  play: async ({ canvasElement }) => {
    const canvas = canvasElement;
    const pagerdutyTab = canvas.querySelector('button[value="pagerduty"]') as HTMLElement;
    if (pagerdutyTab) pagerdutyTab.click();
  },
};

/**
 * Global Tab - System settings
 * Shows enable/disable toggle and throttling configuration
 */
export const GlobalTab: Story = {
  play: async ({ canvasElement }) => {
    const canvas = canvasElement;
    const generalTab = canvas.querySelector('button[value="general"]') as HTMLElement;
    if (generalTab) generalTab.click();
  },
};

/**
 * Mobile View - Email tab
 * Tests responsive design on mobile viewport (375px)
 */
export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet View - Slack tab
 * Tests responsive design on tablet viewport (768px)
 */
export const Tablet: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = canvasElement;
    const slackTab = canvas.querySelector('button[value="slack"]') as HTMLElement;
    if (slackTab) slackTab.click();
  },
};
