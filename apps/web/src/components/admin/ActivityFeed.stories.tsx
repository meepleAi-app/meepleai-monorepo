import { ActivityFeed } from './ActivityFeed';

import type { ActivityEvent } from './ActivityFeed';
import type { Meta, StoryObj } from '@storybook/react';

/**
 * ActivityFeed - Issue #884
 *
 * Timeline for recent system activity events.
 * Features:
 * - Timeline layout with event type icons
 * - Relative timestamps (e.g., "5 minuti fa")
 * - Scrollable container (max 10 events)
 * - View All link
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
  argTypes: {
    showViewAll: {
      control: 'boolean',
      description: 'Show or hide the View All link',
    },
    viewAllHref: {
      control: 'text',
      description: 'Custom href for View All link',
    },
    maxEvents: {
      control: { type: 'number', min: 1, max: 20 },
      description: 'Maximum number of events to display',
    },
  },
} satisfies Meta<typeof ActivityFeed>;

export default meta;
type Story = StoryObj<typeof meta>;

// Generate dynamic timestamps relative to "now" for realistic relative times
const now = new Date();
const minutesAgo = (minutes: number) => new Date(now.getTime() - minutes * 60 * 1000).toISOString();
const hoursAgo = (hours: number) => new Date(now.getTime() - hours * 60 * 60 * 1000).toISOString();

const mockEvents: ActivityEvent[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'New user registered: john.doe@example.com',
    userId: 'user-123',
    userEmail: 'john.doe@example.com',
    entityId: 'user-123',
    entityType: 'User',
    timestamp: minutesAgo(5),
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
    timestamp: minutesAgo(15),
    severity: 'Info',
  },
  {
    id: '3',
    eventType: 'AlertCreated',
    description: 'Alert: High error rate detected (Severity: warning)',
    entityId: 'alert-456',
    entityType: 'Alert',
    timestamp: minutesAgo(30),
    severity: 'Warning',
  },
  {
    id: '4',
    eventType: 'ErrorOccurred',
    description: 'AI Request failed: Rate limit exceeded (Endpoint: qa)',
    userId: 'user-789',
    entityId: 'request-999',
    entityType: 'AiRequestLog',
    timestamp: minutesAgo(45),
    severity: 'Error',
  },
  {
    id: '5',
    eventType: 'AlertResolved',
    description: 'Alert: Database connection restored (Severity: critical)',
    entityId: 'alert-123',
    entityType: 'Alert',
    timestamp: hoursAgo(1),
    severity: 'Critical',
  },
  {
    id: '6',
    eventType: 'GameAdded',
    description: 'New game added: Wingspan',
    userId: 'admin-001',
    entityId: 'game-999',
    entityType: 'Game',
    timestamp: hoursAgo(2),
    severity: 'Info',
  },
  {
    id: '7',
    eventType: 'ConfigurationChanged',
    description: 'Configuration updated: RateLimit.MaxRequestsPerMinute changed to 100',
    userId: 'admin-001',
    entityId: 'config-123',
    entityType: 'Configuration',
    timestamp: hoursAgo(3),
    severity: 'Info',
  },
  {
    id: '8',
    eventType: 'PdfProcessed',
    description: 'PDF processing complete: Ticket-to-Ride-Rules.pdf',
    userId: 'user-222',
    entityId: 'pdf-888',
    entityType: 'PdfDocument',
    timestamp: hoursAgo(5),
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
    timestamp: hoursAgo(8),
    severity: 'Info',
  },
  {
    id: '10',
    eventType: 'SystemEvent',
    description: 'Daily backup completed successfully',
    entityId: 'backup-20251208',
    entityType: 'System',
    timestamp: hoursAgo(12),
    severity: 'Info',
  },
];

/**
 * Default activity feed with 10 events and View All link
 */
export const Default: Story = {
  args: {
    events: mockEvents,
  },
};

/**
 * Empty state (no activity) with enhanced icon
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
 * Limited to 5 events with View All link
 */
export const Limited: Story = {
  args: {
    events: mockEvents,
    maxEvents: 5,
  },
};

/**
 * Without View All link
 */
export const NoViewAllLink: Story = {
  args: {
    events: mockEvents,
    showViewAll: false,
  },
};

/**
 * Custom View All href
 */
export const CustomViewAllHref: Story = {
  args: {
    events: mockEvents,
    viewAllHref: '/admin/logs',
  },
};

/**
 * Many events to show scrolling behavior
 */
export const Scrollable: Story = {
  args: {
    events: Array.from({ length: 15 }, (_, i) => ({
      id: `event-${i}`,
      eventType: 'SystemEvent',
      description: `System event ${i + 1}: Processing batch operation`,
      timestamp: minutesAgo(i * 10),
      severity: 'Info' as const,
    })),
    maxEvents: 15,
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
