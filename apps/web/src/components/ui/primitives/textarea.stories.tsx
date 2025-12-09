import type { Meta, StoryObj } from '@storybook/react';
import { Textarea } from './textarea';
import { Label } from './label';

/**
 * Textarea component for multi-line text input.
 *
 * ## shadcn/ui Component
 * Standard textarea with consistent styling and accessibility.
 *
 * ## Features
 * - **Auto-resizing height**: Minimum height with flexible expansion
 * - **Placeholder support**: Muted text for empty state
 * - **Disabled state**: Visual and functional disabled support
 * - **Focus ring**: Clear focus indicator for accessibility
 *
 * ## Accessibility
 * - ✅ Focus-visible ring indicator
 * - ✅ Keyboard navigation
 * - ✅ Proper label association
 * - ✅ Disabled state support
 */
const meta = {
  title: 'UI/Textarea',
  component: Textarea,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Displays a form textarea or a component that looks like a textarea.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    placeholder: {
      control: 'text',
      description: 'Placeholder text',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
    rows: {
      control: { type: 'number', min: 1, max: 20 },
      description: 'Number of visible text rows',
    },
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Textarea>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default textarea.
 * Basic multi-line text input.
 */
export const Default: Story = {
  args: {
    placeholder: 'Type your message here.',
  },
};

/**
 * Textarea with label.
 * Common pattern for form fields.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="message">Your message</Label>
      <Textarea id="message" placeholder="Type your message here." />
    </div>
  ),
};

/**
 * Textarea with helper text.
 * Shows descriptive text below input.
 */
export const WithHelperText: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="bio">Bio</Label>
      <Textarea id="bio" placeholder="Tell us about yourself" />
      <p className="text-sm text-muted-foreground">Your bio will be displayed on your profile.</p>
    </div>
  ),
};

/**
 * Textarea with default value.
 * Pre-filled textarea.
 */
export const WithDefaultValue: Story = {
  args: {
    defaultValue: 'This is some pre-filled content in the textarea that can be edited by the user.',
  },
};

/**
 * Disabled textarea.
 * Non-editable state.
 */
export const Disabled: Story = {
  args: {
    disabled: true,
    placeholder: 'This textarea is disabled',
  },
};

/**
 * Disabled with value.
 * Non-editable with content.
 */
export const DisabledWithValue: Story = {
  args: {
    disabled: true,
    defaultValue: 'This content cannot be edited because the textarea is disabled.',
  },
};

/**
 * Custom rows height.
 * Textarea with specific row count.
 */
export const CustomRows: Story = {
  args: {
    rows: 10,
    placeholder: 'This textarea has 10 rows',
  },
};

/**
 * Form example with textarea.
 * Complete form pattern.
 */
export const FormExample: Story = {
  render: () => (
    <div className="grid w-full gap-4">
      <div className="grid gap-1.5">
        <Label htmlFor="subject">Subject</Label>
        <input
          type="text"
          id="subject"
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          placeholder="Enter subject"
        />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="message-form">
          Message <span className="text-red-500">*</span>
        </Label>
        <Textarea id="message-form" placeholder="Type your message here." rows={5} />
        <p className="text-sm text-muted-foreground">
          Your message will be sent to the support team.
        </p>
      </div>
      <button className="inline-flex h-9 items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground">
        Send Message
      </button>
    </div>
  ),
};

/**
 * Textarea with character limit.
 * Shows remaining characters.
 */
export const WithCharacterLimit: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="limited">Comments (max 200 characters)</Label>
      <Textarea id="limited" placeholder="Type your comments here." maxLength={200} />
      <p className="text-sm text-muted-foreground text-right">0 / 200</p>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Textarea with maxLength attribute and character counter.',
      },
    },
  },
};

/**
 * Required textarea.
 * Shows required field indicator.
 */
export const Required: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="required">
        Feedback <span className="text-red-500">*</span>
      </Label>
      <Textarea id="required" placeholder="Your feedback is required" required />
    </div>
  ),
};

/**
 * Error state.
 * Shows textarea with validation error.
 */
export const ErrorState: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="error" className="text-red-500">
        Description
      </Label>
      <Textarea id="error" placeholder="Type your description" className="border-red-500" />
      <p className="text-sm text-red-500">Description must be at least 10 characters.</p>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows textarea appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="grid w-full gap-4">
      <div className="grid gap-1.5">
        <Label htmlFor="dark-message">Message</Label>
        <Textarea id="dark-message" placeholder="Type your message here." rows={5} />
        <p className="text-sm text-muted-foreground">Your message will be sent to the team.</p>
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="dark-disabled">Disabled</Label>
        <Textarea
          id="dark-disabled"
          placeholder="This is disabled"
          disabled
          defaultValue="Cannot edit this content"
        />
      </div>
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
