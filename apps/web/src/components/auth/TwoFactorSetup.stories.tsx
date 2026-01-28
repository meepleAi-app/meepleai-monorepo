/**
 * TwoFactorSetup Storybook Stories (Issue #3077)
 *
 * Real component stories replacing placeholder.
 * Covers: QR code display, manual code entry, verification, success, errors.
 */

import { fn } from 'storybook/test';

import type { Meta, StoryObj } from '@storybook/react';
import { TwoFactorSetup } from './TwoFactorSetup';

const mockSetupData = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUrl: 'otpauth://totp/MeepleAI:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
  backupCodes: [
    '12345678',
    '23456789',
    '34567890',
    '45678901',
    '56789012',
    '67890123',
    '78901234',
    '89012345',
  ],
};

const meta = {
  title: 'Components/Auth/TwoFactorSetup',
  component: TwoFactorSetup,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Two-factor authentication setup component with QR code display, manual secret entry, and verification input.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    setupData: mockSetupData,
    onVerify: fn(),
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
        story: 'Verification in progress after user submits code',
      },
    },
  },
};

export const WithError: Story = {
  args: {
    error: 'Invalid verification code. Please try again.',
  },
};

export const WithoutCancel: Story = {
  args: {
    onCancel: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Setup without cancel button',
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
