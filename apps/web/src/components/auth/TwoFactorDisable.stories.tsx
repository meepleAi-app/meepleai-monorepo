/**
 * TwoFactorDisable Storybook Stories (Issue #3077)
 *
 * Real component stories replacing placeholder.
 * Covers: password input, code input, warnings, errors, loading states.
 */

import { fn } from 'storybook/test';

import type { Meta, StoryObj } from '@storybook/react';
import { TwoFactorDisable } from './TwoFactorDisable';

const meta = {
  title: 'Components/Auth/TwoFactorDisable',
  component: TwoFactorDisable,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Two-factor authentication disable component with password verification, TOTP/backup code input, and security warnings.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    onDisable: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof TwoFactorDisable>;

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
        story: 'Disabling 2FA in progress',
      },
    },
  },
};

export const WithError: Story = {
  args: {
    error: 'Invalid password or verification code. Please try again.',
  },
};

export const InvalidPassword: Story = {
  args: {
    error: 'The password you entered is incorrect.',
  },
};

export const InvalidCode: Story = {
  args: {
    error: 'Invalid verification code. Please enter a valid 6-digit code or backup code.',
  },
};

export const LowBackupCodes: Story = {
  args: {
    remainingBackupCodes: 2,
  },
  parameters: {
    docs: {
      description: {
        story: 'Warning when user has few backup codes remaining',
      },
    },
  },
};

export const OneBackupCode: Story = {
  args: {
    remainingBackupCodes: 1,
  },
  parameters: {
    docs: {
      description: {
        story: 'Warning when user has only one backup code remaining',
      },
    },
  },
};

export const WithoutCancel: Story = {
  args: {
    onCancel: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Disable form without cancel button',
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
