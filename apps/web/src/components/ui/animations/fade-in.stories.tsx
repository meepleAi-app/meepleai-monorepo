import { FadeIn } from './FadeIn';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * FadeIn animation component using Framer Motion.
 *
 * ## Features
 * - **Direction support**: Fade from up, down, left, right, or none
 * - **Customizable timing**: Delay and duration control
 * - **Distance control**: Adjustable slide distance
 * - **Accessibility**: Respects prefers-reduced-motion
 *
 * ## Use Cases
 * - Page content reveals
 * - Card animations
 * - Hero section intros
 */
const meta = {
  title: 'Animations/FadeIn',
  component: FadeIn,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Fade-in animation wrapper using Framer Motion. Supports directional slides and respects motion preferences.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    delay: {
      control: { type: 'number', min: 0, max: 2, step: 0.1 },
      description: 'Delay before animation starts (seconds)',
    },
    duration: {
      control: { type: 'number', min: 0.1, max: 2, step: 0.1 },
      description: 'Animation duration (seconds)',
    },
    direction: {
      control: 'select',
      options: ['up', 'down', 'left', 'right', 'none'],
      description: 'Direction to slide in from',
    },
    distance: {
      control: { type: 'number', min: 0, max: 100, step: 5 },
      description: 'Distance to slide (pixels)',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-96 h-64 flex items-center justify-center">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof FadeIn>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default fade-in without direction.
 */
export const Default: Story = {
  render: () => (
    <FadeIn>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">Fade In</h3>
        <p className="text-sm text-muted-foreground">
          Simple fade-in animation without directional slide.
        </p>
      </div>
    </FadeIn>
  ),
};

/**
 * Fade in from bottom (most common).
 */
export const FromBottom: Story = {
  args: {
    direction: 'up',
    duration: 0.6,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">From Bottom</h3>
        <p className="text-sm text-muted-foreground">Slides up while fading in.</p>
      </div>
    </FadeIn>
  ),
};

/**
 * Fade in from top.
 */
export const FromTop: Story = {
  args: {
    direction: 'down',
    duration: 0.6,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">From Top</h3>
        <p className="text-sm text-muted-foreground">Slides down while fading in.</p>
      </div>
    </FadeIn>
  ),
};

/**
 * Fade in from left.
 */
export const FromLeft: Story = {
  args: {
    direction: 'right',
    duration: 0.6,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">From Left</h3>
        <p className="text-sm text-muted-foreground">Slides right while fading in.</p>
      </div>
    </FadeIn>
  ),
};

/**
 * Fade in from right.
 */
export const FromRight: Story = {
  args: {
    direction: 'left',
    duration: 0.6,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">From Right</h3>
        <p className="text-sm text-muted-foreground">Slides left while fading in.</p>
      </div>
    </FadeIn>
  ),
};

/**
 * With custom delay.
 */
export const WithDelay: Story = {
  args: {
    delay: 1,
    duration: 0.8,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">Delayed</h3>
        <p className="text-sm text-muted-foreground">Waits 1 second before animating.</p>
      </div>
    </FadeIn>
  ),
};

/**
 * Long distance slide.
 */
export const LongDistance: Story = {
  args: {
    direction: 'up',
    distance: 80,
    duration: 0.8,
  },
  render: (args) => (
    <FadeIn {...args}>
      <div className="p-6 bg-card border rounded-lg">
        <h3 className="font-semibold mb-2">Long Distance</h3>
        <p className="text-sm text-muted-foreground">Slides 80px with slow duration.</p>
      </div>
    </FadeIn>
  ),
};
