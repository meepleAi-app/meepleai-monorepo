/**
 * ResourceTracker Storybook Stories
 * Issue #2406: Game State Editor UI
 *
 * Visual regression tests for Chromatic.
 */

import { fn } from 'storybook/test';

import { ResourceTracker } from './ResourceTracker';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/GameState/ResourceTracker',
  component: ResourceTracker,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Visual resource counter with increment/decrement buttons for tracking game resources, scores, and numeric values.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: 'number',
      description: 'Current value to display',
    },
    editable: {
      control: 'boolean',
      description: 'Whether increment/decrement buttons are shown',
    },
    min: {
      control: 'number',
      description: 'Minimum allowed value',
    },
    max: {
      control: 'number',
      description: 'Maximum allowed value',
    },
    step: {
      control: 'number',
      description: 'Increment/decrement step size',
    },
  },
  args: {
    onChange: fn(),
  },
} satisfies Meta<typeof ResourceTracker>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {
  args: {
    value: 5,
    editable: true,
  },
};

export const ReadOnly: Story = {
  args: {
    value: 10,
    editable: false,
  },
};

export const LargeValue: Story = {
  args: {
    value: 9999,
    editable: true,
  },
};

export const MinValue: Story = {
  args: {
    value: 0,
    editable: true,
    min: 0,
  },
};

export const MaxValue: Story = {
  args: {
    value: 100,
    editable: true,
    max: 100,
  },
};

export const CustomStep: Story = {
  args: {
    value: 50,
    editable: true,
    step: 5,
  },
  parameters: {
    docs: {
      description: {
        story: 'Increment/decrement by 5 instead of 1',
      },
    },
  },
};

// Dark Mode
export const DarkMode: Story = {
  args: {
    value: 42,
    editable: true,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-6 bg-slate-900 rounded-lg">
        <Story />
      </div>
    ),
  ],
};
