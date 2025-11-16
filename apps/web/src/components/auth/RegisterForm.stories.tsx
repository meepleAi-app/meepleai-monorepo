import type { Meta, StoryObj } from '@storybook/react';
import { RegisterForm, type RegisterFormData } from './RegisterForm';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * RegisterForm - Reusable registration form with React Hook Form and Zod validation.
 * Features password confirmation, role selector, accessible controls, and loading states.
 */
const meta = {
  title: 'Auth/RegisterForm',
  component: RegisterForm,
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
    showRoleSelector: {
      control: 'boolean',
      description: 'Show role selector dropdown',
    },
  },
  args: {
    onSubmit: fn(),
    onErrorDismiss: fn(),
  },
} satisfies Meta<typeof RegisterForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default registration form
 */
export const Default: Story = {
  args: {},
};

/**
 * With role selector (admin view)
 */
export const WithRoleSelector: Story = {
  args: {
    showRoleSelector: true,
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    loading: true,
  },
};

/**
 * With error message
 */
export const WithError: Story = {
  args: {
    error: 'Email already exists. Please use a different email or sign in.',
  },
};

/**
 * With validation error
 */
export const ValidationError: Story = {
  args: {
    error: 'Password must contain at least one uppercase letter, one lowercase letter, and one number.',
  },
};

/**
 * With server error
 */
export const ServerError: Story = {
  args: {
    error: 'Server error. Unable to create account. Please try again later.',
  },
};

/**
 * Interactive form with validation
 */
const InteractiveFormComponent = () => {
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const [successMessage, setSuccessMessage] = React.useState<string | undefined>();
  const [registeredUsers, setRegisteredUsers] = React.useState<string[]>([]);

  const handleSubmit = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    setIsLoading(true);
    setError(undefined);
    setSuccessMessage(undefined);

    // Simulate API call
    await new Promise((resolve) => setTimeout(resolve, 2000));

    // Simulate email already exists
    if (registeredUsers.includes(data.email)) {
      setError('Email already exists. Please use a different email.');
      setIsLoading(false);
      return;
    }

    // Success
    setRegisteredUsers([...registeredUsers, data.email]);
    setSuccessMessage(
      `Account created successfully for ${data.email}!${
        data.displayName ? ` Welcome, ${data.displayName}!` : ''
      }`
    );
    setIsLoading(false);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h2 className="text-2xl font-bold">Create Account</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-2">
          Fill out the form to register
        </p>
      </div>

      <RegisterForm
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

      {registeredUsers.length > 0 && (
        <div className="rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3">
          <p className="text-xs font-medium text-blue-800 dark:text-blue-200 mb-1">
            Registered emails:
          </p>
          <ul className="text-xs text-blue-700 dark:text-blue-300 space-y-0.5">
            {registeredUsers.map((email, idx) => (
              <li key={idx}>{email}</li>
            ))}
          </ul>
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
 * Full page registration example
 */
const FullPageRegisterComponent = () => {
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();

  const handleSubmit = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    setIsLoading(true);
    setError(undefined);

    await new Promise((resolve) => setTimeout(resolve, 2000));

    // Simulate demo account conflict
    if (data.email === 'demo@meepleai.dev') {
      setError('This email is reserved for demo purposes. Please use a different email.');
    }

    setIsLoading(false);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
      <div className="w-full max-w-md">
        <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-8">
          <div className="text-center mb-8">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">MeepleAI</h1>
            <p className="text-slate-600 dark:text-slate-400 mt-2">Create your account</p>
          </div>

          <RegisterForm
            onSubmit={handleSubmit}
            loading={isLoading}
            error={error}
            onErrorDismiss={() => setError(undefined)}
          />

          <div className="mt-6 text-center">
            <p className="text-sm text-slate-600 dark:text-slate-400">
              Already have an account?{' '}
              <button className="text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300 font-medium">
                Sign in
              </button>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export const FullPageRegister: Story = {
  render: () => <FullPageRegisterComponent />,
  parameters: {
    layout: 'fullscreen',
  },
};

/**
 * Password validation demo
 */
const PasswordValidationComponent = () => {
  const [validationAttempts, setValidationAttempts] = React.useState<
    Array<{ password: string; valid: boolean }>
  >([]);

  const handleSubmit = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/;
    const isValid = passwordRegex.test(data.password);

    setValidationAttempts([
      ...validationAttempts,
      { password: data.password.replace(/./g, '•'), valid: isValid },
    ]);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h3 className="text-lg font-semibold">Password Validation Demo</h3>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
          Try: weak passwords, mismatched passwords, valid passwords
        </p>
      </div>

      <RegisterForm onSubmit={handleSubmit} />

      {validationAttempts.length > 0 && (
        <div className="rounded-md bg-slate-50 dark:bg-slate-900/50 border border-slate-200 dark:border-slate-700 p-3">
          <p className="text-sm font-medium mb-2">Validation attempts:</p>
          <ul className="text-xs space-y-1">
            {validationAttempts.map((attempt, idx) => (
              <li
                key={idx}
                className={
                  attempt.valid
                    ? 'text-green-600 dark:text-green-400'
                    : 'text-red-600 dark:text-red-400'
                }
              >
                {attempt.password} - {attempt.valid ? '✓ Valid' : '✗ Invalid'}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export const PasswordValidation: Story = {
  render: () => <PasswordValidationComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Admin registration with role selector
 */
const AdminRegisterComponent = () => {
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState<string | undefined>();
  const [createdUsers, setCreatedUsers] = React.useState<
    Array<{ email: string; role: string }>
  >([]);

  const handleSubmit = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    setIsLoading(true);
    setError(undefined);

    await new Promise((resolve) => setTimeout(resolve, 1500));

    setCreatedUsers([...createdUsers, { email: data.email, role: data.role }]);
    setIsLoading(false);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h3 className="text-lg font-semibold">Admin User Creation</h3>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
          Create users with specific roles
        </p>
      </div>

      <RegisterForm
        onSubmit={handleSubmit}
        loading={isLoading}
        error={error}
        onErrorDismiss={() => setError(undefined)}
        showRoleSelector={true}
      />

      {createdUsers.length > 0 && (
        <div className="rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3">
          <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">
            Created users:
          </p>
          <ul className="text-xs text-blue-700 dark:text-blue-300 space-y-1">
            {createdUsers.map((user, idx) => (
              <li key={idx}>
                {user.email} - <span className="font-medium">{user.role}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export const AdminRegister: Story = {
  render: () => <AdminRegisterComponent />,
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
    error: 'Email already exists. Please use a different email.',
    showRoleSelector: true,
  },
};
