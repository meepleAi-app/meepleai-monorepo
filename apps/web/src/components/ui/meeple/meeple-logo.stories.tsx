import { MeepleLogo } from './meeple-logo';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * MeepleLogo - Brand identity component.
 *
 * ## Custom MeepleAI Component
 * SVG logo with animated meeple character.
 *
 * ## Variants
 * - **full**: Icon + wordmark
 * - **icon**: Meeple character only
 * - **wordmark**: Text only
 *
 * ## Features
 * - **Sizes**: sm, md, lg, xl
 * - **Animation**: Optional bounce effect
 * - **Hover**: Wiggle interaction
 * - **Theme**: Adapts to light/dark mode
 */
const meta = {
  title: 'MeepleAI/Logo',
  component: MeepleLogo,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'MeepleAI brand logo with animated meeple character. Supports multiple variants and sizes.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['full', 'icon', 'wordmark'],
      description: 'Logo variant',
      table: {
        defaultValue: { summary: 'full' },
      },
    },
    size: {
      control: 'select',
      options: ['sm', 'md', 'lg', 'xl'],
      description: 'Logo size',
      table: {
        defaultValue: { summary: 'md' },
      },
    },
    animated: {
      control: 'boolean',
      description: 'Enable bounce animation',
      table: {
        defaultValue: { summary: 'false' },
      },
    },
  },
} satisfies Meta<typeof MeepleLogo>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Full logo (default).
 */
export const Full: Story = {
  args: {
    variant: 'full',
    size: 'md',
  },
};

/**
 * Icon only.
 */
export const IconOnly: Story = {
  args: {
    variant: 'icon',
    size: 'md',
  },
};

/**
 * Wordmark only.
 */
export const WordmarkOnly: Story = {
  args: {
    variant: 'wordmark',
    size: 'md',
  },
};

/**
 * All sizes comparison.
 */
export const AllSizes: Story = {
  render: () => (
    <div className="space-y-6">
      <MeepleLogo variant="full" size="sm" />
      <MeepleLogo variant="full" size="md" />
      <MeepleLogo variant="full" size="lg" />
      <MeepleLogo variant="full" size="xl" />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Logo sizes from small to extra large.',
      },
    },
  },
};

/**
 * Animated logo.
 */
export const Animated: Story = {
  args: {
    variant: 'full',
    size: 'lg',
    animated: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Logo with bounce animation enabled.',
      },
    },
  },
};

/**
 * Icon variants by size.
 */
export const IconSizes: Story = {
  render: () => (
    <div className="flex items-end gap-4">
      <MeepleLogo variant="icon" size="sm" />
      <MeepleLogo variant="icon" size="md" />
      <MeepleLogo variant="icon" size="lg" />
      <MeepleLogo variant="icon" size="xl" />
    </div>
  ),
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  args: {
    variant: 'full',
    size: 'lg',
  },
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
