/**
 * AdminBreadcrumbs Stories - Issue #881
 *
 * Visual testing for AdminBreadcrumbs component.
 * Covers: auto-generated, custom items, various paths.
 */

import { AdminBreadcrumbs } from './AdminBreadcrumbs';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AdminBreadcrumbs> = {
  title: 'Admin/AdminBreadcrumbs',
  component: AdminBreadcrumbs,
  parameters: {
    layout: 'padded',
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
type Story = StoryObj<typeof AdminBreadcrumbs>;

/**
 * Auto-generated breadcrumbs for admin root
 */
export const AdminRoot: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin',
      },
    },
  },
};

/**
 * Auto-generated breadcrumbs for admin users page
 */
export const AdminUsers: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/users',
      },
    },
  },
};

/**
 * Auto-generated breadcrumbs for configuration page
 */
export const AdminConfiguration: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/configuration',
      },
    },
  },
};

/**
 * Auto-generated breadcrumbs for N8N templates
 */
export const AdminN8nTemplates: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/n8n-templates',
      },
    },
  },
};

/**
 * Auto-generated breadcrumbs for deep path
 */
export const DeepPath: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/prompts/123/audit',
      },
    },
  },
};

/**
 * Custom breadcrumb items
 */
export const CustomItems: Story = {
  args: {
    items: [
      { label: 'Admin', href: '/admin' },
      { label: 'Users', href: '/admin/users' },
      { label: 'Edit User' },
    ],
  },
};

/**
 * Without home icon
 */
export const WithoutHomeIcon: Story = {
  args: {
    showHomeIcon: false,
    items: [{ label: 'Admin', href: '/admin' }, { label: 'Settings' }],
  },
};

/**
 * Single item (current page only)
 */
export const SingleItem: Story = {
  args: {
    items: [{ label: 'Dashboard' }],
  },
};

/**
 * Long path with many segments
 */
export const LongPath: Story = {
  args: {
    items: [
      { label: 'Admin', href: '/admin' },
      { label: 'Configuration', href: '/admin/configuration' },
      { label: 'Feature Flags', href: '/admin/configuration/feature-flags' },
      { label: 'Edit Flag' },
    ],
  },
};

/**
 * Dark mode variant
 */
export const DarkMode: Story = {
  args: {
    items: [
      { label: 'Admin', href: '/admin' },
      { label: 'Users', href: '/admin/users' },
      { label: 'Details' },
    ],
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark bg-gray-950 p-4">
        <Story />
      </div>
    ),
  ],
};
