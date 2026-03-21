'use client';

import { useState, useEffect } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { AuthModal } from '@/components/auth';
import { RequestAccessForm } from '@/components/auth/RequestAccessForm';
import { AuthLayout } from '@/components/layouts';
import { useTranslation } from '@/hooks/useTranslation';
import { useApiClient } from '@/lib/api/context';

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
  const [mounted, setMounted] = useState(false);
  const [showAuthModal, setShowAuthModal] = useState(true);
  const [registrationMode, setRegistrationMode] = useState<RegistrationMode>('loading');
  const [oauthEnabled, setOauthEnabled] = useState(false);

  const api = useApiClient();

  useEffect(() => {
    setMounted(true);
  }, []);

  // Fetch registration mode on mount (after client hydration)
  useEffect(() => {
    if (!mounted) return;

    api.accessRequests
      .getRegistrationMode()
      .then(result => {
        setRegistrationMode(result.publicRegistrationEnabled ? 'public' : 'invite-only');
        setOauthEnabled(result.oauthEnabled ?? false);
      })
      .catch(() => {
        // Fail closed: default to invite-only if we cannot determine the mode
        setRegistrationMode('invite-only');
      });
  }, [mounted, api]);

  const router = useRouter();
  const searchParams = useSearchParams();
  const oauthDisabled = searchParams?.get('oauth_disabled') === 'true';
  const finalDestination = searchParams?.get('from') ?? '/dashboard';
  // Redirect to welcome page first, which will then redirect to final destination
  const redirectTo = `/welcome?redirectTo=${encodeURIComponent(finalDestination)}`;

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  // Prevent SSR issues with TanStack Query
  if (!mounted || registrationMode === 'loading') {
    return (
      <AuthLayout title="Loading...">
        <div className="text-center py-8" data-testid="register-loading">
          <div className="animate-pulse text-slate-500">Loading...</div>
        </div>
      </AuthLayout>
    );
  }

  // Invite-only mode: show request access form
  if (registrationMode === 'invite-only') {
    return (
      <AuthLayout
        title="Request Access"
        subtitle="Registration is currently by invitation only"
        data-testid="register-page"
      >
        {oauthDisabled && (
          <div
            role="alert"
            className="text-sm text-amber-600 bg-amber-50 dark:text-amber-400 dark:bg-amber-900/20 p-3 rounded-md mb-4"
          >
            OAuth login is not available during alpha. Please request access below.
          </div>
        )}
        <RequestAccessForm />
      </AuthLayout>
    );
  }

  // Public mode: show the standard registration modal
  return (
    <AuthLayout
      title="Create Account"
      subtitle="Join MeepleAI to get started"
      data-testid="register-page"
    >
      {/* Unified Auth Modal - Registration Mode */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={handleClose}
        defaultMode="register"
        redirectTo={redirectTo}
        hideOAuth={!oauthEnabled}
      />
    </AuthLayout>
  );
}
