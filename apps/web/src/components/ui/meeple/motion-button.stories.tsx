import { Download, Heart, Share2 } from 'lucide-react';

import { MotionButton } from './motion-button';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * MotionButton - Animated button wrapper.
 *
 * ## Custom Component
 * Button with Framer Motion animations.
 *
 * ## Features
 * - **Hover scale**: 1.05 scale on hover
 * - **Tap scale**: 0.95 scale on click
 * - **Custom animations**: Override default motion
 * - **Full Button props**: All shadcn/ui Button props
 *
 * ## Use Cases
 * - Call-to-action buttons
 * - Interactive elements
 * - Playful UI interactions
 */
const meta = {
  title: 'MeepleAI/MotionButton',
  component: MotionButton,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Animated button component using Framer Motion. Adds hover and tap scale effects to standard Button.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      description: 'Button variant',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Button size',
    },
  },
} satisfies Meta<typeof MotionButton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default motion button.
 */
export const Default: Story = {
  args: {
    children: 'Click me',
  },
};

/**
 * All variants with animation.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2">
        <MotionButton variant="default">Default</MotionButton>
        <MotionButton variant="destructive">Destructive</MotionButton>
        <MotionButton variant="outline">Outline</MotionButton>
      </div>
      <div className="flex gap-2">
        <MotionButton variant="secondary">Secondary</MotionButton>
        <MotionButton variant="ghost">Ghost</MotionButton>
        <MotionButton variant="link">Link</MotionButton>
      </div>
    </div>
  ),
};

/**
 * With icons.
 */
export const WithIcons: Story = {
  render: () => (
    <div className="flex gap-2">
      <MotionButton>
        <Download className="mr-2 h-4 w-4" />
        Download
      </MotionButton>
      <MotionButton variant="outline">
        <Heart className="mr-2 h-4 w-4" />
        Like
      </MotionButton>
      <MotionButton variant="secondary">
        <Share2 className="mr-2 h-4 w-4" />
        Share
      </MotionButton>
    </div>
  ),
};

/**
 * Icon-only buttons.
 */
export const IconButtons: Story = {
  render: () => (
    <div className="flex gap-2">
      <MotionButton size="icon">
        <Download className="h-4 w-4" />
      </MotionButton>
      <MotionButton size="icon" variant="outline">
        <Heart className="h-4 w-4" />
      </MotionButton>
      <MotionButton size="icon" variant="ghost">
        <Share2 className="h-4 w-4" />
      </MotionButton>
    </div>
  ),
};

/**
 * Custom animation.
 */
export const CustomAnimation: Story = {
  render: () => (
    <div className="flex gap-2">
      <MotionButton whileHover={{ scale: 1.1, rotate: 5 }} whileTap={{ scale: 0.9 }}>
        Rotate on Hover
      </MotionButton>
      <MotionButton
        whileHover={{ scale: 1.05, y: -5 }}
        whileTap={{ scale: 0.95 }}
        variant="outline"
      >
        Lift on Hover
      </MotionButton>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Custom animation overrides for unique effects.',
      },
    },
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="flex gap-2">
      <MotionButton>Default</MotionButton>
      <MotionButton variant="outline">Outline</MotionButton>
      <MotionButton variant="ghost">Ghost</MotionButton>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
