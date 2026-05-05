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

import clsx from 'clsx';
import { useRouter } from 'next/navigation';

import { AccessibleModal } from '@/components/accessible';
import { Button } from '@/components/ui/primitives/button';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { useAuth, type AuthUser } from '@/hooks/useAuth';
import { useTranslation } from '@/hooks/useTranslation';
import { trackSignUp } from '@/lib/analytics/flywheel-events';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { isAdminRole } from '@/lib/utils/roles';

import { LoginForm, LoginFormData } from './LoginForm';
import { buildOAuthUrl } from './oauth-url';
import { RegisterForm, RegisterSubmitPayload } from './RegisterForm';
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
  /** URL to redirect to after successful authentication. Defaults to '/library' */
  redirectTo?: string;
  /** Hide OAuth buttons (e.g. during alpha invite-only mode) */
  hideOAuth?: boolean;
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
  hideOAuth = false,
}: AuthModalProps) {
  const router = useRouter();
  const { t } = useTranslation();
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
    async (data: RegisterSubmitPayload) => {
      setIsAuthenticating(true);
      setError('');

      try {
        // Bot protection: silently drop submissions that fill the honeypot.
        if (data.honeypot && data.honeypot.length > 0) {
          return;
        }

        const user = await register({
          email: data.email,
          password: data.password,
        });
        trackSignUp({ method: 'email' });
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

  // Handle tab change (segmented control)
  const handleTabChange = useCallback(
    (value: 'login' | 'register') => {
      setActiveTab(value);
      clearError();
      setError('');
      setIsAuthenticating(false);
    },
    [clearError]
  );

  // Handle OAuth login — preserves legacy buildOAuthUrl behavior
  const handleOAuthLogin = useCallback((provider: 'google' | 'discord' | 'github') => {
    window.location.assign(buildOAuthUrl(provider));
  }, []);

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

            {/* Auth Tabs (v2 segmented control) */}
            <div
              role="tablist"
              aria-label={t('auth.modal.tabsLabel')}
              className="grid grid-cols-2 gap-1 p-1 bg-muted rounded-lg mb-4"
            >
              {(['login', 'register'] as const).map(m => (
                <button
                  key={m}
                  role="tab"
                  type="button"
                  aria-selected={activeTab === m}
                  onClick={() => handleTabChange(m)}
                  data-testid={`auth-tab-${m}`}
                  className={clsx(
                    'px-3 py-1.5 rounded-md text-sm font-body transition-colors',
                    activeTab === m
                      ? 'bg-background text-foreground shadow-sm'
                      : 'text-muted-foreground hover:text-foreground'
                  )}
                >
                  {m === 'login' ? t('auth.modal.loginTab') : t('auth.modal.registerTab')}
                </button>
              ))}
            </div>

            {/* Active form */}
            {activeTab === 'login' ? (
              <LoginForm
                onSubmit={handleLogin}
                loading={isAuthenticating}
                error={error}
                onErrorDismiss={() => setError('')}
              />
            ) : (
              <RegisterForm
                onSubmit={handleRegister}
                loading={isAuthenticating}
                error={error}
                onErrorDismiss={() => setError('')}
              />
            )}

            {/* OAuth section (hidden in alpha invite-only mode) */}
            {!hideOAuth && (
              <>
                <Divider label={t('auth.oauth.separator')} />
                <div className="flex flex-col gap-2">
                  <OAuthButton provider="google" onClick={() => handleOAuthLogin('google')} />
                  <OAuthButton provider="discord" onClick={() => handleOAuthLogin('discord')} />
                  <OAuthButton provider="github" onClick={() => handleOAuthLogin('github')} />
                </div>
              </>
            )}
          </>
        )}
      </div>
    </AccessibleModal>
  );
}
