import type { Meta, StoryObj } from '@storybook/react';
import { Alert, AlertTitle, AlertDescription } from './alert';
import { AlertCircle, Terminal } from 'lucide-react';

/**
 * Alert component displays a callout for user attention.
 * Based on Radix UI primitives with class-variance-authority styling.
 */
const meta = {
  title: 'UI/Alert',
  component: Alert,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive'],
      description: 'Visual style variant of the alert',
    },
  },
} satisfies Meta<typeof Alert>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default alert with informational content
 */
export const Default: Story = {
  args: {
    children: (
      <>
        <Terminal className="h-4 w-4" />
        <AlertTitle>Heads up!</AlertTitle>
        <AlertDescription>
          You can add components to your app using the cli.
        </AlertDescription>
      </>
    ),
  },
};

/**
 * Destructive variant for error or warning messages
 */
export const Destructive: Story = {
  args: {
    variant: 'destructive',
    children: (
      <>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Error</AlertTitle>
        <AlertDescription>
          Your session has expired. Please log in again.
        </AlertDescription>
      </>
    ),
  },
};

/**
 * Alert with only a title
 */
export const TitleOnly: Story = {
  args: {
    children: (
      <>
        <Terminal className="h-4 w-4" />
        <AlertTitle>System notification</AlertTitle>
      </>
    ),
  },
};

/**
 * Alert with only a description
 */
export const DescriptionOnly: Story = {
  args: {
    children: (
      <>
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          This is a simple alert message without a title.
        </AlertDescription>
      </>
    ),
  },
};

/**
 * Alert without an icon
 */
export const NoIcon: Story = {
  args: {
    children: (
      <>
        <AlertTitle>Information</AlertTitle>
        <AlertDescription>
          This alert doesn't have an icon.
        </AlertDescription>
      </>
    ),
  },
};
