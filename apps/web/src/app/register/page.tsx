'use client';

/**
 * Register Page - App Router
 *
 * Standalone registration page with AuthModal.
 * Mirrors /login structure but with defaultMode="register"
 */

import { Suspense, useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AuthModal } from '@/components/auth';
import { useTranslation } from '@/hooks/useTranslation';

function RegisterPageContent() {
  const [mounted, setMounted] = useState(false);
  const [showAuthModal, setShowAuthModal] = useState(true);

  useEffect(() => {
    setMounted(true);
  }, []);

  const router = useRouter();

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  // Prevent SSR issues with TanStack Query
  if (!mounted) {
    return null;
  }

  return (
    <div
      className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4"
      data-testid="register-page"
    >
      {/* Unified Auth Modal - Registration Mode */}
      <AuthModal isOpen={showAuthModal} onClose={handleClose} defaultMode="register" />
    </div>
  );
}

function RegisterFallback() {
  const { t } = useTranslation();
  return (
    <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
      {t('auth.register.loadingMessage')}
    </div>
  );
}

export default function RegisterPage() {
  return (
    <Suspense fallback={<RegisterFallback />}>
      <RegisterPageContent />
    </Suspense>
  );
}
