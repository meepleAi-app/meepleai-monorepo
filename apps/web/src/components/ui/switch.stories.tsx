import type { Meta, StoryObj } from '@storybook/react';
import { Switch } from './switch';
import { Label } from './label';

/**
 * Switch component for toggle controls and boolean settings.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Switch with smooth toggle animation.
 *
 * ## Features
 * - **States**: checked (on) and unchecked (off)
 * - **Keyboard accessible**: Space to toggle, Tab to navigate
 * - **Disabled support**: Visual and functional disabled state
 * - **Label integration**: Works seamlessly with Label component
 *
 * ## Accessibility
 * - ✅ ARIA role="switch"
 * - ✅ Keyboard navigation (Space, Tab)
 * - ✅ Focus ring indicator
 * - ✅ Screen reader friendly with proper labeling
 */
const meta = {
  title: 'UI/Switch',
  component: Switch,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'A control that allows the user to toggle between checked and not checked.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    checked: {
      control: 'boolean',
      description: 'Switch checked state',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
} satisfies Meta<typeof Switch>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default unchecked switch.
 * Initial off state.
 */
export const Default: Story = {
  args: {
    checked: false,
  },
};

/**
 * Checked switch.
 * Active on state.
 */
export const Checked: Story = {
  args: {
    checked: true,
  },
};

/**
 * Disabled unchecked switch.
 * Non-interactive off state.
 */
export const DisabledUnchecked: Story = {
  args: {
    checked: false,
    disabled: true,
  },
};

/**
 * Disabled checked switch.
 * Non-interactive on state.
 */
export const DisabledChecked: Story = {
  args: {
    checked: true,
    disabled: true,
  },
};

/**
 * Switch with label.
 * Common pattern for settings controls.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Switch id="airplane-mode" />
      <Label htmlFor="airplane-mode" className="cursor-pointer">
        Airplane Mode
      </Label>
    </div>
  ),
};

/**
 * Multiple switches in settings.
 * Group of toggle settings.
 */
export const SettingsGroup: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Label htmlFor="notifications" className="cursor-pointer">
          Notifications
        </Label>
        <Switch id="notifications" defaultChecked />
      </div>
      <div className="flex items-center justify-between">
        <Label htmlFor="marketing" className="cursor-pointer">
          Marketing emails
        </Label>
        <Switch id="marketing" />
      </div>
      <div className="flex items-center justify-between">
        <Label htmlFor="social" className="cursor-pointer">
          Social mentions
        </Label>
        <Switch id="social" defaultChecked />
      </div>
      <div className="flex items-center justify-between">
        <Label htmlFor="security" className="cursor-pointer opacity-50">
          Security alerts
        </Label>
        <Switch id="security" checked disabled />
      </div>
    </div>
  ),
};

/**
 * Switch with description.
 * Toggle with additional context text.
 */
export const WithDescription: Story = {
  render: () => (
    <div className="flex items-start space-x-2">
      <Switch id="analytics" className="mt-1" />
      <div className="grid gap-1.5 leading-none">
        <Label htmlFor="analytics" className="cursor-pointer">
          Analytics
        </Label>
        <p className="text-sm text-muted-foreground">
          Share anonymous usage data to help improve the product.
        </p>
      </div>
    </div>
  ),
};

/**
 * Feature toggle example.
 * Switches for feature flags.
 */
export const FeatureToggles: Story = {
  render: () => (
    <div className="w-96 space-y-4">
      <h4 className="text-sm font-medium">Feature Flags</h4>
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="dark-mode" className="cursor-pointer">
              Dark Mode
            </Label>
            <p className="text-sm text-muted-foreground">Enable dark theme</p>
          </div>
          <Switch id="dark-mode" />
        </div>
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="beta" className="cursor-pointer">
              Beta Features
            </Label>
            <p className="text-sm text-muted-foreground">Try experimental features</p>
          </div>
          <Switch id="beta" defaultChecked />
        </div>
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="auto-save" className="cursor-pointer">
              Auto-save
            </Label>
            <p className="text-sm text-muted-foreground">Automatically save changes</p>
          </div>
          <Switch id="auto-save" defaultChecked />
        </div>
      </div>
    </div>
  ),
};

/**
 * All states comparison.
 * Visual comparison of switch states.
 */
export const AllStates: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <div className="flex items-center space-x-2">
          <Switch checked={false} />
          <span className="text-sm">Off</span>
        </div>
        <div className="flex items-center space-x-2">
          <Switch checked={true} />
          <span className="text-sm">On</span>
        </div>
      </div>
      <div className="flex items-center gap-4">
        <div className="flex items-center space-x-2">
          <Switch checked={false} disabled />
          <span className="text-sm text-muted-foreground">Disabled Off</span>
        </div>
        <div className="flex items-center space-x-2">
          <Switch checked={true} disabled />
          <span className="text-sm text-muted-foreground">Disabled On</span>
        </div>
      </div>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows switch appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Label htmlFor="dark-notifications" className="cursor-pointer">
          Notifications
        </Label>
        <Switch id="dark-notifications" />
      </div>
      <div className="flex items-center justify-between">
        <Label htmlFor="dark-emails" className="cursor-pointer">
          Email alerts
        </Label>
        <Switch id="dark-emails" defaultChecked />
      </div>
      <div className="flex items-center justify-between">
        <Label htmlFor="dark-disabled" className="cursor-not-allowed opacity-50">
          Disabled
        </Label>
        <Switch id="dark-disabled" checked disabled />
      </div>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background w-96">
        <Story />
      </div>
    ),
  ],
};
