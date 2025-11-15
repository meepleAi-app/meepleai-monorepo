import type { Meta, StoryObj } from '@storybook/react';
import { Separator } from './separator';

/**
 * Separator component creates visual dividers between content.
 * Based on Radix UI Separator primitive.
 */
const meta = {
  title: 'UI/Separator',
  component: Separator,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    orientation: {
      control: 'select',
      options: ['horizontal', 'vertical'],
      description: 'Orientation of the separator',
    },
    decorative: {
      control: 'boolean',
      description: 'Whether the separator is decorative',
    },
  },
} satisfies Meta<typeof Separator>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default horizontal separator
 */
export const Default: Story = {
  render: () => (
    <div className="w-[350px]">
      <div className="space-y-1">
        <h4 className="text-sm font-medium leading-none">Radix Primitives</h4>
        <p className="text-sm text-muted-foreground">
          An open-source UI component library.
        </p>
      </div>
      <Separator className="my-4" />
      <div className="flex h-5 items-center space-x-4 text-sm">
        <div>Blog</div>
        <Separator orientation="vertical" />
        <div>Docs</div>
        <Separator orientation="vertical" />
        <div>Source</div>
      </div>
    </div>
  ),
};

/**
 * Horizontal separator
 */
export const Horizontal: Story = {
  render: () => (
    <div className="w-[350px] space-y-4">
      <div>Content above separator</div>
      <Separator />
      <div>Content below separator</div>
    </div>
  ),
};

/**
 * Vertical separator
 */
export const Vertical: Story = {
  render: () => (
    <div className="flex h-8 items-center space-x-4">
      <div>Item 1</div>
      <Separator orientation="vertical" />
      <div>Item 2</div>
      <Separator orientation="vertical" />
      <div>Item 3</div>
    </div>
  ),
};

/**
 * In a menu
 */
export const InMenu: Story = {
  render: () => (
    <div className="w-[200px] rounded-md border p-2">
      <div className="px-2 py-1.5 text-sm font-semibold">My Account</div>
      <Separator className="my-1" />
      <div className="space-y-1">
        <div className="px-2 py-1.5 text-sm hover:bg-accent rounded-sm cursor-pointer">
          Profile
        </div>
        <div className="px-2 py-1.5 text-sm hover:bg-accent rounded-sm cursor-pointer">
          Settings
        </div>
        <div className="px-2 py-1.5 text-sm hover:bg-accent rounded-sm cursor-pointer">
          Billing
        </div>
      </div>
      <Separator className="my-1" />
      <div className="px-2 py-1.5 text-sm hover:bg-accent rounded-sm cursor-pointer text-destructive">
        Logout
      </div>
    </div>
  ),
};

/**
 * In a card
 */
export const InCard: Story = {
  render: () => (
    <div className="w-[350px] rounded-lg border p-6">
      <div className="space-y-1">
        <h3 className="text-lg font-semibold">Settings</h3>
        <p className="text-sm text-muted-foreground">
          Manage your account settings and preferences.
        </p>
      </div>
      <Separator className="my-4" />
      <div className="space-y-4">
        <div className="space-y-2">
          <label className="text-sm font-medium">Username</label>
          <input className="w-full px-3 py-2 border rounded-md" defaultValue="johndoe" />
        </div>
        <div className="space-y-2">
          <label className="text-sm font-medium">Email</label>
          <input className="w-full px-3 py-2 border rounded-md" defaultValue="john@example.com" />
        </div>
      </div>
    </div>
  ),
};

/**
 * Multiple separators
 */
export const Multiple: Story = {
  render: () => (
    <div className="w-[350px] space-y-4">
      <div>Section 1</div>
      <Separator />
      <div>Section 2</div>
      <Separator />
      <div>Section 3</div>
      <Separator />
      <div>Section 4</div>
    </div>
  ),
};
