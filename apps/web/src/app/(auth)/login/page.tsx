'use client';

/**
 * Login Page (AUTH-05) - App Router
 *
 * Standalone login page with session expiration handling.
 * Uses AuthLayout wrapper for consistent auth page UX (Issue #2231).
 *
 * Note (FE-IMP-005): Client-side only - AuthModal uses TanStack Query
 */

import { Suspense, useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AuthModal } from '@/components/auth';
import { AuthLayout } from '@/components/layouts';
import { useTranslation } from '@/hooks/useTranslation';

function LoginPageContent() {
  const [mounted, setMounted] = useState(false);
  const [showAuthModal, setShowAuthModal] = useState(true);

  useEffect(() => {
    setMounted(true);
  }, []);

  const router = useRouter();
  const searchParams = useSearchParams();
  const reason = searchParams?.get('reason') ?? null;

  const isSessionExpired = reason === 'session_expired';

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  // Prevent SSR issues with TanStack Query (FE-IMP-005)
  if (!mounted) {
    return (
      <AuthLayout title="Loading...">
        <div className="text-center py-8">
          <div className="animate-pulse text-slate-500">Loading...</div>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title="Welcome Back" subtitle="Sign in to continue to MeepleAI">
      {/* Unified Auth Modal */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={handleClose}
        defaultMode="login"
        sessionExpiredMessage={isSessionExpired}
      />
    </AuthLayout>
  );
}

function LoginFallback() {
  const { t } = useTranslation();
  return (
    <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      {t('auth.login.loadingMessage')}
    </main>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={<LoginFallback />}>
      <LoginPageContent />
    </Suspense>
  );
}
