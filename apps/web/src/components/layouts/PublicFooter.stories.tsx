/**
 * PublicFooter Storybook Stories - Issue #2230
 *
 * Showcases different states of the PublicFooter component.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { PublicFooter } from './PublicFooter';

const meta = {
  title: 'Components/Layouts/PublicFooter',
  component: PublicFooter,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Footer component for public pages with 3-column layout, social links, and responsive design.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    showNewsletter: {
      description: 'Show newsletter subscription section',
      control: 'boolean',
    },
    className: {
      description: 'Additional CSS classes',
      control: 'text',
    },
  },
} satisfies Meta<typeof PublicFooter>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default footer state
 */
export const Default: Story = {
  args: {},
};

/**
 * Footer with newsletter section (when implemented)
 */
export const WithNewsletter: Story = {
  args: {
    showNewsletter: true,
  },
};

/**
 * Mobile view - columns stack vertically
 */
export const Mobile: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet view
 */
export const Tablet: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Custom styling example
 */
export const CustomStyling: Story = {
  args: {
    className: 'bg-slate-100 dark:bg-slate-900',
  },
};

/**
 * Footer in dark mode context
 */
export const DarkMode: Story = {
  args: {},
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
};
