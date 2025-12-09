/**
 * AdminSidebar Stories - Issue #881
 *
 * Visual testing for AdminSidebar component.
 * Covers: expanded, collapsed, badges, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { AdminSidebar } from './AdminSidebar';

const meta: Meta<typeof AdminSidebar> = {
  title: 'Admin/AdminSidebar',
  component: AdminSidebar,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/admin',
      },
    },
  },
  decorators: [
    Story => (
      <div className="h-screen flex">
        <Story />
      </div>
    ),
  ],
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof AdminSidebar>;

/**
 * Default expanded sidebar with no badges
 */
export const Default: Story = {
  args: {
    collapsed: false,
  },
};

/**
 * Collapsed sidebar showing only icons
 */
export const Collapsed: Story = {
  args: {
    collapsed: true,
  },
};

/**
 * Sidebar with badge counts on navigation items
 */
export const WithBadges: Story = {
  args: {
    collapsed: false,
    badges: {
      '/admin/users': { count: 5, variant: 'default' },
      '/admin/prompts': { count: 12, variant: 'secondary' },
      '/admin/cache': { count: 3, variant: 'destructive' },
    },
  },
};

/**
 * Collapsed sidebar with badges (shown in tooltips)
 */
export const CollapsedWithBadges: Story = {
  args: {
    collapsed: true,
    badges: {
      '/admin/users': { count: 5, variant: 'default' },
      '/admin/prompts': { count: 99, variant: 'secondary' },
      '/admin/cache': { count: 150, variant: 'destructive' },
    },
  },
};

/**
 * Active on Dashboard page
 */
export const ActiveDashboard: Story = {
  args: {
    collapsed: false,
  },
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin',
      },
    },
  },
};

/**
 * Active on Users page
 */
export const ActiveUsers: Story = {
  args: {
    collapsed: false,
  },
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/users',
      },
    },
  },
};

/**
 * Active on Configuration page
 */
export const ActiveConfiguration: Story = {
  args: {
    collapsed: false,
  },
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/configuration',
      },
    },
  },
};

/**
 * Dark mode variant
 */
export const DarkMode: Story = {
  args: {
    collapsed: false,
    badges: {
      '/admin/users': { count: 5, variant: 'default' },
    },
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark h-screen flex bg-gray-950">
        <Story />
      </div>
    ),
  ],
};
