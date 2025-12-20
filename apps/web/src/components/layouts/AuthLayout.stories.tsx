/**
 * AuthLayout Storybook Stories - Issue #2231
 *
 * Showcases different states of the AuthLayout component
 * for authentication pages (login, register, reset-password).
 */

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

import { AuthLayout } from './AuthLayout';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Layouts/AuthLayout',
  component: AuthLayout,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Specialized layout for authentication pages with minimal header, centered card, and minimal footer.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    children: {
      description: 'Auth form content to render in the centered card',
      control: false,
    },
    title: {
      description: 'Optional page title',
      control: 'text',
    },
    subtitle: {
      description: 'Optional subtitle/description',
      control: 'text',
    },
    showBackLink: {
      description: 'Show back link to home (default: true)',
      control: 'boolean',
    },
    className: {
      description: 'Additional CSS classes for customization',
      control: 'text',
    },
  },
} satisfies Meta<typeof AuthLayout>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock Login Form Component
const LoginFormExample = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="email">Email</Label>
      <Input id="email" type="email" placeholder="you@example.com" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="password">Password</Label>
      <Input id="password" type="password" placeholder="••••••••" />
    </div>
    <Button className="w-full">Sign In</Button>
    <div className="text-center">
      <a href="/reset-password" className="text-sm text-primary hover:underline">
        Forgot password?
      </a>
    </div>
  </div>
);

// Mock Register Form Component
const RegisterFormExample = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="name">Full Name</Label>
      <Input id="name" type="text" placeholder="Mario Rossi" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="email">Email</Label>
      <Input id="email" type="email" placeholder="you@example.com" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="password">Password</Label>
      <Input id="password" type="password" placeholder="••••••••" />
    </div>
    <div className="space-y-2">
      <Label htmlFor="confirmPassword">Confirm Password</Label>
      <Input id="confirmPassword" type="password" placeholder="••••••••" />
    </div>
    <Button className="w-full">Create Account</Button>
  </div>
);

// Mock Reset Password Form Component
const ResetPasswordFormExample = () => (
  <div className="space-y-4">
    <div className="space-y-2">
      <Label htmlFor="email">Email Address</Label>
      <Input id="email" type="email" placeholder="you@example.com" />
    </div>
    <Button className="w-full">Send Reset Instructions</Button>
    <div className="text-center">
      <a href="/login" className="text-sm text-primary hover:underline">
        Back to Login
      </a>
    </div>
  </div>
);

/**
 * Default auth layout without title
 */
export const Default: Story = {
  args: {
    children: <LoginFormExample />,
  },
};

/**
 * Login page variant
 */
export const Login: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue to MeepleAI',
    children: <LoginFormExample />,
  },
};

/**
 * Register page variant
 */
export const Register: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI to get started',
    children: <RegisterFormExample />,
  },
};

/**
 * Reset password page variant
 */
export const ResetPassword: Story = {
  args: {
    title: 'Reset Password',
    subtitle: "Enter your email and we'll send you instructions",
    children: <ResetPasswordFormExample />,
  },
};

/**
 * Layout without back link
 */
export const WithoutBackLink: Story = {
  args: {
    title: 'Welcome',
    subtitle: 'Sign in to continue',
    showBackLink: false,
    children: <LoginFormExample />,
  },
};

/**
 * Layout with custom styling
 */
export const CustomStyling: Story = {
  args: {
    title: 'Custom Styled',
    subtitle: 'Example with custom className',
    className: 'max-w-lg',
    children: <LoginFormExample />,
  },
};

/**
 * Mobile view - Login
 */
export const MobileLogin: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue',
    children: <LoginFormExample />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Mobile view - Register
 */
export const MobileRegister: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI',
    children: <RegisterFormExample />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet view
 */
export const Tablet: Story = {
  args: {
    title: 'Welcome Back',
    subtitle: 'Sign in to continue',
    children: <LoginFormExample />,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
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
    children: <LoginFormExample />,
  },
  parameters: {
    backgrounds: {
      default: 'dark',
    },
    theme: 'dark',
  },
};

/**
 * Dark mode - Register
 */
export const DarkModeRegister: Story = {
  args: {
    title: 'Create Account',
    subtitle: 'Join MeepleAI',
    children: <RegisterFormExample />,
  },
  parameters: {
    backgrounds: {
      default: 'dark',
    },
    theme: 'dark',
  },
};

/**
 * Dark mode - Reset Password
 */
export const DarkModeResetPassword: Story = {
  args: {
    title: 'Reset Password',
    subtitle: "Enter your email and we'll send you instructions",
    children: <ResetPasswordFormExample />,
  },
  parameters: {
    backgrounds: {
      default: 'dark',
    },
    theme: 'dark',
  },
};

/**
 * Minimal content example
 */
export const MinimalContent: Story = {
  args: {
    children: (
      <div className="text-center space-y-4">
        <h2 className="text-xl font-semibold">Check Your Email</h2>
        <p className="text-sm text-muted-foreground">
          We've sent password reset instructions to your email address.
        </p>
        <Button variant="secondary">Back to Login</Button>
      </div>
    ),
  },
};

/**
 * Success state example
 */
export const SuccessState: Story = {
  args: {
    title: 'Password Reset Successful',
    subtitle: 'Your password has been successfully reset',
    children: (
      <div className="text-center space-y-4">
        <div className="text-6xl">✅</div>
        <p className="text-sm text-muted-foreground">Redirecting to chat...</p>
      </div>
    ),
  },
};

/**
 * Error state example
 */
export const ErrorState: Story = {
  args: {
    title: 'Invalid or Expired Link',
    subtitle: 'This password reset link is no longer valid',
    children: (
      <div className="text-center space-y-4">
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
};
