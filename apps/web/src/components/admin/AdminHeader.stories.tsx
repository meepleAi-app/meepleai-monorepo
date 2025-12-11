/**
 * AdminHeader Stories - Issue #881
 *
 * Visual testing for AdminHeader component.
 * Covers: default, with user, with actions.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { AdminHeader } from './AdminHeader';
import { Button } from '@/components/ui/button';
import { BellIcon, MenuIcon } from 'lucide-react';

const meta: Meta<typeof AdminHeader> = {
  title: 'Admin/AdminHeader',
  component: AdminHeader,
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
type Story = StoryObj<typeof AdminHeader>;

/**
 * Default header without user info
 */
export const Default: Story = {
  args: {},
};

/**
 * Header with user info showing initials
 */
export const WithUser: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'John Doe',
      role: 'Admin',
    },
  },
};

/**
 * Header with user email only (no display name)
 */
export const WithEmailOnly: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
    },
  },
};

/**
 * Header with mobile menu trigger
 */
export const WithMobileMenu: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin User',
    },
    mobileMenuTrigger: (
      <Button variant="ghost" size="icon">
        <MenuIcon className="h-5 w-5" />
      </Button>
    ),
  },
};

/**
 * Header with additional actions
 */
export const WithActions: Story = {
  args: {
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'John Doe',
    },
    actions: (
      <Button variant="ghost" size="icon" className="relative">
        <BellIcon className="h-5 w-5" />
        <span className="absolute top-0 right-0 h-2 w-2 bg-red-500 rounded-full" />
      </Button>
    ),
  },
};

/**
 * Header with custom title
 */
export const CustomTitle: Story = {
  args: {
    title: 'MeepleAI Dashboard',
    user: {
      id: '1',
      email: 'admin@meepleai.com',
      displayName: 'Admin',
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
      displayName: 'John Doe',
    },
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark bg-gray-950">
        <Story />
      </div>
    ),
  ],
};
