import { GameLibraryLimitsClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Game Library Tier Limits Configuration
 * Issue #2444: Admin UI - Configure Game Library Tier Limits
 *
 * ## Features
 * - **Tier Configuration**: Set max games for Free, Normal, Premium tiers
 * - **Validation**: Min=1, Max=1000, tier hierarchy enforcement
 * - **Real-time Updates**: Changes propagate within 5 minutes
 * - **Audit Trail**: Display last updated timestamp and user
 */
const meta = {
  title: 'Admin/Configuration/GameLibraryLimits',
  component: GameLibraryLimitsClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameLibraryLimitsClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data
const mockLimits = {
  freeTierLimit: 5,
  normalTierLimit: 20,
  premiumTierLimit: 50,
  lastUpdatedAt: '2026-01-16T10:00:00Z',
  lastUpdatedByUserId: '123e4567-e89b-12d3-a456-426614174000',
};

/**
 * Default state with current configuration loaded.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: mockLimits,
      },
    ],
  },
};

/**
 * Loading state while fetching configuration.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Error state when fetch fails.
 */
export const ErrorState: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Custom limits (higher tier values).
 */
export const CustomLimits: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: {
          freeTierLimit: 10,
          normalTierLimit: 50,
          premiumTierLimit: 200,
          lastUpdatedAt: '2026-01-16T14:30:00Z',
          lastUpdatedByUserId: '987e6543-e21b-34c5-b678-426614174999',
        },
      },
    ],
  },
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: mockLimits,
      },
    ],
  },
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: mockLimits,
      },
    ],
  },
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: mockLimits,
      },
    ],
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    mockData: [
      {
        url: '/api/v1/admin/config/game-library-limits',
        method: 'GET',
        status: 200,
        response: mockLimits,
      },
    ],
  },
  decorators: [
    (Story) => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};
