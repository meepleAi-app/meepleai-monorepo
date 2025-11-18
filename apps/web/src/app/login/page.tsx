'use client';

/**
 * Login Page (AUTH-05) - App Router
 *
 * Standalone login page with session expiration handling.
 * Uses the unified AuthModal component for consistent UX.
 *
 * Note (FE-IMP-005): Client-side only - AuthModal uses TanStack Query
 */

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AuthModal } from '@/components/auth';

export default function LoginPage() {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  const router = useRouter();
  const searchParams = useSearchParams();
  const reason = searchParams?.get('reason') ?? null;
  const [showAuthModal, setShowAuthModal] = useState(true);

  const isSessionExpired = reason === 'session_expired';

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  // Prevent SSR issues with TanStack Query (FE-IMP-005)
  if (!mounted) {
    return null;
  }

  return (
    <div className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4">
      {/* Unified Auth Modal */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={handleClose}
        defaultMode="login"
        sessionExpiredMessage={isSessionExpired}
        showDemoCredentials={true}
      />
    </div>
  );
}
