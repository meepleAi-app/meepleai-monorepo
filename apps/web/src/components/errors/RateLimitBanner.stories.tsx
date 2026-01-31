import { fn } from 'storybook/test';

import { RateLimitBanner } from './RateLimitBanner';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Rate Limit Banner - Alert banner for API rate limit messages
 *
 * ## Features
 * - **Countdown Timer**: Shows remaining seconds until retry available
 * - **User-friendly Message**: Clear explanation of rate limit
 * - **Dismissible**: Optional close button
 * - **Accessibility**: ARIA live region for screen readers
 * - **Auto-hide**: Returns null when remainingSeconds <= 0
 *
 * ## Usage
 * Display when API rate limits are encountered to inform users.
 */
const meta = {
  title: 'Errors/RateLimitBanner',
  component: RateLimitBanner,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    message: {
      control: 'text',
      description: 'User-friendly rate limit message',
    },
    remainingSeconds: {
      control: { type: 'number', min: 0, max: 120 },
      description: 'Seconds until retry available',
    },
    showCountdown: {
      control: 'boolean',
      description: 'Show countdown timer',
    },
    dismissible: {
      control: 'boolean',
      description: 'Allow banner dismissal',
    },
    onDismiss: {
      action: 'dismissed',
      description: 'Callback when dismissed',
    },
  },
} satisfies Meta<typeof RateLimitBanner>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default rate limit banner with countdown.
 * Shows standard rate limit message with 60s countdown.
 */
export const Default: Story = {
  args: {
    message: 'You have made too many requests. Please wait before trying again.',
    remainingSeconds: 60,
    showCountdown: true,
    dismissible: false,
  },
};

/**
 * Rate limit with longer countdown (120s).
 * Shows when user hits stricter rate limits.
 */
export const LongCountdown: Story = {
  args: {
    message: 'Rate limit exceeded for API requests. Please wait 2 minutes before retrying.',
    remainingSeconds: 120,
    showCountdown: true,
    dismissible: false,
  },
};

/**
 * Rate limit about to expire (5s remaining).
 * Shows final seconds countdown.
 */
export const AboutToExpire: Story = {
  args: {
    message: 'Rate limit almost expired. Retry available soon.',
    remainingSeconds: 5,
    showCountdown: true,
    dismissible: false,
  },
};

/**
 * Dismissible rate limit banner.
 * User can close the banner manually.
 */
export const Dismissible: Story = {
  args: {
    message: 'You have exceeded the rate limit. Please slow down your requests.',
    remainingSeconds: 45,
    showCountdown: true,
    dismissible: true,
    onDismiss: fn(),
  },
};

/**
 * Rate limit without countdown display.
 * Shows message only, no timer.
 */
export const WithoutCountdown: Story = {
  args: {
    message: 'Too many requests. Please wait a moment before retrying.',
    remainingSeconds: 30,
    showCountdown: false,
    dismissible: false,
  },
};

/**
 * Custom message for specific endpoint.
 * Shows endpoint-specific rate limit.
 */
export const CustomMessage: Story = {
  args: {
    message: 'Chat API rate limit reached. You can send 10 messages per minute.',
    remainingSeconds: 42,
    showCountdown: true,
    dismissible: true,
    onDismiss: fn(),
  },
};

/**
 * Hidden state (remainingSeconds = 0).
 * Banner does not render when countdown expires.
 */
export const Hidden: Story = {
  args: {
    message: 'This should not be visible',
    remainingSeconds: 0,
    showCountdown: true,
    dismissible: false,
  },
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
  },
  args: {
    message: 'Rate limit exceeded. Please wait before trying again.',
    remainingSeconds: 30,
    showCountdown: true,
    dismissible: true,
    onDismiss: fn(),
  },
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
  },
  args: {
    message: 'You have made too many requests. Please wait 60 seconds.',
    remainingSeconds: 60,
    showCountdown: true,
    dismissible: false,
  },
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
  },
  args: {
    message: 'API rate limit exceeded. Requests will be throttled for 90 seconds.',
    remainingSeconds: 90,
    showCountdown: true,
    dismissible: true,
    onDismiss: fn(),
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  args: {
    message: 'Too many requests detected. Please wait before continuing.',
    remainingSeconds: 45,
    showCountdown: true,
    dismissible: true,
    onDismiss: fn(),
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
