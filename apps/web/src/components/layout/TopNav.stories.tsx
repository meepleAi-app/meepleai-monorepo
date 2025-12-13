/**
 * Storybook stories for TopNav component
 * Issue #2053 - User notifications with bell icon
 *
 * Stories:
 * - Default (Home active)
 * - Games Active
 * - Chat Active
 * - Settings Active
 * - With Notifications
 * - Desktop View (1024px)
 * - User Menu Open
 */

import type { Meta, StoryObj } from '@storybook/react';
import { TopNav } from './TopNav';

const meta: Meta<typeof TopNav> = {
  title: 'Layout/TopNav',
  component: TopNav,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/dashboard',
      },
    },
    docs: {
      description: {
        component: `
Desktop top navigation bar with notification bell and user menu.

**Design System**: Playful Boardroom
**Accessibility**: WCAG 2.1 AA compliant
**Responsive**: Visible on desktop (>=768px), hidden on mobile

Features:
- Active state with orange primary color
- Lucide icons with labels
- NotificationBell with unread count badge
- User dropdown menu with logout
- Smooth 200ms transitions
- Keyboard navigation support
        `,
      },
    },
    // Visual regression testing
    chromatic: {
      viewports: [1024, 1440], // Desktop sizes
      delay: 300, // Wait for animations
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="min-h-[200px]">
        <Story />
      </div>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof TopNav>;

/**
 * Default state with Home active.
 * Pathname: /dashboard
 */
export const Default: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/dashboard',
      },
    },
    viewport: {
      defaultViewport: 'desktop', // 1024px
    },
    docs: {
      description: {
        story: 'Default state with Home tab active (pathname: /dashboard)',
      },
    },
  },
};

/**
 * Games tab active.
 * Pathname: /games
 */
export const GamesActive: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/games',
      },
    },
    viewport: {
      defaultViewport: 'desktop',
    },
    docs: {
      description: {
        story: 'Games catalog active state (pathname: /games)',
      },
    },
  },
};

/**
 * Chat tab active.
 * Pathname: /chat
 */
export const ChatActive: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/chat',
      },
    },
    viewport: {
      defaultViewport: 'desktop',
    },
    docs: {
      description: {
        story: 'Chat interface active state (pathname: /chat)',
      },
    },
  },
};

/**
 * Settings tab active.
 * Pathname: /settings
 */
export const SettingsActive: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/settings',
      },
    },
    viewport: {
      defaultViewport: 'desktop',
    },
    docs: {
      description: {
        story: 'Settings page active state (pathname: /settings)',
      },
    },
  },
};

/**
 * Desktop view (1024px width).
 * Shows full top navigation with notification bell.
 */
export const DesktopView: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/dashboard',
      },
    },
    viewport: {
      defaultViewport: 'desktop', // 1024px
    },
    docs: {
      description: {
        story: 'Desktop viewport (1024px) - Navigation fully visible with notification bell',
      },
    },
    chromatic: {
      viewports: [1024],
    },
  },
};

/**
 * Wide desktop view (1440px width).
 * Shows username next to avatar.
 */
export const WideDesktopView: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/dashboard',
      },
    },
    viewport: {
      defaultViewport: 'desktop', // Will be 1440px in Chromatic
    },
    docs: {
      description: {
        story: 'Wide desktop viewport (1440px) - Shows full username next to avatar',
      },
    },
    chromatic: {
      viewports: [1440],
    },
  },
};

/**
 * Mobile view (375px width).
 * Top nav should be hidden (hidden md:flex).
 */
export const MobileView: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/dashboard',
      },
    },
    viewport: {
      defaultViewport: 'mobile1', // 375px
    },
    docs: {
      description: {
        story: 'Mobile viewport (375px) - Navigation hidden (hidden md:flex)',
      },
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Nested route active state.
 * Pathname: /games/catan
 */
export const NestedRouteActive: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/games/catan',
      },
    },
    viewport: {
      defaultViewport: 'desktop',
    },
    docs: {
      description: {
        story: 'Games tab active for nested route (pathname: /games/catan)',
      },
    },
  },
};

/**
 * Root path (/) should activate Home.
 */
export const RootPath: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/',
      },
    },
    viewport: {
      defaultViewport: 'desktop',
    },
    docs: {
      description: {
        story: 'Root path (/) activates Home tab',
      },
    },
  },
};
