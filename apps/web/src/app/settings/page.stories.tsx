import type { Meta, StoryObj } from '@storybook/react';
import SettingsPage from './page';

/**
 * Settings Page - Comprehensive 4-Tab User Settings
 *
 * ## Features
 * - **Profile Tab**: Display name, email, password change
 * - **Preferences Tab**: Language, theme, notifications, data retention
 * - **Privacy Tab**: 2FA management (setup, enable, disable), OAuth account linking (Google, Discord, GitHub)
 * - **Advanced Tab**: API key authentication, active session management, account deletion
 *
 * ## Security
 * - Session-based authentication required
 * - 2FA setup with QR code and backup codes
 * - OAuth integration for social login
 * - API key storage in sessionStorage
 * - Session revocation capabilities
 */
const meta = {
  title: 'Pages/Settings',
  component: SettingsPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof SettingsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock user data
const mockUserProfile = {
  DisplayName: 'John Doe',
  Email: 'john.doe@example.com',
  Role: 'User',
  CreatedAt: '2025-01-15T10:00:00Z',
  Language: 'en',
  Theme: 'system',
  EmailNotifications: true,
  DataRetentionDays: 90,
};

const mock2FAStatus = {
  IsEnabled: false,
  UnusedBackupCodesCount: 0,
};

const mock2FAEnabled = {
  IsEnabled: true,
  UnusedBackupCodesCount: 8,
};

const mockOAuthAccounts = [
  {
    provider: 'google',
    createdAt: '2025-01-20T12:00:00Z',
  },
];

const mockSessions = [
  {
    Id: 'session-1',
    IpAddress: '192.168.1.100',
    UserAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
    CreatedAt: '2025-12-05T10:00:00Z',
    LastSeenAt: '2025-12-05T12:30:00Z',
    ExpiresAt: '2025-12-12T10:00:00Z',
  },
  {
    Id: 'session-2',
    IpAddress: '192.168.1.101',
    UserAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X)',
    CreatedAt: '2025-12-04T15:00:00Z',
    LastSeenAt: '2025-12-05T09:00:00Z',
    ExpiresAt: '2025-12-11T15:00:00Z',
  },
];

/**
 * Default settings page with Profile tab active.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Loading state while fetching profile data.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Profile tab - Success state after profile update.
 */
export const ProfileUpdateSuccess: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/users/profile',
        method: 'PUT',
        status: 200,
        response: { success: true },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Profile tab - Error state when update fails.
 */
export const ProfileUpdateError: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/users/profile',
        method: 'PUT',
        status: 400,
        response: { message: 'Invalid display name' },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Privacy tab - 2FA disabled (ready for setup).
 */
export const Privacy2FADisabled: Story = {
  parameters: {
    initialTab: 'privacy',
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Privacy tab - 2FA enabled with backup codes warning.
 */
export const Privacy2FAEnabled: Story = {
  parameters: {
    initialTab: 'privacy',
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAEnabled,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Privacy tab - OAuth accounts linked.
 */
export const PrivacyOAuthLinked: Story = {
  parameters: {
    initialTab: 'privacy',
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: mockOAuthAccounts,
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Advanced tab - API key authenticated.
 */
export const AdvancedAPIKeyActive: Story = {
  parameters: {
    initialTab: 'advanced',
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
};

/**
 * Advanced tab - Active sessions list.
 */
export const AdvancedActiveSessions: Story = {
  parameters: {
    initialTab: 'advanced',
    mockData: [
      {
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
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
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
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
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
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
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
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
        url: '/api/v1/auth/me',
        method: 'GET',
        status: 200,
        response: { user: mockUserProfile },
      },
      {
        url: '/api/v1/auth/2fa/status',
        method: 'GET',
        status: 200,
        response: mock2FAStatus,
      },
      {
        url: '/api/v1/users/me/oauth-accounts',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/users/me/sessions',
        method: 'GET',
        status: 200,
        response: mockSessions,
      },
    ],
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};
