'use client';

/**
 * Login Page (AUTH-05) - App Router
 *
 * Standalone login page with session expiration handling.
 * Uses the unified AuthModal component for consistent UX.
 *
 * Note (FE-IMP-005): Client-side only - AuthModal uses TanStack Query
 */

import { Suspense, useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AuthModal } from '@/components/auth';
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
      <main className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4">
        <div className="animate-pulse text-slate-500">Loading...</div>
      </main>
    );
  }

  return (
    <main className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4">
      {/* Unified Auth Modal */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={handleClose}
        defaultMode="login"
        sessionExpiredMessage={isSessionExpired}
      />
    </main>
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
