import type { Meta, StoryObj } from '@storybook/react';
import { Checkbox } from './checkbox';
import { Label } from './label';

/**
 * Checkbox component for boolean input.
 * Based on Radix UI Checkbox primitive with accessible design.
 */
const meta = {
  title: 'UI/Checkbox',
  component: Checkbox,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    disabled: {
      control: 'boolean',
      description: 'Whether the checkbox is disabled',
    },
    checked: {
      control: 'boolean',
      description: 'Controlled checked state',
    },
  },
} satisfies Meta<typeof Checkbox>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default checkbox
 */
export const Default: Story = {
  args: {},
};

/**
 * Checked checkbox
 */
export const Checked: Story = {
  args: {
    checked: true,
  },
};

/**
 * Disabled checkbox
 */
export const Disabled: Story = {
  args: {
    disabled: true,
  },
};

/**
 * Disabled and checked
 */
export const DisabledChecked: Story = {
  args: {
    disabled: true,
    checked: true,
  },
};

/**
 * Checkbox with label
 */
export const WithLabel: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Checkbox id="terms" />
      <Label htmlFor="terms">Accept terms and conditions</Label>
    </div>
  ),
};

/**
 * Multiple checkboxes
 */
export const Group: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center space-x-2">
        <Checkbox id="option1" />
        <Label htmlFor="option1">Option 1</Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="option2" />
        <Label htmlFor="option2">Option 2</Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="option3" />
        <Label htmlFor="option3">Option 3</Label>
      </div>
    </div>
  ),
};

/**
 * Form example with checkboxes
 */
export const FormExample: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label className="text-base font-semibold">Notifications</Label>
        <p className="text-sm text-muted-foreground">
          Select the notifications you want to receive.
        </p>
      </div>
      <div className="space-y-3">
        <div className="flex items-center space-x-2">
          <Checkbox id="email" defaultChecked />
          <div className="space-y-0.5">
            <Label htmlFor="email">Email notifications</Label>
            <p className="text-sm text-muted-foreground">
              Receive email about your account activity.
            </p>
          </div>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox id="marketing" />
          <div className="space-y-0.5">
            <Label htmlFor="marketing">Marketing emails</Label>
            <p className="text-sm text-muted-foreground">
              Receive emails about new products and features.
            </p>
          </div>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox id="security" defaultChecked disabled />
          <div className="space-y-0.5">
            <Label htmlFor="security">Security alerts</Label>
            <p className="text-sm text-muted-foreground">
              Receive alerts about your account security (required).
            </p>
          </div>
        </div>
      </div>
    </div>
  ),
};
