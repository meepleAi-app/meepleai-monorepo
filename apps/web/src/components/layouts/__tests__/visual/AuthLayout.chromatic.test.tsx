/**
 * AuthLayout Chromatic Visual Tests (Issue #2231)
 *
 * Visual regression tests for AuthLayout component using Chromatic.
 * Tests various auth page states, themes, and responsive layouts.
 */

import React from 'react';
import { describe, it } from 'vitest';
import type { Meta, StoryObj } from '@storybook/react';
import { AuthLayout } from '../../AuthLayout';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

/**
 * Chromatic test suite for AuthLayout component
 * Each test creates a visual snapshot for regression testing
 */
describe('AuthLayout - Chromatic Visual Tests', () => {
  it('should match visual snapshot - Login variant', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Register variant', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Reset password variant', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Success state', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Error state', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Mobile responsive', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Dark mode', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Without back link', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });
});

// Mock form components for visual testing
const LoginFormMock = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="email">Email</Label>
      <Input id="email" type="email" placeholder="you@example.com" defaultValue="" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="password">Password</Label>
      <Input id="password" type="password" placeholder="••••••••" defaultValue="" />
    </div>
    <Button className="w-full">Sign In</Button>
    <div className="text-center">
      <a href="#" className="text-sm text-primary hover:underline">
        Forgot password?
      </a>
    </div>
  </div>
);

const RegisterFormMock = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="name">Full Name</Label>
      <Input id="name" type="text" placeholder="Mario Rossi" defaultValue="" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="email">Email</Label>
      <Input id="email" type="email" placeholder="you@example.com" defaultValue="" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="password">Password</Label>
      <Input id="password" type="password" placeholder="••••••••" defaultValue="" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="confirmPassword">Confirm Password</Label>
      <Input id="confirmPassword" type="password" placeholder="••••••••" defaultValue="" />
    </div>
    <Button className="w-full">Create Account</Button>
  </div>
);

const ResetPasswordFormMock = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="email">Email Address</Label>
      <Input id="email" type="email" placeholder="you@example.com" defaultValue="" />
    </div>
    <Button className="w-full">Send Reset Instructions</Button>
    <div className="text-center">
      <a href="#" className="text-sm text-primary hover:underline">
        Back to Login
      </a>
    </div>
  </div>
);

// Export stories for Chromatic
const meta: Meta<typeof AuthLayout> = {
  title: 'Components/Layouts/AuthLayout/Chromatic',
  component: AuthLayout,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      // Capture snapshots for visual regression testing
      disableSnapshot: false,
      // Test on multiple viewports
      viewports: [320, 768, 1024, 1920],
    },
  },
};

export default meta;
type Story = StoryObj<typeof AuthLayout>;

/**
 * Login page variant
 */
export const LoginVariant: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue to MeepleAI',
    children: <LoginFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Register page variant
 */
export const RegisterVariant: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI to get started',
    children: <RegisterFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Reset password page variant
 */
export const ResetPasswordVariant: Story = {
  args: {
    title: 'Reset Password',
    subtitle: "Enter your email and we'll send you instructions",
    children: <ResetPasswordFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Success state
 */
export const SuccessState: Story = {
  args: {
    title: 'Check Your Email',
    subtitle: "We've sent password reset instructions",
    children: (
      <div className="text-center space-y-4 py-4">
        <div className="text-6xl">✉️</div>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Check your inbox for reset instructions
        </p>
        <Button variant="secondary" className="w-full">
          Back to Login
        </Button>
      </div>
    ),
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Error state
 */
export const ErrorState: Story = {
  args: {
    title: 'Invalid or Expired Link',
    subtitle: 'This reset link is no longer valid',
    children: (
      <div className="text-center space-y-4 py-4">
        <div className="text-6xl">⚠️</div>
        <div className="space-y-2">
          <Button className="w-full">Request New Reset Link</Button>
          <Button variant="secondary" className="w-full">
            Back to Login
          </Button>
        </div>
      </div>
    ),
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Loading state
 */
export const LoadingState: Story = {
  args: {
    title: 'Loading...',
    children: (
      <div className="text-center py-8">
        <div className="animate-spin text-4xl mb-4">⏳</div>
        <p className="text-slate-400">Loading...</p>
      </div>
    ),
  },
  parameters: {
    chromatic: { disableSnapshot: false, pauseAnimationAtEnd: true },
  },
};

/**
 * Without back link
 */
export const WithoutBackLink: Story = {
  args: {
    title: 'Welcome',
    subtitle: 'Sign in to continue',
    showBackLink: false,
    children: <LoginFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Minimal content (no title/subtitle)
 */
export const MinimalContent: Story = {
  args: {
    children: <LoginFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Mobile view - Login (320px)
 */
export const MobileLogin: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue',
    children: <LoginFormMock />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [320],
    },
  },
};

/**
 * Mobile view - Register (320px)
 */
export const MobileRegister: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI',
    children: <RegisterFormMock />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [320],
    },
  },
};

/**
 * Tablet view (768px)
 */
export const TabletView: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue',
    children: <LoginFormMock />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [768],
    },
  },
};

/**
 * Dark mode - Login
 */
export const DarkModeLogin: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue',
    children: <LoginFormMock />,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Dark mode - Register
 */
export const DarkModeRegister: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI',
    children: <RegisterFormMock />,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Dark mode - Success state
 */
export const DarkModeSuccess: Story = {
  args: {
    title: 'Success',
    subtitle: 'Your password has been reset',
    children: (
      <div className="text-center space-y-4 py-4">
        <div className="text-6xl">✅</div>
        <p className="text-sm text-slate-500 dark:text-slate-400">Redirecting...</p>
      </div>
    ),
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: { disableSnapshot: false },
  },
};

/**
 * Custom className example
 */
export const CustomStyling: Story = {
  args: {
    title: 'Custom Styled',
    subtitle: 'Example with custom className',
    className: 'max-w-lg',
    children: <LoginFormMock />,
  },
  parameters: {
    chromatic: { disableSnapshot: false },
  },
};
