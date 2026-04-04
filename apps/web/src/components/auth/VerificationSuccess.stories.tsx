/**
 * VerificationSuccess Storybook Stories (Issue #3076)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, with email, countdown variations, dark mode, mobile.
 */

import { fn } from 'storybook/test';

import { VerificationSuccess } from './VerificationSuccess';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Auth/VerificationSuccess',
  component: VerificationSuccess,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Displays success message after email verification. ' +
          'Features success animation, auto-redirect countdown, and manual redirect button.',
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
      description: 'The verified email address (optional)',
    },
    redirectUrl: {
      control: 'text',
      description: 'URL to redirect after success',
    },
    autoRedirectSeconds: {
      control: { type: 'number', min: 0, max: 10 },
      description: 'Seconds before auto-redirect (0 = no auto-redirect)',
    },
  },
  args: {
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
    onRedirect: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof VerificationSuccess>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

/**
 * Default state - success with countdown
 */
export const Default: Story = {
  args: {
    email: undefined,
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
  },
};

/**
 * With email displayed
 */
export const WithEmail: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows the verified email address in a success badge',
      },
    },
  },
};

/**
 * No auto-redirect
 */
export const NoAutoRedirect: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 0,
  },
  parameters: {
    docs: {
      description: {
        story: 'User must click the button to continue - no countdown shown',
      },
    },
  },
};

// =============================================================================
// Countdown Variations
// =============================================================================

/**
 * Countdown at 5 seconds
 */
export const CountdownAt5: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 5,
  },
};

/**
 * Countdown at 10 seconds
 */
export const CountdownAt10: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 10,
  },
};

/**
 * Short countdown - 1 second
 */
export const CountdownAt1: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 1,
  },
  parameters: {
    docs: {
      description: {
        story: 'Very short countdown, almost immediately redirects',
      },
    },
  },
};

// =============================================================================
// Email Variations
// =============================================================================

/**
 * Long email address
 */
export const LongEmail: Story = {
  args: {
    email: 'verylongusername.with.many.parts@longdomainname.example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Test layout with a very long email address',
      },
    },
  },
};

/**
 * Short email address
 */
export const ShortEmail: Story = {
  args: {
    email: 'a@b.io',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
 * Mobile without email
 */
export const MobileNoEmail: Story = {
  args: {
    email: undefined,
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
 * Dark mode - with email
 */
export const DarkMode: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
 * Dark mode - without email
 */
export const DarkModeNoEmail: Story = {
  args: {
    email: undefined,
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
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
 * Dark mode on mobile
 */
export const DarkModeMobile: Story = {
  args: {
    email: 'user@example.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
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
// Real-World Scenarios
// =============================================================================

/**
 * OAuth verification success
 */
export const OAuthSuccess: Story = {
  args: {
    email: 'user.from.google@gmail.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'User verified email after OAuth registration',
      },
    },
  },
};

/**
 * Corporate email verified
 */
export const CorporateEmail: Story = {
  args: {
    email: 'john.smith@acmecorp.com',
    redirectUrl: '/library',
    autoRedirectSeconds: 5,
  },
  parameters: {
    docs: {
      description: {
        story: 'Corporate user verified their work email',
      },
    },
  },
};

/**
 * Re-verification success
 */
export const ReVerification: Story = {
  args: {
    email: 'returning.user@example.com',
    redirectUrl: '/settings',
    autoRedirectSeconds: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'User re-verified email from settings (redirects to settings)',
      },
    },
  },
};
