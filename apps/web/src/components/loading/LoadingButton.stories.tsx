import type { Meta, StoryObj } from '@storybook/react';
import { LoadingButton } from './LoadingButton';
import { Mail, Save, Send } from 'lucide-react';

/**
 * LoadingButton component with integrated loading state and spinner.
 * Automatically disables and shows loading indicator when isLoading is true.
 * Uses shadcn/ui Button internally for consistent styling.
 */
const meta = {
  title: 'Loading/LoadingButton',
  component: LoadingButton,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    isLoading: {
      control: 'boolean',
      description: 'Whether the button is in loading state',
    },
    loadingText: {
      control: 'text',
      description: 'Text to display when loading (replaces children)',
    },
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      description: 'Visual style variant',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Size variant',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the button is disabled',
    },
  },
} satisfies Meta<typeof LoadingButton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default loading button state
 */
export const Default: Story = {
  args: {
    children: 'Submit',
    isLoading: false,
  },
};

/**
 * Button in loading state with spinner
 */
export const Loading: Story = {
  args: {
    children: 'Submit',
    isLoading: true,
    loadingText: 'Submitting...',
  },
};

/**
 * Loading button without custom loading text
 */
export const LoadingNoText: Story = {
  args: {
    children: 'Save',
    isLoading: true,
  },
};

/**
 * Small loading button
 */
export const Small: Story = {
  args: {
    children: 'Save',
    size: 'sm',
    isLoading: true,
    loadingText: 'Saving...',
  },
};

/**
 * Large loading button
 */
export const Large: Story = {
  args: {
    children: 'Submit Form',
    size: 'lg',
    isLoading: true,
    loadingText: 'Processing...',
  },
};

/**
 * Destructive loading button for dangerous actions
 */
export const Destructive: Story = {
  args: {
    children: 'Delete Account',
    variant: 'destructive',
    isLoading: true,
    loadingText: 'Deleting...',
  },
};

/**
 * Outline variant loading button
 */
export const Outline: Story = {
  args: {
    children: 'Upload',
    variant: 'outline',
    isLoading: true,
    loadingText: 'Uploading...',
  },
};

/**
 * Button with icon and loading state
 */
export const WithIcon: Story = {
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
 * Disabled button (different from loading)
 */
export const Disabled: Story = {
  args: {
    children: 'Submit',
    disabled: true,
    isLoading: false,
  },
};

/**
 * Interactive demo showing loading state toggle
 */
export const Interactive: Story = {
  render: () => {
    const [isLoading, setIsLoading] = React.useState(false);

    const handleClick = () => {
      setIsLoading(true);
      setTimeout(() => {
        setIsLoading(false);
      }, 2000);
    };

    return (
      <LoadingButton
        isLoading={isLoading}
        loadingText="Processing..."
        onClick={handleClick}
      >
        Click Me
      </LoadingButton>
    );
  },
};

/**
 * All variants showcase
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2">
        <LoadingButton isLoading loadingText="Loading...">Default</LoadingButton>
        <LoadingButton variant="destructive" isLoading loadingText="Deleting...">Destructive</LoadingButton>
        <LoadingButton variant="outline" isLoading loadingText="Saving...">Outline</LoadingButton>
      </div>
      <div className="flex gap-2">
        <LoadingButton variant="secondary" isLoading loadingText="Processing...">Secondary</LoadingButton>
        <LoadingButton variant="ghost" isLoading loadingText="Loading...">Ghost</LoadingButton>
        <LoadingButton variant="link" isLoading>Link</LoadingButton>
      </div>
    </div>
  ),
};

/**
 * Form submission example
 */
export const FormSubmission: Story = {
  render: () => {
    const [isSubmitting, setIsSubmitting] = React.useState(false);

    const handleSubmit = (e: React.FormEvent) => {
      e.preventDefault();
      setIsSubmitting(true);
      setTimeout(() => {
        setIsSubmitting(false);
      }, 2000);
    };

    return (
      <form onSubmit={handleSubmit} className="space-y-4 p-4 border rounded">
        <div>
          <label className="block mb-2">Name:</label>
          <input type="text" className="border rounded px-3 py-2 w-full" />
        </div>
        <LoadingButton type="submit" isLoading={isSubmitting} loadingText="Submitting...">
          Submit Form
        </LoadingButton>
      </form>
    );
  },
};

// Import React for interactive stories
import React from 'react';
