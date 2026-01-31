/**
 * VerificationPending Storybook Stories (Issue #3076)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, resending, cooldown, error, dark mode, mobile.
 */

import { fn } from 'storybook/test';

import { VerificationPending } from './VerificationPending';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Auth/VerificationPending',
  component: VerificationPending,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Displays "Check your email" message after registration. ' +
          'Shows masked email, resend button with cooldown timer, and spam folder hint.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    email: {
      control: 'text',
      description: 'Email address verification was sent to',
    },
    isResending: {
      control: 'boolean',
      description: 'Whether resend operation is in progress',
    },
    cooldownSeconds: {
      control: { type: 'number', min: 0, max: 120 },
      description: 'Seconds until resend is allowed (0 = can resend)',
    },
    error: {
      control: 'text',
      description: 'Error message if resend failed',
    },
  },
  args: {
    email: 'user@example.com',
    onResend: fn(),
    isResending: false,
    cooldownSeconds: 0,
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof VerificationPending>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

/**
 * Default state - verification email sent, waiting for user
 */
export const Default: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error: undefined,
  },
};

/**
 * Resending state - resend operation in progress
 */
export const Resending: Story = {
  args: {
    email: 'user@example.com',
    isResending: true,
    cooldownSeconds: 0,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Button shows "Sending..." and is disabled during resend operation',
      },
    },
  },
};

/**
 * Cooldown state - must wait before resending
 */
export const Cooldown: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 45,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Button shows countdown timer and is disabled. Shows cooldown message below.',
      },
    },
  },
};

// =============================================================================
// Email Masking Variations
// =============================================================================

/**
 * Short email - 2 character local part
 */
export const ShortEmail: Story = {
  args: {
    email: 'ab@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Short email addresses are masked appropriately (a***@example.com)',
      },
    },
  },
};

/**
 * Long email - full professional email
 */
export const LongEmail: Story = {
  args: {
    email: 'johnathan.smith@verylongcompanyname.com',
    isResending: false,
    cooldownSeconds: 0,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Long email addresses are masked (j***h@verylongcompanyname.com)',
      },
    },
  },
};

/**
 * Single character email
 */
export const SingleCharEmail: Story = {
  args: {
    email: 'a@test.io',
    isResending: false,
    cooldownSeconds: 0,
    error: undefined,
  },
};

// =============================================================================
// Error States
// =============================================================================

/**
 * Resend failed - network error
 */
export const ResendError: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error: 'Failed to resend verification email. Please check your connection and try again.',
  },
};

/**
 * Rate limit error from API
 */
export const RateLimitError: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 60,
    error: 'Too many requests. Please wait before trying again.',
  },
};

/**
 * Server error during resend
 */
export const ServerError: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error: 'Server error occurred. Please try again later.',
  },
};

// =============================================================================
// Responsive & Dark Mode
// =============================================================================

/**
 * Mobile viewport - 375px width
 */
export const Mobile: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Mobile with cooldown
 */
export const MobileWithCooldown: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 30,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet viewport - 768px width
 */
export const Tablet: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Dark mode - default state
 */
export const DarkMode: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-6 bg-slate-900 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode with error
 */
export const DarkModeWithError: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error: 'Failed to resend verification email.',
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-6 bg-slate-900 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode with cooldown
 */
export const DarkModeWithCooldown: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 45,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-6 bg-slate-900 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
};

// =============================================================================
// Edge Cases
// =============================================================================

/**
 * Long error message - test text wrapping
 */
export const LongErrorMessage: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 0,
    error:
      'An unexpected error occurred while trying to resend the verification email. ' +
      'This might be due to a temporary server issue or network connectivity problems. ' +
      'Please check your internet connection and try again in a few moments.',
  },
};

/**
 * Maximum cooldown - 2 minutes
 */
export const MaxCooldown: Story = {
  args: {
    email: 'user@example.com',
    isResending: false,
    cooldownSeconds: 120,
  },
  parameters: {
    docs: {
      description: {
        story: 'Maximum cooldown period of 120 seconds',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * Fresh registration - just registered
 */
export const FreshRegistration: Story = {
  args: {
    email: 'newuser@gmail.com',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'User just registered and is seeing this screen for the first time',
      },
    },
  },
};

/**
 * Impatient user - clicked resend multiple times
 */
export const ImpatientUser: Story = {
  args: {
    email: 'impatient@example.com',
    isResending: false,
    cooldownSeconds: 55,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'User has clicked resend and must wait for cooldown',
      },
    },
  },
};
