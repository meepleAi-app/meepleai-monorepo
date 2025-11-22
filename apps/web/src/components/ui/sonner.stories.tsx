import type { Meta, StoryObj } from '@storybook/react';
import { Toaster } from './sonner';
import { Button } from './button';
import { toast } from 'sonner';

/**
 * Sonner component for toast notifications.
 * Based on the Sonner library with customizable styling.
 *
 * Note: Make sure to include <Toaster /> in your app layout.
 */
const meta = {
  title: 'UI/Sonner',
  component: Toaster,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Toaster>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default toast notification
 */
export const Default: Story = {
  render: () => (
    <>
      <Toaster />
      <Button onClick={() => toast('This is a toast message')}>
        Show Toast
      </Button>
    </>
  ),
};

/**
 * Success toast
 */
export const Success: Story = {
  render: () => (
    <>
      <Toaster />
      <Button onClick={() => toast.success('Operation completed successfully')}>
        Show Success
      </Button>
    </>
  ),
};

/**
 * Error toast
 */
export const Error: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        variant="destructive"
        onClick={() => toast.error('Something went wrong')}
      >
        Show Error
      </Button>
    </>
  ),
};

/**
 * Info toast
 */
export const Info: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        variant="outline"
        onClick={() => toast.info('This is an informational message')}
      >
        Show Info
      </Button>
    </>
  ),
};

/**
 * Warning toast
 */
export const Warning: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        variant="outline"
        onClick={() => toast.warning('Please review your input')}
      >
        Show Warning
      </Button>
    </>
  ),
};

/**
 * Toast with description
 */
export const WithDescription: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        onClick={() =>
          toast('Event has been created', {
            description: 'Sunday, December 03, 2023 at 9:00 AM',
          })
        }
      >
        Show with Description
      </Button>
    </>
  ),
};

/**
 * Toast with action
 */
export const WithAction: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        onClick={() =>
          toast('Event has been created', {
            action: {
              label: 'Undo',
              // onClick: () => console.log('Undo'),
              onClick: () => {
                // Handle undo action
              },
            },
          })
        }
      >
        Show with Action
      </Button>
    </>
  ),
};

/**
 * Loading toast
 */
export const Loading: Story = {
  render: () => (
    <>
      <Toaster />
      <Button onClick={() => toast.loading('Loading...')}>
        Show Loading
      </Button>
    </>
  ),
};

/**
 * Promise toast
 */
export const Promise: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        onClick={() => {
          const promise = () => new Promise((resolve) => setTimeout(resolve, 2000));
          toast.promise(promise, {
            loading: 'Loading...',
            success: 'Data loaded successfully',
            error: 'Error loading data',
          });
        }}
      >
        Show Promise
      </Button>
    </>
  ),
};

/**
 * Multiple toasts
 */
export const Multiple: Story = {
  render: () => (
    <>
      <Toaster />
      <div className="flex gap-2">
        <Button onClick={() => toast('First message')}>Toast 1</Button>
        <Button onClick={() => toast.success('Second message')}>Toast 2</Button>
        <Button onClick={() => toast.error('Third message')}>Toast 3</Button>
      </div>
    </>
  ),
};

/**
 * Custom duration
 */
export const CustomDuration: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        onClick={() =>
          toast('This toast will stay for 10 seconds', {
            duration: 10000,
          })
        }
      >
        Long Duration
      </Button>
    </>
  ),
};

/**
 * Dismissible toast
 */
export const Dismissible: Story = {
  render: () => (
    <>
      <Toaster />
      <Button
        onClick={() =>
          toast('You can dismiss this', {
            duration: Infinity,
          })
        }
      >
        Show Dismissible
      </Button>
    </>
  ),
};