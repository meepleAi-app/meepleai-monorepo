import { Terminal, AlertCircle, CheckCircle, Info } from 'lucide-react';

import { Alert, AlertTitle, AlertDescription } from './alert';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Alert component for displaying contextual messages and notifications.
 *
 * ## shadcn/ui Component
 * Based on accessible alert patterns with variant support.
 *
 * ## Features
 * - **2 variants**: default, destructive
 * - **Composition**: AlertTitle and AlertDescription sub-components
 * - **Icon support**: Custom icons via children composition
 * - **Semantic HTML**: Uses role="alert" for accessibility
 *
 * ## Accessibility
 * - ✅ ARIA role="alert" for screen readers
 * - ✅ Semantic structure with title and description
 * - ✅ Color coding with sufficient contrast
 * - ✅ Icon indicators for visual clarity
 */
const meta = {
  title: 'UI/Alert',
  component: Alert,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Displays a callout for user attention with optional title, description, and icon.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive'],
      description: 'Visual style variant',
      table: {
        type: { summary: '"default" | "destructive"' },
        defaultValue: { summary: 'default' },
      },
    },
  },
  decorators: [
    Story => (
      <div className="w-[500px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Alert>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default alert variant.
 * Use for general informational messages.
 */
export const Default: Story = {
  args: {
    variant: 'default',
    children: (
      <>
        <AlertTitle>Heads up!</AlertTitle>
        <AlertDescription>You can add components to your app using the cli.</AlertDescription>
      </>
    ),
  },
};

/**
 * Destructive alert variant.
 * Use for errors, warnings, and critical messages.
 */
export const Destructive: Story = {
  args: {
    variant: 'destructive',
    children: (
      <>
        <AlertTitle>Error</AlertTitle>
        <AlertDescription>Your session has expired. Please log in again.</AlertDescription>
      </>
    ),
  },
};

/**
 * Alert with icon.
 * Common pattern for visual context with terminal icon.
 */
export const WithIcon: Story = {
  render: () => (
    <Alert>
      <Terminal className="h-4 w-4" />
      <AlertTitle>Heads up!</AlertTitle>
      <AlertDescription>
        You can add components and dependencies to your app using the cli.
      </AlertDescription>
    </Alert>
  ),
};

/**
 * Destructive alert with error icon.
 * Shows error state with AlertCircle icon.
 */
export const DestructiveWithIcon: Story = {
  render: () => (
    <Alert variant="destructive">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Error</AlertTitle>
      <AlertDescription>
        Your session has expired. Please log in again to continue.
      </AlertDescription>
    </Alert>
  ),
};

/**
 * Success alert pattern.
 * Custom styling for success messages using default variant.
 */
export const Success: Story = {
  render: () => (
    <Alert className="border-green-500/50 text-green-600 dark:border-green-500 dark:text-green-400">
      <CheckCircle className="h-4 w-4" />
      <AlertTitle>Success</AlertTitle>
      <AlertDescription>Your changes have been saved successfully.</AlertDescription>
    </Alert>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Custom success styling using className override.',
      },
    },
  },
};

/**
 * Info alert pattern.
 * Custom styling for informational messages.
 */
export const InfoAlert: Story = {
  render: () => (
    <Alert className="border-blue-500/50 text-blue-600 dark:border-blue-500 dark:text-blue-400">
      <Info className="h-4 w-4" />
      <AlertTitle>Information</AlertTitle>
      <AlertDescription>
        This feature is currently in beta. Report any issues you encounter.
      </AlertDescription>
    </Alert>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Custom info styling using className override.',
      },
    },
  },
};

/**
 * Title only alert.
 * Minimal alert with just a title.
 */
export const TitleOnly: Story = {
  render: () => (
    <Alert>
      <AlertTitle>Important Update</AlertTitle>
    </Alert>
  ),
};

/**
 * Description only alert.
 * Alert without title for simple messages.
 */
export const DescriptionOnly: Story = {
  render: () => (
    <Alert>
      <AlertDescription>This is a simple alert message without a title.</AlertDescription>
    </Alert>
  ),
};

/**
 * Long content alert.
 * Tests alert with extended message content.
 */
export const LongContent: Story = {
  render: () => (
    <Alert>
      <Terminal className="h-4 w-4" />
      <AlertTitle>System Maintenance Notice</AlertTitle>
      <AlertDescription>
        Our systems will undergo scheduled maintenance on Sunday, March 15th from 2:00 AM to 6:00 AM
        EST. During this time, some features may be unavailable. We apologize for any inconvenience
        and appreciate your patience as we work to improve our services. If you experience any
        issues after the maintenance window, please contact our support team.
      </AlertDescription>
    </Alert>
  ),
};

/**
 * All variants comparison.
 * Visual comparison of default and destructive variants.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="space-y-4">
      <Alert>
        <Terminal className="h-4 w-4" />
        <AlertTitle>Default Variant</AlertTitle>
        <AlertDescription>This is the default alert variant for general messages.</AlertDescription>
      </Alert>
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Destructive Variant</AlertTitle>
        <AlertDescription>
          This is the destructive variant for errors and warnings.
        </AlertDescription>
      </Alert>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available alert variants.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows alert appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-4">
      <Alert>
        <Terminal className="h-4 w-4" />
        <AlertTitle>Default in Dark Mode</AlertTitle>
        <AlertDescription>Alert component adapts to dark theme automatically.</AlertDescription>
      </Alert>
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Destructive in Dark Mode</AlertTitle>
        <AlertDescription>Error messages maintain visibility in dark theme.</AlertDescription>
      </Alert>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
