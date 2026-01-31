import { Progress } from './progress';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Progress component for displaying task completion and loading states.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Progress with smooth transitions.
 *
 * ## Features
 * - **Value range**: 0-100 percentage-based progress
 * - **Smooth transitions**: Animated progress changes
 * - **Accessible**: ARIA attributes for screen readers
 * - **Customizable**: Size and color variants via className
 *
 * ## Accessibility
 * - ✅ ARIA role="progressbar"
 * - ✅ aria-valuenow, aria-valuemin, aria-valuemax
 * - ✅ Screen reader friendly progress announcements
 * - ✅ Visual progress indication
 */
const meta = {
  title: 'UI/Progress',
  component: Progress,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Displays an indicator showing the completion progress of a task.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: { type: 'range', min: 0, max: 100, step: 1 },
      description: 'Progress value (0-100)',
    },
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Progress>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default progress at 0%.
 * Initial empty state.
 */
export const Default: Story = {
  args: {
    value: 0,
  },
};

/**
 * Progress at 25%.
 * Shows low progress state.
 */
export const TwentyFivePercent: Story = {
  args: {
    value: 25,
  },
};

/**
 * Progress at 50%.
 * Halfway completion state.
 */
export const FiftyPercent: Story = {
  args: {
    value: 50,
  },
};

/**
 * Progress at 75%.
 * Nearly complete state.
 */
export const SeventyFivePercent: Story = {
  args: {
    value: 75,
  },
};

/**
 * Progress at 100%.
 * Completed state.
 */
export const Complete: Story = {
  args: {
    value: 100,
  },
};

/**
 * Progress with label.
 * Shows progress with percentage text.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="space-y-2">
      <div className="flex justify-between text-sm">
        <span className="text-muted-foreground">Uploading...</span>
        <span className="font-medium">65%</span>
      </div>
      <Progress value={65} />
    </div>
  ),
};

/**
 * Multiple progress bars.
 * Shows different tasks with various completion levels.
 */
export const MultipleProgressBars: Story = {
  render: () => (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">Task 1</span>
          <span className="font-medium">100%</span>
        </div>
        <Progress value={100} />
      </div>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">Task 2</span>
          <span className="font-medium">75%</span>
        </div>
        <Progress value={75} />
      </div>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">Task 3</span>
          <span className="font-medium">30%</span>
        </div>
        <Progress value={30} />
      </div>
    </div>
  ),
};

/**
 * Custom colored progress.
 * Shows progress with custom styling.
 */
export const CustomColors: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">Success</p>
        <Progress value={85} className="[&>div]:bg-green-500" />
      </div>
      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">Warning</p>
        <Progress value={60} className="[&>div]:bg-yellow-500" />
      </div>
      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">Danger</p>
        <Progress value={30} className="[&>div]:bg-red-500" />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Custom progress bar colors using className override.',
      },
    },
  },
};

/**
 * Loading states example.
 * Shows progress in a loading context.
 */
export const LoadingExample: Story = {
  render: () => (
    <div className="space-y-4 p-6 border rounded-lg">
      <h3 className="font-semibold">Installing dependencies...</h3>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">react</span>
          <span className="font-medium">45%</span>
        </div>
        <Progress value={45} />
      </div>
      <p className="text-sm text-muted-foreground">This may take a few moments...</p>
    </div>
  ),
};

/**
 * All progress levels.
 * Visual comparison of different progress values.
 */
export const AllLevels: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="space-y-2">
        <span className="text-sm">0%</span>
        <Progress value={0} />
      </div>
      <div className="space-y-2">
        <span className="text-sm">25%</span>
        <Progress value={25} />
      </div>
      <div className="space-y-2">
        <span className="text-sm">50%</span>
        <Progress value={50} />
      </div>
      <div className="space-y-2">
        <span className="text-sm">75%</span>
        <Progress value={75} />
      </div>
      <div className="space-y-2">
        <span className="text-sm">100%</span>
        <Progress value={100} />
      </div>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows progress appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">Processing...</span>
          <span className="font-medium">60%</span>
        </div>
        <Progress value={60} />
      </div>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">Complete</span>
          <span className="font-medium">100%</span>
        </div>
        <Progress value={100} />
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
