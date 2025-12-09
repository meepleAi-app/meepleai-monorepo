import type { Meta, StoryObj } from '@storybook/react';
import { Label } from './label';
import { Input } from './input';
import { Checkbox } from './checkbox';

/**
 * Label component for form field labels with accessibility support.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Label with proper form control association.
 *
 * ## Features
 * - **Form control linking**: Connects to inputs via htmlFor
 * - **Peer state support**: Responds to peer-disabled states
 * - **Accessibility**: Proper label-input association
 * - **Styling**: Consistent typography and spacing
 *
 * ## Accessibility
 * - ✅ Proper label-input association via htmlFor
 * - ✅ Click label to focus input
 * - ✅ Disabled state styling
 * - ✅ Screen reader friendly
 */
const meta = {
  title: 'UI/Label',
  component: Label,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Renders an accessible label associated with form controls.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    htmlFor: {
      control: 'text',
      description: 'ID of the form control this label is for',
    },
    children: {
      control: 'text',
      description: 'Label text content',
    },
  },
} satisfies Meta<typeof Label>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default label.
 * Basic label for form controls.
 */
export const Default: Story = {
  args: {
    children: 'Label',
    htmlFor: 'input',
  },
};

/**
 * Label with input.
 * Common pattern for form fields.
 */
export const WithInput: Story = {
  render: () => (
    <div className="grid w-full max-w-sm items-center gap-1.5">
      <Label htmlFor="email">Email</Label>
      <Input type="email" id="email" placeholder="Email" />
    </div>
  ),
};

/**
 * Label with required indicator.
 * Shows required field pattern with asterisk.
 */
export const Required: Story = {
  render: () => (
    <div className="grid w-full max-w-sm items-center gap-1.5">
      <Label htmlFor="name">
        Name <span className="text-red-500">*</span>
      </Label>
      <Input type="text" id="name" placeholder="Enter your name" />
    </div>
  ),
};

/**
 * Label with helper text.
 * Shows label with descriptive help text below.
 */
export const WithHelperText: Story = {
  render: () => (
    <div className="grid w-full max-w-sm items-center gap-1.5">
      <Label htmlFor="username">Username</Label>
      <Input type="text" id="username" placeholder="@username" />
      <p className="text-sm text-muted-foreground">This will be your public display name.</p>
    </div>
  ),
};

/**
 * Label with checkbox.
 * Shows label paired with checkbox control.
 */
export const WithCheckbox: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Checkbox id="terms" />
      <Label htmlFor="terms" className="cursor-pointer">
        Accept terms and conditions
      </Label>
    </div>
  ),
};

/**
 * Disabled label and input.
 * Shows disabled state styling.
 */
export const Disabled: Story = {
  render: () => (
    <div className="grid w-full max-w-sm items-center gap-1.5">
      <Label htmlFor="disabled-input" className="opacity-70">
        Disabled Field
      </Label>
      <Input type="text" id="disabled-input" placeholder="Disabled" disabled />
    </div>
  ),
};

/**
 * Multiple labels in form.
 * Complete form example with multiple fields.
 */
export const FormExample: Story = {
  render: () => (
    <div className="grid w-full max-w-sm gap-4">
      <div className="grid gap-1.5">
        <Label htmlFor="first-name">First Name</Label>
        <Input type="text" id="first-name" placeholder="John" />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="last-name">Last Name</Label>
        <Input type="text" id="last-name" placeholder="Doe" />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="email-form">
          Email <span className="text-red-500">*</span>
        </Label>
        <Input type="email" id="email-form" placeholder="john@example.com" />
        <p className="text-sm text-muted-foreground">We'll never share your email.</p>
      </div>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows label appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="grid w-full max-w-sm gap-4">
      <div className="grid gap-1.5">
        <Label htmlFor="dark-email">Email</Label>
        <Input type="email" id="dark-email" placeholder="Email" />
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="dark-terms" />
        <Label htmlFor="dark-terms" className="cursor-pointer">
          Accept terms
        </Label>
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
