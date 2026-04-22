'use client';

import { useCallback, useEffect, useState } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { buildOAuthUrl } from '@/components/auth/oauth-url';
import { RegisterForm, type RegisterSubmitPayload } from '@/components/auth/RegisterForm';
import { RequestAccessForm } from '@/components/auth/RequestAccessForm';
import { AuthCard } from '@/components/ui/v2/auth-card';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/hooks/useTranslation';
import { trackSignUp } from '@/lib/analytics/flywheel-events';
import { useApiClient } from '@/lib/api/context';
import { logger } from '@/lib/logger';

// ============================================================================
// Types
// ============================================================================

type RegistrationMode = 'loading' | 'public' | 'invite-only';

// ============================================================================
// Fallback (Suspense boundary)
// ============================================================================

export function RegisterFallback() {
  const { t } = useTranslation();
  return (
    <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      {t('auth.register.loadingMessage')}
    </main>
  );
}

// ============================================================================
// Main content
// ============================================================================

export function RegisterPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();
  const { register } = useAuth();
  const api = useApiClient();

  const [registrationMode, setRegistrationMode] = useState<RegistrationMode>('loading');
  const [oauthEnabled, setOauthEnabled] = useState(false);
  const [isAuthenticating, setIsAuthenticating] = useState(false);
  const [error, setError] = useState<string>('');

  const oauthDisabled = searchParams?.get('oauth_disabled') === 'true';

  // Fetch registration mode on mount
  useEffect(() => {
    let cancelled = false;
    api.accessRequests
      .getRegistrationMode()
      .then(result => {
        if (cancelled) return;
        setRegistrationMode(result.publicRegistrationEnabled ? 'public' : 'invite-only');
        setOauthEnabled(result.oauthEnabled ?? false);
      })
      .catch(() => {
        if (cancelled) return;
        // Fail closed: default to invite-only if we cannot determine the mode
        setRegistrationMode('invite-only');
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  const handleRegister = useCallback(
    async (data: RegisterSubmitPayload) => {
      // Bot protection: silently drop submissions that fill the honeypot
      if (data.honeypot && data.honeypot.length > 0) {
        return;
      }

      setIsAuthenticating(true);
      setError('');
      try {
        await register({
          email: data.email,
          password: data.password,
        });
        trackSignUp({ method: 'email' });

        // Small delay to ensure session cookie is persisted before navigation
        // (matches AuthModal.handleRegister behavior)
        await new Promise(resolve => setTimeout(resolve, 100));

        await router.push(`/verification-pending?email=${encodeURIComponent(data.email)}`);
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : t('auth.register.genericError');
        setError(errorMessage);
        logger.error('Registration failed:', err);
      } finally {
        setIsAuthenticating(false);
      }
    },
    [register, router, t]
  );

  const handleOAuthLogin = useCallback((provider: 'google' | 'discord' | 'github') => {
    window.location.assign(buildOAuthUrl(provider));
  }, []);

  // Loading state
  if (registrationMode === 'loading') {
    return (
      <div
        className="min-h-dvh flex items-center justify-center bg-background text-muted-foreground"
        data-testid="register-loading"
      >
        <div className="animate-pulse">{t('auth.register.loadingMessage')}</div>
      </div>
    );
  }

  // Invite-only mode: show request access form
  if (registrationMode === 'invite-only') {
    return (
      <AuthCard
        title={t('auth.register.inviteOnlyTitle')}
        subtitle={t('auth.register.inviteOnlySubtitle')}
      >
        {oauthDisabled && (
          <div
            role="alert"
            className="mb-4 rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 p-3"
            data-testid="invite-oauth-disabled-alert"
          >
            <p className="text-sm text-amber-800 dark:text-amber-200">
              {t('auth.register.inviteOauthDisabled')}
            </p>
          </div>
        )}
        <RequestAccessForm />
      </AuthCard>
    );
  }

  // Public mode: standard registration with optional OAuth
  const footerAction = (
    <span>
      {t('auth.register.hasAccount')}{' '}
      <Link
        href="/login"
        className="font-medium text-foreground hover:underline"
        data-testid="register-login-link"
      >
        {t('auth.register.loginCta')}
      </Link>
    </span>
  );

  return (
    <AuthCard
      title={t('auth.register.title')}
      subtitle={t('auth.register.subtitle')}
      footerAction={footerAction}
    >
      <RegisterForm
        onSubmit={handleRegister}
        loading={isAuthenticating}
        error={error}
        onErrorDismiss={() => setError('')}
      />

      {oauthEnabled && (
        <>
          <Divider label={t('auth.oauth.separator')} />

          <div className="flex flex-col gap-2">
            <OAuthButton provider="google" onClick={() => handleOAuthLogin('google')} />
            <OAuthButton provider="discord" onClick={() => handleOAuthLogin('discord')} />
            <OAuthButton provider="github" onClick={() => handleOAuthLogin('github')} />
          </div>
        </>
      )}
    </AuthCard>
  );
}
