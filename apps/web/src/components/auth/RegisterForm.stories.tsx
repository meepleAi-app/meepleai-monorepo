/**
 * RegisterForm Storybook Stories (Issue #1496: E2E-010)
 *
 * Visual regression tests for Chromatic.
 * Covers: default, loading, error, validation, role selector, dark mode, mobile.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { RegisterForm } from './RegisterForm';

const meta = {
  title: 'Components/Auth/RegisterForm',
  component: RegisterForm,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Registration form with email, password, password confirmation, and optional display name. ' +
          'Includes password complexity validation, role selector (admin only), loading states, and i18n support.',
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
    showRoleSelector: {
      control: 'boolean',
      description: 'Show role selector (admin feature)',
    },
  },
  args: {
    onSubmit: fn(),
    onErrorDismiss: fn(),
    showRoleSelector: false,
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-6 bg-white dark:bg-slate-800 rounded-lg shadow-lg">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof RegisterForm>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Basic States
// =============================================================================

/**
 * Default state - empty registration form
 */
export const Default: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
};

/**
 * Loading state - account creation in progress
 */
export const Loading: Story = {
  args: {
    loading: true,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Submit button shows "Creazione account..." and all fields are disabled',
      },
    },
  },
};

// =============================================================================
// Error States
// =============================================================================

/**
 * Email already exists error
 */
export const EmailExistsError: Story = {
  args: {
    loading: false,
    error: 'Questo indirizzo email è già registrato. Effettua il login o usa un altro indirizzo.',
    showRoleSelector: false,
  },
};

/**
 * Weak password error
 */
export const WeakPasswordError: Story = {
  args: {
    loading: false,
    error: 'La password deve contenere almeno una lettera maiuscola, una minuscola e un numero.',
    showRoleSelector: false,
  },
};

/**
 * Network error during registration
 */
export const NetworkError: Story = {
  args: {
    loading: false,
    error: 'Errore di connessione durante la registrazione. Verifica la tua rete e riprova.',
    showRoleSelector: false,
  },
};

/**
 * Server error - internal server issue
 */
export const ServerError: Story = {
  args: {
    loading: false,
    error: "Errore del server durante la creazione dell'account. Riprova più tardi.",
    showRoleSelector: false,
  },
};

/**
 * Rate limit error - too many registration attempts
 */
export const RateLimitError: Story = {
  args: {
    loading: false,
    error: 'Troppi tentativi di registrazione. Attendi 15 minuti prima di riprovare.',
    showRoleSelector: false,
  },
};

// =============================================================================
// Validation States
// =============================================================================

/**
 * Password mismatch - confirmation doesn't match
 */
export const PasswordMismatchError: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Form validates that password and confirmPassword fields match. Shows inline validation error on confirmPassword field.',
      },
    },
  },
};

/**
 * Invalid email format
 */
export const InvalidEmailValidation: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Email field validates format on submit (e.g., missing @, invalid domain)',
      },
    },
  },
};

/**
 * Password too short - minimum 8 characters
 */
export const PasswordTooShortValidation: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Password must be at least 8 characters. Shows inline validation error.',
      },
    },
  },
};

/**
 * Display name too short - minimum 2 characters
 */
export const DisplayNameTooShort: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Display name is optional but must be at least 2 characters if provided.',
      },
    },
  },
};

// =============================================================================
// Role Selector States (Admin Feature)
// =============================================================================

/**
 * With role selector - admin creating user accounts
 */
export const WithRoleSelector: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Admin feature: Shows role dropdown (User, Editor, Admin) for account creation',
      },
    },
  },
};

/**
 * Role selector loading state
 */
export const RoleSelectorLoading: Story = {
  args: {
    loading: true,
    error: undefined,
    showRoleSelector: true,
  },
};

/**
 * Role selector with error
 */
export const RoleSelectorError: Story = {
  args: {
    loading: false,
    error: "Errore durante la creazione dell'account con il ruolo selezionato.",
    showRoleSelector: true,
  },
};

// =============================================================================
// Interactive States
// =============================================================================

/**
 * Form with pre-filled data (editing/autocomplete)
 */
export const PrefilledData: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
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
 * Disabled state - all fields disabled during submission
 */
export const Disabled: Story = {
  args: {
    loading: true,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'All form fields and submit button disabled during account creation',
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
    showRoleSelector: false,
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
 * Mobile with role selector
 */
export const MobileWithRoleSelector: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: true,
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
    showRoleSelector: false,
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
    error: 'Email già registrata',
    showRoleSelector: false,
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
 * Dark mode with role selector
 */
export const DarkModeWithRoleSelector: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: true,
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
      'Si è verificato un errore durante la creazione del tuo account. Il server potrebbe essere temporaneamente sovraccarico o la tua connessione potrebbe essersi interrotta. Verifica che tutti i campi siano compilati correttamente e riprova tra qualche minuto. Se il problema persiste, contatta il supporto tecnico.',
    showRoleSelector: false,
  },
};

/**
 * Long display name - test field max length
 */
export const LongDisplayName: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Display name maximum length is 50 characters. Validation shows error if exceeded.',
      },
    },
  },
};

// =============================================================================
// Real-World Scenarios
// =============================================================================

/**
 * Admin creating editor account
 */
export const AdminCreatingEditor: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Admin workflow: Creating an Editor account for content management',
      },
    },
  },
};

/**
 * New user self-registration
 */
export const SelfRegistration: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Standard user self-registration flow (no role selector, defaults to User role)',
      },
    },
  },
};

/**
 * Success state - account created
 */
export const Success: Story = {
  args: {
    loading: false,
    error: undefined,
    showRoleSelector: false,
  },
  parameters: {
    docs: {
      description: {
        story:
          'After successful registration, user is redirected to /chat or shown a success message',
      },
    },
  },
};
