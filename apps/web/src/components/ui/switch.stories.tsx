import type { Meta, StoryObj } from '@storybook/react';
import { Switch } from './switch';
import { Label } from './label';

/**
 * Switch component for boolean toggle input.
 * Based on Radix UI Switch primitive.
 */
const meta = {
  title: 'UI/Switch',
  component: Switch,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    checked: {
      control: 'boolean',
      description: 'Controlled checked state',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the switch is disabled',
    },
  },
} satisfies Meta<typeof Switch>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default switch (unchecked)
 */
export const Default: Story = {
  args: {},
};

/**
 * Checked switch
 */
export const Checked: Story = {
  args: {
    checked: true,
  },
};

/**
 * Disabled switch
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
 * Switch with label
 */
export const WithLabel: Story = {
  render: () => (
    <div className="flex items-center space-x-2">
      <Switch id="airplane-mode" />
      <Label htmlFor="airplane-mode">Airplane Mode</Label>
    </div>
  ),
};

/**
 * Switch with description
 */
export const WithDescription: Story = {
  render: () => (
    <div className="flex items-center justify-between space-x-2 w-[350px]">
      <div className="space-y-0.5">
        <Label htmlFor="marketing">Marketing emails</Label>
        <p className="text-sm text-muted-foreground">
          Receive emails about new products and features.
        </p>
      </div>
      <Switch id="marketing" />
    </div>
  ),
};

/**
 * Multiple switches
 */
export const Group: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center space-x-2">
        <Switch id="option1" defaultChecked />
        <Label htmlFor="option1">Option 1</Label>
      </div>
      <div className="flex items-center space-x-2">
        <Switch id="option2" />
        <Label htmlFor="option2">Option 2</Label>
      </div>
      <div className="flex items-center space-x-2">
        <Switch id="option3" defaultChecked />
        <Label htmlFor="option3">Option 3</Label>
      </div>
    </div>
  ),
};

/**
 * Settings form example
 */
export const SettingsForm: Story = {
  render: () => (
    <div className="w-[400px] space-y-6">
      <div>
        <h3 className="text-lg font-medium">Notifications</h3>
        <p className="text-sm text-muted-foreground">
          Configure how you receive notifications.
        </p>
      </div>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="email-notifs">Email notifications</Label>
            <p className="text-sm text-muted-foreground">
              Receive email about your account activity.
            </p>
          </div>
          <Switch id="email-notifs" defaultChecked />
        </div>
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="push-notifs">Push notifications</Label>
            <p className="text-sm text-muted-foreground">
              Receive push notifications on your device.
            </p>
          </div>
          <Switch id="push-notifs" />
        </div>
        <div className="flex items-center justify-between">
          <div className="space-y-0.5">
            <Label htmlFor="sms-notifs">SMS notifications</Label>
            <p className="text-sm text-muted-foreground">
              Receive text messages about your account.
            </p>
          </div>
          <Switch id="sms-notifs" />
        </div>
      </div>
    </div>
  ),
};

/**
 * Privacy settings example
 */
export const PrivacySettings: Story = {
  render: () => (
    <div className="w-[400px] space-y-6">
      <div>
        <h3 className="text-lg font-medium">Privacy</h3>
        <p className="text-sm text-muted-foreground">
          Manage your privacy settings.
        </p>
      </div>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <Label htmlFor="public-profile">Public profile</Label>
          <Switch id="public-profile" defaultChecked />
        </div>
        <div className="flex items-center justify-between">
          <Label htmlFor="show-email">Show email address</Label>
          <Switch id="show-email" />
        </div>
        <div className="flex items-center justify-between">
          <Label htmlFor="searchable">Make profile searchable</Label>
          <Switch id="searchable" defaultChecked />
        </div>
      </div>
    </div>
  ),
};
