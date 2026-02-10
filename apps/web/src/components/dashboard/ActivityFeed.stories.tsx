/**
 * ActivityFeed Storybook Stories
 * Issue #3911 - Enhanced Activity Feed Timeline Component
 *
 * Visual regression stories for:
 * - Default state with events
 * - Loading skeleton
 * - Empty state
 * - Long event list (>10 events)
 * - Different event types
 * - Dark mode variants
 */

import { ActivityFeed, type ActivityEvent } from './ActivityFeed';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Dashboard/ActivityFeed',
  component: ActivityFeed,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Timeline component showing user activity events with icons, relative timestamps, and entity links. Part of Dashboard Hub (Epic #3901).',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[400px] min-h-[500px] p-4 bg-background">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ActivityFeed>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Mock Data
// ============================================================================

const recentEvents: ActivityEvent[] = [
  {
    id: 'event-1',
    type: 'game_added',
    title: 'Aggiunto "Wingspan"',
    description: 'Aggiunto alla collezione',
    entityId: 'game-1',
    entityType: 'game',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), // 2 hours ago
  },
  {
    id: 'event-2',
    type: 'session_completed',
    title: 'Giocato "Catan"',
    description: '4 giocatori • 90 min',
    entityId: 'session-1',
    entityType: 'session',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5).toISOString(), // 5 hours ago
  },
  {
    id: 'event-3',
    type: 'chat_saved',
    title: 'Chat "Regole Wingspan"',
    description: '12 messaggi',
    entityId: 'chat-1',
    entityType: 'chat',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 28).toISOString(), // Yesterday
  },
  {
    id: 'event-4',
    type: 'wishlist_added',
    title: 'Aggiunto "Terraforming Mars"',
    description: 'Priorità alta',
    entityId: 'wishlist-1',
    entityType: 'wishlist',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(), // 2 days ago
  },
  {
    id: 'event-5',
    type: 'achievement_unlocked',
    title: 'Achievement: "Streak 7gg"',
    description: 'Giocato per 7 giorni consecutivi',
    entityId: 'achievement-1',
    entityType: 'achievement',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(), // 3 days ago
  },
];

const manyEvents: ActivityEvent[] = [
  ...recentEvents,
  ...Array.from({ length: 8 }, (_, i) => ({
    id: `event-${i + 6}`,
    type: (['game_added', 'session_completed', 'chat_saved', 'wishlist_added'] as const)[i % 4],
    title: `Event ${i + 6}`,
    description: `Description for event ${i + 6}`,
    entityId: `entity-${i + 6}`,
    entityType: 'game' as const,
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * (i + 4)).toISOString(),
  })),
];

// ============================================================================
// Stories
// ============================================================================

/**
 * Default state with recent activity events
 */
export const Default: Story = {
  args: {
    events: recentEvents,
    limit: 10,
    isLoading: false,
  },
};

/**
 * Loading skeleton state
 */
export const Loading: Story = {
  args: {
    events: [],
    isLoading: true,
  },
};

/**
 * Empty state when no activity exists
 */
export const Empty: Story = {
  args: {
    events: [],
    isLoading: false,
  },
};

/**
 * With many events (shows "View All" link)
 */
export const ManyEvents: Story = {
  args: {
    events: manyEvents,
    totalCount: 13,
    limit: 10,
    isLoading: false,
  },
};

/**
 * Limited to 5 events
 */
export const Limited: Story = {
  args: {
    events: recentEvents,
    limit: 5,
    isLoading: false,
  },
};

/**
 * Single event only
 */
export const SingleEvent: Story = {
  args: {
    events: [recentEvents[0]],
    isLoading: false,
  },
};

/**
 * All event type variants
 */
export const AllEventTypes: Story = {
  args: {
    events: [
      {
        id: 'event-game',
        type: 'game_added',
        title: 'Aggiunto "Wingspan"',
        description: 'Collezione',
        entityId: 'game-1',
        entityType: 'game',
        timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
      },
      {
        id: 'event-session',
        type: 'session_completed',
        title: 'Giocato "Catan"',
        description: '4 giocatori',
        entityId: 'session-1',
        entityType: 'session',
        timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
      },
      {
        id: 'event-chat',
        type: 'chat_saved',
        title: 'Chat salvata',
        description: '15 messaggi',
        entityId: 'chat-1',
        entityType: 'chat',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(),
      },
      {
        id: 'event-wishlist',
        type: 'wishlist_added',
        title: 'Aggiunto "Terraforming Mars"',
        description: 'Wishlist',
        entityId: 'wishlist-1',
        entityType: 'wishlist',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
      },
      {
        id: 'event-achievement',
        type: 'achievement_unlocked',
        title: 'Achievement: Collezionista',
        description: '10 giochi',
        entityId: 'achievement-1',
        entityType: 'achievement',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
      },
    ],
    isLoading: false,
  },
};

/**
 * Dark mode variant (requires Storybook dark background)
 */
export const DarkMode: Story = {
  args: {
    events: recentEvents,
    limit: 10,
    isLoading: false,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
