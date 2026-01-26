import { toast } from 'sonner';

import { Button } from '../primitives/button';
import { Toaster } from './sonner';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Toaster component for toast notifications.
 *
 * ## Integration
 * Wrapper around sonner with theme integration.
 *
 * ## Features
 * - **Multiple types**: Success, error, info, warning
 * - **Actions**: With action buttons
 * - **Promise states**: Loading, success, error
 * - **Positioning**: Customizable position
 *
 * ## Usage
 * Add `<Toaster />` to your layout and use `toast()` function.
 */
const meta = {
  title: 'UI/Toaster',
  component: Toaster,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Toast notification system using sonner. Supports various notification types and actions.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <>
        <Story />
        <Toaster />
      </>
    ),
  ],
} satisfies Meta<typeof Toaster>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default toast notification.
 */
export const Default: Story = {
  render: () => (
    <Button onClick={() => toast('Event has been created')}>Show Toast</Button>
  ),
};

/**
 * Success toast.
 */
export const Success: Story = {
  render: () => (
    <Button onClick={() => toast.success('Successfully saved!')}>Show Success</Button>
  ),
};

/**
 * Error toast.
 */
export const Error: Story = {
  render: () => (
    <Button onClick={() => toast.error('Something went wrong!')}>Show Error</Button>
  ),
};

/**
 * Toast with description.
 */
export const WithDescription: Story = {
  render: () => (
    <Button
      onClick={() =>
        toast('Event scheduled', {
          description: 'Your event has been scheduled for tomorrow at 10:00 AM',
        })
      }
    >
      Show with Description
    </Button>
  ),
};

/**
 * Toast with action button.
 */
export const WithAction: Story = {
  render: () => (
    <Button
      onClick={() =>
        toast('Event created', {
          action: {
            label: 'Undo',
            onClick: () => toast('Undone'),
          },
        })
      }
    >
      Show with Action
    </Button>
  ),
};

/**
 * All toast types.
 */
export const AllTypes: Story = {
  render: () => (
    <div className="flex flex-col gap-2">
      <Button onClick={() => toast('Default notification')}>Default</Button>
      <Button onClick={() => toast.success('Success!')}>Success</Button>
      <Button onClick={() => toast.error('Error!')}>Error</Button>
      <Button onClick={() => toast.info('Info')}>Info</Button>
      <Button onClick={() => toast.warning('Warning')}>Warning</Button>
    </div>
  ),
};
