'use client';

import { useState, useEffect } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { AuthModal } from '@/components/auth';
import { AuthLayout } from '@/components/layouts';
import { useTranslation } from '@/hooks/useTranslation';

export function RegisterFallback() {
  const { t } = useTranslation();
  return (
    <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      {t('auth.register.loadingMessage')}
    </main>
  );
}

export function RegisterPageContent() {
  const [mounted, setMounted] = useState(false);
  const [showAuthModal, setShowAuthModal] = useState(true);

  useEffect(() => {
    setMounted(true);
  }, []);

  const router = useRouter();
  const searchParams = useSearchParams();
  const finalDestination = searchParams?.get('from') ?? '/dashboard';
  // Redirect to welcome page first, which will then redirect to final destination
  const redirectTo = `/welcome?redirectTo=${encodeURIComponent(finalDestination)}`;

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  // Prevent SSR issues with TanStack Query
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
      />
    </AuthLayout>
  );
}
