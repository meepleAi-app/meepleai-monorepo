import type { Meta, StoryObj } from '@storybook/react';
import { Label } from './label';
import { Input } from './input';
import { Checkbox } from './checkbox';

/**
 * Label component for form field labels.
 * Based on Radix UI Label primitive with accessible design.
 */
const meta = {
  title: 'UI/Label',
  component: Label,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Label>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default label
 */
export const Default: Story = {
  args: {
    children: 'Label',
  },
};

/**
 * Label with input
 */
export const WithInput: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="input">Email address</Label>
      <Input id="input" type="email" placeholder="email@example.com" />
    </div>
  ),
};

/**
 * Label with checkbox
 */
export const WithCheckbox: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Checkbox id="terms" />
      <Label htmlFor="terms">Accept terms and conditions</Label>
    </div>
  ),
};

/**
 * Required field label
 */
export const Required: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="required-input">
        Name <span className="text-destructive">*</span>
      </Label>
      <Input id="required-input" placeholder="John Doe" />
    </div>
  ),
};

/**
 * Label with helper text
 */
export const WithHelperText: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="helper-input">Username</Label>
      <Input id="helper-input" placeholder="@username" />
      <p className="text-sm text-muted-foreground">
        This is your public display name.
      </p>
    </div>
  ),
};

/**
 * Multiple labels in a form
 */
export const FormExample: Story = {
  render: () => (
    <div className="w-[350px] space-y-4">
      <div className="space-y-2">
        <Label htmlFor="first-name">First name</Label>
        <Input id="first-name" placeholder="John" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="last-name">Last name</Label>
        <Input id="last-name" placeholder="Doe" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="email-form">Email</Label>
        <Input id="email-form" type="email" placeholder="john@example.com" />
      </div>
    </div>
  ),
};

/**
 * Disabled label (with disabled input)
 */
export const Disabled: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="disabled-input" className="text-muted-foreground">
        Disabled field
      </Label>
      <Input id="disabled-input" placeholder="Disabled" disabled />
    </div>
  ),
};
