'use client';

/**
 * Password Reset Page — v2 AuthCard migration (Task 13).
 *
 * Two-mode flow:
 * 1. Request mode (no ?token=): user enters email to receive reset instructions.
 * 2. Reset mode (?token=<t>): token is verified, then user sets a new password.
 *
 * Auto-redirects already-authenticated users to /chat. After a successful
 * reset, attempts a best-effort auto-login and redirects to /chat (or /)
 * after a 2-second delay.
 */

import { useEffect, useState, type FormEvent, type JSX } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';
import { SuccessCard } from '@/components/ui/v2/success-card';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

// ---------------------------------------------------------------------------
// Types & helpers
// ---------------------------------------------------------------------------

type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

interface PasswordValidation {
  readonly minLength: boolean;
  readonly hasUppercase: boolean;
  readonly hasLowercase: boolean;
  readonly hasNumber: boolean;
  readonly isValid: boolean;
}

function validatePassword(password: string): PasswordValidation {
  const minLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasLowercase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);
  return {
    minLength,
    hasUppercase,
    hasLowercase,
    hasNumber,
    isValid: minLength && hasUppercase && hasLowercase && hasNumber,
  };
}

// ---------------------------------------------------------------------------
// Suspense fallback
// ---------------------------------------------------------------------------

export function ResetPasswordFallback(): JSX.Element {
  const { t } = useTranslation();
  return (
    <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      <div className="animate-pulse" data-testid="reset-password-loading">
        {t('auth.resetPassword.loadingTitle')}
      </div>
    </main>
  );
}

// ---------------------------------------------------------------------------
// Main content
// ---------------------------------------------------------------------------

export function ResetPasswordPageContent(): JSX.Element | null {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();

  const token = searchParams?.get('token') ?? null;
  const mode: 'request' | 'reset' = token ? 'reset' : 'request';

  // Auth state
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);

  // Request-mode state
  const [email, setEmail] = useState('');
  const [requestSuccess, setRequestSuccess] = useState(false);

  // Reset-mode state
  const [tokenValid, setTokenValid] = useState<boolean | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [resetSuccess, setResetSuccess] = useState(false);

  // UI state
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const passwordValidation = validatePassword(newPassword);

  // Auth guard: redirect to /chat if already authenticated
  useEffect(() => {
    let cancelled = false;
    const checkAuth = async () => {
      try {
        const user = await api.auth.getMe();
        if (cancelled) return;
        if (user) {
          setAuthUser(user);
          void router.push('/chat');
        }
      } catch {
        if (!cancelled) setAuthUser(null);
      } finally {
        if (!cancelled) setIsCheckingAuth(false);
      }
    };
    void checkAuth();
    return () => {
      cancelled = true;
    };
  }, [router]);

  // Token verification on mount (reset mode only)
  useEffect(() => {
    if (mode !== 'reset' || !token) return;
    let cancelled = false;
    const verify = async () => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        await api.auth.verifyResetToken(token);
        if (!cancelled) setTokenValid(true);
      } catch (err) {
        if (!cancelled) {
          setTokenValid(false);
          setErrorMessage(getErrorMessage(err, t('auth.resetPassword.invalidTokenFromServer')));
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    void verify();
    return () => {
      cancelled = true;
    };
  }, [mode, token, t]);

  // ------------------------- Handlers -------------------------

  const handleRequestSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');
    setIsLoading(true);
    try {
      await api.auth.requestPasswordReset(email);
      setRequestSuccess(true);
    } catch (err) {
      setErrorMessage(getErrorMessage(err, t('auth.resetPassword.genericRequestError')));
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfirmSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (!passwordValidation.isValid) {
      setErrorMessage(t('auth.resetPassword.passwordRequirements'));
      return;
    }
    if (newPassword !== confirmPassword) {
      setErrorMessage(t('auth.resetPassword.passwordsDoNotMatch'));
      return;
    }
    if (!token) {
      setErrorMessage(t('auth.resetPassword.invalidToken'));
      return;
    }

    setIsLoading(true);
    try {
      await api.auth.confirmPasswordReset(token, newPassword);
      setResetSuccess(true);

      // Best-effort auto-login. Email may not be known in reset mode — we
      // preserve legacy behaviour (pass '' if we don't have it) and fall
      // through to the login page if auto-login fails or 2FA is required.
      try {
        const loginResponse = await api.auth.login({
          email: email || '',
          password: newPassword,
        });
        if (loginResponse.user && !loginResponse.requiresTwoFactor) {
          setAuthUser(loginResponse.user);
          setTimeout(() => {
            void router.push('/chat');
          }, 2000);
        } else {
          setTimeout(() => {
            void router.push('/');
          }, 2000);
        }
      } catch {
        setTimeout(() => {
          void router.push('/');
        }, 2000);
      }
    } catch (err) {
      setErrorMessage(getErrorMessage(err, t('auth.resetPassword.genericConfirmError')));
      setIsLoading(false);
    }
  };

  const handleTryAgain = () => {
    setRequestSuccess(false);
    setEmail('');
    setErrorMessage('');
  };

  // ------------------------- Render -------------------------

  // 1. Checking authentication
  if (isCheckingAuth) {
    return (
      <AuthCard title={t('auth.resetPassword.loadingTitle')}>
        <div className="text-center py-8" data-testid="reset-password-auth-check">
          <div className="animate-pulse text-muted-foreground text-sm">
            {t('auth.resetPassword.loadingTitle')}
          </div>
        </div>
      </AuthCard>
    );
  }

  // 2. Already authenticated: redirecting to /chat
  if (authUser && !resetSuccess) {
    return null;
  }

  const backToLogin = (
    <Link
      href="/"
      className="font-medium text-foreground hover:underline"
      data-testid="reset-password-back-to-login"
    >
      {t('auth.resetPassword.backToLogin')}
    </Link>
  );

  // 3. Request mode — success state
  if (mode === 'request' && requestSuccess) {
    return (
      <AuthCard title={t('auth.resetPassword.sentTitle')} footerAction={backToLogin}>
        <SuccessCard
          emoji="✉️"
          body={t('auth.resetPassword.sentBody', { email })}
          cta={t('auth.resetPassword.tryAgain')}
          onCta={handleTryAgain}
        />
      </AuthCard>
    );
  }

  // 4. Request mode — form
  if (mode === 'request') {
    return (
      <AuthCard
        title={t('auth.resetPassword.title')}
        subtitle={t('auth.resetPassword.subtitle')}
        footerAction={backToLogin}
      >
        {errorMessage && (
          <div
            role="alert"
            aria-live="polite"
            className="bg-destructive/10 border border-destructive/30 text-destructive px-4 py-3 rounded-lg mb-4 text-sm"
            data-testid="reset-password-error"
          >
            {errorMessage}
          </div>
        )}

        <form noValidate onSubmit={handleRequestSubmit} className="space-y-4">
          <InputField
            label={t('auth.resetPassword.email')}
            type="email"
            value={email}
            onChange={setEmail}
            required
            autoComplete="email"
            placeholder={t('auth.resetPassword.placeholder')}
          />

          <Btn
            type="submit"
            variant="primary"
            fullWidth
            className="mt-6"
            loading={isLoading}
            disabled={!email.trim()}
            data-testid="reset-password-submit"
          >
            {isLoading ? t('auth.resetPassword.sending') : t('auth.resetPassword.submitCta')}
          </Btn>
        </form>
      </AuthCard>
    );
  }

  // 5. Reset mode — verifying token
  if (mode === 'reset' && tokenValid === null) {
    return (
      <AuthCard title={t('auth.resetPassword.verifyingTitle')}>
        <div className="text-center py-8" data-testid="reset-password-verifying">
          <div className="animate-pulse text-muted-foreground text-sm">
            {t('auth.resetPassword.verifyingBody')}
          </div>
        </div>
      </AuthCard>
    );
  }

  // 6. Reset mode — invalid token
  if (mode === 'reset' && tokenValid === false) {
    return (
      <AuthCard
        title={t('auth.resetPassword.invalidLinkTitle')}
        subtitle={t('auth.resetPassword.invalidLinkSubtitle')}
        footerAction={backToLogin}
      >
        <div className="text-center py-4 space-y-4" data-testid="reset-password-invalid-token">
          {errorMessage && (
            <p role="alert" className="text-sm text-destructive">
              {errorMessage}
            </p>
          )}
          <Btn
            variant="primary"
            fullWidth
            onClick={() => router.push('/reset-password')}
            data-testid="reset-password-request-new"
          >
            {t('auth.resetPassword.invalidLinkCta')}
          </Btn>
        </div>
      </AuthCard>
    );
  }

  // 7. Reset mode — success state
  if (mode === 'reset' && resetSuccess) {
    return (
      <AuthCard title={t('auth.resetPassword.successTitle')}>
        <div
          className="flex flex-col items-center text-center gap-3 p-6"
          data-testid="reset-password-success"
          role="status"
          aria-live="polite"
        >
          <div
            aria-hidden="true"
            className="w-16 h-16 flex items-center justify-center rounded-full bg-[hsl(var(--c-toolkit)/0.1)] text-3xl"
          >
            <span>✅</span>
          </div>
          <p className="font-body text-sm text-muted-foreground m-0">
            {t('auth.resetPassword.successBody')}
          </p>
        </div>
      </AuthCard>
    );
  }

  // 8. Reset mode — valid token, password form
  if (mode === 'reset' && tokenValid === true) {
    const confirmMismatch = confirmPassword && newPassword !== confirmPassword;
    return (
      <AuthCard
        title={t('auth.resetPassword.confirmTitle')}
        subtitle={t('auth.resetPassword.confirmSubtitle')}
        footerAction={backToLogin}
      >
        {errorMessage && (
          <div
            role="alert"
            aria-live="polite"
            className="bg-destructive/10 border border-destructive/30 text-destructive px-4 py-3 rounded-lg mb-4 text-sm"
            data-testid="reset-password-error"
          >
            {errorMessage}
          </div>
        )}

        <form noValidate onSubmit={handleConfirmSubmit} className="space-y-4">
          <PwdInput
            label={t('auth.resetPassword.passwordLabel')}
            value={newPassword}
            onChange={setNewPassword}
            required
            autoComplete="new-password"
            showStrength
            toggleShowLabel={t('auth.visibility.show')}
            toggleHideLabel={t('auth.visibility.hide')}
            strengthPrefix={t('auth.meter.prefix')}
            strengthLabels={[
              t('auth.meter.weak'),
              t('auth.meter.weak'),
              t('auth.meter.fair'),
              t('auth.meter.good'),
              t('auth.meter.strong'),
            ]}
          />

          <PwdInput
            label={t('auth.resetPassword.confirmPasswordLabel')}
            value={confirmPassword}
            onChange={setConfirmPassword}
            required
            autoComplete="new-password"
            error={confirmMismatch ? t('auth.resetPassword.passwordsDoNotMatch') : undefined}
            toggleShowLabel={t('auth.visibility.show')}
            toggleHideLabel={t('auth.visibility.hide')}
          />

          <Btn
            type="submit"
            variant="primary"
            fullWidth
            className="mt-6"
            loading={isLoading}
            disabled={
              !passwordValidation.isValid ||
              newPassword !== confirmPassword ||
              !confirmPassword.trim()
            }
            data-testid="reset-password-confirm"
          >
            {isLoading ? t('auth.resetPassword.resetting') : t('auth.resetPassword.confirmCta')}
          </Btn>
        </form>
      </AuthCard>
    );
  }

  return null;
}
