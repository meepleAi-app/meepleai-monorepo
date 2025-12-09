/**
 * TwoFactorVerification Storybook Stories (Issue #1496: E2E-010)
 *
 * PLACEHOLDER STORIES - 2FA components not yet implemented
 * These stories define the expected UI states for future 2FA implementation.
 *
 * Visual regression tests for Chromatic.
 * Covers: code input, backup codes, remember device, errors.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';

// Placeholder component (will be replaced with real component)
const TwoFactorVerification = ({ onVerify, onUseBackupCode, loading, error }: any) => (
  <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg space-y-4">
    <h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100">
      Two-Factor Authentication
    </h2>
    <p className="text-sm text-slate-600 dark:text-slate-400">
      Enter the 6-digit code from your authenticator app
    </p>

    {/* Verification Code Input */}
    <div className="space-y-2">
      <input
        type="text"
        placeholder="000000"
        maxLength={6}
        className="w-full px-4 py-3 border border-slate-300 dark:border-slate-700 rounded-md text-center text-3xl font-mono tracking-widest focus:ring-2 focus:ring-blue-500"
        disabled={loading}
        autoFocus
      />
    </div>

    {/* Error Display */}
    {error && (
      <div
        className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
        role="alert"
      >
        <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
      </div>
    )}

    {/* Remember Device */}
    <div className="flex items-center gap-2">
      <input
        type="checkbox"
        id="remember-device"
        className="w-4 h-4 text-blue-600 border-slate-300 rounded focus:ring-blue-500"
      />
      <label htmlFor="remember-device" className="text-sm text-slate-700 dark:text-slate-300">
        Trust this device for 30 days
      </label>
    </div>

    {/* Verify Button */}
    <button
      onClick={onVerify}
      disabled={loading}
      className="w-full px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
    >
      {loading ? 'Verifying...' : 'Verify'}
    </button>

    {/* Backup Code Link */}
    <div className="text-center">
      <button
        onClick={onUseBackupCode}
        className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
      >
        Use a backup code instead
      </button>
    </div>
  </div>
);

const meta = {
  title: 'Components/Auth/TwoFactorVerification (Placeholder)',
  component: TwoFactorVerification,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          '⚠️ PLACEHOLDER - 2FA verification component not yet implemented. These stories define expected UI states for future implementation. ' +
          'Shows code input, remember device option, backup code link, and error handling.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    onVerify: fn(),
    onUseBackupCode: fn(),
  },
} satisfies Meta<typeof TwoFactorVerification>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {};

export const Verifying: Story = {
  args: {
    loading: true,
  },
};

export const InvalidCode: Story = {
  args: {
    error: 'Invalid code. Please try again.',
  },
};

export const ExpiredCode: Story = {
  args: {
    error: 'Code expired. Please use a new code from your authenticator app.',
  },
};

export const TooManyAttempts: Story = {
  args: {
    error: 'Too many failed attempts. Please wait 5 minutes before trying again.',
  },
};

// Mobile
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
  },
};

// Dark Mode
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};
