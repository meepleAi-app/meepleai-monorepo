/**
 * ReviewActionButtons Component Stories
 *
 * Visual testing for review lock action buttons with all states.
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReviewActionButtons } from './ReviewActionButtons';

// Mock QueryClient for hooks
const createMockQueryClient = () => {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Infinity,
      },
      mutations: {
        retry: false,
      },
    },
  });
};

const meta: Meta<typeof ReviewActionButtons> = {
  title: 'Admin/ShareRequests/ReviewActionButtons',
  component: ReviewActionButtons,
  parameters: {
    layout: 'padded',
  },
  decorators: [
    Story => {
      const queryClient = createMockQueryClient();
      return (
        <QueryClientProvider client={queryClient}>
          <div className="w-80">
            <Story />
          </div>
        </QueryClientProvider>
      );
    },
  ],
  tags: ['autodocs'],
  argTypes: {
    onAction: { action: 'action-triggered' },
  },
};

export default meta;
type Story = StoryObj<typeof ReviewActionButtons>;

/**
 * Not Locked - Available for Review
 * Shows "Start Review" button
 */
export const NotLocked: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: false,
      isLockedByCurrentAdmin: false,
      lockedByAdminName: null,
      lockExpiresAt: null,
    },
  },
};

/**
 * Locked by Current Admin - Normal State
 * Shows timer (> 5 min) + "Release Review" button
 */
export const LockedByYou: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: true,
      isLockedByCurrentAdmin: true,
      lockedByAdminName: 'You',
      lockExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(), // 15 minutes from now
    },
  },
};

/**
 * Locked by Current Admin - Expiring Soon
 * Shows timer (< 5 min) with warning + "Release Review" button
 */
export const LockedByYouExpiring: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: true,
      isLockedByCurrentAdmin: true,
      lockedByAdminName: 'You',
      lockExpiresAt: new Date(Date.now() + 3 * 60 * 1000).toISOString(), // 3 minutes from now
    },
  },
};

/**
 * Locked by Current Admin - Almost Expired
 * Timer with < 1 minute remaining
 */
export const LockedByYouAlmostExpired: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: true,
      isLockedByCurrentAdmin: true,
      lockedByAdminName: 'You',
      lockExpiresAt: new Date(Date.now() + 45 * 1000).toISOString(), // 45 seconds from now
    },
  },
};

/**
 * Locked by Another Admin
 * Shows info message with admin name
 */
export const LockedByOther: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: true,
      isLockedByCurrentAdmin: false,
      lockedByAdminName: 'Sarah Johnson',
      lockExpiresAt: new Date(Date.now() + 20 * 60 * 1000).toISOString(),
    },
  },
};

/**
 * Keyboard Shortcut Disabled
 * Locked by current admin but Escape shortcut disabled
 */
export const KeyboardShortcutDisabled: Story = {
  args: {
    shareRequestId: 'share-123',
    lockStatus: {
      isLocked: true,
      isLockedByCurrentAdmin: true,
      lockedByAdminName: 'You',
      lockExpiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
    },
    enableKeyboardShortcut: false,
  },
};
