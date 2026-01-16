/**
 * QuotaStatusBar Component Stories (Issue #2445)
 *
 * Visual documentation and testing for library quota status display.
 * Demonstrates different quota states: normal, near limit, at limit.
 */

import { QuotaStatusBar } from './QuotaStatusBar';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * QuotaStatusBar displays library quota usage with visual progress bar and tier information.
 *
 * ## Features
 * - Visual progress bar showing quota usage
 * - Color-coded warnings (yellow >80%, red at limit)
 * - Tier-specific upgrade prompts
 * - Italian language UI messages
 *
 * ## Accessibility
 * - ✅ Semantic HTML with proper ARIA labels
 * - ✅ Color contrast compliant (WCAG AA)
 * - ✅ Progress bar with visual and text indicators
 * - ✅ Keyboard accessible upgrade links
 */
const meta = {
  title: 'Library/QuotaStatusBar',
  component: QuotaStatusBar,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Displays user library quota status with usage progress, tier information, and upgrade prompts.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    currentCount: {
      control: { type: 'number', min: 0, max: 100 },
      description: 'Number of games currently in library',
    },
    maxAllowed: {
      control: { type: 'number', min: 1, max: 100 },
      description: 'Maximum games allowed for user tier',
    },
    userTier: {
      control: 'select',
      options: ['free', 'normal', 'premium'],
      description: 'User subscription tier',
    },
    percentageUsed: {
      control: { type: 'number', min: 0, max: 100 },
      description: 'Percentage of quota used (0-100)',
    },
    remainingSlots: {
      control: { type: 'number', min: 0, max: 100 },
      description: 'Number of slots remaining',
    },
    showUpgradeLink: {
      control: 'boolean',
      description: 'Show upgrade link to next tier',
    },
  },
} satisfies Meta<typeof QuotaStatusBar>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Free tier user with minimal usage (20%).
 * Comfortable state with plenty of room to grow.
 */
export const FreeTierLowUsage: Story = {
  args: {
    currentCount: 1,
    maxAllowed: 5,
    userTier: 'free',
    percentageUsed: 20,
    remainingSlots: 4,
    showUpgradeLink: true,
  },
};

/**
 * Free tier user approaching limit (85%).
 * Yellow warning state with upgrade prompt.
 */
export const FreeTierNearLimit: Story = {
  args: {
    currentCount: 4,
    maxAllowed: 5,
    userTier: 'free',
    percentageUsed: 80,
    remainingSlots: 1,
    showUpgradeLink: true,
  },
};

/**
 * Free tier user at limit (100%).
 * Red alert state with disabled add functionality.
 */
export const FreeTierAtLimit: Story = {
  args: {
    currentCount: 5,
    maxAllowed: 5,
    userTier: 'free',
    percentageUsed: 100,
    remainingSlots: 0,
    showUpgradeLink: true,
  },
};

/**
 * Normal tier user with moderate usage (50%).
 */
export const NormalTierMidUsage: Story = {
  args: {
    currentCount: 10,
    maxAllowed: 20,
    userTier: 'normal',
    percentageUsed: 50,
    remainingSlots: 10,
    showUpgradeLink: true,
  },
};

/**
 * Normal tier user approaching limit (90%).
 */
export const NormalTierNearLimit: Story = {
  args: {
    currentCount: 18,
    maxAllowed: 20,
    userTier: 'normal',
    percentageUsed: 90,
    remainingSlots: 2,
    showUpgradeLink: true,
  },
};

/**
 * Premium tier user with high usage (80%).
 * No upgrade link since already at max tier.
 */
export const PremiumTierHighUsage: Story = {
  args: {
    currentCount: 40,
    maxAllowed: 50,
    userTier: 'premium',
    percentageUsed: 80,
    remainingSlots: 10,
    showUpgradeLink: false,
  },
};

/**
 * Premium tier user at limit (100%).
 * No upgrade option available.
 */
export const PremiumTierAtLimit: Story = {
  args: {
    currentCount: 50,
    maxAllowed: 50,
    userTier: 'premium',
    percentageUsed: 100,
    remainingSlots: 0,
    showUpgradeLink: false,
  },
};

/**
 * Minimal layout without upgrade link.
 * Useful for embedded contexts or simplified displays.
 */
export const NoUpgradeLink: Story = {
  args: {
    currentCount: 3,
    maxAllowed: 5,
    userTier: 'free',
    percentageUsed: 60,
    remainingSlots: 2,
    showUpgradeLink: false,
  },
};
