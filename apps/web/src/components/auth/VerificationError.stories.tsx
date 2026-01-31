/**
 * VerificationError Storybook Stories (Issue #3076)
 *
 * Visual regression tests for Chromatic.
 * Covers all error types: expired, invalid, already_verified, not_found, rate_limited, unknown.
 * Also covers: actions, dark mode, mobile.
 */

import { fn } from 'storybook/test';

import { VerificationError } from './VerificationError';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Auth/VerificationError',
  component: VerificationError,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Displays error states for email verification with appropriate icons, messages, and actions. ' +
          'Handles expired, invalid, already_verified, not_found, rate_limited, and unknown error types.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    errorType: {
      control: 'select',
      options: ['expired', 'invalid', 'already_verified', 'not_found', 'rate_limited', 'unknown'],
      description: 'Type of verification error',
    },
    errorMessage: {
      control: 'text',
      description: 'Custom error message (overrides default)',
    },
    isResending: {
      control: 'boolean',
      description: 'Whether resend operation is in progress',
    },
    cooldownSeconds: {
      control: { type: 'number', min: 0, max: 120 },
      description: 'Cooldown seconds for resend button',
    },
  },
  args: {
    onResend: fn(),
    onGoToLogin: fn(),
    onRetry: fn(),
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
} satisfies Meta<typeof VerificationError>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Error Type: Expired
// =============================================================================

/**
 * Expired token - offer to resend
 */
export const Expired: Story = {
  args: {
    errorType: 'expired',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Token has expired. Shows clock icon and resend button.',
      },
    },
  },
};

/**
 * Expired - resending in progress
 */
export const ExpiredResending: Story = {
  args: {
    errorType: 'expired',
    isResending: true,
    cooldownSeconds: 0,
  },
};

/**
 * Expired - cooldown active
 */
export const ExpiredCooldown: Story = {
  args: {
    errorType: 'expired',
    isResending: false,
    cooldownSeconds: 45,
  },
};

// =============================================================================
// Error Type: Invalid
// =============================================================================

/**
 * Invalid token - malformed or tampered
 */
export const Invalid: Story = {
  args: {
    errorType: 'invalid',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Token is invalid or malformed. Shows X icon and retry button.',
      },
    },
  },
};

// =============================================================================
// Error Type: Already Verified
// =============================================================================

/**
 * Already verified - redirect to login
 */
export const AlreadyVerified: Story = {
  args: {
    errorType: 'already_verified',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Email was already verified. Shows check icon and login button.',
      },
    },
  },
};

// =============================================================================
// Error Type: Not Found
// =============================================================================

/**
 * Token not found in database
 */
export const NotFound: Story = {
  args: {
    errorType: 'not_found',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Token not found in database. Shows X icon and retry button.',
      },
    },
  },
};

// =============================================================================
// Error Type: Rate Limited
// =============================================================================

/**
 * Rate limited - too many requests
 */
export const RateLimited: Story = {
  args: {
    errorType: 'rate_limited',
    isResending: false,
    cooldownSeconds: 60,
  },
  parameters: {
    docs: {
      description: {
        story: 'Too many verification attempts. Shows shield icon and cooldown message.',
      },
    },
  },
};

/**
 * Rate limited - short cooldown
 */
export const RateLimitedShortCooldown: Story = {
  args: {
    errorType: 'rate_limited',
    isResending: false,
    cooldownSeconds: 15,
  },
};

// =============================================================================
// Error Type: Unknown
// =============================================================================

/**
 * Unknown error - generic fallback
 */
export const Unknown: Story = {
  args: {
    errorType: 'unknown',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Generic error occurred. Shows alert icon with both resend and retry buttons.',
      },
    },
  },
};

/**
 * Unknown error with custom message
 */
export const UnknownCustomMessage: Story = {
  args: {
    errorType: 'unknown',
    errorMessage: 'A temporary issue occurred while verifying your email. Our team has been notified.',
    isResending: false,
    cooldownSeconds: 0,
  },
};

// =============================================================================
// Responsive & Dark Mode
// =============================================================================

/**
 * Mobile viewport - expired error
 */
export const MobileExpired: Story = {
  args: {
    errorType: 'expired',
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
 * Mobile viewport - already verified
 */
export const MobileAlreadyVerified: Story = {
  args: {
    errorType: 'already_verified',
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
 * Tablet viewport
 */
export const Tablet: Story = {
  args: {
    errorType: 'expired',
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
 * Dark mode - expired
 */
export const DarkModeExpired: Story = {
  args: {
    errorType: 'expired',
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
 * Dark mode - invalid
 */
export const DarkModeInvalid: Story = {
  args: {
    errorType: 'invalid',
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
 * Dark mode - already verified
 */
export const DarkModeAlreadyVerified: Story = {
  args: {
    errorType: 'already_verified',
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
 * Dark mode - rate limited
 */
export const DarkModeRateLimited: Story = {
  args: {
    errorType: 'rate_limited',
    isResending: false,
    cooldownSeconds: 60,
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
 * Dark mode - unknown
 */
export const DarkModeUnknown: Story = {
  args: {
    errorType: 'unknown',
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

// =============================================================================
// Edge Cases
// =============================================================================

/**
 * Long custom error message
 */
export const LongErrorMessage: Story = {
  args: {
    errorType: 'unknown',
    errorMessage:
      'We encountered an unexpected error while processing your email verification request. ' +
      'This could be due to a temporary server issue, network connectivity problems, or an outdated verification link. ' +
      'Please try refreshing the page or request a new verification email.',
    isResending: false,
    cooldownSeconds: 0,
  },
};

/**
 * All action buttons visible (unknown error)
 */
export const AllActions: Story = {
  args: {
    errorType: 'unknown',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'Unknown error type shows both resend and retry buttons',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * Link clicked after 24 hours
 */
export const LinkExpiredAfter24Hours: Story = {
  args: {
    errorType: 'expired',
    errorMessage: 'This verification link expired 24 hours ago. Please request a new one.',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'User clicked verification link after it expired',
      },
    },
  },
};

/**
 * Copied link incorrectly
 */
export const CorruptedLink: Story = {
  args: {
    errorType: 'invalid',
    errorMessage: 'The verification link appears to be incomplete. Please copy the entire link from your email.',
    isResending: false,
    cooldownSeconds: 0,
  },
};

/**
 * Already verified and confused
 */
export const DoubleClickedLink: Story = {
  args: {
    errorType: 'already_verified',
    isResending: false,
    cooldownSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'User clicked the verification link a second time',
      },
    },
  },
};

/**
 * Aggressive user hitting rate limit
 */
export const AggressiveRateLimit: Story = {
  args: {
    errorType: 'rate_limited',
    isResending: false,
    cooldownSeconds: 120,
  },
  parameters: {
    docs: {
      description: {
        story: 'User made too many verification attempts, maximum cooldown',
      },
    },
  },
};
