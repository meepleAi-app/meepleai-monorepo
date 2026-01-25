import { Label } from './label';
import { RadioGroup, RadioGroupItem } from './radio-group';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * RadioGroup component for single selection from multiple options.
 *
 * ## Implementation
 * Lightweight native HTML radio implementation with React context.
 *
 * ## Features
 * - **Single selection**: One option from multiple choices
 * - **Keyboard support**: Arrow keys, Space, Tab navigation
 * - **Accessibility**: ARIA radiogroup with proper roles
 * - **Controlled/Uncontrolled**: Supports both modes
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Arrow keys, Space, Tab)
 * - ✅ Focus-visible ring indicator
 * - ✅ ARIA radiogroup role
 * - ✅ Disabled state support
 * - ✅ Label association via htmlFor
 */
const meta = {
  title: 'UI/RadioGroup',
  component: RadioGroup,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A radio group component for selecting a single option from multiple choices. Uses native HTML radio inputs with context-based state management.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: 'text',
      description: 'Controlled selected value',
      table: {
        type: { summary: 'string' },
      },
    },
    defaultValue: {
      control: 'text',
      description: 'Default value for uncontrolled mode',
      table: {
        type: { summary: 'string' },
      },
    },
    onValueChange: {
      action: 'value changed',
      description: 'Callback when selection changes',
      table: {
        type: { summary: '(value: string) => void' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state for all items',
    },
    name: {
      control: 'text',
      description: 'Name attribute for form submission',
      table: {
        type: { summary: 'string' },
      },
    },
  },
} satisfies Meta<typeof RadioGroup>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default radio group with three options.
 * Most common use case for single selection.
 */
export const Default: Story = {
  render: () => (
    <RadioGroup defaultValue="option-2">
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-1" id="option-1" />
        <Label htmlFor="option-1">Option 1</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-2" id="option-2" />
        <Label htmlFor="option-2">Option 2</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-3" id="option-3" />
        <Label htmlFor="option-3">Option 3</Label>
      </div>
    </RadioGroup>
  ),
};

/**
 * Controlled radio group with state.
 * Use for form state management.
 */
export const Controlled: Story = {
  render: () => (
    <div className="space-y-4">
      <RadioGroup defaultValue="comfortable">
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="default" id="r1" />
          <Label htmlFor="r1">Default</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="comfortable" id="r2" />
          <Label htmlFor="r2">Comfortable</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="compact" id="r3" />
          <Label htmlFor="r3">Compact</Label>
        </div>
      </RadioGroup>
      <p className="text-sm text-muted-foreground">Selected: comfortable (default)</p>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Controlled radio group with real-time value display.',
      },
    },
  },
};

/**
 * Disabled radio group.
 * Non-interactive state for all options.
 */
export const Disabled: Story = {
  render: () => (
    <RadioGroup disabled defaultValue="option-2">
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-1" id="d1" />
        <Label htmlFor="d1">Option 1</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-2" id="d2" />
        <Label htmlFor="d2">Option 2 (selected)</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-3" id="d3" />
        <Label htmlFor="d3">Option 3</Label>
      </div>
    </RadioGroup>
  ),
};

/**
 * Individual disabled items.
 * Selective disabled states within group.
 */
export const PartiallyDisabled: Story = {
  render: () => (
    <RadioGroup defaultValue="option-2">
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-1" id="p1" disabled />
        <Label htmlFor="p1" className="text-muted-foreground">
          Option 1 (disabled)
        </Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-2" id="p2" />
        <Label htmlFor="p2">Option 2</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-3" id="p3" />
        <Label htmlFor="p3">Option 3</Label>
      </div>
    </RadioGroup>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Radio group with selective disabled items.',
      },
    },
  },
};

/**
 * Payment method selection.
 * Real-world example for checkout forms.
 */
export const PaymentMethod: Story = {
  render: () => (
    <div className="space-y-4">
      <label className="text-sm font-medium">Payment Method</label>
      <RadioGroup defaultValue="card">
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="card" id="card" />
          <Label htmlFor="card">Credit Card</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="paypal" id="paypal" />
          <Label htmlFor="paypal">PayPal</Label>
        </div>
        <div className="flex items-center space-x-2">
          <RadioGroupItem value="bank" id="bank" />
          <Label htmlFor="bank">Bank Transfer</Label>
        </div>
      </RadioGroup>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Payment method selector for checkout flows.',
      },
    },
  },
};

/**
 * Notification preferences.
 * Settings UI example with descriptions.
 */
export const NotificationPreferences: Story = {
  render: () => (
    <div className="space-y-4">
      <label className="text-sm font-medium">Email Notifications</label>
      <RadioGroup defaultValue="daily">
          <div className="flex items-start space-x-2">
            <RadioGroupItem value="realtime" id="realtime" className="mt-1" />
            <div>
              <Label htmlFor="realtime">Real-time</Label>
              <p className="text-sm text-muted-foreground">
                Get notified immediately for every event
              </p>
            </div>
          </div>
          <div className="flex items-start space-x-2">
            <RadioGroupItem value="daily" id="daily" className="mt-1" />
            <div>
              <Label htmlFor="daily">Daily Summary</Label>
              <p className="text-sm text-muted-foreground">
                Receive a daily digest of all activities
              </p>
            </div>
          </div>
          <div className="flex items-start space-x-2">
            <RadioGroupItem value="weekly" id="weekly" className="mt-1" />
            <div>
              <Label htmlFor="weekly">Weekly Summary</Label>
              <p className="text-sm text-muted-foreground">
                Receive a weekly recap every Monday
              </p>
            </div>
          </div>
          <div className="flex items-start space-x-2">
            <RadioGroupItem value="none" id="none" className="mt-1" />
            <div>
              <Label htmlFor="none">None</Label>
              <p className="text-sm text-muted-foreground">
                Turn off all email notifications
              </p>
            </div>
          </div>
        </RadioGroup>
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Notification preferences with detailed descriptions.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows radio group appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <RadioGroup defaultValue="option-2">
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-1" id="dark-1" />
        <Label htmlFor="dark-1">Option 1</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-2" id="dark-2" />
        <Label htmlFor="dark-2">Option 2</Label>
      </div>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value="option-3" id="dark-3" />
        <Label htmlFor="dark-3">Option 3</Label>
      </div>
    </RadioGroup>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
