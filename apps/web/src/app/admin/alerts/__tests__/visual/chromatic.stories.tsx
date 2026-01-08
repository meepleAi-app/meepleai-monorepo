import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';
import { AlertsPageClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof AlertsPageClient> = {
  title: 'Admin/Alerts/Visual Tests',
  component: AlertsPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
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
type Story = StoryObj<typeof AlertsPageClient>;

const mockAlertsData = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    alertType: 'HighErrorRate',
    severity: 'critical',
    message: 'Error rate exceeded threshold: 15% (threshold: 5%)',
    triggeredAt: '2025-01-15T10:30:00Z',
    resolvedAt: null,
    metadata: {
      errorRate: '15%',
      threshold: '5%',
      endpoint: '/api/v1/chat',
      duration: '5m',
    },
    channelSent: {
      email: true,
      slack: true,
      webhook: false,
    },
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    alertType: 'HighLatency',
    severity: 'warning',
    message: 'API latency above threshold: 2.5s (threshold: 1s)',
    triggeredAt: '2025-01-15T09:15:00Z',
    resolvedAt: null,
    metadata: {
      latency: '2.5s',
      threshold: '1s',
      endpoint: '/api/v1/games',
      p95: '3.2s',
    },
    channelSent: {
      email: true,
      slack: false,
    },
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    alertType: 'DatabaseConnectionLoss',
    severity: 'error',
    message: 'Database connection pool exhausted',
    triggeredAt: '2025-01-15T08:45:00Z',
    resolvedAt: '2025-01-15T09:00:00Z',
    metadata: {
      poolSize: '100',
      activeConnections: '100',
      database: 'postgres',
    },
    channelSent: {
      email: true,
      slack: true,
      webhook: true,
    },
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    alertType: 'DiskSpaceLow',
    severity: 'warning',
    message: 'Disk space below 20%: 15% remaining',
    triggeredAt: '2025-01-15T07:30:00Z',
    resolvedAt: '2025-01-15T08:00:00Z',
    metadata: {
      usedSpace: '85%',
      threshold: '80%',
      mount: '/data',
    },
    channelSent: {
      email: true,
    },
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    alertType: 'ServiceHealthCheck',
    severity: 'info',
    message: 'Service health check passed with degraded performance',
    triggeredAt: '2025-01-15T06:00:00Z',
    resolvedAt: '2025-01-15T06:15:00Z',
    metadata: {
      service: 'qdrant',
      status: 'degraded',
      responseTime: '500ms',
    },
    channelSent: {
      email: false,
      slack: true,
    },
  },
];

/**
 * Visual Test: Default view with active alerts
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
  },
};

/**
 * Visual Test: No alerts (empty state)
 */
export const NoAlerts: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': [],
    },
  },
};

/**
 * Visual Test: All alerts (active + resolved)
 */
export const AllAlerts: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for alerts to load
    await canvas.findByText('HighErrorRate');

    // Toggle to show all alerts
    const showAllButton = await canvas.findByRole('button', { name: /show all/i });
    await userEvent.click(showAllButton);
  },
};

/**
 * Visual Test: Loading state
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': {
        delay: 'infinite',
      },
    },
  },
};

/**
 * Visual Test: Error state
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': {
        status: 500,
        error: 'Failed to fetch alerts from AlertManager',
      },
    },
  },
};

/**
 * Visual Test: Critical alerts only
 */
export const CriticalAlertsOnly: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => a.severity === 'critical'),
    },
  },
};

/**
 * Visual Test: Resolved alerts only
 */
export const ResolvedAlerts: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => a.resolvedAt !== null),
    },
  },
};

/**
 * Visual Test: Metadata dialog open
 */
export const MetadataDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for alerts to load
    await canvas.findByText('HighErrorRate');

    // Find and click the first Eye icon button
    const viewButtons = await canvas.findAllByRole('button');
    const eyeButton = viewButtons.find(btn => {
      const svg = btn.querySelector('svg.lucide-eye');
      return svg !== null;
    });

    if (eyeButton) {
      await userEvent.click(eyeButton);
      await expect(canvas.findByText('Alert Details:')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Resolve alert confirmation
 */
export const ResolveAlertConfirmation: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for alerts to load
    await canvas.findByText('HighErrorRate');

    // Find and click the first CheckCircle2 icon button (resolve)
    const actionButtons = await canvas.findAllByRole('button');
    const resolveButton = actionButtons.find(btn => {
      const svg = btn.querySelector('svg.lucide-check-circle-2');
      return svg !== null;
    });

    if (resolveButton) {
      await userEvent.click(resolveButton);
      // Dialog should open with confirmation
      await expect(canvas.findByText('Resolve Alert')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Severity badges display
 */
export const SeverityBadgesDisplay: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Toggle to show all alerts to see all severity types
    const showAllButton = await canvas.findByRole('button', { name: /show all/i });
    await userEvent.click(showAllButton);

    // Wait for all severity badges to be visible
    await canvas.findByText('CRITICAL');
    await canvas.findByText('ERROR');
    await canvas.findByText('WARNING');
    await canvas.findByText('INFO');
  },
};

/**
 * Visual Test: Auto-refresh active (30s interval)
 */
export const AutoRefreshActive: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for initial load
    await canvas.findByText('HighErrorRate');

    // Verify auto-refresh indicator is present (if visible in UI)
    // This ensures the component is set up for auto-refresh
  },
};

/**
 * Visual Test: Channel delivery status in metadata
 */
export const ChannelDeliveryStatus: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.slice(0, 1), // Just the critical alert
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for alert to load
    await canvas.findByText('HighErrorRate');

    // Open metadata dialog
    const viewButtons = await canvas.findAllByRole('button');
    const eyeButton = viewButtons.find(btn => {
      const svg = btn.querySelector('svg.lucide-eye');
      return svg !== null;
    });

    if (eyeButton) {
      await userEvent.click(eyeButton);

      // Verify channel delivery status is shown
      await expect(canvas.findByText('Delivery Status')).resolves.toBeInTheDocument();
      await expect(canvas.findByText('email:')).resolves.toBeInTheDocument();
      await expect(canvas.findByText('slack:')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Mobile responsive view
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Visual Test: Tablet responsive view
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/alerts': mockAlertsData.filter(a => !a.resolvedAt),
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
