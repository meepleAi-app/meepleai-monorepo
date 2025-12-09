import type { Meta, StoryObj } from '@storybook/react';
import { ActivityFeed } from './ActivityFeed';
import type { ActivityEvent } from './ActivityFeed';

/**
 * ActivityFeed - Issue #874
 *
 * Displays recent system activity events.
 * Shows last 10 events with severity indicators and icons.
 */
const meta = {
  title: 'Admin/ActivityFeed',
  component: ActivityFeed,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ActivityFeed>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockEvents: ActivityEvent[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'New user registered: john.doe@example.com',
    userId: 'user-123',
    userEmail: 'john.doe@example.com',
    entityId: 'user-123',
    entityType: 'User',
    timestamp: new Date('2025-12-08T14:30:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '2',
    eventType: 'PdfUploaded',
    description: 'PDF uploaded: Catan-Rules.pdf (2456789 bytes)',
    userId: 'user-456',
    userEmail: 'alice@example.com',
    entityId: 'pdf-789',
    entityType: 'PdfDocument',
    timestamp: new Date('2025-12-08T14:25:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '3',
    eventType: 'AlertCreated',
    description: 'Alert: High error rate detected (Severity: warning)',
    entityId: 'alert-456',
    entityType: 'Alert',
    timestamp: new Date('2025-12-08T14:20:00Z').toISOString(),
    severity: 'Warning',
  },
  {
    id: '4',
    eventType: 'ErrorOccurred',
    description: 'AI Request failed: Rate limit exceeded (Endpoint: qa)',
    userId: 'user-789',
    entityId: 'request-999',
    entityType: 'AiRequestLog',
    timestamp: new Date('2025-12-08T14:15:00Z').toISOString(),
    severity: 'Error',
  },
  {
    id: '5',
    eventType: 'AlertResolved',
    description: 'Alert: Database connection restored (Severity: critical)',
    entityId: 'alert-123',
    entityType: 'Alert',
    timestamp: new Date('2025-12-08T14:10:00Z').toISOString(),
    severity: 'Critical',
  },
  {
    id: '6',
    eventType: 'GameAdded',
    description: 'New game added: Wingspan',
    userId: 'admin-001',
    entityId: 'game-999',
    entityType: 'Game',
    timestamp: new Date('2025-12-08T14:05:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '7',
    eventType: 'ConfigurationChanged',
    description: 'Configuration updated: RateLimit.MaxRequestsPerMinute changed to 100',
    userId: 'admin-001',
    entityId: 'config-123',
    entityType: 'Configuration',
    timestamp: new Date('2025-12-08T14:00:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '8',
    eventType: 'PdfProcessed',
    description: 'PDF processing complete: Ticket-to-Ride-Rules.pdf',
    userId: 'user-222',
    entityId: 'pdf-888',
    entityType: 'PdfDocument',
    timestamp: new Date('2025-12-08T13:55:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '9',
    eventType: 'UserLogin',
    description: 'User logged in: admin@meepleai.com',
    userId: 'admin-001',
    userEmail: 'admin@meepleai.com',
    entityId: 'session-777',
    entityType: 'Session',
    timestamp: new Date('2025-12-08T13:50:00Z').toISOString(),
    severity: 'Info',
  },
  {
    id: '10',
    eventType: 'SystemEvent',
    description: 'Daily backup completed successfully',
    entityId: 'backup-20251208',
    entityType: 'System',
    timestamp: new Date('2025-12-08T13:45:00Z').toISOString(),
    severity: 'Info',
  },
];

/**
 * Default activity feed with 10 events
 */
export const Default: Story = {
  args: {
    events: mockEvents,
  },
};

/**
 * Empty state (no activity)
 */
export const Empty: Story = {
  args: {
    events: [],
  },
};

/**
 * Only error events
 */
export const ErrorsOnly: Story = {
  args: {
    events: mockEvents.filter(e => e.severity === 'Error' || e.severity === 'Critical'),
  },
};

/**
 * Only info events
 */
export const InfoOnly: Story = {
  args: {
    events: mockEvents.filter(e => e.severity === 'Info'),
  },
};

/**
 * Limited to 5 events
 */
export const Limited: Story = {
  args: {
    events: mockEvents,
    maxEvents: 5,
  },
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {
    events: mockEvents,
  },
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
 * Tablet viewport
 */
export const Tablet: Story = {
  args: {
    events: mockEvents,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};
