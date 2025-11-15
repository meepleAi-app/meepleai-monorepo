import type { Meta, StoryObj } from '@storybook/react';
import { Progress } from './progress';
import { useEffect, useState } from 'react';

/**
 * Progress component displays progress indicator.
 * Based on Radix UI Progress primitive.
 */
const meta = {
  title: 'UI/Progress',
  component: Progress,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: { type: 'range', min: 0, max: 100, step: 1 },
      description: 'Progress value (0-100)',
    },
  },
} satisfies Meta<typeof Progress>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default progress at 50%
 */
export const Default: Story = {
  args: {
    value: 50,
    className: 'w-[60%]',
  },
};

/**
 * Empty progress (0%)
 */
export const Empty: Story = {
  args: {
    value: 0,
    className: 'w-[60%]',
  },
};

/**
 * Full progress (100%)
 */
export const Full: Story = {
  args: {
    value: 100,
    className: 'w-[60%]',
  },
};

/**
 * Progress at 25%
 */
export const Quarter: Story = {
  args: {
    value: 25,
    className: 'w-[60%]',
  },
};

/**
 * Progress at 75%
 */
export const ThreeQuarters: Story = {
  args: {
    value: 75,
    className: 'w-[60%]',
  },
};

/**
 * Animated progress
 */
export const Animated: Story = {
  render: () => {
    const [progress, setProgress] = useState(0);

    useEffect(() => {
      const timer = setInterval(() => {
        setProgress((prev) => {
          if (prev >= 100) return 0;
          return prev + 10;
        });
      }, 500);
      return () => clearInterval(timer);
    }, []);

    return <Progress value={progress} className="w-[60%]" />;
  },
};

/**
 * Progress with label
 */
export const WithLabel: Story = {
  render: () => (
    <div className="w-[60%] space-y-2">
      <div className="flex justify-between text-sm">
        <span>Progress</span>
        <span>60%</span>
      </div>
      <Progress value={60} />
    </div>
  ),
};

/**
 * Multiple progress bars
 */
export const Multiple: Story = {
  render: () => (
    <div className="w-[60%] space-y-4">
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span>Task 1</span>
          <span>100%</span>
        </div>
        <Progress value={100} />
      </div>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span>Task 2</span>
          <span>70%</span>
        </div>
        <Progress value={70} />
      </div>
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span>Task 3</span>
          <span>30%</span>
        </div>
        <Progress value={30} />
      </div>
    </div>
  ),
};

/**
 * Upload progress simulation
 */
export const UploadSimulation: Story = {
  render: () => {
    const [progress, setProgress] = useState(0);

    useEffect(() => {
      const timer = setInterval(() => {
        setProgress((prev) => {
          if (prev >= 100) {
            clearInterval(timer);
            return 100;
          }
          return prev + 2;
        });
      }, 100);
      return () => clearInterval(timer);
    }, []);

    return (
      <div className="w-[60%] space-y-2">
        <div className="flex justify-between text-sm">
          <span>Uploading file.pdf</span>
          <span>{progress}%</span>
        </div>
        <Progress value={progress} />
        {progress === 100 && (
          <p className="text-sm text-muted-foreground">Upload complete!</p>
        )}
      </div>
    );
  },
};
