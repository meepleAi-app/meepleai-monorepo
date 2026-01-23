/**
 * ReviewConflictDialog Component Stories
 *
 * Visual testing for 409 conflict dialog when review lock is already taken.
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ReviewConflictDialog } from './ReviewConflictDialog';

const meta: Meta<typeof ReviewConflictDialog> = {
  title: 'Admin/ShareRequests/ReviewConflictDialog',
  component: ReviewConflictDialog,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    onClose: { action: 'closed' },
  },
};

export default meta;
type Story = StoryObj<typeof ReviewConflictDialog>;

/**
 * Dialog Closed
 * Default closed state (not visible)
 */
export const Closed: Story = {
  args: {
    open: false,
    conflictDetails: {
      adminName: 'Sarah Johnson',
      adminId: 'admin-123',
    },
    onClose: () => {},
  },
};

/**
 * Dialog Open - Conflict with Known Admin
 * Shows when another admin has locked the review
 */
export const Open: Story = {
  args: {
    open: true,
    conflictDetails: {
      adminName: 'Sarah Johnson',
      adminId: 'admin-123',
    },
    onClose: () => {},
  },
};

/**
 * Dialog Open - Conflict with Unknown Admin
 * Shows when admin name is not available
 */
export const UnknownAdmin: Story = {
  args: {
    open: true,
    conflictDetails: null,
    onClose: () => {},
  },
};
