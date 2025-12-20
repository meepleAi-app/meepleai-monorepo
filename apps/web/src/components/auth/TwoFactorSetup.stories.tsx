/**
 * TwoFactorSetup Storybook Stories (Issue #1496: E2E-010)
 *
 * PLACEHOLDER STORIES - 2FA components not yet implemented
 * These stories define the expected UI states for future 2FA implementation.
 *
 * Visual regression tests for Chromatic.
 * Covers: QR code display, manual code entry, verification, success, errors.
 */

import { fn } from 'storybook/test';

import type { Meta, StoryObj } from '@storybook/react';

// Placeholder component (will be replaced with real component)
const TwoFactorSetup = ({ onComplete, onCancel, loading, error }: any) => (
  <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg space-y-4">
    <h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100">
      Set Up Two-Factor Authentication
    </h2>
    <p className="text-sm text-slate-600 dark:text-slate-400">
      Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)
    </p>

    {/* QR Code Placeholder */}
    <div className="flex justify-center p-6 bg-white border-2 border-slate-200 rounded-lg">
      <div className="w-48 h-48 bg-slate-100 flex items-center justify-center">
        {loading ? (
          <div className="animate-spin h-8 w-8 border-4 border-blue-500 border-t-transparent rounded-full" />
        ) : (
          <span className="text-slate-400 text-center text-sm">
            QR Code
            <br />
            Placeholder
          </span>
        )}
      </div>
    </div>

    {/* Manual Entry Code */}
    <div className="p-4 bg-slate-50 dark:bg-slate-900 rounded-md">
      <p className="text-xs text-slate-600 dark:text-slate-400 mb-2">
        Can't scan? Enter this code manually:
      </p>
      <code className="text-sm font-mono bg-white dark:bg-slate-800 px-3 py-2 rounded border border-slate-200 dark:border-slate-700">
        JBSWY3DPEHPK3PXP
      </code>
    </div>

    {/* Verification Input */}
    <div className="space-y-2">
      <label className="block text-sm font-medium text-slate-900 dark:text-slate-100">
        Enter verification code
      </label>
      <input
        type="text"
        placeholder="000000"
        maxLength={6}
        className="w-full px-4 py-2 border border-slate-300 dark:border-slate-700 rounded-md text-center text-2xl font-mono tracking-widest"
        disabled={loading}
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

    {/* Action Buttons */}
    <div className="flex gap-3">
      <button
        onClick={onCancel}
        disabled={loading}
        className="flex-1 px-4 py-2 border border-slate-300 dark:border-slate-700 rounded-md text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700"
      >
        Cancel
      </button>
      <button
        onClick={onComplete}
        disabled={loading}
        className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
      >
        {loading ? 'Verifying...' : 'Verify & Enable'}
      </button>
    </div>
  </div>
);

const meta = {
  title: 'Components/Auth/TwoFactorSetup (Placeholder)',
  component: TwoFactorSetup,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          '⚠️ PLACEHOLDER - 2FA setup component not yet implemented. These stories define expected UI states for future implementation. ' +
          'Shows QR code for app scanning, manual code entry, verification input, and error handling.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    onComplete: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof TwoFactorSetup>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {};

export const Loading: Story = {
  args: {
    loading: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading QR code from server',
      },
    },
  },
};

export const WithError: Story = {
  args: {
    error: 'Invalid verification code. Please try again.',
  },
};

export const VerificationInProgress: Story = {
  args: {
    loading: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'User submitted code, verification in progress',
      },
    },
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
