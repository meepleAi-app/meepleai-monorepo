/**
 * BadgeGrid Storybook Stories (Issue #2747)
 */

import type { Meta, StoryObj } from '@storybook/react';

import { BadgeGrid } from './BadgeGrid';
import { BadgeTier, type UserBadgeDto } from '@/types/badges';

const mockBadges: UserBadgeDto[] = [
  {
    id: '1',
    name: 'Legendary Contributor',
    description: '100+ contributions',
    tier: BadgeTier.Diamond,
    iconUrl: '/icons/legendary.png',
    earnedAt: '2026-01-20T10:00:00Z',
    isDisplayed: true,
  },
  {
    id: '2',
    name: 'Platinum Member',
    description: '50 contributions',
    tier: BadgeTier.Platinum,
    iconUrl: '/icons/platinum.png',
    earnedAt: '2026-01-19T10:00:00Z',
    isDisplayed: true,
  },
  {
    id: '3',
    name: 'Gold Contributor',
    description: '25 contributions',
    tier: BadgeTier.Gold,
    iconUrl: '/icons/gold.png',
    earnedAt: '2026-01-18T10:00:00Z',
    isDisplayed: true,
  },
  {
    id: '4',
    name: 'Silver Helper',
    description: '10 contributions',
    tier: BadgeTier.Silver,
    iconUrl: '/icons/silver.png',
    earnedAt: '2026-01-17T10:00:00Z',
    isDisplayed: true,
  },
  {
    id: '5',
    name: 'First Contribution',
    description: 'Made your first contribution',
    tier: BadgeTier.Bronze,
    iconUrl: '/icons/first.png',
    earnedAt: '2026-01-16T10:00:00Z',
    isDisplayed: true,
  },
  {
    id: '6',
    name: 'Secret Achievement',
    description: 'Unlocked hidden badge',
    tier: BadgeTier.Gold,
    iconUrl: '/icons/secret.png',
    earnedAt: '2026-01-15T10:00:00Z',
    isDisplayed: false,
  },
];

const meta = {
  title: 'Components/Badges/BadgeGrid',
  component: BadgeGrid,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component: 'Badge grid component with tier-based grouping and Framer Motion animations',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof BadgeGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    badges: mockBadges,
    showHidden: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Default badge grid with tier grouping (hidden badges excluded)',
      },
    },
  },
};

export const WithHiddenBadges: Story = {
  args: {
    badges: mockBadges,
    showHidden: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Badge grid showing all badges including hidden ones',
      },
    },
  },
};

export const Empty: Story = {
  args: {
    badges: [],
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty state when user has no badges',
      },
    },
  },
};

export const WithClickHandler: Story = {
  args: {
    badges: mockBadges.slice(0, 3),
    onBadgeClick: (badge) => alert(`Clicked: ${badge.name}`),
  },
  parameters: {
    docs: {
      description: {
        story: 'Badge grid with click handlers (try clicking badges)',
      },
    },
  },
};

export const SingleTier: Story = {
  args: {
    badges: mockBadges.filter((b) => b.tier === BadgeTier.Gold),
  },
  parameters: {
    docs: {
      description: {
        story: 'Badge grid with only one tier (Gold)',
      },
    },
  },
};
