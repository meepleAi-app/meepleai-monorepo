/**
 * TwoFactorVerification Storybook Stories (Issue #3077)
 *
 * Real component stories replacing placeholder.
 * Covers: code input, backup codes, remember device, errors.
 */

import { fn } from 'storybook/test';

import type { Meta, StoryObj } from '@storybook/react';

import { TwoFactorVerification } from './TwoFactorVerification';

const meta = {
  title: 'Components/Auth/TwoFactorVerification',
  component: TwoFactorVerification,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Two-factor authentication verification component with 6-digit code input, auto-submit, remember device option, and backup code link.',
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
    onCancel: fn(),
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

export const WithoutRememberDevice: Story = {
  args: {
    showRememberDevice: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Verification without remember device option',
      },
    },
  },
};

export const WithoutBackupCodeLink: Story = {
  args: {
    onUseBackupCode: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Verification without backup code link',
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
        story: 'Verification without cancel button',
      },
    },
  },
};

export const CustomTitle: Story = {
  args: {
    title: 'Verify Your Identity',
    subtitle: 'Enter the code from your authenticator to continue',
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
