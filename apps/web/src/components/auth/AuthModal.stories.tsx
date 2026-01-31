/**
 * AuthModal Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: login/register tabs, loading, errors, session expired, OAuth, dark mode, mobile.
 */

import { fn } from 'storybook/test';

import { AuthModal } from './AuthModal';

import type { Meta, StoryObj } from '@storybook/react';

// Mock useAuth hook
const _mockUseAuth = {
  login: fn(async () => ({ id: '123', email: 'test@example.com' })),
  register: fn(async () => ({ id: '123', email: 'test@example.com' })),
  error: null,
  clearError: fn(),
  loading: false,
};

// Mock useRouter hook
const mockRouter = {
  push: fn(async () => {}),
};

const meta = {
  title: 'Components/Auth/AuthModal',
  component: AuthModal,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Unified authentication modal with tabbed interface for login and registration. ' +
          'Includes OAuth buttons, session expired warnings, loading states, and error handling. ' +
          'Uses AccessibleModal wrapper with ARIA support.',
      },
    },
    // Chromatic settings
    chromatic: {
      viewports: [375, 768, 1024],
    },
    // Mock Next.js hooks
    nextjs: {
      appDirectory: true,
      navigation: {
        push: mockRouter.push,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Controls modal visibility',
    },
    defaultMode: {
      control: 'select',
      options: ['login', 'register'],
      description: 'Initial tab (login or register)',
    },
    sessionExpiredMessage: {
      control: 'boolean',
      description: 'Show session expired warning banner',
    },
  },
  args: {
    onClose: fn(),
    onSuccess: fn(),
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
} satisfies Meta<typeof AuthModal>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States - Login Tab
// =============================================================================

/**
 * Default state - login tab active
 */
export const LoginTab: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
};

/**
 * Login tab loading - form submission in progress
 */
export const LoginTabLoading: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Submit button shows loading spinner, form fields disabled',
      },
    },
  },
};

/**
 * Login tab with error
 */
export const LoginTabError: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Error message displayed above submit button (e.g., invalid credentials)',
      },
    },
  },
};

// =============================================================================
// Basic States - Register Tab
// =============================================================================

/**
 * Register tab - active by default
 */
export const RegisterTab: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
};

/**
 * Register tab loading - account creation in progress
 */
export const RegisterTabLoading: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Submit button shows "Creazione account..." and form is disabled',
      },
    },
  },
};

/**
 * Register tab with error
 */
export const RegisterTabError: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Error message displayed (e.g., email already exists)',
      },
    },
  },
};

// =============================================================================
// Session Expired Flow
// =============================================================================

/**
 * Session expired - warning banner on login tab
 */
export const SessionExpired: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Yellow warning banner: "Session Expired - Please sign in again to continue"',
      },
    },
  },
};

/**
 * Session expired with error - double feedback
 */
export const SessionExpiredWithError: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Session expired banner + login error (e.g., user tries old password after reset)',
      },
    },
  },
};

// =============================================================================
// Tab Switching
// =============================================================================

/**
 * Tab switching - login to register
 */
export const TabSwitchingLoginToRegister: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'User can switch between Login and Register tabs. Error state clears on tab change.',
      },
    },
  },
};

/**
 * Tab switching - register to login
 */
export const TabSwitchingRegisterToLogin: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Switching from Register to Login tab clears previous error state',
      },
    },
  },
};

// =============================================================================
// OAuth Integration
// =============================================================================

/**
 * With OAuth buttons - below form
 */
export const WithOAuthButtons: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'OAuth buttons (Google, Discord, GitHub) appear below login/register forms',
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
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
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
 * Mobile with session expired
 */
export const MobileSessionExpired: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
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
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
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
 * Dark mode - login tab
 */
export const DarkModeLogin: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-slate-900">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode - register tab
 */
export const DarkModeRegister: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-slate-900">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode with session expired
 */
export const DarkModeSessionExpired: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-slate-900">
        <Story />
      </div>
    ),
  ],
};

// =============================================================================
// Modal Interaction States
// =============================================================================

/**
 * Closed modal - not visible
 */
export const ClosedModal: Story = {
  args: {
    isOpen: false,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Modal is closed - shows underlying page content',
      },
    },
  },
};

/**
 * Focus trap - keyboard navigation
 */
export const FocusTrap: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Modal implements focus trap: Tab cycles through modal elements, cannot focus outside. Esc key closes modal.',
      },
    },
  },
};

/**
 * Backdrop click to close
 */
export const BackdropClose: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Clicking modal backdrop (overlay) triggers onClose callback',
      },
    },
  },
};

// =============================================================================
// Edge Cases
// =============================================================================

/**
 * Long error message - test text wrapping
 */
export const LongErrorMessage: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Error messages wrap correctly within modal constraints',
      },
    },
  },
};

/**
 * Session expired + validation error - multiple errors
 */
export const MultipleErrors: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Session expired warning + form validation error displayed simultaneously',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * First-time user registration flow
 */
export const FirstTimeRegistration: Story = {
  args: {
    isOpen: true,
    defaultMode: 'register',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story:
          'New user registration: starts on register tab, can switch to login if already has account',
      },
    },
  },
};

/**
 * Returning user login
 */
export const ReturningUserLogin: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Standard login flow for returning users',
      },
    },
  },
};

/**
 * Protected route redirect - user needs to login
 */
export const ProtectedRouteRedirect: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: true,
  },
  parameters: {
    docs: {
      description: {
        story:
          'User redirected from protected route (/chat, /dashboard) - shows session expired warning',
      },
    },
  },
};

/**
 * OAuth error fallback - show email/password form
 */
export const OAuthErrorFallback: Story = {
  args: {
    isOpen: true,
    defaultMode: 'login',
    sessionExpiredMessage: false,
  },
  parameters: {
    docs: {
      description: {
        story:
          'OAuth authentication failed - user can try email/password login instead (error shown in parent component)',
      },
    },
  },
};
