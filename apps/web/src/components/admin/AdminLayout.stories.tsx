/**
 * AdminLayout Stories - Issue #881
 *
 * Visual testing for AdminLayout component.
 * Covers: full layout, collapsed, badges, responsive.
 */

import { BellIcon, RefreshCwIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { AdminLayout } from './AdminLayout';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AdminLayout> = {
  title: 'Admin/AdminLayout',
  component: AdminLayout,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/admin',
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof AdminLayout>;

const SampleContent = () => (
  <div className="space-y-6">
    <div>
      <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Dashboard Overview</h1>
      <p className="mt-2 text-gray-600 dark:text-gray-400">
        Welcome to the MeepleAI Admin Dashboard.
      </p>
    </div>
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      {[1, 2, 3, 4].map(i => (
        <div
          key={i}
          className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6"
        >
          <div className="text-sm text-gray-500 dark:text-gray-400">Metric {i}</div>
          <div className="mt-2 text-2xl font-bold text-gray-900 dark:text-white">
            {Math.floor(Math.random() * 1000)}
          </div>
        </div>
      ))}
    </div>
    <div className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
      <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Recent Activity</h2>
      <div className="space-y-3">
        {[1, 2, 3].map(i => (
          <div
            key={i}
            className="flex items-center gap-3 py-2 border-b border-gray-100 dark:border-gray-700 last:border-0"
          >
            <div className="w-2 h-2 bg-green-500 rounded-full" />
            <div className="text-sm text-gray-600 dark:text-gray-400">Activity item {i}</div>
          </div>
        ))}
      </div>
    </div>
  </div>
);

/**
 * Default admin layout with sample content
 */
export const Default: Story = {
  args: {
    children: <SampleContent />,
  },
};

/**
 * Layout with user info in header
 */
export const WithUser: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'John Doe',
      role: 'Admin',
    },
    children: <SampleContent />,
  },
};

/**
 * Layout with navigation badges
 */
export const WithBadges: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    badges: {
      '/admin/users': { count: 5 },
      '/admin/prompts': { count: 12, variant: 'secondary' },
      '/admin/cache': { count: 3, variant: 'destructive' },
    },
    children: <SampleContent />,
  },
};

/**
 * Layout with header actions
 */
export const WithHeaderActions: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    headerActions: (
      <>
        <Button variant="ghost" size="icon">
          <RefreshCwIcon className="h-4 w-4" />
        </Button>
        <Button variant="ghost" size="icon" className="relative">
          <BellIcon className="h-4 w-4" />
          <span className="absolute top-0 right-0 h-2 w-2 bg-red-500 rounded-full" />
        </Button>
      </>
    ),
    children: <SampleContent />,
  },
};

/**
 * Layout with custom breadcrumbs
 */
export const WithCustomBreadcrumbs: Story = {
  args: {
    breadcrumbs: [
      { label: 'Admin', href: '/admin' },
      { label: 'Users', href: '/admin/users' },
      { label: 'Edit User' },
    ],
    children: (
      <div className="bg-white dark:bg-gray-800 rounded-lg border p-6">
        <h1 className="text-2xl font-bold">Edit User</h1>
        <p className="mt-2 text-gray-600">User edit form would go here.</p>
      </div>
    ),
  },
};

/**
 * Layout without breadcrumbs
 */
export const WithoutBreadcrumbs: Story = {
  args: {
    showBreadcrumbs: false,
    children: <SampleContent />,
  },
};

/**
 * Users page active
 */
export const UsersPageActive: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    children: (
      <div className="bg-white dark:bg-gray-800 rounded-lg border p-6">
        <h1 className="text-2xl font-bold">Users</h1>
        <p className="mt-2 text-gray-600">User management page.</p>
      </div>
    ),
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
 * Configuration page active
 */
export const ConfigurationPageActive: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    children: (
      <div className="bg-white dark:bg-gray-800 rounded-lg border p-6">
        <h1 className="text-2xl font-bold">Configuration</h1>
        <p className="mt-2 text-gray-600">System configuration page.</p>
      </div>
    ),
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
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    badges: {
      '/admin/users': { count: 5 },
    },
    children: <SampleContent />,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    children: <SampleContent />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet viewport
 */
export const Tablet: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
    },
    children: <SampleContent />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
