import { Checkbox } from './checkbox';
import { Label } from './label';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Checkbox component for boolean selections and multi-select interfaces.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Checkbox with accessible keyboard navigation.
 *
 * ## Features
 * - **States**: checked, unchecked, indeterminate
 * - **Keyboard accessible**: Space to toggle, Tab to navigate
 * - **Disabled support**: Visual and functional disabled state
 * - **Label integration**: Works with Label component
 *
 * ## Accessibility
 * - ✅ ARIA attributes (checked, disabled states)
 * - ✅ Keyboard navigation (Space, Tab)
 * - ✅ Focus ring indicator
 * - ✅ Screen reader friendly with proper labels
 */
const meta = {
  title: 'UI/Checkbox',
  component: Checkbox,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A checkbox component for binary selections with support for indeterminate state.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    checked: {
      control: 'boolean',
      description: 'Checkbox checked state',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
} satisfies Meta<typeof Checkbox>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default unchecked checkbox.
 * Initial state for user selection.
 */
export const Default: Story = {
  args: {
    checked: false,
  },
};

/**
 * Checked checkbox.
 * Shows active selected state.
 */
export const Checked: Story = {
  args: {
    checked: true,
  },
};

/**
 * Disabled unchecked checkbox.
 * Non-interactive disabled state.
 */
export const DisabledUnchecked: Story = {
  args: {
    checked: false,
    disabled: true,
  },
};

/**
 * Disabled checked checkbox.
 * Non-interactive selected state.
 */
export const DisabledChecked: Story = {
  args: {
    checked: true,
    disabled: true,
  },
};

/**
 * Checkbox with label.
 * Common pattern for form controls.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Checkbox id="terms" />
      <Label htmlFor="terms" className="cursor-pointer">
        Accept terms and conditions
      </Label>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Checkbox paired with a clickable label for better UX.',
      },
    },
  },
};

/**
 * Multiple checkboxes.
 * Shows checkbox group pattern for multi-select.
 */
export const MultipleCheckboxes: Story = {
  render: () => (
    <div className="space-y-3">
      <div className="flex items-center space-x-2">
        <Checkbox id="option1" defaultChecked />
        <Label htmlFor="option1" className="cursor-pointer">
          Option 1
        </Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="option2" />
        <Label htmlFor="option2" className="cursor-pointer">
          Option 2
        </Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="option3" />
        <Label htmlFor="option3" className="cursor-pointer">
          Option 3
        </Label>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Multiple checkboxes for independent selections.',
      },
    },
  },
};

/**
 * Checkbox with description.
 * Shows checkbox with additional context text.
 */
export const WithDescription: Story = {
  render: () => (
    <div className="flex items-start space-x-2">
      <Checkbox id="marketing" className="mt-1" />
      <div className="grid gap-1.5 leading-none">
        <Label htmlFor="marketing" className="cursor-pointer">
          Marketing emails
        </Label>
        <p className="text-sm text-muted-foreground">
          Receive emails about new products and special offers.
        </p>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Checkbox with descriptive text for clarity.',
      },
    },
  },
};

/**
 * Form example with checkboxes.
 * Complete form pattern with multiple options.
 */
export const FormExample: Story = {
  render: () => (
    <div className="space-y-4 w-96">
      <div className="space-y-3">
        <h4 className="text-sm font-medium">Select preferences</h4>
        <div className="flex items-center space-x-2">
          <Checkbox id="notifications" defaultChecked />
          <Label htmlFor="notifications" className="cursor-pointer">
            Email notifications
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox id="newsletter" />
          <Label htmlFor="newsletter" className="cursor-pointer">
            Newsletter subscription
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox id="updates" defaultChecked />
          <Label htmlFor="updates" className="cursor-pointer">
            Product updates
          </Label>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox id="beta" disabled />
          <Label htmlFor="beta" className="cursor-not-allowed">
            Beta features (coming soon)
          </Label>
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Complete form example with multiple checkbox options.',
      },
    },
  },
};

/**
 * All states comparison.
 * Visual comparison of checkbox states.
 */
export const AllStates: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <div className="flex items-center space-x-2">
          <Checkbox checked={false} />
          <span className="text-sm">Unchecked</span>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox checked={true} />
          <span className="text-sm">Checked</span>
        </div>
      </div>
      <div className="flex items-center gap-4">
        <div className="flex items-center space-x-2">
          <Checkbox checked={false} disabled />
          <span className="text-sm text-muted-foreground">Disabled Unchecked</span>
        </div>
        <div className="flex items-center space-x-2">
          <Checkbox checked={true} disabled />
          <span className="text-sm text-muted-foreground">Disabled Checked</span>
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available checkbox states.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows checkbox appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-3">
      <div className="flex items-center space-x-2">
        <Checkbox id="dark1" />
        <Label htmlFor="dark1" className="cursor-pointer">
          Unchecked checkbox
        </Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="dark2" defaultChecked />
        <Label htmlFor="dark2" className="cursor-pointer">
          Checked checkbox
        </Label>
      </div>
      <div className="flex items-center space-x-2">
        <Checkbox id="dark3" disabled />
        <Label htmlFor="dark3" className="cursor-not-allowed">
          Disabled checkbox
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
