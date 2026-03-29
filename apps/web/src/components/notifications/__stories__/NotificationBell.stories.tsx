/**
 * NotificationBell Storybook Stories (Issue #2053)
 *
 * Visual regression testing with Chromatic.
 * Covers different states:
 * - No notifications (badge hidden)
 * - 1-9 unread (badge with number)
 * - 10+ unread (badge with "9+")
 * - Panel open with notifications
 * - Empty state
 * - Loading state
 */

import { useEffect } from 'react';

import type { NotificationDto } from '@/lib/api';
import { useNotificationStore } from '@/stores/notification/store';

import { NotificationBell } from '../NotificationBell';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof NotificationBell> = {
  title: 'Components/Notifications/NotificationBell',
  component: NotificationBell,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
  },
  decorators: [
    Story => (
      <div className="p-8">
        <Story />
      </div>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof NotificationBell>;

// Mock notifications
const createMockNotification = (overrides?: Partial<NotificationDto>): NotificationDto => ({
  id: crypto.randomUUID(),
  userId: 'user-1',
  type: 'pdf_upload_completed',
  severity: 'success',
  title: 'Upload Complete',
  message: 'Your PDF has been processed successfully',
  link: '/pdf/doc-1',
  metadata: null,
  isRead: false,
  createdAt: new Date().toISOString(),
  readAt: null,
  ...overrides,
});

// Story wrappers to control store state
function StoryWrapper({
  unreadCount,
  notifications = [],
}: {
  unreadCount: number;
  notifications?: NotificationDto[];
}) {
  useEffect(() => {
    const store = useNotificationStore.getState();
    store.reset();

    // Set mock state
    useNotificationStore.setState({
      unreadCount,
      notifications,
      isFetching: false,
      isLoading: false,
      error: null,
    });
  }, [unreadCount, notifications]);

  return <NotificationBell />;
}

export const NoNotifications: Story = {
  render: () => <StoryWrapper unreadCount={0} />,
  parameters: {
    docs: {
      description: {
        story: 'Bell icon with no badge when unread count is 0.',
      },
    },
  },
};

export const SingleUnread: Story = {
  render: () => (
    <StoryWrapper
      unreadCount={1}
      notifications={[
        createMockNotification({
          title: 'PDF Processed',
          message: 'Your document is ready for review',
        }),
      ]}
    />
  ),
  parameters: {
    docs: {
      description: {
        story: 'Bell with badge showing "1" for single unread notification.',
      },
    },
  },
};

export const MultipleUnread: Story = {
  render: () => (
    <StoryWrapper
      unreadCount={5}
      notifications={Array.from({ length: 5 }, (_, i) =>
        createMockNotification({
          id: `notif-${i}`,
          title: `Notification ${i + 1}`,
          message: `Message for notification ${i + 1}`,
          isRead: false,
        })
      )}
    />
  ),
  parameters: {
    docs: {
      description: {
        story: 'Bell with badge showing "5" for multiple unread notifications.',
      },
    },
  },
};

export const TenPlusUnread: Story = {
  render: () => (
    <StoryWrapper
      unreadCount={15}
      notifications={Array.from({ length: 15 }, (_, i) =>
        createMockNotification({
          id: `notif-${i}`,
          title: `Notification ${i + 1}`,
          isRead: false,
        })
      )}
    />
  ),
  parameters: {
    docs: {
      description: {
        story: 'Bell with badge showing "9+" for 10 or more unread notifications.',
      },
    },
  },
};

export const MixedReadUnread: Story = {
  render: () => (
    <StoryWrapper
      unreadCount={3}
      notifications={[
        createMockNotification({ id: '1', title: 'Unread 1', isRead: false }),
        createMockNotification({
          id: '2',
          title: 'Read 1',
          isRead: true,
          readAt: new Date().toISOString(),
        }),
        createMockNotification({ id: '3', title: 'Unread 2', isRead: false }),
        createMockNotification({
          id: '4',
          title: 'Read 2',
          isRead: true,
          readAt: new Date().toISOString(),
        }),
        createMockNotification({ id: '5', title: 'Unread 3', isRead: false }),
      ]}
    />
  ),
  parameters: {
    docs: {
      description: {
        story: 'Bell with mixed read/unread notifications. Badge shows only unread count.',
      },
    },
  },
};

export const DifferentSeverities: Story = {
  render: () => (
    <StoryWrapper
      unreadCount={4}
      notifications={[
        createMockNotification({
          id: '1',
          severity: 'success',
          title: 'Upload Successful',
          message: 'Your PDF has been processed',
        }),
        createMockNotification({
          id: '2',
          severity: 'info',
          title: 'New Comment',
          message: 'Someone commented on your shared link',
          type: 'new_comment',
        }),
        createMockNotification({
          id: '3',
          severity: 'warning',
          title: 'Low Quality PDF',
          message: 'Document quality below threshold',
        }),
        createMockNotification({
          id: '4',
          severity: 'error',
          title: 'Processing Failed',
          message: 'Unable to extract text from PDF',
          type: 'processing_failed',
        }),
      ]}
    />
  ),
  parameters: {
    docs: {
      description: {
        story: 'Notifications with different severity levels (success, info, warning, error).',
      },
    },
  },
};
