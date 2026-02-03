import { ActivityList } from './activity-list';

import type { ActivityItem } from './utils';
import type { Meta, StoryObj } from '@storybook/react';

/**
 * Sample events for stories
 */
const sampleEvents: ActivityItem[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'New user registered',
    userEmail: 'john@example.com',
    timestamp: new Date(Date.now() - 5 * 60000).toISOString(),
    severity: 'Info',
  },
  {
    id: '2',
    eventType: 'FileUploaded',
    description: 'Document uploaded successfully',
    userEmail: 'jane@example.com',
    timestamp: new Date(Date.now() - 15 * 60000).toISOString(),
    severity: 'Info',
  },
  {
    id: '3',
    eventType: 'AlertCreated',
    description: 'High latency detected on API endpoint',
    timestamp: new Date(Date.now() - 30 * 60000).toISOString(),
    severity: 'Warning',
  },
  {
    id: '4',
    eventType: 'ErrorOccurred',
    description: 'Database connection timeout',
    timestamp: new Date(Date.now() - 45 * 60000).toISOString(),
    severity: 'Error',
  },
  {
    id: '5',
    eventType: 'ConfigurationChanged',
    description: 'System configuration updated',
    userEmail: 'admin@example.com',
    timestamp: new Date(Date.now() - 60 * 60000).toISOString(),
    severity: 'Info',
  },
  {
    id: '6',
    eventType: 'ItemAdded',
    description: 'New item added to catalog',
    userEmail: 'user@example.com',
    timestamp: new Date(Date.now() - 90 * 60000).toISOString(),
    severity: 'Info',
  },
];

const criticalEvent: ActivityItem = {
  id: '7',
  eventType: 'ErrorOccurred',
  description: 'Critical: System failure detected',
  timestamp: new Date(Date.now() - 2 * 60000).toISOString(),
  severity: 'Critical',
};

/**
 * ActivityList - Generic Activity Feed/Timeline
 *
 * A reusable scrollable list of activity events with configurable styling,
 * animations, and i18n support.
 *
 * @see Issue #2925 - Component Library extraction
 */
const meta = {
  title: 'Data Display/ActivityList',
  component: ActivityList,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
    docs: {
      description: {
        component: `
A versatile activity feed/timeline component that supports:
- **Icon modes**: Severity-based or category-based icons
- **Animations**: Optional staggered entry animation
- **i18n**: English and Italian language support
- **Highlighting**: Warning/Error events get visual emphasis
- **Scrollable**: Max height with overflow scroll
- **View All**: Link to full activity page

## Usage

\`\`\`tsx
import { ActivityList, type ActivityItem } from '@/components/ui/data-display/activity-list';

const events: ActivityItem[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'New user registered',
    timestamp: new Date().toISOString(),
    severity: 'Info',
  },
];

<ActivityList events={events} />
\`\`\`
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    iconMode: {
      control: 'select',
      options: ['severity', 'category'],
      description: 'Icon display mode',
    },
    locale: {
      control: 'select',
      options: ['en', 'it'],
      description: 'UI language',
    },
    animated: {
      control: 'boolean',
      description: 'Enable entry animation',
    },
    maxEvents: {
      control: { type: 'number', min: 1, max: 50 },
      description: 'Maximum events to display',
    },
    showViewAll: {
      control: 'boolean',
      description: 'Show View All link',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-[500px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ActivityList>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default activity list with severity-based icons
 */
export const Default: Story = {
  args: {
    events: sampleEvents,
  },
};

/**
 * Category-based icons (user/content/system/ai)
 */
export const CategoryIcons: Story = {
  args: {
    events: sampleEvents,
    iconMode: 'category',
  },
};

/**
 * With entry animation
 */
export const Animated: Story = {
  args: {
    events: sampleEvents,
    animated: true,
  },
};

/**
 * Italian language
 */
export const ItalianLocale: Story = {
  args: {
    events: sampleEvents,
    locale: 'it',
  },
};

/**
 * With warning/error highlighting
 */
export const WithHighlighting: Story = {
  args: {
    events: [criticalEvent, ...sampleEvents],
    iconMode: 'severity',
  },
};

/**
 * Empty state
 */
export const Empty: Story = {
  args: {
    events: [],
  },
};

/**
 * Custom title
 */
export const CustomTitle: Story = {
  args: {
    events: sampleEvents,
    title: 'System Events',
  },
};

/**
 * Without View All link
 */
export const NoViewAllLink: Story = {
  args: {
    events: sampleEvents,
    showViewAll: false,
  },
};

/**
 * Limited events (with overflow indicator)
 */
export const LimitedEvents: Story = {
  args: {
    events: [...sampleEvents, ...sampleEvents],
    maxEvents: 3,
  },
};

/**
 * Full configuration example
 */
export const FullConfig: Story = {
  args: {
    events: sampleEvents,
    iconMode: 'category',
    animated: true,
    locale: 'it',
    title: 'Attivita del Sistema',
    viewAllHref: '/admin/activity',
    maxEvents: 5,
  },
};

/**
 * Many events (scrollable)
 */
export const ManyEvents: Story = {
  args: {
    events: Array.from({ length: 20 }, (_, i) => ({
      id: `event-${i}`,
      eventType: ['UserRegistered', 'FileUploaded', 'ConfigurationChanged', 'AlertCreated'][i % 4],
      description: `Event ${i + 1}: ${['User action', 'File operation', 'Config change', 'Alert'][i % 4]}`,
      timestamp: new Date(Date.now() - i * 10 * 60000).toISOString(),
      severity: ['Info', 'Info', 'Info', 'Warning'][i % 4] as 'Info' | 'Warning',
    })),
    maxEvents: 20,
  },
};
