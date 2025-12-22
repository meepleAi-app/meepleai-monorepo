import { Mail, Download } from 'lucide-react';

import { LoadingButton } from './LoadingButton';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * LoadingButton - Button with integrated loading state
 *
 * ## Features
 * - Automatic loading spinner (Lucide Loader2 icon)
 * - Custom loading text support
 * - Disabled state during loading
 * - All shadcn/ui Button variants supported
 * - All shadcn/ui Button sizes supported
 * - ARIA busy and live region attributes
 * - Icon integration support
 *
 * ## Accessibility
 * - ✅ aria-busy attribute when loading
 * - ✅ aria-live="polite" for status updates
 * - ✅ Disabled state with pointer-events-none
 * - ✅ Loading spinner with aria-hidden
 * - ✅ Screen reader announcements for state changes
 */
const meta = {
  title: 'Loading/LoadingButton',
  component: LoadingButton,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Button component with integrated loading state. Automatically disables and shows spinner when isLoading is true. Built on top of shadcn/ui Button.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isLoading: {
      control: 'boolean',
      description: 'Loading state - shows spinner and disables button',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    loadingText: {
      control: 'text',
      description: 'Text to display when loading (replaces children)',
      table: {
        type: { summary: 'string' },
      },
    },
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      description: 'Button variant from shadcn/ui',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Button size from shadcn/ui',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state (independent of loading)',
    },
  },
} satisfies Meta<typeof LoadingButton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default button in normal state.
 * Not loading, fully interactive.
 */
export const Default: Story = {
  args: {
    children: 'Submit',
    isLoading: false,
  },
};

/**
 * Button in loading state.
 * Shows spinner, disabled, uses default loading text.
 */
export const Loading: Story = {
  args: {
    children: 'Submit',
    isLoading: true,
  },
};

/**
 * Loading with custom text.
 * Replaces children with custom loading message.
 */
export const LoadingWithText: Story = {
  args: {
    children: 'Upload File',
    isLoading: true,
    loadingText: 'Uploading...',
  },
};

/**
 * Disabled button (not loading).
 * Shows normal disabled state without spinner.
 */
export const Disabled: Story = {
  args: {
    children: 'Submit',
    disabled: true,
    isLoading: false,
  },
};

/**
 * Loading button with icon.
 * Shows spinner alongside loading text.
 */
export const LoadingWithIcon: Story = {
  args: {
    children: (
      <>
        <Mail className="mr-2 h-4 w-4" />
        Send Email
      </>
    ),
    isLoading: true,
    loadingText: 'Sending...',
  },
};

/**
 * All variants in loading state.
 * Comparison of all button variants with spinner.
 */
export const AllVariantsLoading: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2 flex-wrap">
        <LoadingButton variant="default" isLoading>
          Default
        </LoadingButton>
        <LoadingButton variant="destructive" isLoading>
          Destructive
        </LoadingButton>
        <LoadingButton variant="outline" isLoading>
          Outline
        </LoadingButton>
      </div>
      <div className="flex gap-2 flex-wrap">
        <LoadingButton variant="secondary" isLoading>
          Secondary
        </LoadingButton>
        <LoadingButton variant="ghost" isLoading>
          Ghost
        </LoadingButton>
        <LoadingButton variant="link" isLoading>
          Link
        </LoadingButton>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'All button variants in loading state with spinner.',
      },
    },
  },
};

/**
 * All sizes in loading state.
 * Comparison of all button sizes with spinner.
 */
export const AllSizesLoading: Story = {
  render: () => (
    <div className="flex items-center gap-4 flex-wrap">
      <LoadingButton size="sm" isLoading loadingText="Small">
        Small
      </LoadingButton>
      <LoadingButton size="default" isLoading loadingText="Default">
        Default
      </LoadingButton>
      <LoadingButton size="lg" isLoading loadingText="Large">
        Large
      </LoadingButton>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'All button sizes in loading state.',
      },
    },
  },
};

/**
 * Destructive action loading.
 * Delete operation in progress.
 */
export const DestructiveLoading: Story = {
  args: {
    children: 'Delete Game',
    variant: 'destructive',
    isLoading: true,
    loadingText: 'Deleting...',
  },
};

/**
 * Secondary action loading.
 * Secondary button with spinner.
 */
export const SecondaryLoading: Story = {
  args: {
    children: 'Save Draft',
    variant: 'secondary',
    isLoading: true,
    loadingText: 'Saving...',
  },
};

/**
 * Outline button loading.
 * Outline variant with spinner.
 */
export const OutlineLoading: Story = {
  args: {
    children: 'Export PDF',
    variant: 'outline',
    isLoading: true,
    loadingText: 'Exporting...',
  },
};

/**
 * Large loading button.
 * Prominent CTA in loading state.
 */
export const LargeLoading: Story = {
  args: {
    children: 'Start Upload',
    size: 'lg',
    isLoading: true,
    loadingText: 'Uploading Files...',
  },
};

/**
 * Small loading button.
 * Compact size in loading state.
 */
export const SmallLoading: Story = {
  args: {
    children: 'Retry',
    size: 'sm',
    isLoading: true,
    loadingText: 'Retrying...',
  },
};

/**
 * Form submission loading.
 * Common pattern for async form submits.
 */
export const FormSubmit: Story = {
  args: {
    type: 'submit',
    children: 'Create Account',
    isLoading: true,
    loadingText: 'Creating Account...',
  },
};

/**
 * File upload loading.
 * Upload button with download icon.
 */
export const FileUpload: Story = {
  args: {
    children: (
      <>
        <Download className="mr-2 h-4 w-4" />
        Upload File
      </>
    ),
    isLoading: true,
    loadingText: 'Uploading...',
  },
};

/**
 * Dark theme variant.
 * Shows loading button on dark background.
 */
export const DarkTheme: Story = {
  args: {
    children: 'Submit',
    isLoading: true,
    loadingText: 'Processing...',
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
