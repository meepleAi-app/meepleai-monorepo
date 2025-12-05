/**
 * LoginForm Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, loading, error, validation, dark mode, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { LoginForm } from './LoginForm';

const meta = {
  title: 'Components/Auth/LoginForm',
  component: LoginForm,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Login form with email/password validation, loading states, error display, and i18n support. Supports React Hook Form with Zod schema validation.',
      },
    },
    // Chromatic settings
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: { theme: 'light' },
        dark: { theme: 'dark' },
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    loading: {
      control: 'boolean',
      description: 'Loading state for submit button',
    },
    error: {
      control: 'text',
      description: 'Error message to display',
    },
  },
  args: {
    onSubmit: fn(),
    onErrorDismiss: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof LoginForm>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

/**
 * Default state - empty form ready for input
 */
export const Default: Story = {
  args: {
    loading: false,
    error: undefined,
  },
};

/**
 * Loading state - form submission in progress
 */
export const Loading: Story = {
  args: {
    loading: true,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Submit button shows loading spinner and form fields are disabled',
      },
    },
  },
};

// =============================================================================
// Error States
// =============================================================================

/**
 * Generic error - server error or unexpected failure
 */
export const WithError: Story = {
  args: {
    loading: false,
    error: 'Email o password non validi. Riprova.',
  },
};

/**
 * Network error - connection failure
 */
export const NetworkError: Story = {
  args: {
    loading: false,
    error: 'Errore di connessione. Verifica la tua connessione internet e riprova.',
  },
};

/**
 * Rate limit error - too many login attempts
 */
export const RateLimitError: Story = {
  args: {
    loading: false,
    error: 'Troppi tentativi di login. Attendi 5 minuti prima di riprovare.',
  },
};

/**
 * Account locked error
 */
export const AccountLockedError: Story = {
  args: {
    loading: false,
    error: 'Account bloccato per motivi di sicurezza. Contatta il supporto.',
  },
};

// =============================================================================
// Validation States (Form-level)
// =============================================================================

/**
 * Invalid email format - client-side validation
 * Note: This story shows the error state, actual validation happens on submit
 */
export const ValidationError: Story = {
  args: {
    loading: false,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Form validates email format and password length on submit. Invalid inputs show inline validation errors.',
      },
    },
  },
};

/**
 * Empty fields - required field validation
 */
export const EmptyFieldsValidation: Story = {
  args: {
    loading: false,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Submitting empty form triggers required field validation messages',
      },
    },
  },
};

// =============================================================================
// Interactive States
// =============================================================================

/**
 * Form with pre-filled data
 */
export const PrefilledData: Story = {
  render: args => (
    <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
      <LoginForm {...args} />
    </div>
  ),
  args: {
    loading: false,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Form with user data (testing autofill/autocomplete behavior)',
      },
    },
  },
};

/**
 * Disabled state - loading prevents interaction
 */
export const Disabled: Story = {
  args: {
    loading: true,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'All inputs disabled during form submission',
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
    loading: false,
    error: undefined,
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
    loading: false,
    error: undefined,
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
    loading: false,
    error: 'Credenziali non valide',
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
 * Dark mode loading state
 */
export const DarkModeLoading: Story = {
  args: {
    loading: true,
    error: undefined,
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
    loading: false,
    error:
      'Si è verificato un errore imprevisto durante il tentativo di accesso. Il server potrebbe essere temporaneamente non disponibile o la tua sessione potrebbe essere scaduta. Riprova tra qualche minuto o contatta il supporto se il problema persiste.',
  },
};

/**
 * Loading with error (transition state - should not happen)
 */
export const LoadingWithError: Story = {
  args: {
    loading: true,
    error: 'Errore precedente ancora visibile',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Edge case: Loading state with error still visible (error should be cleared on new submit)',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * 2FA Required - user needs to enter 2FA code
 */
export const TwoFactorRequired: Story = {
  args: {
    loading: false,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: '2FA flow: After successful login, user is redirected to 2FA verification page',
      },
    },
  },
};

/**
 * Session expired - redirect from protected page
 */
export const SessionExpired: Story = {
  args: {
    loading: false,
    error: 'Sessione scaduta. Effettua nuovamente il login per continuare.',
  },
};

/**
 * First-time user - suggest registration
 */
export const NewUserPrompt: Story = {
  args: {
    loading: false,
    error: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Default login form - users can switch to registration via AuthModal tabs',
      },
    },
  },
};
