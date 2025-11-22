import type { Meta, StoryObj } from '@storybook/react';
import { Badge } from './badge';

/**
 * Badge component displays small count and labeling UI elements.
 * Supports multiple variants for different contexts.
 */
const meta = {
  title: 'UI/Badge',
  component: Badge,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'secondary', 'destructive', 'outline'],
      description: 'Visual style variant of the badge',
    },
  },
} satisfies Meta<typeof Badge>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default badge variant
 */
export const Default: Story = {
  args: {
    children: 'Badge',
  },
};

/**
 * Secondary badge variant
 */
export const Secondary: Story = {
  args: {
    variant: 'secondary',
    children: 'Secondary',
  },
};

/**
 * Destructive badge variant for errors or warnings
 */
export const Destructive: Story = {
  args: {
    variant: 'destructive',
    children: 'Destructive',
  },
};

/**
 * Outline badge variant
 */
export const Outline: Story = {
  args: {
    variant: 'outline',
    children: 'Outline',
  },
};

/**
 * Badge with a number
 */
export const WithNumber: Story = {
  args: {
    children: '99+',
  },
};

/**
 * Multiple badges in a row
 */
export const Group: Story = {
  render: () => (
    <div className="flex gap-2">
      <Badge>Default</Badge>
      <Badge variant="secondary">Secondary</Badge>
      <Badge variant="destructive">Destructive</Badge>
      <Badge variant="outline">Outline</Badge>
    </div>
  ),
};

/**
 * Badge with status indicators
 */
export const StatusIndicators: Story = {
  render: () => (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2 items-center">
        <Badge variant="default">Active</Badge>
        <span className="text-sm">User is active</span>
      </div>
      <div className="flex gap-2 items-center">
        <Badge variant="secondary">Pending</Badge>
        <span className="text-sm">Request pending</span>
      </div>
      <div className="flex gap-2 items-center">
        <Badge variant="destructive">Error</Badge>
        <span className="text-sm">Action failed</span>
      </div>
    </div>
  ),
};
