import { DatabaseIcon, ServerIcon, WifiIcon, CpuIcon } from 'lucide-react';

import { StatusCard } from './status-card';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * StatusCard - Generic Health/Status Display Card
 *
 * A reusable card for displaying health status of any monitored entity.
 * Features pulsating status indicator, latency metrics, and error display.
 *
 * @see Issue #2925 - Component Library extraction
 */
const meta = {
  title: 'Data Display/StatusCard',
  component: StatusCard,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
    docs: {
      description: {
        component: `
A health/status display card that supports:
- **Status states**: healthy, degraded, unhealthy with visual indicators
- **Pulsating dot**: Animated status indicator
- **Latency display**: Response time in ms or seconds
- **Last check**: Relative timestamp
- **Error messages**: Displayed for degraded/unhealthy states
- **Custom icons**: Override default status icons
- **i18n support**: Customizable labels

## Usage

\`\`\`tsx
import { StatusCard } from '@/components/ui/data-display/status-card';

<StatusCard
  name="Database"
  status="healthy"
  latencyMs={45}
  lastCheck={new Date()}
/>
\`\`\`
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    status: {
      control: 'select',
      options: ['healthy', 'degraded', 'unhealthy'],
      description: 'Current health status',
    },
    loading: {
      control: 'boolean',
      description: 'Show loading skeleton',
    },
    latencyMs: {
      control: { type: 'number', min: 0, max: 10000 },
      description: 'Response time in milliseconds',
    },
  },
} satisfies Meta<typeof StatusCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Healthy status - everything is working
 */
export const Healthy: Story = {
  args: {
    name: 'Database',
    status: 'healthy',
    latencyMs: 45,
    lastCheck: new Date(),
  },
};

/**
 * Degraded status - working but with issues
 */
export const Degraded: Story = {
  args: {
    name: 'API Server',
    status: 'degraded',
    latencyMs: 850,
    lastCheck: new Date(Date.now() - 5 * 60000),
    errorMessage: 'High latency detected',
  },
};

/**
 * Unhealthy status - not working
 */
export const Unhealthy: Story = {
  args: {
    name: 'Cache Service',
    status: 'unhealthy',
    latencyMs: undefined,
    lastCheck: new Date(Date.now() - 15 * 60000),
    errorMessage: 'Connection refused: Unable to connect to Redis cluster',
  },
};

/**
 * With custom icon
 */
export const WithCustomIcon: Story = {
  args: {
    name: 'Vector Store',
    status: 'healthy',
    latencyMs: 120,
    lastCheck: new Date(),
    icon: DatabaseIcon,
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    name: 'Database',
    status: 'healthy',
    loading: true,
  },
};

/**
 * Without latency (status check only)
 */
export const WithoutLatency: Story = {
  args: {
    name: 'External API',
    status: 'healthy',
    lastCheck: new Date(),
  },
};

/**
 * High latency warning
 */
export const HighLatency: Story = {
  args: {
    name: 'Slow Service',
    status: 'degraded',
    latencyMs: 2500,
    lastCheck: new Date(),
    errorMessage: 'Response time exceeds threshold',
  },
};

/**
 * Infrastructure monitoring grid
 */
export const InfrastructureGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 max-w-2xl">
      <StatusCard
        name="PostgreSQL"
        status="healthy"
        latencyMs={32}
        lastCheck={new Date()}
        icon={DatabaseIcon}
      />
      <StatusCard
        name="Redis Cache"
        status="healthy"
        latencyMs={8}
        lastCheck={new Date()}
        icon={ServerIcon}
      />
      <StatusCard
        name="Vector Store"
        status="degraded"
        latencyMs={450}
        lastCheck={new Date(Date.now() - 2 * 60000)}
        errorMessage="Replication lag detected"
        icon={CpuIcon}
      />
      <StatusCard
        name="AI Services"
        status="unhealthy"
        lastCheck={new Date(Date.now() - 10 * 60000)}
        errorMessage="Model endpoint unreachable"
        icon={WifiIcon}
      />
    </div>
  ),
};

/**
 * All status states comparison
 */
export const StatusComparison: Story = {
  render: () => (
    <div className="space-y-4 max-w-md">
      <StatusCard
        name="Healthy Service"
        status="healthy"
        latencyMs={25}
        lastCheck={new Date()}
      />
      <StatusCard
        name="Degraded Service"
        status="degraded"
        latencyMs={750}
        lastCheck={new Date()}
        errorMessage="Performance degradation"
      />
      <StatusCard
        name="Unhealthy Service"
        status="unhealthy"
        lastCheck={new Date()}
        errorMessage="Service unavailable"
      />
    </div>
  ),
};

/**
 * With custom i18n labels (Italian)
 */
export const CustomLabels: Story = {
  args: {
    name: 'Database',
    status: 'healthy',
    latencyMs: 45,
    lastCheck: new Date(),
    labels: {
      status: 'Stato',
      responseTime: 'Tempo di risposta',
      lastCheck: 'Ultimo controllo',
      errorMessage: 'Errore',
      now: 'Ora',
      minAgo: 'min fa',
      hoursAgo: 'h fa',
    },
  },
};

/**
 * Loading grid
 */
export const LoadingGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 max-w-2xl">
      <StatusCard name="Service 1" status="healthy" loading />
      <StatusCard name="Service 2" status="healthy" loading />
      <StatusCard name="Service 3" status="healthy" loading />
      <StatusCard name="Service 4" status="healthy" loading />
    </div>
  ),
};
