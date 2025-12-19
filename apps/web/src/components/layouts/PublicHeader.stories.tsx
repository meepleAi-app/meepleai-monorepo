/**
 * PublicHeader Storybook Stories - Issue #2230
 *
 * Showcases different states of the PublicHeader component.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { PublicHeader } from './PublicHeader';
import { fn } from 'storybook/test';

const meta = {
  title: 'Components/Layouts/PublicHeader',
  component: PublicHeader,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Header component for public pages with responsive navigation, theme switcher, and user menu.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    user: {
      description: 'Current user object (undefined if not authenticated)',
      control: 'object',
    },
    onLogout: {
      description: 'Callback function when user logs out',
      action: 'logout',
    },
    className: {
      description: 'Additional CSS classes',
      control: 'text',
    },
  },
  args: {
    onLogout: fn(),
  },
} satisfies Meta<typeof PublicHeader>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state - no user authenticated
 */
export const Default: Story = {
  args: {},
};

/**
 * Authenticated user with all features
 */
export const Authenticated: Story = {
  args: {
    user: {
      name: 'Mario Rossi',
      email: 'mario.rossi@example.com',
      avatar: 'https://i.pravatar.cc/150?img=12',
    },
  },
};

/**
 * Authenticated user without avatar
 */
export const AuthenticatedNoAvatar: Story = {
  args: {
    user: {
      name: 'Luigi Verdi',
      email: 'luigi.verdi@example.com',
    },
  },
};

/**
 * Mobile view - open the mobile menu to see
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
  args: {
    user: {
      name: 'Anna Bianchi',
      email: 'anna.bianchi@example.com',
      avatar: 'https://i.pravatar.cc/150?img=5',
    },
  },
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
    className: 'border-b-4 border-primary',
    user: {
      name: 'Test User',
      email: 'test@example.com',
    },
  },
};

/**
 * Long user name to test overflow
 */
export const LongUserName: Story = {
  args: {
    user: {
      name: 'Giovanni Francesco Alessandro della Montagna',
      email: 'giovanni.francesco.alessandro@example.com',
      avatar: 'https://i.pravatar.cc/150?img=8',
    },
  },
};
