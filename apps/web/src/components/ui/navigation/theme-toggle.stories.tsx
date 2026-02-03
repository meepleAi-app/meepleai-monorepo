import { ThemeToggle } from './ThemeToggle';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * ThemeToggle component for dark/light mode switching.
 *
 * ## Custom Component
 * Uses next-themes with sun/moon icons.
 *
 * ## Features
 * - **Visual feedback**: Sun/Moon icons with colors
 * - **Accessible**: ARIA labels, keyboard support
 * - **SSR-safe**: Handles hydration correctly
 * - **Flexible**: Icon-only or with label
 *
 * ## Accessibility
 * - ✅ ARIA labels for screen readers
 * - ✅ Keyboard accessible
 * - ✅ Visual state indicators
 */
const meta = {
  title: 'UI/ThemeToggle',
  component: ThemeToggle,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A theme toggle button for switching between light and dark modes. Uses next-themes for theme management.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    showLabel: {
      control: 'boolean',
      description: 'Show text label next to icon',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    size: {
      control: 'select',
      options: ['sm', 'md', 'lg'],
      description: 'Button size',
      table: {
        type: { summary: '"sm" | "md" | "lg"' },
        defaultValue: { summary: 'md' },
      },
    },
  },
} satisfies Meta<typeof ThemeToggle>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default icon-only toggle.
 */
export const Default: Story = {
  args: {},
};

/**
 * With label text.
 */
export const WithLabel: Story = {
  args: {
    showLabel: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Theme toggle with text label showing current theme.',
      },
    },
  },
};

/**
 * Small size variant.
 */
export const Small: Story = {
  args: {
    size: 'sm',
  },
};

/**
 * Large size variant.
 */
export const Large: Story = {
  args: {
    size: 'lg',
  },
};

/**
 * In toolbar context.
 */
export const InToolbar: Story = {
  render: () => (
    <div className="flex items-center gap-2 rounded-md border p-2">
      <ThemeToggle />
      <div className="h-4 w-px bg-border" />
      <span className="text-sm text-muted-foreground">Other actions</span>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Theme toggle in a toolbar layout.',
      },
    },
  },
};
