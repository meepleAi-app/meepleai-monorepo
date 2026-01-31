import { StaggerChildren } from './StaggerChildren';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * StaggerChildren animation for sequential reveals.
 *
 * ## Features
 * - **Sequential animation**: Children appear one after another
 * - **Customizable timing**: Control stagger delay and duration
 * - **Initial delay**: Wait before starting sequence
 *
 * ## Use Cases
 * - List item reveals
 * - Grid animations
 * - Feature showcases
 */
const meta = {
  title: 'Animations/StaggerChildren',
  component: StaggerChildren,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Staggered animation wrapper that reveals children sequentially. Perfect for lists and grids.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    staggerDelay: {
      control: { type: 'number', min: 0, max: 1, step: 0.05 },
      description: 'Delay between each child (seconds)',
    },
    initialDelay: {
      control: { type: 'number', min: 0, max: 2, step: 0.1 },
      description: 'Initial delay before first child (seconds)',
    },
    duration: {
      control: { type: 'number', min: 0.1, max: 2, step: 0.1 },
      description: 'Animation duration for each child (seconds)',
    },
  },
} satisfies Meta<typeof StaggerChildren>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default stagger with 0.1s delay.
 */
export const Default: Story = {
  render: () => (
    <StaggerChildren>
      <div className="p-4 bg-card border rounded-lg mb-2">Item 1</div>
      <div className="p-4 bg-card border rounded-lg mb-2">Item 2</div>
      <div className="p-4 bg-card border rounded-lg mb-2">Item 3</div>
      <div className="p-4 bg-card border rounded-lg mb-2">Item 4</div>
    </StaggerChildren>
  ),
};

/**
 * Fast stagger (0.05s delay).
 */
export const FastStagger: Story = {
  args: {
    staggerDelay: 0.05,
    duration: 0.3,
  },
  render: (args) => (
    <StaggerChildren {...args}>
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="p-3 bg-card border rounded-lg mb-2">
          Fast Item {i + 1}
        </div>
      ))}
    </StaggerChildren>
  ),
};

/**
 * Slow stagger (0.3s delay).
 */
export const SlowStagger: Story = {
  args: {
    staggerDelay: 0.3,
    duration: 0.8,
  },
  render: (args) => (
    <StaggerChildren {...args}>
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="p-4 bg-card border rounded-lg mb-2">
          Slow Item {i + 1}
        </div>
      ))}
    </StaggerChildren>
  ),
};

/**
 * Card grid example.
 */
export const CardGrid: Story = {
  args: {
    staggerDelay: 0.1,
    duration: 0.5,
  },
  render: (args) => (
    <StaggerChildren {...args} className="grid grid-cols-2 gap-4 w-96">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="p-6 bg-card border rounded-lg text-center">
          <div className="text-2xl mb-2">🎲</div>
          <div className="font-medium">Feature {i + 1}</div>
        </div>
      ))}
    </StaggerChildren>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Staggered reveal for card grid layouts.',
      },
    },
  },
};

/**
 * Feature list example.
 */
export const FeatureList: Story = {
  args: {
    staggerDelay: 0.15,
    duration: 0.6,
  },
  render: (args) => (
    <div className="w-96">
      <h3 className="text-xl font-bold mb-4">Features</h3>
      <StaggerChildren {...args}>
        <div className="flex items-start gap-3 mb-3">
          <div className="w-8 h-8 bg-primary rounded-full flex items-center justify-center text-primary-foreground">
            1
          </div>
          <div>
            <h4 className="font-medium">Fast Performance</h4>
            <p className="text-sm text-muted-foreground">Optimized for speed</p>
          </div>
        </div>
        <div className="flex items-start gap-3 mb-3">
          <div className="w-8 h-8 bg-primary rounded-full flex items-center justify-center text-primary-foreground">
            2
          </div>
          <div>
            <h4 className="font-medium">Easy to Use</h4>
            <p className="text-sm text-muted-foreground">Simple API</p>
          </div>
        </div>
        <div className="flex items-start gap-3 mb-3">
          <div className="w-8 h-8 bg-primary rounded-full flex items-center justify-center text-primary-foreground">
            3
          </div>
          <div>
            <h4 className="font-medium">Fully Accessible</h4>
            <p className="text-sm text-muted-foreground">WCAG 2.1 compliant</p>
          </div>
        </div>
      </StaggerChildren>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Feature list with staggered item reveals.',
      },
    },
  },
};
