'use client';

import { useCallback, useState } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { LoginForm, type LoginFormData } from '@/components/auth/LoginForm';
import { buildOAuthUrl } from '@/components/auth/oauth-url';
import {
  TwoFactorVerification,
  type TwoFactorVerificationData,
} from '@/components/auth/TwoFactorVerification';
import { AuthCard } from '@/components/ui/v2/auth-card';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { isAdminRole } from '@/lib/utils/roles';

// ============================================================================
// Fallback (Suspense boundary)
// ============================================================================

export function LoginFallback() {
  const { t } = useTranslation();
  return (
    <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      {t('auth.login.loadingMessage')}
    </main>
  );
}

// ============================================================================
// Main content
// ============================================================================

export function LoginPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();
  const { loadCurrentUser } = useAuth();

  const from = searchParams?.get('from') ?? '/library';
  const isSessionExpired = searchParams?.get('reason') === 'session_expired';

  const [isAuthenticating, setIsAuthenticating] = useState(false);
  const [error, setError] = useState<string>('');

  // 2FA state (parity with AuthModal flow)
  const [show2FA, setShow2FA] = useState(false);
  const [tempSessionToken, setTempSessionToken] = useState<string | null>(null);
  const [twoFactorError, setTwoFactorError] = useState<string>('');

  const redirectAfterAuth = useCallback(
    async (role: string | null | undefined) => {
      const targetUrl = isAdminRole(role ?? undefined) ? '/admin' : from;
      // Small delay to ensure session cookie is persisted before navigation
      await new Promise(resolve => setTimeout(resolve, 100));
      router.refresh();
      await router.push(targetUrl);
    },
    [from, router]
  );

  const handleLogin = useCallback(
    async (data: LoginFormData) => {
      setIsAuthenticating(true);
      setError('');
      try {
        const response = await api.auth.login({
          email: data.email,
          password: data.password,
        });

        if (response.requiresTwoFactor && response.tempSessionToken) {
          setTempSessionToken(response.tempSessionToken);
          setShow2FA(true);
          setIsAuthenticating(false);
          return;
        }

        if (response.user) {
          await loadCurrentUser();
          await redirectAfterAuth(response.user.role);
        } else {
          throw new Error(t('auth.login.genericError'));
        }
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : t('auth.login.genericError');
        setError(errorMessage);
        logger.error('Login failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [loadCurrentUser, redirectAfterAuth, t]
  );

  const handle2FAVerify = useCallback(
    async (data: TwoFactorVerificationData) => {
      if (!tempSessionToken) {
        setTwoFactorError(t('auth.session.expired'));
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
        await loadCurrentUser();
        await redirectAfterAuth(user.role);
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : t('auth.twoFactor.error');
        setTwoFactorError(errorMessage);
        logger.error('2FA verification failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [tempSessionToken, loadCurrentUser, redirectAfterAuth, t]
  );

  const handleCancel2FA = useCallback(() => {
    setShow2FA(false);
    setTempSessionToken(null);
    setTwoFactorError('');
  }, []);

  const handleOAuthLogin = useCallback((provider: 'google' | 'discord' | 'github') => {
    window.location.assign(buildOAuthUrl(provider));
  }, []);

  // 2FA challenge screen — rendered inside AuthCard wrapper for visual parity
  if (show2FA) {
    return (
      <AuthCard
        title={t('auth.twoFactor.title')}
        subtitle={t('auth.twoFactor.verificationSubtitle')}
      >
        <TwoFactorVerification
          onVerify={handle2FAVerify}
          onCancel={handleCancel2FA}
          loading={isAuthenticating}
          error={twoFactorError}
          onErrorDismiss={() => setTwoFactorError('')}
          showRememberDevice
        />
      </AuthCard>
    );
  }

  const footerAction = (
    <span>
      {t('auth.login.noAccount')}{' '}
      <Link
        href="/register"
        className="font-medium text-foreground hover:underline"
        data-testid="login-register-link"
      >
        {t('auth.login.registerCta')}
      </Link>
    </span>
  );

  return (
    <AuthCard
      title={t('auth.login.title')}
      subtitle={t('auth.login.subtitle')}
      footerAction={footerAction}
    >
      {isSessionExpired && (
        <div
          className="mb-4 rounded-md bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 p-3"
          role="alert"
          data-testid="session-expired-banner"
        >
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            {t('auth.session.expired')}
          </p>
        </div>
      )}

      <LoginForm
        onSubmit={handleLogin}
        loading={isAuthenticating}
        error={error}
        onErrorDismiss={() => setError('')}
      />

      <div className="mt-3 text-right">
        <Link
          href="/reset-password"
          className="text-sm text-muted-foreground hover:text-foreground hover:underline"
          data-testid="login-forgot-password-link"
        >
          {t('auth.login.forgotPassword')}
        </Link>
      </div>

      <Divider label={t('auth.oauth.separator')} />

      <div className="flex flex-col gap-2">
        <OAuthButton provider="google" onClick={() => handleOAuthLogin('google')} />
        <OAuthButton provider="discord" onClick={() => handleOAuthLogin('discord')} />
        <OAuthButton provider="github" onClick={() => handleOAuthLogin('github')} />
      </div>
    </AuthCard>
  );
}
