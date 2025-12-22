import { useState } from 'react';

import { AlertDialog } from './alert-dialog';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * AlertDialog - Custom alert dialog component that replaces window.alert()
 * with a styled, testable, and accessible modal dialog.
 *
 * ## shadcn/ui Component
 * Built on Radix UI Dialog with custom styling and icon support.
 *
 * ## Features
 * - **5 variants**: info, success, warning, error, loading
 * - **Customizable content**: title, message, button text
 * - **Icon indicators**: Contextual icons for each variant
 * - **Fully testable**: No browser APIs (replaces window.alert)
 * - **SSR-safe**: Compatible with server-side rendering
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Tab, Enter, Escape)
 * - ✅ Focus management (auto-focus on OK button)
 * - ✅ ARIA labels for icon and dialog role
 * - ✅ Screen reader friendly structure
 */
const meta = {
  title: 'UI/AlertDialog',
  component: AlertDialog,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A replacement for window.alert() with styled variants, icons, and full accessibility support.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['info', 'success', 'warning', 'error', 'loading'],
      description: 'Visual variant with contextual icon and color',
      table: {
        type: { summary: '"info" | "success" | "warning" | "error" | "loading"' },
        defaultValue: { summary: 'info' },
      },
    },
    title: {
      control: 'text',
      description: 'Dialog title text',
    },
    message: {
      control: 'text',
      description: 'Dialog message content',
    },
    buttonText: {
      control: 'text',
      description: 'OK button text',
      table: {
        defaultValue: { summary: 'OK' },
      },
    },
    open: {
      control: 'boolean',
      description: 'Dialog open state',
    },
  },
  decorators: [
    Story => (
      <div className="min-w-[500px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof AlertDialog>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Info variant with blue icon.
 * Use for informational messages and general notifications.
 */
export const Info: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Information',
    message: 'This is an informational message to keep you updated.',
    variant: 'info',
    buttonText: 'OK',
  },
};

/**
 * Success variant with green icon.
 * Use for successful operations and confirmations.
 */
export const Success: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Success',
    message: 'Your changes have been saved successfully.',
    variant: 'success',
    buttonText: 'Great!',
  },
};

/**
 * Warning variant with yellow icon.
 * Use for warnings and cautionary messages.
 */
export const Warning: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Warning',
    message: 'This action may have unintended consequences. Please review before proceeding.',
    variant: 'warning',
    buttonText: 'Understood',
  },
};

/**
 * Error variant with red icon.
 * Use for error messages and critical alerts.
 */
export const Error: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Error',
    message: 'An error occurred while processing your request. Please try again.',
    variant: 'error',
    buttonText: 'Dismiss',
  },
};

/**
 * Loading variant with animated spinner.
 * Use for ongoing operations and processing states.
 */
export const Loading: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Processing',
    message: 'Please wait while we process your request...',
    variant: 'loading',
    buttonText: 'Cancel',
  },
};

/**
 * Long message content test.
 * Verifies dialog handles long text with proper wrapping.
 */
export const LongMessage: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Terms and Conditions',
    message:
      'By continuing, you agree to our terms of service and privacy policy. This includes data collection, processing, and storage as outlined in our documentation. We take your privacy seriously and implement industry-standard security measures to protect your information. Your data will never be sold to third parties without explicit consent.',
    variant: 'info',
    buttonText: 'I Agree',
  },
};

/**
 * Interactive example with state management.
 * Demonstrates dialog opening and closing behavior.
 */
export const Interactive: Story = {
  render: () => {
    const [open, setOpen] = useState(false);
    return (
      <>
        <button
          onClick={() => setOpen(true)}
          className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          Open Alert Dialog
        </button>
        <AlertDialog
          open={open}
          onOpenChange={setOpen}
          title="Interactive Example"
          message="This dialog can be opened and closed using the button."
          variant="info"
        />
      </>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Click the button to open the alert dialog and test interaction.',
      },
    },
  },
};

/**
 * All variants comparison.
 * Visual comparison of all alert dialog variants.
 */
export const AllVariants: Story = {
  render: () => {
    const [activeVariant, setActiveVariant] = useState<
      'info' | 'success' | 'warning' | 'error' | 'loading' | null
    >(null);

    return (
      <div className="flex flex-col gap-2">
        <button
          onClick={() => setActiveVariant('info')}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Info
        </button>
        <button
          onClick={() => setActiveVariant('success')}
          className="rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700"
        >
          Success
        </button>
        <button
          onClick={() => setActiveVariant('warning')}
          className="rounded-md bg-yellow-600 px-4 py-2 text-sm font-medium text-white hover:bg-yellow-700"
        >
          Warning
        </button>
        <button
          onClick={() => setActiveVariant('error')}
          className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700"
        >
          Error
        </button>
        <button
          onClick={() => setActiveVariant('loading')}
          className="rounded-md bg-gray-600 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700"
        >
          Loading
        </button>
        {activeVariant && (
          <AlertDialog
            open={!!activeVariant}
            onOpenChange={() => setActiveVariant(null)}
            title={activeVariant.charAt(0).toUpperCase() + activeVariant.slice(1)}
            message={`This is a ${activeVariant} alert dialog.`}
            variant={activeVariant}
          />
        )}
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Click buttons to see each variant in action.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows alert dialog appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    title: 'Dark Theme',
    message: 'This alert dialog adapts to dark theme automatically.',
    variant: 'success',
  },
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
