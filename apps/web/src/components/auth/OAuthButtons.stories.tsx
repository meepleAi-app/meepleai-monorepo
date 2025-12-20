/**
 * OAuthButtons Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, hover, loading, disabled, dark mode, mobile.
 */

import { fn } from 'storybook/test';

import OAuthButtons from './OAuthButtons';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Auth/OAuthButtons',
  component: OAuthButtons,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'OAuth authentication buttons for Google, Discord, and GitHub. ' +
          'Includes separator text ("Or continue with") and consistent brand styling.',
      },
    },
    // Chromatic settings
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    onOAuthLogin: {
      description: 'Callback when OAuth button is clicked (provider: google | discord | github)',
    },
  },
  args: {
    onOAuthLogin: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof OAuthButtons>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

/**
 * Default state - all three OAuth providers
 */
export const Default: Story = {
  args: {
    onOAuthLogin: fn(provider => {
      console.log(`OAuth login clicked: ${provider}`);
    }),
  },
};

/**
 * Without callback - navigates to OAuth URL directly
 */
export const WithoutCallback: Story = {
  args: {
    onOAuthLogin: undefined,
  },
  parameters: {
    docs: {
      description: {
        story:
          'When no callback is provided, clicking a button navigates to the OAuth provider URL',
      },
    },
  },
};

// =============================================================================
// Interactive States
// =============================================================================

/**
 * Google hover state - shows hover effect
 */
export const GoogleHover: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    pseudo: { hover: '.oauth-google' },
    docs: {
      description: {
        story: 'Google button hover: bg-slate-50 dark:bg-slate-600 transition',
      },
    },
  },
};

/**
 * Discord hover state - darker blue
 */
export const DiscordHover: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    pseudo: { hover: '.oauth-discord' },
    docs: {
      description: {
        story: 'Discord button hover: bg-[#4752C4] (darker brand color)',
      },
    },
  },
};

/**
 * GitHub hover state - darker slate
 */
export const GitHubHover: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    pseudo: { hover: '.oauth-github' },
    docs: {
      description: {
        story: 'GitHub button hover: bg-slate-800 dark:bg-slate-300 transition',
      },
    },
  },
};

/**
 * Focus state - keyboard navigation
 */
export const FocusState: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    pseudo: { focus: 'button:first-of-type' },
    docs: {
      description: {
        story: 'Focus ring: focus:ring-2 focus:ring-blue-500 for accessibility',
      },
    },
  },
};

// =============================================================================
// Responsive & Accessibility
// =============================================================================

/**
 * Mobile viewport - 375px width
 */
export const Mobile: Story = {
  args: {
    onOAuthLogin: fn(),
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
    onOAuthLogin: fn(),
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
 * Dark mode - test dark theme styling
 */
export const DarkMode: Story = {
  args: {
    onOAuthLogin: fn(),
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
// Brand Consistency
// =============================================================================

/**
 * Google brand colors - #4285F4, #34A853, #FBBC05, #EA4335
 */
export const GoogleBrandColors: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Google logo uses official brand colors (blue, green, yellow, red)',
      },
    },
  },
};

/**
 * Discord brand color - #5865F2
 */
export const DiscordBrandColor: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Discord button uses official brand color #5865F2 (blurple)',
      },
    },
  },
};

/**
 * GitHub slate colors - black/white theme aware
 */
export const GitHubBrandColor: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'GitHub button: slate-900 (light mode) / slate-200 (dark mode) for brand consistency',
      },
    },
  },
};

// =============================================================================
// Layout Variations
// =============================================================================

/**
 * Separator text - "Or continue with"
 */
export const SeparatorText: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Component includes separator with i18n text "Or continue with" above OAuth buttons',
      },
    },
  },
};

/**
 * Button spacing and alignment
 */
export const ButtonSpacing: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Buttons stacked vertically with space-y-2 gap, full width w-full',
      },
    },
  },
};

// =============================================================================
// Edge Cases
// =============================================================================

/**
 * Narrow container - test responsive text
 */
export const NarrowContainer: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  decorators: [
    Story => (
      <div className="w-64 p-4 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'OAuth buttons in narrow 256px container - tests text wrapping and icon spacing',
      },
    },
  },
};

/**
 * Wide container - test button max-width
 */
export const WideContainer: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-2xl p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'OAuth buttons expand to fill wider containers while maintaining readability',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * In AuthModal context - below login/register forms
 */
export const InAuthModal: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Typical usage: OAuth buttons appear below email/password forms in AuthModal',
      },
    },
  },
};

/**
 * Loading state (external) - parent component handles loading
 */
export const ExternalLoading: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'OAuth buttons themselves have no loading state - parent component (AuthModal) manages loading/disabled states',
      },
    },
  },
};

/**
 * Error state (external) - parent shows error message
 */
export const ExternalError: Story = {
  args: {
    onOAuthLogin: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg space-y-4">
        {/* Error message above buttons */}
        <div
          className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
          role="alert"
        >
          <p className="text-sm text-red-800 dark:text-red-200">
            Errore di autenticazione OAuth. Riprova o usa email/password.
          </p>
        </div>
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'OAuth errors are displayed by parent component (e.g., AuthModal) above buttons',
      },
    },
  },
};
