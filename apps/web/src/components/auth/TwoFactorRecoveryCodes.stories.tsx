/**
 * TwoFactorRecoveryCodes Storybook Stories (Issue #3077)
 *
 * Real component stories replacing placeholder.
 * Covers: backup codes grid, copy, download, acknowledgment.
 */

import { fn } from 'storybook/test';

import { TwoFactorRecoveryCodes } from './TwoFactorRecoveryCodes';

import type { Meta, StoryObj } from '@storybook/react';

const mockBackupCodes = [
  '12345678',
  '23456789',
  '34567890',
  '45678901',
  '56789012',
  '67890123',
  '78901234',
  '89012345',
];

const meta = {
  title: 'Components/Auth/TwoFactorRecoveryCodes',
  component: TwoFactorRecoveryCodes,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Recovery codes display component with copy to clipboard, download as text file, and acknowledgment button.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  args: {
    backupCodes: mockBackupCodes,
    onContinue: fn(),
  },
} satisfies Meta<typeof TwoFactorRecoveryCodes>;

export default meta;
type Story = StoryObj<typeof meta>;

// Basic States
export const Default: Story = {};

export const WithCustomTitle: Story = {
  args: {
    title: 'Your Recovery Codes',
  },
};

export const WithoutAcknowledgment: Story = {
  args: {
    showAcknowledgment: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Recovery codes display without acknowledgment button (read-only view)',
      },
    },
  },
};

export const CustomAcknowledgmentText: Story = {
  args: {
    acknowledgmentText: 'Continue to Dashboard',
  },
};

export const FewCodes: Story = {
  args: {
    backupCodes: ['12345678', '23456789', '34567890', '45678901'],
  },
  parameters: {
    docs: {
      description: {
        story: 'Display with fewer backup codes (e.g., some already used)',
      },
    },
  },
};

export const ManyCodes: Story = {
  args: {
    backupCodes: [
      '12345678',
      '23456789',
      '34567890',
      '45678901',
      '56789012',
      '67890123',
      '78901234',
      '89012345',
      '90123456',
      '01234567',
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Display with more backup codes',
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
