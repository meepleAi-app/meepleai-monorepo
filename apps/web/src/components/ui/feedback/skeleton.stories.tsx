import { Skeleton } from './skeleton';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Skeleton component for loading placeholders and content shimmer.
 *
 * ## shadcn/ui Component
 * Simple animated placeholder for loading states.
 *
 * ## Features
 * - **Pulse animation**: Smooth loading shimmer effect
 * - **Flexible sizing**: Any width/height via className
 * - **Rounded corners**: Default rounded-md styling
 * - **Composable**: Build complex skeleton layouts
 *
 * ## Accessibility
 * - ✅ Visual loading indicator
 * - ✅ Non-interactive element
 * - ✅ Should be paired with aria-busy="true" on parent
 * - ✅ Screen reader friendly when properly labeled
 */
const meta = {
  title: 'UI/Skeleton',
  component: Skeleton,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Use to show a placeholder while content is loading.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    className: {
      control: 'text',
      description: 'Custom className for sizing and styling',
    },
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Skeleton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default skeleton.
 * Basic skeleton placeholder.
 */
export const Default: Story = {
  args: {
    className: 'h-12 w-full',
  },
};

/**
 * Text line skeleton.
 * Simulates a single line of text.
 */
export const TextLine: Story = {
  render: () => <Skeleton className="h-4 w-full" />,
};

/**
 * Heading skeleton.
 * Simulates a heading.
 */
export const Heading: Story = {
  render: () => <Skeleton className="h-8 w-3/4" />,
};

/**
 * Avatar skeleton.
 * Circular skeleton for profile images.
 */
export const Avatar: Story = {
  render: () => <Skeleton className="h-12 w-12 rounded-full" />,
};

/**
 * Button skeleton.
 * Simulates a button.
 */
export const Button: Story = {
  render: () => <Skeleton className="h-10 w-24" />,
};

/**
 * Card skeleton.
 * Complete card loading state.
 */
export const Card: Story = {
  render: () => (
    <div className="flex flex-col space-y-3">
      <Skeleton className="h-[125px] w-full rounded-xl" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-4/5" />
      </div>
    </div>
  ),
};

/**
 * List item skeleton.
 * Simulates a list item with avatar and text.
 */
export const ListItem: Story = {
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
 * Multiple list items.
 * Simulates a loading list.
 */
export const ListSkeleton: Story = {
  render: () => (
    <div className="space-y-4">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex items-center space-x-4">
          <Skeleton className="h-12 w-12 rounded-full" />
          <div className="space-y-2 flex-1">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-4/5" />
          </div>
        </div>
      ))}
    </div>
  ),
};

/**
 * Article skeleton.
 * Simulates an article with heading and paragraphs.
 */
export const Article: Story = {
  render: () => (
    <div className="space-y-4">
      <Skeleton className="h-8 w-3/4" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-5/6" />
      <Skeleton className="h-4 w-full mt-4" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-4/5" />
    </div>
  ),
};

/**
 * Profile skeleton.
 * Simulates a user profile.
 */
export const Profile: Story = {
  render: () => (
    <div className="flex flex-col items-center space-y-4">
      <Skeleton className="h-24 w-24 rounded-full" />
      <div className="space-y-2 text-center w-full">
        <Skeleton className="h-6 w-48 mx-auto" />
        <Skeleton className="h-4 w-64 mx-auto" />
      </div>
      <div className="flex gap-4 w-full justify-center">
        <Skeleton className="h-10 w-24" />
        <Skeleton className="h-10 w-24" />
      </div>
    </div>
  ),
};

/**
 * Table skeleton.
 * Simulates a data table.
 */
export const Table: Story = {
  render: () => (
    <div className="space-y-2">
      <div className="flex gap-4">
        <Skeleton className="h-10 w-1/4" />
        <Skeleton className="h-10 w-1/4" />
        <Skeleton className="h-10 w-1/4" />
        <Skeleton className="h-10 w-1/4" />
      </div>
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex gap-4">
          <Skeleton className="h-8 w-1/4" />
          <Skeleton className="h-8 w-1/4" />
          <Skeleton className="h-8 w-1/4" />
          <Skeleton className="h-8 w-1/4" />
        </div>
      ))}
    </div>
  ),
};

/**
 * Dashboard skeleton.
 * Complex loading layout with multiple skeleton types.
 */
export const Dashboard: Story = {
  render: () => (
    <div className="space-y-6">
      <div className="grid grid-cols-3 gap-4">
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-24 w-full rounded-lg" />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-3">
          <Skeleton className="h-[200px] w-full rounded-lg" />
          <Skeleton className="h-4 w-3/4" />
        </div>
        <div className="space-y-3">
          <Skeleton className="h-[200px] w-full rounded-lg" />
          <Skeleton className="h-4 w-3/4" />
        </div>
      </div>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows skeleton appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center space-x-4">
        <Skeleton className="h-12 w-12 rounded-full" />
        <div className="space-y-2 flex-1">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-4/5" />
        </div>
      </div>
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-3/4" />
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
