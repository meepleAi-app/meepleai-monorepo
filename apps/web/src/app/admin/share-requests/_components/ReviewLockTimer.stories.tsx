/**
 * ReviewLockTimer Component Stories
 *
 * Visual testing for review lock countdown timer with expiring warning.
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ReviewLockTimer } from './ReviewLockTimer';

const meta: Meta<typeof ReviewLockTimer> = {
  title: 'Admin/ShareRequests/ReviewLockTimer',
  component: ReviewLockTimer,
  parameters: {
    layout: 'centered',
    chromatic: {
      delay: 1500, // Allow timer to update
    },
  },
  tags: ['autodocs'],
  argTypes: {
    onExpired: { action: 'expired' },
  },
};

export default meta;
type Story = StoryObj<typeof ReviewLockTimer>;

/**
 * Normal State - Plenty of Time Remaining
 * Timer with > 5 minutes remaining (no warning)
 */
export const NormalState: Story = {
  args: {
    expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(), // 15 minutes from now
  },
};

/**
 * Expiring Soon - Warning State
 * Timer with < 5 minutes remaining (yellow warning)
 */
export const ExpiringSoon: Story = {
  args: {
    expiresAt: new Date(Date.now() + 3 * 60 * 1000).toISOString(), // 3 minutes from now
  },
};

/**
 * Almost Expired - Critical Warning
 * Timer with < 1 minute remaining
 */
export const AlmostExpired: Story = {
  args: {
    expiresAt: new Date(Date.now() + 45 * 1000).toISOString(), // 45 seconds from now
  },
};

/**
 * Just Expired
 * Timer at 0:00 (expired state)
 */
export const Expired: Story = {
  args: {
    expiresAt: new Date(Date.now() - 5000).toISOString(), // 5 seconds ago
  },
};

/**
 * Very Long Duration
 * Timer with 30 minutes remaining
 */
export const LongDuration: Story = {
  args: {
    expiresAt: new Date(Date.now() + 30 * 60 * 1000).toISOString(), // 30 minutes from now
  },
};
