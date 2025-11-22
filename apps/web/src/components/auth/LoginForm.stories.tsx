import type { Meta, StoryObj } from '@storybook/react';
import { LoginForm, type LoginFormData } from './LoginForm';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * LoginForm - Reusable login form with React Hook Form and Zod validation.
 * Features accessible form controls, loading states, and custom error display.
 */
const meta = {
  title: 'Auth/LoginForm',
  component: LoginForm,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    loading: {
      control: 'boolean',
      description: 'Loading state (disables form)',
    },
    error: {
      control: 'text',
      description: 'Error message to display',
    },
    initialEmail: {
      control: 'text',
      description: 'Initial email value',
    },
    initialPassword: {
      control: 'text',
      description: 'Initial password value',
    },
  },
  args: {
    onSubmit: fn(),
    onErrorDismiss: fn(),
  },
} satisfies Meta<typeof LoginForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default login form (empty)
 */
export const Default: Story = {
  args: {},
};

/**
 * With initial values
 */
export const WithInitialValues: Story = {
  args: {
    initialEmail: 'user@example.com',
    initialPassword: 'password123',
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    loading: true,
    initialEmail: 'user@example.com',
    initialPassword: 'password123',
  },
};

/**
 * With error message
 */
export const WithError: Story = {
  args: {
    error: 'Invalid email or password. Please try again.',
  },
};

/**
 * With network error
 */
export const NetworkError: Story = {
  args: {
    error: 'Network connection failed. Please check your internet connection and try again.',
  },
};

/**
 * With server error
 */
export const ServerError: Story = {
  args: {
    error: 'Server error. Our team has been notified. Please try again later.',
    initialEmail: 'user@example.com',
  },
};

/**
 * Interactive form with validation
 */
const InteractiveFormComponent = () => {
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const [successMessage, setSuccessMessage] = React.useState<string | undefined>();

  const handleSubmit = async (data: LoginFormData) => {
    setIsLoading(true);
    setError(undefined);
    setSuccessMessage(undefined);

    // Simulate API call
    await new Promise((resolve) => setTimeout(resolve, 1500));

    // Simulate validation (test credentials for Storybook only)
    if (data.email === 'demo@meepleai.dev' && data.password === 'TestPassword123!') {
      setSuccessMessage(`Successfully logged in as ${data.email}!`);
    } else {
      setError('Invalid email or password. Try: demo@meepleai.dev / TestPassword123!');
    }

    setIsLoading(false);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h2 className="text-2xl font-bold">Sign In</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-2">
          Try: demo@meepleai.dev / TestPassword123!
        </p>
      </div>

      <LoginForm
        onSubmit={handleSubmit}
        loading={isLoading}
        error={error}
        onErrorDismiss={() => setError(undefined)}
      />

      {successMessage && (
        <div className="rounded-md bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 p-3">
          <p className="text-sm text-green-800 dark:text-green-200">{successMessage}</p>
        </div>
      )}
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveFormComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Full page login example
 */
const FullPageLoginComponent = () => {
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();

  const handleSubmit = async (data: LoginFormData) => {
    setIsLoading(true);
    setError(undefined);

    await new Promise((resolve) => setTimeout(resolve, 2000));

    if (data.email !== 'demo@meepleai.dev') {
      setError('Account not found. Please check your email or create a new account.');
    } else if (data.password !== 'TestPassword123!') {
      setError('Incorrect password. Please try again.');
    }

    setIsLoading(false);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
      <div className="w-full max-w-md">
        <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-8">
          <div className="text-center mb-8">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">MeepleAI</h1>
            <p className="text-slate-600 dark:text-slate-400 mt-2">
              Sign in to your account
            </p>
          </div>

          <LoginForm
            onSubmit={handleSubmit}
            loading={isLoading}
            error={error}
            onErrorDismiss={() => setError(undefined)}
          />

          <div className="mt-6 text-center">
            <p className="text-sm text-slate-600 dark:text-slate-400">
              Don&apos;t have an account?{' '}
              <button className="text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300 font-medium">
                Sign up
              </button>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export const FullPageLogin: Story = {
  render: () => <FullPageLoginComponent />,
  parameters: {
    layout: 'fullscreen',
  },
};

/**
 * Form validation demo
 */
const ValidationDemoComponent = () => {
  const [attemptedEmails, setAttemptedEmails] = React.useState<string[]>([]);

  const handleSubmit = async (data: LoginFormData) => {
    setAttemptedEmails([...attemptedEmails, data.email]);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h3 className="text-lg font-semibold">Try Invalid Inputs</h3>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
          Test validation: short password, invalid email, etc.
        </p>
      </div>

      <LoginForm onSubmit={handleSubmit} />

      {attemptedEmails.length > 0 && (
        <div className="rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3">
          <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">
            Form submissions:
          </p>
          <ul className="text-xs text-blue-700 dark:text-blue-300 space-y-1">
            {attemptedEmails.map((email, idx) => (
              <li key={idx}>
                {idx + 1}. {email}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export const ValidationDemo: Story = {
  render: () => <ValidationDemoComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <div className="bg-slate-800 p-8 rounded-lg">
          <Story />
        </div>
      </div>
    ),
  ],
  args: {
    error: 'Invalid credentials. Please try again.',
    initialEmail: 'user@example.com',
  },
};
