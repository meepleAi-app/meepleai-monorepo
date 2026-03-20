/**
 * AuthModal Component (Updated Issue #3077)
 *
 * Unified authentication modal supporting:
 * - Login and registration tabs
 * - Two-factor authentication flow
 * - OAuth authentication buttons
 * - Session expired warnings
 * - Accessible modal with Shadcn UI components
 *
 * This component consolidates authentication UI previously
 * scattered across index.tsx and login.tsx
 */

import { useState, useEffect, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { AccessibleModal } from '@/components/accessible';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { useAuth, type AuthUser } from '@/hooks/useAuth';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { isAdminRole } from '@/lib/utils/roles';

import { LoginForm, LoginFormData } from './LoginForm';
import OAuthButtons from './OAuthButtons';
import { RegisterForm, RegisterFormData } from './RegisterForm';
import { TwoFactorVerification, TwoFactorVerificationData } from './TwoFactorVerification';

// ============================================================================
// Component Props
// ============================================================================

export interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultMode?: 'login' | 'register';
  onSuccess?: (user: AuthUser) => void;
  sessionExpiredMessage?: boolean;
  /** URL to redirect to after successful authentication. Defaults to '/dashboard' */
  redirectTo?: string;
}

// ============================================================================
// Component
// ============================================================================

export function AuthModal({
  isOpen,
  onClose,
  defaultMode = 'login',
  onSuccess,
  sessionExpiredMessage = false,
  redirectTo = '/dashboard',
}: AuthModalProps) {
  const router = useRouter();
  const { register, loadCurrentUser, clearError } = useAuth();
  const [activeTab, setActiveTab] = useState<'login' | 'register'>(defaultMode);
  const [isAuthenticating, setIsAuthenticating] = useState(false);
  const [error, setError] = useState<string>('');

  // 2FA state (Issue #3077)
  const [show2FA, setShow2FA] = useState(false);
  const [tempSessionToken, setTempSessionToken] = useState<string | null>(null);
  const [twoFactorError, setTwoFactorError] = useState<string>('');

  // Reset state when modal opens/closes or default mode changes
  useEffect(() => {
    if (isOpen) {
      setActiveTab(defaultMode);
      clearError();
      setError('');
      setIsAuthenticating(false);
      setShow2FA(false);
      setTempSessionToken(null);
      setTwoFactorError('');
    }
  }, [isOpen, defaultMode, clearError]);

  // Handle login submission with 2FA support
  const handleLogin = useCallback(
    async (data: LoginFormData) => {
      setIsAuthenticating(true);
      setError('');

      try {
        const response = await api.auth.login({
          email: data.email,
          password: data.password,
        });

        // Check if 2FA is required
        if (response.requiresTwoFactor && response.tempSessionToken) {
          setTempSessionToken(response.tempSessionToken);
          setShow2FA(true);
          setIsAuthenticating(false);
          return;
        }

        // Normal login - user should be present
        if (response.user) {
          await loadCurrentUser(); // Sync useAuth state
          onSuccess?.(response.user);
          onClose();
          // Redirect admins to admin dashboard, others to specified redirect or default dashboard
          const targetUrl = isAdminRole(response.user.role) ? '/admin' : redirectTo;

          // Small delay to ensure session cookie is persisted before navigation
          // This prevents race condition with middleware session validation
          await new Promise(resolve => setTimeout(resolve, 100));

          // Refresh router to revalidate middleware with new session cookie
          router.refresh();

          // Navigate using Next.js router for smooth client-side transition
          await router.push(targetUrl);
        } else {
          throw new Error('Login failed: No user data received');
        }
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : 'Login failed. Please check your credentials.';
        setError(errorMessage);
        logger.error('Login failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [loadCurrentUser, onSuccess, onClose, router, redirectTo]
  );

  // Handle 2FA verification (Issue #3077)
  const handle2FAVerify = useCallback(
    async (data: TwoFactorVerificationData) => {
      if (!tempSessionToken) {
        setTwoFactorError('Session expired. Please login again.');
        return;
      }

      setIsAuthenticating(true);
      setTwoFactorError('');

      try {
        const user = await api.auth.verify2FALogin(
          tempSessionToken,
          data.code,
          data.rememberDevice
        );

        await loadCurrentUser(); // Sync useAuth state
        onSuccess?.(user);
        onClose();
        // Redirect admins to admin dashboard, others to specified redirect or default dashboard
        const targetUrl = isAdminRole(user.role) ? '/admin' : redirectTo;

        // Small delay to ensure session cookie is persisted before navigation
        await new Promise(resolve => setTimeout(resolve, 100));

        // Refresh router to revalidate middleware with new session cookie
        router.refresh();

        // Navigate using Next.js router for smooth client-side transition
        await router.push(targetUrl);
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : 'Invalid verification code. Please try again.';
        setTwoFactorError(errorMessage);
        logger.error('2FA verification failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [tempSessionToken, loadCurrentUser, onSuccess, onClose, router, redirectTo]
  );

  // Handle cancel 2FA - go back to login form
  const handleCancel2FA = useCallback(() => {
    setShow2FA(false);
    setTempSessionToken(null);
    setTwoFactorError('');
  }, []);

  // Handle registration submission
  const handleRegister = useCallback(
    async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
      setIsAuthenticating(true);
      setError('');

      try {
        const user = await register(data);
        onSuccess?.(user);
        onClose();
        // Redirect to email verification pending page with email as parameter
        // The user needs to verify their email before accessing the dashboard

        // Small delay to ensure session cookie is persisted before navigation
        await new Promise(resolve => setTimeout(resolve, 100));

        // Navigate using Next.js router for smooth client-side transition
        await router.push(`/verification-pending?email=${encodeURIComponent(data.email)}`);
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : 'Registration failed. Please try again.';
        setError(errorMessage);
        logger.error('Registration failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [register, onSuccess, onClose, router]
  );

  // Handle tab change
  const handleTabChange = useCallback(
    (value: string) => {
      setActiveTab(value as 'login' | 'register');
      clearError();
      setError('');
      setIsAuthenticating(false);
    },
    [clearError]
  );

  // Determine modal title based on current state
  const getModalTitle = () => {
    if (show2FA) return 'Two-Factor Authentication';
    return activeTab === 'login' ? 'Sign In to MeepleAI' : 'Create Your Account';
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={show2FA ? handleCancel2FA : onClose}
      title={getModalTitle()}
      description="Access AI-powered board game rules assistance"
      size="md"
      data-testid="auth-modal"
    >
      <div className="space-y-6">
        {/* 2FA Verification Screen (Issue #3077) */}
        {show2FA ? (
          <div data-testid="2fa-verification-screen">
            <TwoFactorVerification
              onVerify={handle2FAVerify}
              onCancel={handleCancel2FA}
              loading={isAuthenticating}
              error={twoFactorError}
              onErrorDismiss={() => setTwoFactorError('')}
              showRememberDevice={true}
            />
            <div className="mt-4 text-center">
              <Button
                variant="ghost"
                size="sm"
                onClick={handleCancel2FA}
                className="text-muted-foreground"
                data-testid="back-to-login-button"
              >
                ← Back to login
              </Button>
            </div>
          </div>
        ) : (
          <>
            {/* Session Expired Warning */}
            {sessionExpiredMessage && (
              <div
                className="rounded-md bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 p-3"
                role="alert"
              >
                <div className="flex">
                  <span className="text-yellow-600 dark:text-yellow-400 mr-2" aria-hidden="true">
                    ⚠️
                  </span>
                  <div>
                    <p className="text-sm font-medium text-yellow-800 dark:text-yellow-200">
                      Session Expired
                    </p>
                    <p className="text-xs text-yellow-700 dark:text-yellow-300 mt-1">
                      Your session has expired. Please sign in again to continue.
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Auth Tabs */}
            <Tabs value={activeTab} onValueChange={handleTabChange} className="w-full">
              <TabsList className="grid w-full grid-cols-2">
                <TabsTrigger value="login" data-testid="auth-tab-login">
                  Login
                </TabsTrigger>
                <TabsTrigger value="register" data-testid="auth-tab-register">
                  Register
                </TabsTrigger>
              </TabsList>

              {/* Login Tab */}
              <TabsContent value="login" className="space-y-4 mt-4">
                <LoginForm
                  onSubmit={handleLogin}
                  loading={isAuthenticating}
                  error={error}
                  onErrorDismiss={() => setError('')}
                />
              </TabsContent>

              {/* Register Tab */}
              <TabsContent value="register" className="space-y-4 mt-4">
                <RegisterForm
                  onSubmit={handleRegister}
                  loading={isAuthenticating}
                  error={error}
                  onErrorDismiss={() => setError('')}
                  showRoleSelector={false}
                />
              </TabsContent>
            </Tabs>

            {/* OAuth Buttons (includes "Or continue with" separator) */}
            <OAuthButtons />
          </>
        )}
      </div>
    </AccessibleModal>
  );
}
