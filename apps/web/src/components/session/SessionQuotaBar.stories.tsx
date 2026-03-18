/**
 * SessionQuotaBar Component Stories (Issue #3075)
 *
 * Visual documentation and testing for session quota status display.
 * Demonstrates different quota states: normal, near limit, at limit, unlimited.
 */

import { SessionQuotaBar } from './SessionQuotaBar';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * SessionQuotaBar displays session quota usage with visual progress bar and tier information.
 *
 * ## Features
 * - Visual progress bar showing quota usage
 * - Color-coded warnings (yellow ≥80%, red at limit)
 * - Tier-specific upgrade prompts
 * - Italian language UI messages
 * - Unlimited sessions display for admins
 * - Compact mode for smaller displays
 * - Loading state
 *
 * ## Accessibility
 * - ✅ Semantic HTML with proper ARIA labels
 * - ✅ Color contrast compliant (WCAG AA)
 * - ✅ Progress bar with visual and text indicators
 * - ✅ Keyboard accessible upgrade links
 */
const meta = {
  title: 'Sessions/SessionQuotaBar',
  component: SessionQuotaBar,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Displays user session quota status with usage progress, tier information, and upgrade prompts.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    currentSessions: {
      control: { type: 'number', min: 0, max: 20 },
      description: 'Number of currently active sessions',
    },
    maxSessions: {
      control: { type: 'number', min: 1, max: 20 },
      description: 'Maximum sessions allowed for user tier',
    },
    userTier: {
      control: 'select',
      options: ['free', 'normal', 'premium'],
      description: 'User subscription tier',
    },
    remainingSlots: {
      control: { type: 'number', min: 0, max: 20 },
      description: 'Number of slots remaining',
    },
    canCreateNew: {
      control: 'boolean',
      description: 'Whether user can create a new session',
    },
    isUnlimited: {
      control: 'boolean',
      description: 'Whether user has unlimited sessions (admin)',
    },
    showUpgradeLink: {
      control: 'boolean',
      description: 'Show upgrade link to next tier',
    },
    isLoading: {
      control: 'boolean',
      description: 'Show loading state',
    },
    compact: {
      control: 'boolean',
      description: 'Compact mode for smaller displays',
    },
  },
} satisfies Meta<typeof SessionQuotaBar>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Free tier user with low usage (40%).
 * Comfortable state with plenty of room for more sessions.
 */
export const FreeTierLowUsage: Story = {
  args: {
    currentSessions: 2,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 3,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};

/**
 * Free tier user approaching limit (80%).
 * Yellow warning state with upgrade prompt.
 */
export const FreeTierNearLimit: Story = {
  args: {
    currentSessions: 4,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 1,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};

/**
 * Free tier user at limit (100%).
 * Red alert state with disabled session creation.
 */
export const FreeTierAtLimit: Story = {
  args: {
    currentSessions: 5,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 0,
    canCreateNew: false,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};

/**
 * Normal tier user with moderate usage (50%).
 */
export const NormalTierMidUsage: Story = {
  args: {
    currentSessions: 5,
    maxSessions: 10,
    userTier: 'normal',
    remainingSlots: 5,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};

/**
 * Normal tier user approaching limit (90%).
 */
export const NormalTierNearLimit: Story = {
  args: {
    currentSessions: 9,
    maxSessions: 10,
    userTier: 'normal',
    remainingSlots: 1,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};

/**
 * Premium tier user with high usage (80%).
 * No upgrade link since already at max tier.
 */
export const PremiumTierHighUsage: Story = {
  args: {
    currentSessions: 16,
    maxSessions: 20,
    userTier: 'premium',
    remainingSlots: 4,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: false,
    isLoading: false,
    compact: false,
  },
};

/**
 * Premium tier user at limit (100%).
 * No upgrade option available.
 */
export const PremiumTierAtLimit: Story = {
  args: {
    currentSessions: 20,
    maxSessions: 20,
    userTier: 'premium',
    remainingSlots: 0,
    canCreateNew: false,
    isUnlimited: false,
    showUpgradeLink: false,
    isLoading: false,
    compact: false,
  },
};

/**
 * Admin user with unlimited sessions.
 * Shows special unlimited indicator.
 */
export const UnlimitedSessions: Story = {
  args: {
    currentSessions: 15,
    maxSessions: 999,
    userTier: 'admin',
    remainingSlots: 984,
    canCreateNew: true,
    isUnlimited: true,
    showUpgradeLink: false,
    isLoading: false,
    compact: false,
  },
};

/**
 * Loading state while fetching quota data.
 */
export const Loading: Story = {
  args: {
    currentSessions: 0,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 5,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: true,
    compact: false,
  },
};

/**
 * Compact mode for smaller displays.
 * Useful for sidebars or embedded contexts.
 */
export const CompactMode: Story = {
  args: {
    currentSessions: 3,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 2,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: true,
  },
};

/**
 * Compact mode near limit.
 */
export const CompactNearLimit: Story = {
  args: {
    currentSessions: 4,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 1,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: true,
  },
};

/**
 * Compact mode at limit.
 */
export const CompactAtLimit: Story = {
  args: {
    currentSessions: 5,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 0,
    canCreateNew: false,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: true,
  },
};

/**
 * Minimal layout without upgrade link.
 * Useful for embedded contexts or simplified displays.
 */
export const NoUpgradeLink: Story = {
  args: {
    currentSessions: 2,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 3,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: false,
    isLoading: false,
    compact: false,
  },
};

/**
 * Zero active sessions.
 */
export const ZeroSessions: Story = {
  args: {
    currentSessions: 0,
    maxSessions: 5,
    userTier: 'free',
    remainingSlots: 5,
    canCreateNew: true,
    isUnlimited: false,
    showUpgradeLink: true,
    isLoading: false,
    compact: false,
  },
};
