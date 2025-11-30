/**
 * Storybook stories for BottomNav component
 * Issue #1829 [UI-002] BottomNav Component (Mobile-First)
 *
 * Stories:
 * - Default (Home active)
 * - Games Active
 * - Chat Active
 * - Settings Active
 * - Profile Active
 * - Mobile View (375px)
 * - Tablet View (768px - should be hidden)
 */

import type { Meta, StoryObj } from '@storybook/react';
import { BottomNav } from './BottomNav';

// Mock Next.js navigation hooks
const mockNavigationDecorator = (pathname: string) => (Story: any) => {
  // Mock usePathname
  jest.mock('next/navigation', () => ({
    usePathname: () => pathname,
  }));
  return <Story />;
};

const meta: Meta<typeof BottomNav> = {
  title: 'Layout/BottomNav',
  component: BottomNav,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component: `
Mobile-first bottom navigation with 5 primary app sections.

**Design System**: Playful Boardroom
**Accessibility**: WCAG 2.1 AA compliant (44x44px touch targets)
**Responsive**: Hidden on desktop (≥768px)

Features:
- Active state with orange primary color
- Lucide icons with labels
- Smooth 200ms transitions
- Keyboard navigation support
        `,
      },
    },
    // Visual regression testing
    chromatic: {
      viewports: [375, 768], // Mobile + Tablet
      delay: 300, // Wait for animations
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof BottomNav>;

/**
 * Default state with Home active.
 * Pathname: /dashboard
 */
export const Default: Story = {
  decorators: [mockNavigationDecorator('/dashboard')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1', // 375px
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
  decorators: [mockNavigationDecorator('/games')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
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
  decorators: [mockNavigationDecorator('/chat')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
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
  decorators: [mockNavigationDecorator('/settings')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Settings page active state (pathname: /settings)',
      },
    },
  },
};

/**
 * Profile tab active.
 * Pathname: /profile
 */
export const ProfileActive: Story = {
  decorators: [mockNavigationDecorator('/profile')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'User profile active state (pathname: /profile)',
      },
    },
  },
};

/**
 * Mobile view (375px width).
 * Shows full bottom navigation.
 */
export const MobileView: Story = {
  decorators: [mockNavigationDecorator('/dashboard')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1', // 375px
    },
    docs: {
      description: {
        story: 'Mobile viewport (375px) - Navigation fully visible',
      },
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet view (768px width).
 * Bottom nav should be hidden (md:hidden).
 */
export const TabletView: Story = {
  decorators: [mockNavigationDecorator('/dashboard')],
  parameters: {
    viewport: {
      defaultViewport: 'tablet', // 768px
    },
    docs: {
      description: {
        story: 'Tablet viewport (768px) - Navigation hidden (md:hidden)',
      },
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Nested route active state.
 * Pathname: /games/catan
 */
export const NestedRouteActive: Story = {
  decorators: [mockNavigationDecorator('/games/catan')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
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
  decorators: [mockNavigationDecorator('/')],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Root path (/) activates Home tab',
      },
    },
  },
};

/**
 * All tabs in a grid (visual reference).
 * Shows all 5 states side-by-side for design review.
 */
export const AllStates: Story = {
  render: () => (
    <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-8 p-8 bg-background">
      <div className="space-y-4">
        <h3 className="font-semibold text-sm">Home Active</h3>
        <div className="relative h-[400px] border border-border rounded-lg overflow-hidden">
          <BottomNav />
        </div>
      </div>
      <div className="space-y-4">
        <h3 className="font-semibold text-sm">Games Active</h3>
        <div className="relative h-[400px] border border-border rounded-lg overflow-hidden">
          <BottomNav />
        </div>
      </div>
      <div className="space-y-4">
        <h3 className="font-semibold text-sm">Chat Active</h3>
        <div className="relative h-[400px] border border-border rounded-lg overflow-hidden">
          <BottomNav />
        </div>
      </div>
      <div className="space-y-4">
        <h3 className="font-semibold text-sm">Settings Active</h3>
        <div className="relative h-[400px] border border-border rounded-lg overflow-hidden">
          <BottomNav />
        </div>
      </div>
      <div className="space-y-4">
        <h3 className="font-semibold text-sm">Profile Active</h3>
        <div className="relative h-[400px] border border-border rounded-lg overflow-hidden">
          <BottomNav />
        </div>
      </div>
    </div>
  ),
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        story: 'Visual reference showing all 5 tab states side-by-side',
      },
    },
  },
};
