/**
 * UserActivityTimeline Stories - Issue #911
 *
 * Visual regression tests for Chromatic.
 */

import { api } from '@/lib/api';
import type { GetUserActivityResult } from '@/lib/api/schemas';

import { UserActivityTimeline } from './UserActivityTimeline';

import type { Meta, StoryObj } from '@storybook/react';

// Mock API responses
const mockEmptyActivity: GetUserActivityResult = {
  activities: [],
  totalCount: 0,
};

const mockLoadedActivity: GetUserActivityResult = {
  activities: [
    {
      id: '1',
      action: 'Login',
      resource: 'Session',
      resourceId: 'sess_123',
      result: 'Success',
      details: 'User logged in successfully',
      createdAt: new Date().toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '2',
      action: 'PasswordChanged',
      resource: 'User',
      resourceId: null,
      result: 'Success',
      details: 'Password updated',
      createdAt: new Date(Date.now() - 3600000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '3',
      action: 'TwoFactorEnabled',
      resource: 'User',
      resourceId: null,
      result: 'Success',
      details: '2FA enabled for account',
      createdAt: new Date(Date.now() - 7200000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '4',
      action: 'ApiKeyCreated',
      resource: 'ApiKey',
      resourceId: 'key_abc',
      result: 'Success',
      details: 'API key created: Production Key',
      createdAt: new Date(Date.now() - 86400000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '5',
      action: 'Login',
      resource: 'Session',
      resourceId: 'sess_456',
      result: 'Failed',
      details: 'Invalid credentials',
      createdAt: new Date(Date.now() - 172800000).toISOString(),
      ipAddress: '10.0.0.50',
    },
    {
      id: '6',
      action: 'ProfileUpdated',
      resource: 'User',
      resourceId: null,
      result: 'Success',
      details: 'Display name changed',
      createdAt: new Date(Date.now() - 259200000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '7',
      action: 'PdfUploaded',
      resource: 'PDF',
      resourceId: 'pdf_789',
      result: 'Success',
      details: 'Uploaded: Catan Rules.pdf',
      createdAt: new Date(Date.now() - 345600000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '8',
      action: 'GameCreated',
      resource: 'Game',
      resourceId: 'game_xyz',
      result: 'Success',
      details: 'Created game: Settlers of Catan',
      createdAt: new Date(Date.now() - 432000000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '9',
      action: 'Logout',
      resource: 'Session',
      resourceId: 'sess_789',
      result: 'Success',
      details: 'User logged out',
      createdAt: new Date(Date.now() - 518400000).toISOString(),
      ipAddress: '192.168.1.1',
    },
    {
      id: '10',
      action: 'ApiKeyRevoked',
      resource: 'ApiKey',
      resourceId: 'key_old',
      result: 'Success',
      details: 'API key revoked: Old Test Key',
      createdAt: new Date(Date.now() - 604800000).toISOString(),
      ipAddress: '192.168.1.1',
    },
  ],
  totalCount: 10,
};

const mockErrorActivity = new Error('Network error: Failed to fetch activity');

const meta: Meta<typeof UserActivityTimeline> = {
  title: 'Components/Timeline/UserActivityTimeline',
  component: UserActivityTimeline,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'User activity timeline component that displays audit log events with filtering. Issue #911.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    userId: {
      control: 'text',
      description: 'User ID to fetch activity for. If null, fetches current user activity.',
    },
    maxEvents: {
      control: { type: 'number', min: 1, max: 100 },
      description: 'Maximum number of activities to display',
    },
    showFilters: {
      control: 'boolean',
      description: 'Show filter controls',
    },
    showViewAll: {
      control: 'boolean',
      description: 'Show view all link',
    },
    autoRefreshMs: {
      control: { type: 'number', min: 0 },
      description: 'Auto-refresh interval in milliseconds (0 to disable)',
    },
  },
};

export default meta;
type Story = StoryObj<typeof UserActivityTimeline>;

/**
 * Empty State
 * No activity to display
 */
export const Empty: Story = {
  args: {
    userId: 'user-123',
    maxEvents: 50,
    showFilters: true,
    showViewAll: true,
  },
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/users/user-123/activity',
        method: 'GET',
        status: 200,
        response: mockEmptyActivity,
      },
    ],
  },
  decorators: [
    Story => {
      // Mock API call
      const originalGetUserActivity = api.admin.getUserActivity;
      api.admin.getUserActivity = async () => mockEmptyActivity;

      const result = <Story />;

      // Restore
      api.admin.getUserActivity = originalGetUserActivity;
      return result;
    },
  ],
};

/**
 * Loaded State
 * Activity timeline with 10 events
 */
export const Loaded: Story = {
  args: {
    userId: 'user-123',
    maxEvents: 50,
    showFilters: true,
    showViewAll: true,
    viewAllHref: '/admin/users/user-123/activity',
  },
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/users/user-123/activity',
        method: 'GET',
        status: 200,
        response: mockLoadedActivity,
      },
    ],
  },
  decorators: [
    Story => {
      const originalGetUserActivity = api.admin.getUserActivity;
      api.admin.getUserActivity = async () => mockLoadedActivity;

      const result = <Story />;
      api.admin.getUserActivity = originalGetUserActivity;
      return result;
    },
  ],
};

/**
 * Error State
 * Failed to load activity
 */
export const Error: Story = {
  args: {
    userId: 'user-123',
    maxEvents: 50,
    showFilters: true,
    showViewAll: true,
  },
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/users/user-123/activity',
        method: 'GET',
        status: 500,
        response: { error: 'Internal server error' },
      },
    ],
  },
  decorators: [
    Story => {
      const original = api.admin.getUserActivity;
      api.admin.getUserActivity = async () => {
        throw mockErrorActivity;
      };

      const result = <Story />;
      api.admin.getUserActivity = original;
      return result;
    },
  ],
};

/**
 * My Activity (Current User)
 * Using auth.getMyActivity endpoint
 */
export const MyActivity: Story = {
  args: {
    userId: null, // null = current user
    maxEvents: 20,
    showFilters: false,
    showViewAll: false,
  },
  parameters: {
    mockData: [
      {
        url: '/api/v1/users/me/activity',
        method: 'GET',
        status: 200,
        response: {
          activities: mockLoadedActivity.activities.slice(0, 5),
          totalCount: 5,
        },
      },
    ],
  },
  decorators: [
    Story => {
      const originalGetMyActivity = api.auth.getMyActivity;
      api.auth.getMyActivity = async () => ({
        activities: mockLoadedActivity.activities.slice(0, 5),
        totalCount: 5,
      });

      const result = <Story />;
      api.auth.getMyActivity = originalGetMyActivity;
      return result;
    },
  ],
};

/**
 * With Filters Expanded
 * Shows filter panel open
 */
export const WithFiltersExpanded: Story = {
  args: {
    userId: 'user-123',
    maxEvents: 50,
    showFilters: true,
    showViewAll: true,
  },
  decorators: [
    Story => {
      const originalGetUserActivity = api.admin.getUserActivity;
      api.admin.getUserActivity = async () => mockLoadedActivity;

      const result = <Story />;
      api.admin.getUserActivity = originalGetUserActivity;
      return result;
    },
  ],
  play: async ({ canvasElement }) => {
    // Click filter button to expand
    const filterButton = canvasElement.querySelector('button:has(svg)');
    if (filterButton instanceof HTMLElement) {
      filterButton.click();
    }
  },
};
