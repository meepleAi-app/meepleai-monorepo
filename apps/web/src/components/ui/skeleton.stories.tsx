import type { Meta, StoryObj } from '@storybook/react';
import { Skeleton } from './skeleton';

/**
 * Skeleton component displays placeholder loading states.
 * Useful for indicating content that is loading.
 */
const meta = {
  title: 'UI/Skeleton',
  component: Skeleton,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Skeleton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default skeleton
 */
export const Default: Story = {
  args: {
    className: 'w-[100px] h-[20px]',
  },
};

/**
 * Circular skeleton (avatar)
 */
export const Circle: Story = {
  args: {
    className: 'w-12 h-12 rounded-full',
  },
};

/**
 * Rectangle skeleton
 */
export const Rectangle: Story = {
  args: {
    className: 'w-[200px] h-[100px]',
  },
};

/**
 * Text line skeleton
 */
export const TextLine: Story = {
  args: {
    className: 'w-[250px] h-4',
  },
};

/**
 * Card skeleton
 */
export const CardSkeleton: Story = {
  render: () => (
    <div className="w-[350px] space-y-3">
      <Skeleton className="h-[200px] w-full rounded-xl" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-[250px]" />
        <Skeleton className="h-4 w-[200px]" />
      </div>
    </div>
  ),
};

/**
 * Profile skeleton
 */
export const ProfileSkeleton: Story = {
  render: () => (
    <div className="flex items-center space-x-4">
      <Skeleton className="h-12 w-12 rounded-full" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-[250px]" />
        <Skeleton className="h-4 w-[200px]" />
      </div>
    </div>
  ),
};

/**
 * List skeleton
 */
export const ListSkeleton: Story = {
  render: () => (
    <div className="w-[350px] space-y-3">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex items-center space-x-4">
          <Skeleton className="h-10 w-10 rounded-full" />
          <div className="space-y-2 flex-1">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-3 w-3/4" />
          </div>
        </div>
      ))}
    </div>
  ),
};

/**
 * Article skeleton
 */
export const ArticleSkeleton: Story = {
  render: () => (
    <div className="w-[500px] space-y-4">
      <Skeleton className="h-8 w-3/4" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-4/5" />
      </div>
      <Skeleton className="h-[200px] w-full" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-3/4" />
      </div>
    </div>
  ),
};

/**
 * Table skeleton
 */
export const TableSkeleton: Story = {
  render: () => (
    <div className="w-[600px] space-y-3">
      <div className="grid grid-cols-4 gap-4">
        <Skeleton className="h-4" />
        <Skeleton className="h-4" />
        <Skeleton className="h-4" />
        <Skeleton className="h-4" />
      </div>
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="grid grid-cols-4 gap-4">
          <Skeleton className="h-8" />
          <Skeleton className="h-8" />
          <Skeleton className="h-8" />
          <Skeleton className="h-8" />
        </div>
      ))}
    </div>
  ),
};

/**
 * Dashboard skeleton
 */
export const DashboardSkeleton: Story = {
  render: () => (
    <div className="w-[700px] space-y-4">
      <div className="grid grid-cols-3 gap-4">
        <div className="space-y-2 p-4 border rounded-lg">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-8 w-full" />
        </div>
        <div className="space-y-2 p-4 border rounded-lg">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-8 w-full" />
        </div>
        <div className="space-y-2 p-4 border rounded-lg">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-8 w-full" />
        </div>
      </div>
      <Skeleton className="h-[300px] w-full rounded-lg" />
    </div>
  ),
};
