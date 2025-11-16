import type { Meta, StoryObj } from '@storybook/react';
import OAuthButtons from './OAuthButtons';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * OAuthButtons - Social authentication buttons for Google, Discord, and GitHub.
 * Provides consistent OAuth login UI with brand colors and icons.
 */
const meta = {
  title: 'Auth/OAuthButtons',
  component: OAuthButtons,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {},
  args: {
    onOAuthLogin: fn(),
  },
} satisfies Meta<typeof OAuthButtons>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default OAuth buttons (all providers)
 */
export const Default: Story = {
  args: {},
};

/**
 * Without callback (uses real OAuth URLs)
 */
export const WithoutCallback: Story = {
  args: {
    onOAuthLogin: undefined,
  },
};

/**
 * Interactive OAuth demo
 */
const InteractiveOAuthComponent = () => {
  const [selectedProvider, setSelectedProvider] = React.useState<string | null>(null);
  const [loginHistory, setLoginHistory] = React.useState<string[]>([]);

  const handleOAuthLogin = (provider: string) => {
    setSelectedProvider(provider);
    setLoginHistory([...loginHistory, provider]);

    // Simulate OAuth redirect
    setTimeout(() => {
      setSelectedProvider(null);
    }, 2000);
  };

  return (
    <div className="w-full max-w-md space-y-4">
      <div className="text-center mb-6">
        <h3 className="text-lg font-semibold">OAuth Login</h3>
        <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
          Click a button to simulate OAuth login
        </p>
      </div>

      <OAuthButtons onOAuthLogin={handleOAuthLogin} />

      {selectedProvider && (
        <div className="rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-4 text-center">
          <p className="text-sm text-blue-800 dark:text-blue-200">
            Redirecting to {selectedProvider.charAt(0).toUpperCase() + selectedProvider.slice(1)}...
          </p>
        </div>
      )}

      {loginHistory.length > 0 && (
        <div className="rounded-md bg-slate-50 dark:bg-slate-900/50 border border-slate-200 dark:border-slate-700 p-3">
          <p className="text-xs font-medium mb-2">Login attempts:</p>
          <ul className="text-xs space-y-0.5">
            {loginHistory.map((provider, idx) => (
              <li key={idx}>
                {idx + 1}. {provider.charAt(0).toUpperCase() + provider.slice(1)}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveOAuthComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Full login page with OAuth
 */
const FullLoginPageComponent = () => {
  const [selectedProvider, setSelectedProvider] = React.useState<string | null>(null);

  const handleOAuthLogin = (provider: string) => {
    setSelectedProvider(provider);
    setTimeout(() => setSelectedProvider(null), 2000);
  };

  const handleEmailLogin = (e: React.FormEvent) => {
    e.preventDefault();
    // Handle email login
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
      <div className="w-full max-w-md">
        <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-8">
          <div className="text-center mb-8">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">MeepleAI</h1>
            <p className="text-slate-600 dark:text-slate-400 mt-2">Sign in to your account</p>
          </div>

          <form onSubmit={handleEmailLogin} className="space-y-4 mb-6">
            <div>
              <label className="block text-sm font-medium mb-2">Email</label>
              <input
                type="email"
                className="w-full px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-700 text-slate-900 dark:text-white"
                placeholder="you@example.com"
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-2">Password</label>
              <input
                type="password"
                className="w-full px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-700 text-slate-900 dark:text-white"
                placeholder="••••••••"
              />
            </div>
            <button
              type="submit"
              className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
            >
              Sign In
            </button>
          </form>

          <OAuthButtons onOAuthLogin={handleOAuthLogin} />

          {selectedProvider && (
            <div className="mt-4 rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3 text-center">
              <p className="text-sm text-blue-800 dark:text-blue-200">
                Redirecting to {selectedProvider}...
              </p>
            </div>
          )}

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

export const FullLoginPage: Story = {
  render: () => <FullLoginPageComponent />,
  parameters: {
    layout: 'fullscreen',
  },
};

/**
 * OAuth only (no email/password)
 */
const OAuthOnlyComponent = () => {
  const [selectedProvider, setSelectedProvider] = React.useState<string | null>(null);

  const handleOAuthLogin = (provider: string) => {
    setSelectedProvider(provider);
    setTimeout(() => setSelectedProvider(null), 2000);
  };

  return (
    <div className="w-full max-w-md">
      <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-8">
        <div className="text-center mb-8">
          <h2 className="text-2xl font-bold text-slate-900 dark:text-white">Welcome</h2>
          <p className="text-slate-600 dark:text-slate-400 mt-2">
            Choose a provider to continue
          </p>
        </div>

        <div className="space-y-3">
          <button
            onClick={() => handleOAuthLogin('google')}
            className="w-full flex items-center justify-center gap-3 px-4 py-3 bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-600 rounded-lg text-slate-700 dark:text-slate-200 font-medium hover:bg-slate-50 dark:hover:bg-slate-600"
          >
            <svg className="w-5 h-5" viewBox="0 0 24 24">
              <path
                fill="#4285F4"
                d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
              />
              <path
                fill="#34A853"
                d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
              />
              <path
                fill="#FBBC05"
                d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
              />
              <path
                fill="#EA4335"
                d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
              />
            </svg>
            Continue with Google
          </button>

          <button
            onClick={() => handleOAuthLogin('discord')}
            className="w-full flex items-center justify-center gap-3 px-4 py-3 bg-[#5865F2] hover:bg-[#4752C4] rounded-lg text-white font-medium"
          >
            <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
              <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515a.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0a12.64 12.64 0 0 0-.617-1.25a.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057a19.9 19.9 0 0 0 5.993 3.03a.078.078 0 0 0 .084-.028a14.09 14.09 0 0 0 1.226-1.994a.076.076 0 0 0-.041-.106a13.107 13.107 0 0 1-1.872-.892a.077.077 0 0 1-.008-.128a10.2 10.2 0 0 0 .372-.292a.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127a12.299 12.299 0 0 1-1.873.892a.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028a19.839 19.839 0 0 0 6.002-3.03a.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419c0-1.333.956-2.419 2.157-2.419c1.21 0 2.176 1.096 2.157 2.42c0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419c0-1.333.955-2.419 2.157-2.419c1.21 0 2.176 1.096 2.157 2.42c0 1.333-.946 2.418-2.157 2.418z" />
            </svg>
            Continue with Discord
          </button>

          <button
            onClick={() => handleOAuthLogin('github')}
            className="w-full flex items-center justify-center gap-3 px-4 py-3 bg-slate-900 dark:bg-slate-200 hover:bg-slate-800 dark:hover:bg-slate-300 rounded-lg text-white dark:text-slate-900 font-medium"
          >
            <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
              <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
            </svg>
            Continue with GitHub
          </button>
        </div>

        {selectedProvider && (
          <div className="mt-6 rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3 text-center">
            <p className="text-sm text-blue-800 dark:text-blue-200">
              Redirecting to {selectedProvider}...
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export const OAuthOnly: Story = {
  render: () => <OAuthOnlyComponent />,
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
};
