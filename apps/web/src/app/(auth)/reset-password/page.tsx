/* eslint-disable security/detect-object-injection -- Safe form state object access */
'use client';

/**
 * Password Reset Page (AUTH-04) - App Router
 *
 * Two-mode password reset flow with AuthLayout (Issue #2231):
 * 1. Request Mode (no token): User enters email to request reset
 * 2. Reset Mode (with token): User sets new password
 *
 * Features:
 * - Email-based password reset workflow
 * - Token verification on page load
 * - Client-side password validation
 * - Real-time password strength indicator
 * - Auto-login and redirect after successful reset
 * - Comprehensive error handling
 * - WCAG 2.1 AA compliant with accessible components
 *
 * API Endpoints:
 * - POST /api/v1/auth/password-reset/request - Request reset email
 * - GET /api/v1/auth/password-reset/verify?token=xxx - Verify token
 * - PUT /api/v1/auth/password-reset/confirm - Confirm new password
 */

import { Suspense, useEffect, useState, FormEvent } from 'react';

import { motion } from 'framer-motion';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { AccessibleFormInput, AccessibleButton } from '@/components/accessible';
import { AuthLayout } from '@/components/layouts';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

// Type definitions
type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type PasswordStrength = 'weak' | 'medium' | 'strong';

interface PasswordValidation {
  minLength: boolean;
  hasUppercase: boolean;
  hasLowercase: boolean;
  hasNumber: boolean;
  isValid: boolean;
  strength: PasswordStrength;
}

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <AuthLayout title="Loading...">
          <div className="text-center py-8">
            <div className="animate-pulse text-slate-500">Loading...</div>
          </div>
        </AuthLayout>
      }
    >
      <ResetPasswordPageContent />
    </Suspense>
  );
}

// Password validation utility
const validatePassword = (password: string): PasswordValidation => {
  const minLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasLowercase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);

  const requirementsMet = [minLength, hasUppercase, hasLowercase, hasNumber].filter(Boolean).length;

  let strength: PasswordStrength = 'weak';
  if (requirementsMet === 4) {
    strength = 'strong';
  } else if (requirementsMet >= 2 && minLength) {
    strength = 'medium';
  }

  return {
    minLength,
    hasUppercase,
    hasLowercase,
    hasNumber,
    isValid: minLength && hasUppercase && hasLowercase && hasNumber,
    strength,
  };
};

// Password strength indicator component
const PasswordStrengthIndicator = ({ strength }: { strength: PasswordStrength }) => {
  const strengthConfig = {
    weak: {
      color: 'bg-red-500',
      text: 'Weak',
      textColor: 'text-red-500',
      width: 'w-1/3',
    },
    medium: {
      color: 'bg-orange-500',
      text: 'Medium',
      textColor: 'text-orange-500',
      width: 'w-2/3',
    },
    strong: {
      color: 'bg-green-500',
      text: 'Strong',
      textColor: 'text-green-500',
      width: 'w-full',
    },
  };

  const config = strengthConfig[strength];

  return (
    <div className="space-y-2" role="status" aria-live="polite">
      <div className="flex justify-between items-center">
        <span className="text-sm text-slate-600 dark:text-slate-400">Password strength:</span>
        <span className={`text-sm font-medium ${config.textColor}`}>{config.text}</span>
      </div>
      <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
        <motion.div
          className={`h-full ${config.color}`}
          initial={{ width: 0 }}
          animate={{ width: config.width }}
          transition={{ duration: 0.3 }}
        />
      </div>
    </div>
  );
};

function ResetPasswordPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams?.get('token') ?? null;

  // Mode: 'request' (no token) or 'reset' (with token)
  const mode = token ? 'reset' : 'request';

  // Authentication state
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);

  // Request mode state
  const [email, setEmail] = useState('');
  const [requestSuccess, setRequestSuccess] = useState(false);

  // Reset mode state
  const [tokenValid, setTokenValid] = useState<boolean | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidation>({
    minLength: false,
    hasUppercase: false,
    hasLowercase: false,
    hasNumber: false,
    isValid: false,
    strength: 'weak',
  });
  const [resetSuccess, setResetSuccess] = useState(false);

  // UI state
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  // Check if user is already authenticated
  useEffect(() => {
    const checkAuth = async () => {
      try {
        const user = await api.auth.getMe();
        if (user) {
          setAuthUser(user);
          // Redirect to chat if already logged in
          void router.push('/chat');
        }
      } catch {
        setAuthUser(null);
      } finally {
        setIsCheckingAuth(false);
      }
    };

    void checkAuth();
  }, [router]);

  // Verify token on page load (reset mode only)
  useEffect(() => {
    if (mode === 'reset' && token && typeof token === 'string') {
      const verifyToken = async () => {
        setIsLoading(true);
        setErrorMessage('');
        try {
          await api.auth.verifyResetToken(token);
          setTokenValid(true);
        } catch (err) {
          setTokenValid(false);
          setErrorMessage(getErrorMessage(err, 'Invalid or expired reset token.'));
        } finally {
          setIsLoading(false);
        }
      };

      void verifyToken();
    }
  }, [mode, token]);

  // Update password validation on password change
  useEffect(() => {
    if (newPassword) {
      setPasswordValidation(validatePassword(newPassword));
    } else {
      setPasswordValidation({
        minLength: false,
        hasUppercase: false,
        hasLowercase: false,
        hasNumber: false,
        isValid: false,
        strength: 'weak',
      });
    }
  }, [newPassword]);

  // Handle request reset submission
  const handleRequestReset = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');
    setIsLoading(true);

    try {
      await api.auth.requestPasswordReset(email);
      setRequestSuccess(true);
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to send reset email. Please try again.'));
    } finally {
      setIsLoading(false);
    }
  };

  // Handle password reset submission
  const handleConfirmReset = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    // Client-side validation
    if (!passwordValidation.isValid) {
      setErrorMessage('Password does not meet all requirements.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setErrorMessage('Passwords do not match.');
      return;
    }

    if (!token || typeof token !== 'string') {
      setErrorMessage('Invalid reset token.');
      return;
    }

    setIsLoading(true);

    try {
      // Confirm password reset
      await api.auth.confirmPasswordReset(token, newPassword);

      setResetSuccess(true);

      // Auto-login after reset
      try {
        const loginResponse = await api.auth.login({
          email: email || '', // Email might not be known in reset mode
          password: newPassword,
        });

        // Handle 2FA requirement or successful login
        if (loginResponse.user && !loginResponse.requiresTwoFactor) {
          setAuthUser(loginResponse.user);
          // Redirect to chat after 2 seconds
          setTimeout(() => {
            void router.push('/chat');
          }, 2000);
        } else {
          // If 2FA required or auto-login fails, redirect to login page
          setTimeout(() => {
            void router.push('/');
          }, 2000);
        }
      } catch {
        // If auto-login fails, redirect to login page
        setTimeout(() => {
          void router.push('/');
        }, 2000);
      }
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to reset password. Please try again.'));
      setIsLoading(false);
    }
  };

  // Show loading state while checking authentication
  if (isCheckingAuth) {
    return (
      <AuthLayout title="Loading...">
        <div className="text-center py-8">
          <div className="animate-spin text-4xl mb-4">⏳</div>
          <p className="text-slate-400">Loading...</p>
        </div>
      </AuthLayout>
    );
  }

  // User is already authenticated, redirecting...
  if (authUser && !resetSuccess) {
    return null;
  }

  // Request Reset Mode
  if (mode === 'request' && !requestSuccess) {
    return (
      <AuthLayout
        title="Reset Password"
        subtitle="Enter your email address and we'll send you instructions to reset your password"
      >
        {errorMessage && (
          <div
            role="alert"
            aria-live="polite"
            className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg mb-4"
          >
            {errorMessage}
          </div>
        )}

        <form noValidate onSubmit={handleRequestReset} className="space-y-4">
          <AccessibleFormInput
            label="Email Address"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
            autoComplete="email"
            placeholder="you@example.com"
            inputClassName="w-full bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-600 rounded-lg px-4 py-3 text-slate-900 dark:text-slate-100 focus:border-primary focus:ring-2 focus:ring-ring"
          />

          <AccessibleButton
            type="submit"
            variant="primary"
            className="w-full mt-6"
            isLoading={isLoading}
            loadingText="Sending..."
            disabled={!email.trim()}
          >
            Send Reset Instructions
          </AccessibleButton>
        </form>

        <div className="text-center mt-4">
          <Link
            href="/"
            className="text-sm text-primary hover:text-primary/80 transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
          >
            ← Back to Login
          </Link>
        </div>
      </AuthLayout>
    );
  }

  // Request Success State
  if (mode === 'request' && requestSuccess) {
    return (
      <AuthLayout>
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">✉️</div>
          <h2 className="text-2xl font-bold text-green-400">Check Your Email</h2>
          <p className="text-slate-600 dark:text-slate-300">
            We've sent password reset instructions to <strong>{email}</strong>.
          </p>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Didn't receive the email? Check your spam folder or{' '}
            <button
              onClick={() => {
                setRequestSuccess(false);
                setEmail('');
              }}
              className="text-primary hover:text-primary/80 underline focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded"
            >
              try again
            </button>
            .
          </p>
          <div className="pt-4">
            <Button variant="secondary" asChild>
              <Link href="/">Back to Login</Link>
            </Button>
          </div>
        </div>
      </AuthLayout>
    );
  }

  // Reset Password Mode - Token Verification
  if (mode === 'reset' && tokenValid === null) {
    return (
      <AuthLayout title="Verifying...">
        <div className="text-center space-y-4 py-8">
          <div className="animate-spin text-4xl mb-4">⏳</div>
          <p className="text-slate-400">Verifying reset token...</p>
        </div>
      </AuthLayout>
    );
  }

  // Reset Password Mode - Invalid Token
  if (mode === 'reset' && tokenValid === false) {
    return (
      <AuthLayout
        title="Invalid or Expired Link"
        subtitle="This password reset link is no longer valid"
      >
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">⚠️</div>
          {errorMessage && <p className="text-sm text-red-400">{errorMessage}</p>}
          <div className="pt-4 space-y-2">
            <Button asChild className="w-full">
              <Link href="/reset-password">Request New Reset Link</Link>
            </Button>
            <Button variant="secondary" asChild className="w-full">
              <Link href="/">Back to Login</Link>
            </Button>
          </div>
        </div>
      </AuthLayout>
    );
  }

  // Reset Password Mode - Valid Token
  if (mode === 'reset' && tokenValid === true && !resetSuccess) {
    return (
      <AuthLayout title="Set New Password" subtitle="Choose a strong password for your account">
        {errorMessage && (
          <div
            role="alert"
            aria-live="polite"
            className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg mb-4"
          >
            {errorMessage}
          </div>
        )}

        <form noValidate onSubmit={handleConfirmReset} className="space-y-4">
          <AccessibleFormInput
            label="New Password"
            type="password"
            value={newPassword}
            onChange={e => setNewPassword(e.target.value)}
            required
            autoComplete="new-password"
            inputClassName="w-full bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-600 rounded-lg px-4 py-3 text-slate-900 dark:text-slate-100 focus:border-primary focus:ring-2 focus:ring-ring"
          />

          {/* Password Requirements */}
          {newPassword && (
            <div className="space-y-2">
              <PasswordStrengthIndicator strength={passwordValidation.strength} />
              <div className="text-sm space-y-1">
                <div
                  className={`flex items-center gap-2 ${
                    passwordValidation.minLength
                      ? 'text-green-400'
                      : 'text-slate-500 dark:text-slate-400'
                  }`}
                >
                  <span aria-hidden="true">{passwordValidation.minLength ? '✓' : '○'}</span>
                  <span>At least 8 characters</span>
                </div>
                <div
                  className={`flex items-center gap-2 ${
                    passwordValidation.hasUppercase
                      ? 'text-green-400'
                      : 'text-slate-500 dark:text-slate-400'
                  }`}
                >
                  <span aria-hidden="true">{passwordValidation.hasUppercase ? '✓' : '○'}</span>
                  <span>At least 1 uppercase letter</span>
                </div>
                <div
                  className={`flex items-center gap-2 ${
                    passwordValidation.hasLowercase
                      ? 'text-green-400'
                      : 'text-slate-500 dark:text-slate-400'
                  }`}
                >
                  <span aria-hidden="true">{passwordValidation.hasLowercase ? '✓' : '○'}</span>
                  <span>At least 1 lowercase letter</span>
                </div>
                <div
                  className={`flex items-center gap-2 ${
                    passwordValidation.hasNumber
                      ? 'text-green-400'
                      : 'text-slate-500 dark:text-slate-400'
                  }`}
                >
                  <span aria-hidden="true">{passwordValidation.hasNumber ? '✓' : '○'}</span>
                  <span>At least 1 number</span>
                </div>
              </div>
            </div>
          )}

          <AccessibleFormInput
            label="Confirm Password"
            type="password"
            value={confirmPassword}
            onChange={e => setConfirmPassword(e.target.value)}
            required
            autoComplete="new-password"
            error={
              confirmPassword && newPassword !== confirmPassword
                ? 'Passwords do not match'
                : undefined
            }
            inputClassName="w-full bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-600 rounded-lg px-4 py-3 text-slate-900 dark:text-slate-100 focus:border-primary focus:ring-2 focus:ring-ring"
          />

          <AccessibleButton
            type="submit"
            variant="primary"
            className="w-full mt-6"
            isLoading={isLoading}
            loadingText="Resetting..."
            disabled={
              !passwordValidation.isValid ||
              newPassword !== confirmPassword ||
              !confirmPassword.trim()
            }
          >
            Reset Password
          </AccessibleButton>
        </form>

        <div className="text-center mt-4">
          <Link
            href="/"
            className="text-sm text-primary hover:text-primary/80 transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
          >
            ← Back to Login
          </Link>
        </div>
      </AuthLayout>
    );
  }

  // Reset Success State
  if (mode === 'reset' && resetSuccess) {
    return (
      <AuthLayout
        title="Password Reset Successful"
        subtitle="Your password has been successfully reset"
      >
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">✅</div>
          <p className="text-sm text-slate-500 dark:text-slate-400">Redirecting to chat...</p>
          <div className="animate-spin text-2xl mx-auto w-fit">⏳</div>
        </div>
      </AuthLayout>
    );
  }

  return null;
}
